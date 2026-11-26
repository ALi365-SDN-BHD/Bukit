# Related pages by custom fields

## Why

Similar routes are scored by shared cities and shared audiences, which `site.related` cannot
express. Roadmap item F5 (`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- `site.related.indices[].field`: a content field scored as weight times shared values.
- `site.related.collections.<collection>: { threshold, limit, indices }`: rules for one
  collection's pages, compared only within that collection; `threshold` may be 0.
- Entries gain `id` and `slug`.
- Equal scores keep the collection list order (the sort used to be unstable).

## Impact

- Config: internal `RelatedIndexConfig.Field` and `RelatedConfig.Collections` (with
  `JsonInclude` for i18n workers); no public API change.
- Engine: `RelatedContentPlugin`. Tie order of existing related lists may change.
- Docs: `04-site-yaml-config.md`, the `bukit-templating` skill, CHANGELOG.
