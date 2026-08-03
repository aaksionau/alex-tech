<script>
  import { fly } from 'svelte/transition';

  /** @typedef {{ client: string, startDate: string, endDate: string | null, achievements: string[], tech: string[] }} Engagement */
  /** @typedef {{ id: string, company: string | null, title: string, startDate: string, endDate: string | null, location: string | null, achievements: string[], tech: string[], engagements?: Engagement[] }} Role */

  /** @type {{ roles: Role[] }} */
  let { roles } = $props();

  let revealed = $state(new Set());

  function formatDate(value) {
    if (!value) return 'Present';
    const [year, month] = value.split('-');
    if (!month) return year;
    const label = new Date(Number(year), Number(month) - 1).toLocaleDateString('en-US', {
      month: 'short',
      year: 'numeric',
    });
    return label;
  }

  function dateRange(start, end) {
    return `${formatDate(start)} – ${formatDate(end)}`;
  }

  function reveal(node, id) {
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            revealed.add(id);
            revealed = new Set(revealed);
            observer.unobserve(node);
          }
        }
      },
      { threshold: 0.15, rootMargin: '0px 0px -10% 0px' },
    );
    observer.observe(node);

    return {
      destroy() {
        observer.disconnect();
      },
    };
  }
</script>

<ol class="relative flex flex-col gap-12 border-l border-(--color-border) pl-6 sm:pl-10">
  {#each roles as role (role.id)}
    <li use:reveal={role.id} class="relative">
      <span
        class="absolute top-1.5 -left-[1.75rem] h-3 w-3 rounded-full bg-(--color-accent) sm:-left-[2.75rem]"
      ></span>
      {#if revealed.has(role.id)}
        <div
          in:fly={{ y: 24, duration: 450 }}
          class="flex flex-col gap-4"
        >
          <div class="flex flex-col gap-1">
            <h2 class="font-heading text-xl font-semibold sm:text-2xl">{role.title}</h2>
            <p class="text-(--color-muted)">
              {[role.company, role.location].filter(Boolean).join(' · ')}
            </p>
            <p class="font-mono text-sm text-(--color-accent)">
              {dateRange(role.startDate, role.endDate)}
            </p>
          </div>

          {#if role.achievements.length > 0}
            <ul class="flex flex-col gap-2 text-(--color-foreground)">
              {#each role.achievements as achievement}
                <li class="flex gap-2">
                  <span class="text-(--color-accent)">▸</span>
                  <span>{achievement}</span>
                </li>
              {/each}
            </ul>
          {/if}

          {#if role.tech.length > 0}
            <div class="flex flex-wrap gap-2">
              {#each role.tech as tag}
                <span
                  class="rounded-full border border-(--color-border) px-3 py-1 text-xs text-(--color-muted)"
                >
                  {tag}
                </span>
              {/each}
            </div>
          {/if}

          {#if role.engagements?.length}
            <ul class="mt-2 flex flex-col gap-8 border-l border-(--color-border) pl-6">
              {#each role.engagements as engagement}
                <li class="flex flex-col gap-3">
                  <div class="flex flex-col gap-1">
                    <h3 class="font-heading text-lg font-semibold">{engagement.client}</h3>
                    <p class="font-mono text-sm text-(--color-accent)">
                      {dateRange(engagement.startDate, engagement.endDate)}
                    </p>
                  </div>
                  <ul class="flex flex-col gap-2 text-(--color-foreground)">
                    {#each engagement.achievements as achievement}
                      <li class="flex gap-2">
                        <span class="text-(--color-accent)">▸</span>
                        <span>{achievement}</span>
                      </li>
                    {/each}
                  </ul>
                  {#if engagement.tech.length > 0}
                    <div class="flex flex-wrap gap-2">
                      {#each engagement.tech as tag}
                        <span
                          class="rounded-full border border-(--color-border) px-3 py-1 text-xs text-(--color-muted)"
                        >
                          {tag}
                        </span>
                      {/each}
                    </div>
                  {/if}
                </li>
              {/each}
            </ul>
          {/if}
        </div>
      {/if}
    </li>
  {/each}
</ol>
