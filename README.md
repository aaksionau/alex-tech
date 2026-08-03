# alex-tech

Personal site: Astro frontend + Azure Static Web Apps, with a C# Azure Functions API and an
Azure AI Foundry-backed voice bot ("talk to my virtual self") planned in later issues.

## Stack

- **Frontend**: Astro (static output), TypeScript, Tailwind CSS v4, Svelte islands, MDX/content
  collections
- **Hosting**: Azure Static Web Apps, deployed via GitHub Actions on push to `main`, with PR
  preview environments
- **API** (planned): Azure Functions, C# isolated worker, in `/api`
- **Voice bot** (planned): Azure AI Foundry realtime model (`gpt-realtime-mini`)

## Commands

| Command           | Action                                      |
| :----------------- | :------------------------------------------ |
| `npm install`       | Install dependencies                        |
| `npm run dev`       | Start local dev server at `localhost:4321`  |
| `npm run build`     | Build the production site to `./dist/`      |
| `npm run preview`   | Preview the build locally before deploying  |

## Project structure

```text
/
├── public/
├── src/
│   ├── components/     # Svelte/Astro components
│   ├── content/         # MDX content collections (blog, ...)
│   ├── layouts/
│   ├── pages/            # file-based routes
│   └── styles/            # design tokens, global CSS
├── content.config.ts
└── astro.config.mjs
```

See the tracked issues in this repo for the full roadmap.
