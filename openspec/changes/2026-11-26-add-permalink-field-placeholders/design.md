# Design: permalink field placeholders

- **Option (a).** Bukit expands the placeholder, so a CMS does not need a URL column (option b).
- **Same cleaning as `{title}`.** `RoutePathBuilder.Slugify` makes the segment, so a value cannot add
  path separators or unsafe characters.
- **Fail, never empty.** A missing, empty, non-whole-number or unsluggable value fails with the
  pattern and page id, because an empty segment would silently merge URLs.
- **Duplicates.** The route inventory already rejects two pages with one URL, so no new check.
- **Incremental builds.** The URL comes from page fields, which the page hash already covers.
