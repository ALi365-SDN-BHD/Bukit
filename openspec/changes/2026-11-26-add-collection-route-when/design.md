# Design: routeWhen

- **One test, one operator.** `{ field, eq }` covers the roadmap case. Text compares ignoring case and
  surrounding spaces; booleans as `true`/`false`; whole numbers as digits. A missing field fails.
- **One decision point.** `CollectionPagePolicy.HasPage` (not a data item, and passes `routeWhen`)
  replaces every "gets a page" test, so routing, route inventory, SEO alternates and link validation
  agree.
- **Still a link target.** RoutePipeline passes every document without a page to
  `ContentLinkResolver` as a target without a URL, so links to hidden cities keep their title and
  fields. Hidden entries are not linking pages, matching data documents.
- **Language.** Hidden entries keep the page language rules (`Presentation.Language`), unlike data
  documents (`locale`), because they are still content documents.
