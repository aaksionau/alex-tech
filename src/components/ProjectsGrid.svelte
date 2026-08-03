<script>
  /** @typedef {{ id: string, name: string, organization: string, duration: string | null, tech: string[], achievements: string[] }} Project */
  /** @typedef {{ year: number, projects: Project[] }} YearGroup */

  /** @type {{ years: YearGroup[] }} */
  let { years } = $props();

  let selectedTags = $state(new Set());

  let allTags = $derived(
    [...new Set(years.flatMap((group) => group.projects.flatMap((project) => project.tech)))].sort(
      (a, b) => a.localeCompare(b),
    ),
  );

  let visibleYears = $derived(
    years
      .map((group) => ({
        year: group.year,
        projects:
          selectedTags.size === 0
            ? group.projects
            : group.projects.filter((project) => project.tech.some((tag) => selectedTags.has(tag))),
      }))
      .filter((group) => group.projects.length > 0),
  );

  function toggleTag(tag) {
    const next = new Set(selectedTags);
    if (next.has(tag)) {
      next.delete(tag);
    } else {
      next.add(tag);
    }
    selectedTags = next;
  }

  function clearTags() {
    selectedTags = new Set();
  }
</script>

<div class="flex flex-col gap-10">
  <div class="flex flex-wrap items-center gap-2">
    {#each allTags as tag}
      <button
        type="button"
        onclick={() => toggleTag(tag)}
        class="rounded-full border px-3 py-1 text-xs transition-colors {selectedTags.has(tag)
          ? 'border-(--color-accent) bg-(--color-accent) text-(--color-accent-foreground)'
          : 'border-(--color-border) text-(--color-muted) hover:border-(--color-accent)'}"
      >
        {tag}
      </button>
    {/each}
    {#if selectedTags.size > 0}
      <button
        type="button"
        onclick={clearTags}
        class="rounded-full px-3 py-1 text-xs text-(--color-muted) underline hover:text-(--color-foreground)"
      >
        Clear filters
      </button>
    {/if}
  </div>

  {#if visibleYears.length === 0}
    <p class="text-(--color-muted)">No projects match the selected technologies.</p>
  {/if}

  {#each visibleYears as group (group.year)}
    <section class="flex flex-col gap-6">
      <h2 class="font-heading text-2xl font-semibold">{group.year}</h2>
      <div class="grid grid-cols-1 gap-6 md:grid-cols-2">
        {#each group.projects as project (project.id)}
          <article
            class="flex flex-col gap-3 rounded-lg border border-(--color-border) bg-(--color-surface) p-6"
          >
            <div class="flex flex-col gap-1">
              <h3 class="font-heading text-lg font-semibold">{project.name}</h3>
              <p class="text-sm text-(--color-muted)">
                {[project.organization, project.duration].filter(Boolean).join(' · ')}
              </p>
            </div>

            <ul class="flex flex-col gap-2 text-(--color-foreground)">
              {#each project.achievements as achievement}
                <li class="flex gap-2 text-sm">
                  <span class="text-(--color-accent)">▸</span>
                  <span>{achievement}</span>
                </li>
              {/each}
            </ul>

            <div class="mt-auto flex flex-wrap gap-2 pt-2">
              {#each project.tech as tag}
                <span
                  class="rounded-full border border-(--color-border) px-3 py-1 text-xs text-(--color-muted)"
                >
                  {tag}
                </span>
              {/each}
            </div>
          </article>
        {/each}
      </div>
    </section>
  {/each}
</div>
