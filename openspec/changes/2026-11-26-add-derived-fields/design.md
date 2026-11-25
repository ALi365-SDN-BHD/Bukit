# Design: derived fields

- **Scriban, own page only.** Reuses the template language users know; variables are the page's
  top-level fields, so a value cannot depend on build order or other pages.
- **After links, before readers.** Running after `ContentLinkResolver` lets values read
  `<field>_links`; running before list, SEO, feed and search builders means one value everywhere,
  and the page hash covers it for incremental builds.
- **Deterministic.** Invariant culture, `LoopLimit` 1000, 64 KB output limit, and `date.now`,
  `math.random`, `math.uuid` removed.
- **Fail modes.** A parse error is a config error (it would fail on every page). A runtime error
  is per page: the field stays unchanged and the issue follows `schemaFailMode`.
- **Summary.** Writing `summary`, `description` or `excerpt` also updates the record summary, the
  same fallback order the content parser uses.
- **Reserved names.** Built-in fields (`slug`, `title`, `route`, `language`, ...) are rejected so a
  template cannot change identity or routing.
