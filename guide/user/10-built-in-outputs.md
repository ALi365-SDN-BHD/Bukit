# 10 Built-In Outputs

Build output includes rendered pages, copied assets, static files, reports, and
machine-readable projections.

## Common Output Paths

| Output | Purpose |
|---|---|
| `index.html` and route `index.html` files | Rendered HTML pages. |
| `assets/` | Theme assets and localized media. |
| `content/*.json` and `content/*.md` | Per-document public machine-readable projections. |
| `agent-manifest.json` | Public representation inventory for agents and compatible plugins. |
| `.bukit/build-report.json` | Build summary, timings, render counts, diagnostic counts, and public output inventory. |
| `.bukit/routes.json` | Route inventory. |
| `.bukit/seo-report.json` | SEO quality data consumed by `seo audit`. |
| `.bukit/publish-audit-report.json` | Publish-readiness data consumed by `publish audit`. |
| `.bukit/security-report.json` | Output safety and publish security data. |
| `sitemap.xml` | Sitemap output, controlled by `site.sitemapMode`. |
| `feed/` and feed files | RSS, Atom, or JSON Feed depending on `site.feed`. |
| `search.json` and search UI files | Search index and optional UI. |
| `llms.txt`, `llms-full.txt` | GEO outputs when enabled. |
| `robots.txt` | Generated when SEO robots config requires it. |

RSS, Atom, and JSON Feed contain the same `site.feed.limit` window for their
shared candidate set. Publish audit only expects feed representation coverage
for routes in that window and only for formats listed in `site.feed.formats`.
This rule also applies to merged multilingual feeds.

`search.json` and `bukit-search.html` remain search data and an embeddable UI
fragment. They do not create a formal HTML route and do not enable WebSite
SearchAction structured data. A site that wants SearchAction must provide a
complete final HTML search route and declare it with `site.search.route`.

The default search UI inserts content titles and snippets with text nodes and
uses `<mark>` elements for matches. It does not interpret content fields as
HTML. `site.search.maxContentLength` applies consistently to the `content`
field of document, list, plugin, and publish-projection records in every search
mode; it does not truncate titles, summaries, or generated snippets. Merged mode
caps the root merged records, while split and index modes cap the records in
each language's `search.json`.

The publish audit semantic outline and heading checks use the primary content
scope: the first `article` with a visible, non-empty H1 in the first `main`,
otherwise the first `main`, otherwise the first standalone `article`. Headings
inside `header`, `nav`, and `footer` do not satisfy the primary H1 check, affect
level-skip checks, enter `semanticOutline`, or supply the visible title for
JSON-LD comparison. A standalone article fallback does not suppress
`publish.semantic_main_missing`.

## Public And Internal Output Boundary

Rendered pages and the machine-readable files outside `.bukit/` are public
publish artifacts. For Notion-backed documents, Bukit uses the public canonical
key or route as `id` and removes provider provenance fields such as `source` and
`sourceKey`. It also removes Notion UUIDs from projected entity and relation
identifiers. This applies to document JSON and Markdown, `agent-manifest.json`,
`search.json`, JSON Feed, and `llms-full.txt`.

`.bukit/` is an internal diagnostics directory. Audit reports there may retain
provider names, source item identifiers, and local build paths so that build,
SEO, and publish diagnostics remain traceable. Do not upload or serve `.bukit/`
as website content. `.bukit-build-state.json` and `.bukit-output-marker` are
internal build files as well.

`build-report.json` reports actual build diagnostic counts. Its
`generatedFiles` array is the sorted, root-relative public output inventory and
excludes `.bukit/`, state/marker files, and files reachable only through
directory symlinks. SEO, publish, and security issue totals remain in their own
reports and are not copied into build health.

`security-report.json` includes the `publicOutputPrivacy` check when the build
uses Notion content. The check scans public text output for known Notion
identifiers and generated provider markers while excluding the internal files
described above. An unrelated application UUID is not rejected merely because
it has UUID syntax.

## Plugin Outputs

Built-in after-build plugins write aggregate outputs. External dynamic plugin
commands may write artifacts when invoked, but they are not part of the static
Core build output contract.
