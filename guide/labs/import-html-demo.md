# Import HTML Demo (Labs)

HTML demo import is not part of the static Core command registry. It runs as the
external `import` plugin (`src/Bukit-Plugins/Bukit.Plugin.Import`). Core builds the
result like any other site: `site.yaml`, Markdown content, a theme, and assets.

## Quick start

```bash
bukit import html-demo ./demo --theme corp --verify
```

| Output | Path |
|---|---|
| Theme (layouts, page templates, assets) | `themes/<theme>/` |
| Site config | `sites/<theme>/site.yaml` |
| Content drafts | `sites/<theme>/content/{pages,posts,companies,services}/` |
| Notion seed data | `sites/<theme>/notion-seed/` |
| Copy of the demo | `sites/<theme>/original-demo/` |
| Import report | `sites/<theme>/import-report.md` |
| Import manifest | `sites/<theme>/.bukit-import.json` |

`--verify` builds the site after import. A passing build proves the output is
valid, not that every page looks like the demo: review the report's warnings and
compare the built pages with `original-demo/`.

## What the demo must look like

- A root `index.html` (the home page), or `index.htm` when there is no `index.html`.
  Every other `*.html`/`*.htm` file becomes a page.
- Flat files (`about.html`) and folder pages (`about/index.html`) both work; both
  become the `about` page at `/about/`. Only the root `index.html`/`index.htm` is
  the home page; when both exist, `index.html` wins and the import says so.
- Files may be UTF-8, or declare another encoding with `<meta charset>` (GBK and
  GB2312 are read as GB18030). Undeclared files that are not valid UTF-8 are read
  as GB18030 when that yields Chinese text, else as Windows-1252, and the import
  warns (`ENCODING_GUESSED`); add a `<meta charset>` if the guess is wrong. A file
  that is valid UTF-8 is read as UTF-8 even when it still declares an older
  encoding such as `gb2312`, with the same warning.
- The import refuses demos that contain secrets or tooling: `.env*`, `.npmrc`,
  `.git`, `node_modules`, `.vscode`, SSH keys, and `.key/.pem/.pfx/.p12/.crt/.cert`
  files. `dist/` and `build/` folders are allowed and their assets are imported,
  but HTML pages inside them are skipped as build output (the import reports how
  many).
- Symbolic links are skipped and reported (`SYMLINK_SKIPPED`); their targets are
  never copied.

## How pages are mapped

| Demo page | Kind | URL | Template |
|---|---|---|---|
| `index.html` | home | `/` | `pages/index.html` |
| `about.html`, `about/index.html`, unrecognised pages | page | `/<slug>/` | `pages/<name>.html` |
| `news.html`, `blog.html`, `insights.html` | post list | `/insights/` | its own template |
| pages linked from list cards, or in `news/`, `blog/`, `posts/`… | post detail | `/insights/<slug>/` | its own template |
| `companies.html`, `services.html` and their details | company/service list and detail | `/companies/…`, `/services/…` | its own template |

Every demo page renders with its own template, and keeps its own `<link>`,
`<style>`, and `<script>` tags. `pages/page.html`, `post.html`, `company.html`, and
`service.html` are fallbacks for content that has no demo page of its own.

The home template keeps the demo's markup as written; edit
`themes/<theme>/layouts/pages/index.html` to change it. Other pages put their main
text in a Markdown draft and keep the surrounding markup in the template.

Links may name either `.html` or `.htm`; when a link uses the other extension than
the imported page, it is resolved to that page. A demo typo such as linking to
`about.htm` when only `about.html` exists is therefore corrected silently in the
output (the original demo is still in `original-demo/`).

Template syntax in the demo (Vue or Angular `{{ }}`, `{% %}`) is escaped and prints
exactly as written. Core Markdown does not render raw HTML, so class names inside a
page's main text are not kept; keep styled blocks in the template.

## Route map

`--route-map route-map.yaml` overrides the kind, URL, slug, or template of pages:

```yaml
pages:
  - source: about/index.html   # exact demo path, or a name such as about.html
    type: page                 # home, page, postList, postDetail, companyList,
                               # companyDetail, serviceList, serviceDetail
    route: /company/about/
    template: company-about    # simple name, no path
```

A `source` that names a path (`about/index.html`, `en/about.html`) matches only that
file. A bare file name (`about.html`) matches every page of that name, including
`about/index.html`, unless a path entry claims the page. An unknown `type`, a template
path, or a multi-segment slug fails the import. Two pages may not claim the same
template.

## Site config

`site.yaml` takes the site name from the title segment most pages share
(`关于我们 - 华南精密` → `华南精密`), the address from an absolute `--base-url` or the
home page's canonical or `og:url` link, and the description from the home page's
`<meta name="description">`. Without an address, `site.url` is
`https://example.com` and the import warns: set the real address before
publishing, or canonical links and the sitemap point at the placeholder.

Each page draft takes its `<meta name="description">` as `seo_desc`.

For a sub-path deploy, pass `--base-url /corp/` (or `https://example.org/corp/`);
links and assets are written to work under that path.

## Importing again

Run the same command with `--force` after the demo changes. The theme is rebuilt
from scratch. For `site.yaml` and the content drafts, the import manifest decides:

- files the last import wrote and nobody edited are updated, or removed when the
  demo no longer produces them;
- files edited since the last import are kept, with a warning; pass `--overwrite`
  to replace them;
- files the importer did not write (including output of imports made before the
  manifest existed) are kept unless you pass `--overwrite`.

Drafts are only removed when this import wrote drafts: with `--no-extract-content`
or `--build-source notion`, existing drafts are left alone. If an import fails part
way, nothing is removed and the files it wrote are still recorded.

A kept draft whose template no longer exists is reported, because Core would fail
the build on it.

## Notion

`--push-notion` (or `bukit notion push --input sites/<theme>/notion-seed`) pushes
the seed records. Databases created by the push get every property. Existing
databases need `Title`, `Slug`, `Type`, `Summary`, `Language`, `Published`,
`SeoTitle`, and `SeoDescription`. `Template` and `Url` (both rich text; imported
routes are site paths such as `/about/`) are optional: without them the push skips
those values and says so, and pages use their collection's template and permalink.
Property names are case-sensitive: a property called `URL` is not `Url`.

Link values that are site paths (navigation `Link`, page `Url`) are pushed as rich
text, because Notion url properties are not documented to accept relative paths.
A navigation database whose `Link` property has the url type fails schema
validation on such values: change `Link` to rich text, or let the push create the
database. The token comes from `NOTION_TOKEN`, or the
variable named by `--notion-token-env`.

`notion-seed/notion-database-map.yaml` is created on the first import and never
overwritten: database ids written back by the push stay, and re-imports do not
reset them. If a newer importer adds a collection to the default template, delete
the file and import again to regenerate it; back up the existing ids first.

## Known limits

- List and detail URLs are fixed (`/insights/`, `/companies/`, `/services/`);
  use the route map or edit `site.yaml` to change them.
- No redirects are generated from the demo's old `.html` URLs.
- Slugs come from file names; Chinese file names give Chinese slugs.
- Shared header and footer extraction can fail when pages' markup differs too
  much; the import then says so, and you can write the partials by hand.
