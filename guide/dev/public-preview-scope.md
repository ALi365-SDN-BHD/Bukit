# Public Preview Scope

This file defines what Core 1.0 preview docs may promise.

## Stable Core Scope

Stable Core includes:

- strict `site.yaml` loading;
- Markdown and Notion content sources;
- collection routing and list routes;
- Scriban rendering;
- local filesystem themes and `theme.yaml`;
- built-in plugin runtime;
- SEO, GEO, publish reports, and diff gates;
- static preview server;
- LiveReload development server;
- GitHub Pages deploy;
- shell completion and version commands.

## Preview, Not Promise

The guide may describe implementation details, but public-facing promises should
stay tied to tested behavior. If a feature is only present in design notes,
historical docs, or Labs drafts, do not describe it as Core.

## Explicitly Opt-In Areas

Labs and Archive content is outside the Core preview promise. It can describe
possible workflows, old command shapes, or research paths only when the document
starts with a clear "not Core 1.0" boundary.

## Exit Criteria for Core Docs

- `guide/dev/README.md` links only to Core docs by default.
- CLI docs match `BukitCliSpecs.cs`.
- Config docs match strict validator and schema generator.
- Theme docs do not claim remote theme source support in Core.
- Plugin docs cover built-in runtime only.
- Development server docs use LiveReload/browser reload wording.

