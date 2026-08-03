<script>
  import { buildSystemInstructions, DISCLAIMER_TEXT } from '../lib/systemPrompt.js';

  /** @typedef {{ id: string, role: 'user' | 'assistant', text: string }} TranscriptEntry */

  /** @type {{ roles: import('../lib/systemPrompt.js').Role[], years: import('../lib/systemPrompt.js').YearGroup[] }} */
  let { roles, years } = $props();

  /** @type {'idle' | 'connecting' | 'active' | 'ended' | 'error' | 'capped'} */
  let status = $state('idle');
  let errorMessage = $state('');
  let cappedMessage = $state('');
  let timeLimitReached = $state(false);
  let muted = $state(false);
  let userSpeaking = $state(false);
  let assistantSpeaking = $state(false);
  let userLevel = $state(0);
  let assistantLevel = $state(0);
  /** @type {TranscriptEntry[]} */
  let transcript = $state([]);

  /** @type {RTCPeerConnection | null} */
  let peerConnection = null;
  /** @type {RTCDataChannel | null} */
  let dataChannel = null;
  /** @type {MediaStream | null} */
  let localStream = null;
  /** @type {HTMLAudioElement | null} */
  let audioEl = null;
  let stopUserMeter = () => {};
  let stopAssistantMeter = () => {};
  /** @type {ReturnType<typeof setTimeout> | null} */
  let sessionTimeout = null;

  const DEFAULT_MAX_SESSION_SECONDS = 180;

  /**
   * @param {MediaStream} stream
   * @param {(level: number) => void} onLevel
   */
  function attachLevelMeter(stream, onLevel) {
    const audioCtx = new AudioContext();
    const source = audioCtx.createMediaStreamSource(stream);
    const analyser = audioCtx.createAnalyser();
    analyser.fftSize = 256;
    source.connect(analyser);
    const data = new Uint8Array(analyser.frequencyBinCount);
    let rafId;

    function tick() {
      analyser.getByteFrequencyData(data);
      const avg = data.reduce((sum, v) => sum + v, 0) / data.length;
      onLevel(Math.min(1, avg / 110));
      rafId = requestAnimationFrame(tick);
    }
    tick();

    return () => {
      cancelAnimationFrame(rafId);
      audioCtx.close();
    };
  }

  /**
   * @param {string} itemId
   * @param {string | undefined} text
   * @param {boolean} done
   */
  function upsertAssistantEntry(itemId, text, done) {
    const existing = transcript.find((entry) => entry.id === itemId);
    if (existing) {
      transcript = transcript.map((entry) =>
        entry.id === itemId
          ? { ...entry, text: done ? (text ?? entry.text) : entry.text + (text ?? '') }
          : entry,
      );
    } else {
      transcript = [...transcript, { id: itemId, role: 'assistant', text: text ?? '' }];
    }
  }

  /** @param {string} raw */
  function handleServerEvent(raw) {
    /** @type {any} */
    let event;
    try {
      event = JSON.parse(raw);
    } catch {
      return;
    }

    switch (event.type) {
      case 'input_audio_buffer.speech_started':
        userSpeaking = true;
        break;
      case 'input_audio_buffer.speech_stopped':
        userSpeaking = false;
        break;
      case 'conversation.item.input_audio_transcription.completed':
        transcript = [
          ...transcript,
          { id: event.item_id ?? crypto.randomUUID(), role: 'user', text: event.transcript ?? '' },
        ];
        break;
      case 'response.created':
        assistantSpeaking = true;
        break;
      case 'response.audio_transcript.delta':
        upsertAssistantEntry(event.item_id, event.delta, false);
        break;
      case 'response.audio_transcript.done':
        upsertAssistantEntry(event.item_id, event.transcript, true);
        break;
      case 'response.done':
        assistantSpeaking = false;
        break;
      case 'error':
        status = 'error';
        errorMessage = event.error?.message ?? 'The realtime session reported an error.';
        break;
    }
  }

  function stopLocalStream() {
    localStream?.getTracks().forEach((track) => track.stop());
    localStream = null;
  }

  function endSession() {
    if (sessionTimeout !== null) {
      clearTimeout(sessionTimeout);
      sessionTimeout = null;
    }
    stopUserMeter();
    stopAssistantMeter();
    stopUserMeter = () => {};
    stopAssistantMeter = () => {};
    dataChannel?.close();
    peerConnection?.close();
    dataChannel = null;
    peerConnection = null;
    stopLocalStream();
    userLevel = 0;
    assistantLevel = 0;
    userSpeaking = false;
    assistantSpeaking = false;
    muted = false;
    if (status !== 'error') status = 'ended';
  }

  async function startSession() {
    status = 'connecting';
    errorMessage = '';
    cappedMessage = '';
    timeLimitReached = false;
    transcript = [];

    try {
      localStream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      status = 'error';
      errorMessage = 'Microphone access is required for a voice conversation.';
      return;
    }

    /** @type {{ value: string, expiresAt: number, endpoint: string, deployment: string, region: string, maxSessionSeconds?: number }} */
    let session;
    try {
      const response = await fetch('/api/realtime-session', { method: 'POST' });
      if (response.status === 429) {
        const body = await response.json().catch(() => ({}));
        status = 'capped';
        cappedMessage =
          body.message ??
          "You've reached today's limit for live conversations — check back tomorrow, or browse the Experience/Projects pages.";
        stopLocalStream();
        return;
      }
      if (!response.ok) throw new Error('Failed to reach the voice session service.');
      session = await response.json();
    } catch (err) {
      status = 'error';
      errorMessage = err instanceof Error ? err.message : 'Failed to start the session.';
      stopLocalStream();
      return;
    }

    const pc = new RTCPeerConnection();
    peerConnection = pc;

    audioEl = document.createElement('audio');
    audioEl.autoplay = true;

    pc.ontrack = (event) => {
      const [remoteStream] = event.streams;
      if (!remoteStream || !audioEl) return;
      audioEl.srcObject = remoteStream;
      stopAssistantMeter = attachLevelMeter(remoteStream, (level) => (assistantLevel = level));
    };

    const micTrack = localStream.getAudioTracks()[0];
    pc.addTrack(micTrack, localStream);
    stopUserMeter = attachLevelMeter(localStream, (level) => (userLevel = level));

    const dc = pc.createDataChannel('realtime-channel');
    dataChannel = dc;

    dc.addEventListener('open', () => {
      dc.send(
        JSON.stringify({
          type: 'session.update',
          session: {
            instructions: buildSystemInstructions(roles, years),
            input_audio_transcription: { model: 'whisper-1' },
          },
        }),
      );
      dc.send(
        JSON.stringify({
          type: 'response.create',
          response: {
            instructions: `Say the following disclaimer to the visitor, word for word, as your very first message, then stop and wait for them to respond: "${DISCLAIMER_TEXT}"`,
          },
        }),
      );
      status = 'active';
      sessionTimeout = setTimeout(
        () => {
          timeLimitReached = true;
          endSession();
        },
        (session.maxSessionSeconds ?? DEFAULT_MAX_SESSION_SECONDS) * 1000,
      );
    });

    dc.addEventListener('message', (event) => handleServerEvent(event.data));
    dc.addEventListener('close', () => {
      if (status !== 'error') status = 'ended';
    });

    try {
      const offer = await pc.createOffer();
      await pc.setLocalDescription(offer);

      const sdpResponse = await fetch(
        `https://${session.region}.realtimeapi-preview.ai.azure.com/v1/realtimertc?model=${session.deployment}`,
        {
          method: 'POST',
          body: offer.sdp,
          headers: {
            Authorization: `Bearer ${session.value}`,
            'Content-Type': 'application/sdp',
          },
        },
      );

      if (!sdpResponse.ok) throw new Error('Failed to connect the voice session.');

      await pc.setRemoteDescription({ type: 'answer', sdp: await sdpResponse.text() });
    } catch (err) {
      errorMessage = err instanceof Error ? err.message : 'Failed to connect the voice session.';
      status = 'error';
      endSession();
    }
  }

  function toggleMute() {
    if (!localStream) return;
    muted = !muted;
    localStream.getAudioTracks().forEach((track) => (track.enabled = !muted));
  }
</script>

<div class="flex flex-col gap-8">
  <div class="rounded-lg border border-(--color-border) bg-(--color-surface) p-4 text-sm text-(--color-muted)">
    {DISCLAIMER_TEXT}
  </div>

  <div class="flex flex-wrap items-center gap-4">
    {#if status === 'idle' || status === 'ended' || status === 'error'}
      <button
        type="button"
        onclick={startSession}
        class="rounded-full bg-(--color-accent) px-5 py-2 text-sm font-semibold text-(--color-accent-foreground) hover:opacity-90"
      >
        {status === 'ended' ? 'Start a new conversation' : 'Start conversation'}
      </button>
    {/if}

    {#if status === 'connecting'}
      <span class="text-sm text-(--color-muted)">Connecting…</span>
    {/if}

    {#if status === 'capped'}
      <p class="text-sm text-(--color-muted)">
        {cappedMessage} Browse
        <a href="/experience" class="text-(--color-accent) underline underline-offset-2 hover:opacity-80">Experience</a>
        or
        <a href="/projects" class="text-(--color-accent) underline underline-offset-2 hover:opacity-80">Projects</a>
        instead.
      </p>
    {/if}

    {#if status === 'active'}
      <button
        type="button"
        onclick={toggleMute}
        class="rounded-full border px-5 py-2 text-sm font-semibold transition-colors {muted
          ? 'border-(--color-accent) text-(--color-accent)'
          : 'border-(--color-border) text-(--color-foreground) hover:border-(--color-accent)'}"
      >
        {muted ? 'Unmute' : 'Mute'}
      </button>
      <button
        type="button"
        onclick={endSession}
        class="rounded-full border border-(--color-border) px-5 py-2 text-sm font-semibold text-(--color-foreground) hover:border-(--color-accent)"
      >
        End call
      </button>
    {/if}
  </div>

  {#if status === 'error'}
    <p class="text-sm text-red-400">{errorMessage}</p>
  {/if}

  {#if status === 'ended' && timeLimitReached}
    <p class="text-sm text-(--color-muted)">
      This conversation reached the 3-minute time limit. Start a new conversation to keep talking.
    </p>
  {/if}

  {#if status === 'active'}
    <div class="flex flex-col gap-4 sm:flex-row sm:gap-8">
      <div class="flex flex-1 flex-col gap-2">
        <p class="font-mono text-xs text-(--color-muted)">You{userSpeaking ? ' · speaking' : ''}</p>
        <div class="flex h-10 items-end gap-1">
          {#each Array(5) as _, i}
            <span
              class="w-2 rounded-full bg-(--color-accent) transition-all duration-100"
              style={`height: ${Math.max(8, userLevel * 40 * (0.6 + 0.4 * Math.sin(i + 1)))}px`}
            ></span>
          {/each}
        </div>
      </div>
      <div class="flex flex-1 flex-col gap-2">
        <p class="font-mono text-xs text-(--color-muted)">Alex{assistantSpeaking ? ' · speaking' : ''}</p>
        <div class="flex h-10 items-end gap-1">
          {#each Array(5) as _, i}
            <span
              class="w-2 rounded-full bg-(--color-accent) transition-all duration-100"
              style={`height: ${Math.max(8, assistantLevel * 40 * (0.6 + 0.4 * Math.cos(i + 1)))}px`}
            ></span>
          {/each}
        </div>
      </div>
    </div>
  {/if}

  {#if transcript.length > 0}
    <div class="flex flex-col gap-3 rounded-lg border border-(--color-border) bg-(--color-surface) p-4">
      {#each transcript as entry (entry.id)}
        <p class="text-sm">
          <span class="font-mono text-xs text-(--color-accent)">{entry.role === 'user' ? 'You' : 'Alex'}:</span>
          <span class="text-(--color-foreground)">{entry.text}</span>
        </p>
      {/each}
    </div>
  {/if}
</div>
