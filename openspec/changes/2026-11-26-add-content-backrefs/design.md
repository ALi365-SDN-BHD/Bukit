# Design: content backrefs

## Decisions

- **Config on the link.** `backref` sits on the `content.links` entry, so one key field drives both
  directions. `name` is required; `limit` defaults to 0 (no limit); `select` copies top-level fields
  of the linking page.
- **Output.** `<name>` is a list of ordinal, key-sorted maps with `key` (the linking page slug),
  `title`, `slug`, `url`, `collection` and the selected fields, the same shape as a link.
  `<name>_count` is a number. Every routed page of the target collection gets both, so templates
  never test for a missing field. Entries do not carry backrefs: they are built from the pages after
  links resolve and before any backref is written.
- **Order.** The collection list order (`CollectionRouteIndex.OrderEntries`): publish time
  descending, then `content.ordering`, URL and id. `limit` applies after sorting.
- **Scope.** Linking pages are content pages; targets are routed pages, so data targets get no list.
  A link counts only when it resolved in the page's language, and a key with several targets counts
  for the first one, the target the link itself uses. A link counts even when its own output field
  conflicts.
- **Conflicts.** One writer per field per collection, checked at load time across links and
  backrefs (`<name>` and `<name>_count`). At build time an existing field on a target page is never
  overwritten; `Validate` reports it on the target page, so the target collection's
  `schemaFailMode` applies.
- **Incremental builds.** Backrefs are page fields, so the target page hash covers them.
