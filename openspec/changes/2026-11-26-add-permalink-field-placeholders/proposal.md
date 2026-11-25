# Field placeholders in permalinks

## Why

Attraction URLs include their city (`/malaysia/{city}/attractions/{slug}/`), and today each page
overrides its route in front matter. Roadmap item F12, option (a)
(`docs/plans/2026-11-25-generic-data-derivation-roadmap.zh-CN.md`).

## What Changes

- Permalink patterns accept `{fields.<name>}`: the slugified value of a top-level text or
  whole-number field.
- A page without a usable value fails routing with `RouteInvalidPattern`. Duplicate URLs are
  already rejected by the route inventory (`RouteConflict`).

## Impact

- Routing: `RouteGenerator.ExpandPermalinkPattern`. No public API change.
- Docs: `04-site-yaml-config.md`, the `bukit-routing` skill, CHANGELOG in three languages.
- Out of scope: nested paths and link fields (`{fields.city_links...}`), and per-placeholder defaults.
