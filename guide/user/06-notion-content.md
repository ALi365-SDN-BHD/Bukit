# 06 Content (Notion): Complete Configuration & Examples for Using Notion as CMS

If you want "writing and editing" to happen in Notion rather than in the repository, the Notion mode lets you treat a Notion database as a CMS, automatically fetching and rendering content during build.

This page explains: which Notion fields are needed, how to filter/sort, how to pass custom fields to templates, and common token/permission issues.

For in-depth field normalization rules and developer contracts, see: [guide/dev/content](../dev/content.md) and `docs/notion_schema.md`.

## What You Will Get

- Recommended Notion database fields (can be created directly following this guide)
- A copy-ready `site.yaml` (Notion mode)
- A "simulated database table" (to help understand what each column means)
- Common errors and fixes (token, databaseId, field type mismatch)

## Prerequisites & Security Requirements

### 1) The environment variable NOTION_TOKEN must be set

The Notion token **can only be injected via environment variable** and must not be written into `site.yaml` (nor into any repository file).

Windows PowerShell (current session) example:

```powershell
$env:NOTION_TOKEN="secret_xxx"
```

In GitHub Actions, use repository Secrets (see: [13 Deploy GitHub Pages](./13-deploy-github-pages.md)).

### 2) The Notion Integration needs access to your database

You need to create an Integration in Notion and share the target database with that Integration, otherwise you will encounter "no permission / database not found" errors.

## Minimal Config (Notion provider)

```yaml
content:
  provider: notion
  notion:
    databaseId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
```

It is recommended to start with the "minimal config", get it working, then gradually add filter/sort/fieldPolicy.

## Media Config Change (Breaking Change)

Starting from the current version, image localization config is unified under `content.media` and no longer reads Notion-specific media fields.

Removed (no compatibility):
- `content.notion.downloadImagesToLocal`
- `content.notion.imageDownloadDir`
- `content.notion.imageUrlBase`
- `content.notion.defaultImageUrl`

Please switch to:

```yaml
content:
  provider: notion
  notion:
    databaseId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
  media:
    downloadToLocal: true
    downloadDir: assets/uploads
    urlBase: /assets/uploads
    defaultImageUrl: /assets/images/noneimg-news.jpg
    fieldKeys: [cover, image, thumbnail, og_image]
    maxConcurrency: 4
    maxRetries: 3
    timeoutMs: 10000
```

## Recommended Database Fields (follow this setup)

Field names below are based on Notion UI display names and are case-sensitive (recommend copy-pasting the field names directly).

### Engine Decision Fields (strongly recommended)

| Field Name | Type | Purpose |
|---|---|---|
| `Published` | checkbox | Whether to publish (recommend rendering only published content) |
| `Title` | title | Content title |
| `Slug` | rich_text or formula(string) | URL slug (default can be generated from Title, but explicit is recommended for stability) |
| `Type` | select or multi_select | `page`/`post` (for compatibility layer; recommended to additionally create a `Collection` field corresponding to site.collections key) |
| `PublishAt` | date | Publish date (default can use current time, but explicit is recommended) |

### Multilingual Fields (optional, but recommended)

| Field Name | Type | Purpose |
|---|---|---|
| `language` | rich_text / select | Content language (e.g., `zh-CN`/`en-US`) |
| `i18n_key` | rich_text | Stable key for cross-language content linking (e.g., `about`, `pricing`) |

### Template Custom Fields (as needed)

You can add arbitrary fields as "template fields", for example:

| Field Name | Type | Template Use |
|---|---|---|
| `SEO Title` | rich_text | `page.fields.seo_title.value` |
| `SEO Desc` | rich_text | `page.fields.seo_desc.value` |
| `cover` | files / url | Cover image (`page.fields.cover.value`) |
| `My Link` | url | Link (`page.fields.my_link.value`) |
| `reading_time` | number | Reading time |

## Simulated Data (Sample Database Table)

Below is a "simulated data" table to help you understand how a Notion page becomes site content (you can replicate a few test entries in Notion).

| Published | Title | Slug | Type | PublishAt | language | i18n_key | SEO Title | tags | categories |
|---|---|---|---|---|---|---|---|---|---|
| ✅ | About Us | about | page | 2026-01-01 | zh-CN | about | About Us - My Site | company,intro | docs |
| ✅ | About | about | page | 2026-01-01 | en-US | about | About - My Site | company,intro | docs |
| ✅ | First Blog Post | first-post | post | 2026-01-10 | zh-CN | blog_first | First Blog Post - My Site | release,roadmap | updates |
| ⬜ | Unpublished Draft | draft-1 | post | 2026-01-20 | zh-CN | draft_1 | Draft - My Site | draft | draft |

Notes:

- `Published` is used for build filtering to prevent drafts from going live
- `language + i18n_key` is used for multilingual site content linking (optional)
- Custom fields like `SEO Title` require `fieldPolicy` to allow them into templates (see next section)

> **Recommendation: Use site.collections instead of type default routing.** If you add a `Collection` field (select type, values like `blog`, `docs`) to your Notion database and declare the corresponding collection rules in site.yaml's site.collections, the engine will prefer collection-driven routing over type compatibility fallback.

## Filtering & Sorting (filter / sort)

### Render only published content

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    filterProperty: Published
    filterType: checkbox_true
```

### Sort by publish date descending

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    sortProperty: PublishAt
    sortDirection: descending
```

## Limits, Scoped Fetching & Caching (Large Databases / Reducing Notion Requests)

### 1) maxItems: Limit the maximum number of items fetched

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    maxItems: 5000
```

### 2) includeSlugs: Only fetch pages with the specified slugs

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    includeSlugProperty: Slug
    includeSlugs: [about, first-post]
```

Notes:

- `includeSlugs` filters at the Notion database query stage (not local filtering), suitable for "debugging a few articles / building only part of the site".
- The current filter uses `rich_text.equals`, so the `includeSlugProperty` field should be rich_text type; if your Slug uses formula/string, add a new rich_text field for filtering.

### 3) cacheMode/cacheDir: Cache content body rendering results

When `renderContent=true`, the engine reads Notion blocks and renders HTML. For large databases or CI scenarios, you can enable disk caching:

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    cacheMode: readwrite   # off | readwrite | readonly
    cacheDir: .cache/notion
```

Behavior:

- `off`: No caching (default)
- `readwrite`: Cache hit → reuse; cache miss → request Notion and write to cache
- `readonly`: Read cache only; cache miss → error (suitable for "forced offline / no Notion API" CI)

### 4) renderConcurrency/maxRps/maxRetries: Concurrent rendering & rate limiting (speed up initial builds)

When there are many pages and body content needs to be rendered (blocks API calls will be very dense), it is recommended to enable "controlled concurrency + global rate limiting" to stabilize throughput near Notion's request limit and reduce 429s:

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    renderConcurrency: 4
    maxRps: 3
    maxRetries: 5
```

Notes:

- `renderConcurrency`: How many page bodies to render simultaneously (higher values hide network RTT better, but CPU/memory usage increases).
- `maxRps`: Global rate limit for all Notion HTTP requests from this content source (including database query + blocks children), default 3 is recommended.
- `maxRetries`: Maximum retry count on 429, respects `Retry-After` backoff.

### 5) notion.stats: Request/throttle statistics log during build

When you enable `maxRps` (or trigger 429 retries), it is recommended to pay attention to the statistics log line output by the Notion content source at build completion:

```
event=notion.stats requests=1234 throttle_wait_count=56 throttle_wait_ms=7890
```

Field meanings:

- `requests`: Total Notion HTTP requests (including database query, blocks children, and extra requests from 429 retries)
- `throttle_wait_count`: Number of waits due to `maxRps` rate limiting
- `throttle_wait_ms`: Cumulative wait time (milliseconds) due to `maxRps` rate limiting

## fieldPolicy: Which Notion Fields Enter page.fields

Notion properties often don't all need to enter templates. You can control this with `fieldPolicy`:

## Supported Notion Field Types (entering page.fields)

When a field is allowed by `fieldPolicy`, it is mapped to `page.fields.<key>.type/value` based on the Notion field type:

| Notion Field Type | Template Field Type (page.fields.<key>.type) | value Form |
|---|---|---|
| `title` | `text` | string |
| `rich_text` | `text` | string |
| `url` | `text` | string (URL) |
| `email` | `text` | string |
| `phone_number` | `text` | string |
| `number` | `number` | number |
| `checkbox` | `bool` | bool |
| `date` | `date` | date |
| `created_time` | `date` | date |
| `last_edited_time` | `date` | date |
| `created_by` | `text` | string (username or id) |
| `last_edited_by` | `text` | string (username or id) |
| `select` | `text` | string |
| `status` | `text` | string |
| `multi_select` | `list` | string[] |
| `people` | `list` | string[] (username or id) |
| `relation` | `list` | string[] (related page id) |
| `files` | `file` | string (file URL) |
| `formula` | `text/number/bool/date` | depends on formula type |
| `rollup` | `number/date/list` | depends on rollup type |
| `unique_id` | `text` | string (prefix-number) |
| `verification` | `text` | string (state) |

Tip: If you only want to read a link in a template, avoid naming the field `Url` (normalized to `url`) to prevent confusion with "route override fields".

#### relation *_links Derived Fields (for outputting title + url)

For Notion `relation` fields, in addition to the original `page.fields.<key>.value` (list of related page IDs), the engine generates an additional derived field:

- `page.fields.<key>_links.type == "list"`
- `page.fields.<key>_links.value == [{ id, title, url, slug, type }, ...]`

Where:
- `id/title/slug/type`: Can be populated when the related page is also in the current fetch results; otherwise only `id`, others are null
- `url`: Prioritizes the related page's `Url` property (Notion url type field, normalized key `url`) promoted to meta as the external link; null if not set

Template example (generating a structure like `visa (https://...)`):

```scriban
{{ for x in page.fields.payments_links.value }}
  {{ if x.url }}
    <a href="{{ x.url }}">{{ x.title }}</a>
  {{ else }}
    {{ x.title }}
  {{ end }}
{{ end }}
```

#### Getting Related Page Details by pageId (site.data.pages_by_id)

When you have a Notion pageId in a template (typically from a `relation` field's `page.fields.<key>.value[]`), you can get that page's details via the site-wide index provided by the built-in `pages-index` plugin:

- `site.data.pages_by_id[pageId]` → `{ id, title, url, slug, type, publish_date, summary, fields }`
- pages-index is content-source agnostic: Markdown/Notion/multi-source all can use this index
- The index is generated during the build phase; template reads do not trigger API requests; to complete pages "not within this site's output scope", you need to enable pages-index's Notion completion capability (supports caching; only effective under Notion content sources)

Supplement: For Notion pages "not within this site's output scope" (e.g., relation points to a page in another database), if you enable pages-index's Notion completion capability, the following will be written:

- `url`: empty string (because it's not a route of this site)
- `external_url`: Notion page URL (can be used for direct navigation)

Example (mapping from relation's id list to title and link):

```scriban
{{ for pid in page.fields.related_posts.value }}
  {{ p = site.data.pages_by_id[pid] }}
  {{ if p }}
    {{ if p.url }}
      <a href="{{ site.base_url }}{{ p.url }}">{{ p.title }}</a>
    {{ else }}
      <a href="{{ p.external_url }}">{{ p.title }}</a>
    {{ end }}
  {{ end }}
{{ end }}
```

#### relation Used as tags/categories (taxonomy term generation rules)

The taxonomy plugin only reads `meta.tags` / `meta.categories` to generate `/tags/` and `/categories/` derived pages. The Notion provider promotes `tags/categories` from fields to meta:

- If `tags/categories` are regular `multi_select` (recommended), the term is directly the string you selected
- If `tags/categories` are `relation`, it will prefer using `title` from `tags_links/categories_links` to generate terms (fallback to `slug`, then to `id`)

When the target page of a relation is not in the current `databaseId` query results, the engine will make extra Notion API requests to fetch basic info for those target pages to complete `title/slug`, in order to generate readable terms (up to 200 target pages; exceeded means truncation to avoid request explosion during build).

### Whitelist (Recommended: controllable, safe, templates more stable)

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    fieldPolicy:
      mode: whitelist
      allowed:
        - seo_title
        - seo_desc
        - cover
        - reading_time
        - my_link
```

### All (convenient for debugging, but field changes more easily affect templates)

```yaml
content:
  provider: notion
  notion:
    databaseId: "..."
    fieldPolicy:
      mode: all
```

Field names are normalized (e.g., `SEO Title` → `seo_title`), so it is recommended to:

- Keep Notion-side field names as stable as possible
- Use "lowercase with underscores" uniformly on the template side (`page.fields.seo_title`)

Note:

- Notion's `url` type fields enter `page.fields.<key>.value`, the value is a string URL.
- If you name a field `Url` (normalized to `url`), it will simultaneously be used as a "route override field" (affecting the final URL of that page). If you just want to access a link in a template, it is recommended to use another name (e.g., `My Link`).

## Common Errors and Fixes

### 1) Error: Missing NOTION_TOKEN

Symptom: `doctor` or `build` fails immediately at the config validation stage.

Fix:

- Local: set the environment variable `NOTION_TOKEN`
- CI: add `NOTION_TOKEN` to GitHub Actions Secrets and inject it into the workflow environment

### 2) Error: Invalid databaseId / No Permission

Symptom: Fetch fails during build, Notion API related errors.

Fix checklist:

- Is databaseId the ID of a "database", not a page URL
- Has the Integration been shared with this database (Notion database top-right corner Share)
- Does the token belong to the same workspace / have permission

### 3) Error: Field Type Mismatch

For example, you created `PublishAt` as text but configured it for sorting.

Fix:

- Create fields according to the recommended types on this page (date/checkbox/select, etc.)
- Or adjust `filterProperty/sortProperty` to point to fields whose actual types match
