# 26 Mini-Program Client Integration

This handbook is for the developer of a WeChat mini-program that reads the data
Bukit publishes. [25 Mini-Program Data](25-miniprogram-data.md) covers the site
side: declaring datasets in `site.yaml` and what each build writes. This page
covers the client side: where the files are, what they contain, how to keep a
local copy in sync, and how to render it.

Bukit ships no client code. The code on this page is a reference
implementation you copy into your own mini-program and adapt.

## 1. Before You Start

| Item | Requirement |
|---|---|
| Site URL | `site.url` is an `https://` address on a domain you control. |
| Domain | ICP-registered and added in the WeChat admin console under **request domain** (and **downloadFile domain** if you download images to files). A default `*.github.io` domain usually cannot be added. |
| Caching | `catalog.json` must be served without caching (or with a short TTL); every other file under `mp/` can be cached forever. |
| Base library | Use `async`/`await` with **Enhanced compile** turned on in the project settings, or rewrite the samples with promises. |
| Data owner | Agree on the dataset names, fields and types with whoever maintains `site.yaml`. They are the API contract. |

Ask the site maintainer for the list of datasets, with `kind`, fields,
`types` and `required` for each. Those decide which fields exist and which may
be `null`.

## 2. Where The Files Are

Each site language has its own data root, at the same path as that language's
website plus the `miniprogram.outputDir` (default `mp`):

```text
https://example.com/mp/catalog.json          # default language
https://example.com/en/mp/catalog.json       # a language published under /en/
https://example.com/repo/mp/catalog.json     # a site with baseUrl /repo/
```

`catalog.json` is the only file with a fixed name. Every other file is named
after the hash of its bytes, so the client finds it only through the catalog.
Never build data file URLs yourself.

Configure the catalog URL of each language in the client, for example:

```js
// config.js
module.exports = {
  catalogs: {
    'zh-CN': 'https://example.com/mp/catalog.json',
    en: 'https://example.com/en/mp/catalog.json'
  },
  defaultLanguage: 'zh-CN',
  clientSchema: '1.0'          // the highest catalog schema this build understands
}
```

## 3. Data Contract

### 3.1 Catalog

```json
{
  "schema": "https://bukit.dev/schemas/miniprogram-catalog.v1.json",
  "schemaVersion": "1.0",
  "minClientSchema": "1.0",
  "language": "zh-CN",
  "generatedAt": "2026-11-18T08:00:00+08:00",
  "catalogHash": "sha256:3f9a…",
  "datasets": {
    "site":     { "kind": "site", "hash": "sha256:…", "url": "https://example.com/mp/site.1a2b3c4d5e6f7a8b.json", "size": 212 },
    "articles": {
      "kind": "collection", "hash": "sha256:…",
      "url": "https://example.com/mp/articles.list.9c1d0a2b3c4d5e6f.json", "size": 18342,
      "detailCatalog": { "hash": "sha256:…", "url": "https://example.com/mp/articles.details.77ab01cd23ef4567.json" }
    },
    "banners":  { "kind": "module", "hash": "sha256:…", "url": "https://example.com/mp/banners.5e6f….json", "size": 640 }
  }
}
```

| Field | Meaning |
|---|---|
| `schemaVersion` | Version of this catalog format, `<major>.<minor>`. |
| `minClientSchema` | The lowest client schema version that can read this data. |
| `catalogHash` | Changes whenever anything in the catalog except `generatedAt` changes. Compare it first. |
| `generatedAt` | The latest record update time. Informational only; do not use it to detect changes. |
| `datasets.<name>` | One entry per dataset. `site` is always present. Keys are in ordinal order. |
| `hash` | `sha256:` plus 64 lowercase hex characters of the file bytes. |
| `url` | Absolute `https://` URL of the file. |
| `size` | File size in bytes. |
| `detailCatalog` | Only on `collection` datasets: the file that lists the detail pages. |

The full format is published as a JSON Schema in
`docs/schemas/miniprogram-catalog.v1.schema.json` in the Bukit repository.

### 3.2 Dataset Files

All files are UTF-8 JSON objects. Record order is the order to display.

| Kind | File content |
|---|---|
| `site` | `{ "name", "title", "description", "url", "language", "languages": [...] }`. `url` is this language's site root. |
| `collection` list | `{ "items": [ { "id", ...listFields } ] }` |
| `collection` detail catalog | `{ "dataset": "<name>", "items": [ { "id", "hash", "url", "size", "updatedAt" } ] }`, ordered by `id` |
| `collection` detail | `{ "id", ...detailFields }`, one file per page |
| `module`, `records` | `{ "items": [ { "id", ...fields } ] }` |
| `dataIndex` | `{ "values": { "<key>": <value>, ... } }`, keys in ordinal order |
| `taxonomy` | `{ "items": [ { "name": "Bukit", "count": 3 } ] }`, ordered by name |
| `menu` | `{ "items": [ { "title", "url", "children": [...] } ] }`; `children` only when present |

Examples:

```json
{"items":[{"id":"/posts/hello/","title":"Hello","summary":"First post","cover":"https://example.com/media/a1b2.jpg","publishedAt":"2026-11-01","tags":["news"]}]}
```

```json
{"id":"/posts/hello/","title":"Hello","body":"<h2>Intro</h2><p>Text <a href=\"https://example.com/posts/world/\" data-bukit-id=\"/posts/world/\">next</a></p>","author":null}
```

```json
{"values":{"contactPhone":"+60 3-1234 5678","maintenance":false,"maxCart":20}}
```

### 3.3 Field Rules

- **`id`**: present on every record. Treat it as an opaque string key; do not
  parse it. A content page's id is its canonical key or, without one, its
  route, so it changes only when that changes. A data record's id is a hash of
  the source row's identity and stays the same while the row exists.
- **Declared fields only**: a record holds `id` plus exactly the fields
  declared for that dataset. An optional field with no value is written as
  `null`, not left out. A `required` field is never `null` or empty: the build
  fails instead.
- **Strings**: text, and also `image`/`url` values, which are absolute
  `http(s)` URLs, percent-encoded.
- **Numbers**: JSON numbers. Large integers and long decimals are written
  exactly, so values beyond 2^53 lose precision in `JSON.parse`. Ask for a
  `string` type if you need such a value exactly.
- **Dates**: a calendar date is `YYYY-MM-DD`; an instant is ISO 8601 with an
  offset (`2026-11-01T10:00:00+08:00`). Parse a calendar date as a local date,
  not with `new Date('2026-11-01')`, which reads it as UTC midnight.
- **`body`**: an HTML string from the subset in section 6.
- **No signed URLs**: `image`- and `url`-typed fields and the images, links
  and embeds in `body` never carry expiring Notion or cloud signatures; the
  build fails rather than publish one. Other fields (untyped, `string`,
  `string[]`) are written as they are, so ask for a `url` type on any field
  that can hold a file link.

### 3.4 Guarantees You Can Rely On

- The same data produces the same bytes, so an unchanged dataset keeps its hash
  and URL across builds.
- A file URL never changes content. Once downloaded, a file is valid for as
  long as the catalog lists its hash.
- Files from the previous build may be deleted on the next deploy. A URL that
  the current catalog no longer lists can return 404.
- Disabled, draft and non-indexable records are absent. A record that
  disappears from a list was unpublished; drop it locally.

## 4. Sync

### 4.1 Protocol

1. Request the catalog with a cache-busting query: `catalog.json?t=<now>`.
2. If `catalogHash` equals the stored one, stop: nothing changed.
3. If `minClientSchema` is newer than `clientSchema`, keep the local data and
   prompt the user to update the mini-program.
4. For each dataset whose `hash` changed, download the file now. For a changed
   `detailCatalog`, download it and mark details whose `hash` changed as
   stale; download each detail lazily when the user opens it.
5. Remove local datasets and details the catalog no longer lists.
6. Save the new catalog only after every download succeeded. If any download
   fails, keep the old catalog so the next launch retries.
7. If a data file returns 404, a newer deploy has replaced it: fetch the catalog
   again and retry once.
8. Keep about five requests in flight at most, and stay inside the storage
   limits: 1 MB per key and 10 MB in total.

### 4.2 Request Helper

```js
// bukit/request.js
function getJson(url) {
  return new Promise((resolve, reject) => {
    wx.request({
      url,
      method: 'GET',
      dataType: 'json',
      success: res => res.statusCode === 200
        ? resolve(res.data)
        : reject(Object.assign(new Error(`HTTP ${res.statusCode} ${url}`), { statusCode: res.statusCode })),
      fail: err => reject(Object.assign(new Error(err.errMsg || 'request failed'), { statusCode: 0 }))
    })
  })
}

// Runs tasks with at most `limit` in flight; rejects on the first failure.
async function pool(tasks, limit = 5) {
  const results = []
  let next = 0
  async function worker() {
    while (next < tasks.length) {
      const index = next++
      results[index] = await tasks[index]()
    }
  }
  await Promise.all(Array.from({ length: Math.min(limit, tasks.length) }, worker))
  return results
}

module.exports = { getJson, pool }
```

### 4.3 Local Store

Store each dataset and each detail under its own key, so no key gets near the
1 MB limit and details can be evicted one by one.

```js
// bukit/store.js
const prefix = lang => `bukit:${lang}:`

function read(lang, key) {
  try { return wx.getStorageSync(prefix(lang) + key) || null } catch (e) { return null }
}

function write(lang, key, value) {
  wx.setStorageSync(prefix(lang) + key, value)
}

function remove(lang, key) {
  try { wx.removeStorageSync(prefix(lang) + key) } catch (e) { /* already gone */ }
}

// Removes the least recently opened details until storage use is under `limitKb`.
function evictDetails(lang, limitKb = 8 * 1024) {
  const access = read(lang, 'access') || {}
  const keys = Object.keys(access).sort((a, b) => access[a] - access[b])
  while (wx.getStorageInfoSync().currentSize > limitKb && keys.length) {
    const key = keys.shift()
    remove(lang, key)
    delete access[key]
  }
  write(lang, 'access', access)
}

function touch(lang, key) {
  const access = read(lang, 'access') || {}
  access[key] = Date.now()
  write(lang, 'access', access)
}

module.exports = { read, write, remove, evictDetails, touch }
```

Keys used below:

| Key | Value |
|---|---|
| `catalog` | The last fully synced catalog. |
| `data:<dataset>` | The dataset file content. |
| `details:<dataset>` | The detail catalog content. |
| `detail:<dataset>:<id>` | `{ hash, data }` for one downloaded detail. |
| `access` | Last-open times of details, for eviction. |

### 4.4 Sync Function

```js
// bukit/sync.js
const config = require('../config')
const { getJson, pool } = require('./request')
const store = require('./store')

function newerThan(a, b) {               // 'major.minor' strings
  const [am, an] = a.split('.').map(Number)
  const [bm, bn] = b.split('.').map(Number)
  return am !== bm ? am > bm : an > bn
}

async function sync(lang = config.defaultLanguage, retried = false) {
  const remote = await getJson(`${config.catalogs[lang]}?t=${Date.now()}`)
  const local = store.read(lang, 'catalog')
  if (local && local.catalogHash === remote.catalogHash) return { changed: false }
  if (newerThan(remote.minClientSchema, config.clientSchema)) return { changed: false, updateRequired: true }

  const localSets = (local && local.datasets) || {}
  const tasks = []
  for (const [name, entry] of Object.entries(remote.datasets)) {
    const old = localSets[name] || {}
    if (old.hash !== entry.hash || !store.read(lang, `data:${name}`)) {
      tasks.push(async () => ({ key: `data:${name}`, value: await getJson(entry.url) }))
    }
    if (entry.detailCatalog && (!old.detailCatalog || old.detailCatalog.hash !== entry.detailCatalog.hash)) {
      tasks.push(async () => ({ key: `details:${name}`, value: await getJson(entry.detailCatalog.url) }))
    }
  }

  let downloads
  try {
    downloads = await pool(tasks)
  } catch (err) {
    if (err.statusCode === 404 && !retried) return sync(lang, true)
    throw err                             // the old catalog stays; next launch retries
  }

  for (const { key, value } of downloads) store.write(lang, key, value)
  for (const name of Object.keys(localSets)) {
    if (!remote.datasets[name]) {
      store.remove(lang, `data:${name}`)
      store.remove(lang, `details:${name}`)
    }
  }
  removeMissingDetails(lang, remote)
  store.write(lang, 'catalog', remote)    // only after every write succeeded
  return { changed: true }
}

// Drops downloaded details whose page was removed. Changed ones stay as stale copies.
function removeMissingDetails(lang, catalog) {
  const access = store.read(lang, 'access') || {}
  for (const key of Object.keys(access)) {
    const [, name, id] = key.match(/^detail:([^:]+):(.*)$/) || []
    if (!name) continue
    const details = catalog.datasets[name] && store.read(lang, `details:${name}`)
    if (!details || !details.items.some(item => item.id === id)) {
      store.remove(lang, key)
      delete access[key]
    }
  }
  store.write(lang, 'access', access)
}

module.exports = { sync }
```

Call it on launch and when the app returns to the foreground. Do not block
the first screen on it: render the local copy, sync in the background, then
refresh.

```js
// app.js
const { sync } = require('./bukit/sync')

App({
  onLaunch() { this.refresh() },
  onShow() { this.refresh() },
  refresh() {
    if (this.syncing) return
    this.syncing = sync()
      .then(result => {
        if (result.updateRequired) wx.showModal({ title: 'Update required', content: 'Please update to the latest version.', showCancel: false })
        if (result.changed) this.globalData.dataVersion = Date.now()
      })
      .catch(err => console.warn('bukit sync failed', err))
      .finally(() => { this.syncing = null })
  },
  globalData: { dataVersion: 0 }
})
```

### 4.5 Reading Data And Details

```js
// bukit/data.js
const { getJson } = require('./request')
const store = require('./store')

function dataset(name, lang) {
  return store.read(lang, `data:${name}`)
}

// Returns the detail, downloading it when missing or stale. Falls back to the
// stale copy when offline.
async function detail(name, id, lang) {
  const key = `detail:${name}:${id}`
  const cached = store.read(lang, key)
  const details = store.read(lang, `details:${name}`)
  const entry = details && details.items.find(item => item.id === id)
  if (!entry) return null                        // unpublished
  if (cached && cached.hash === entry.hash) {
    store.touch(lang, key)
    return cached.data
  }
  try {
    const data = await getJson(entry.url)
    store.write(lang, key, { hash: entry.hash, data })
    store.touch(lang, key)
    store.evictDetails(lang)
    return data
  } catch (err) {
    if (cached) return cached.data               // show the stale copy
    throw err
  }
}

module.exports = { dataset, detail }
```

A list page reads the list file; a detail page reads one detail:

```js
// pages/article/index.js
const { detail } = require('../../bukit/data')

Page({
  data: { article: null },
  async onLoad({ id }) {
    const lang = getApp().globalData.language || 'zh-CN'
    const article = await detail('articles', decodeURIComponent(id), lang)
    if (!article) return wx.showToast({ title: 'Not found', icon: 'none' })
    this.setData({ article, html: prepareBody(article.body) })
  }
})
```

Encode ids with `encodeURIComponent` when you put them in a page path; they
can contain `/`.

## 5. Language

- Keep a separate local copy per language (the store above prefixes keys with
  the language) and sync only the language in use.
- The `site` dataset lists `languages`. Show only languages you configured a
  catalog URL for.
- Ids are per language. A page in another language has a different id; do not
  map ids across languages.

## 6. Rendering `body`

`body` contains only these tags: `p`, `h1`–`h6`, `ul`, `ol`, `li`,
`blockquote`, `pre`, `code`, `strong`, `em`, `b`, `i`, `u`, `s`, `del`, `sub`,
`sup`, `a`, `img`, `table`, `thead`, `tbody`, `tfoot`, `tr`, `th`, `td`, `hr`,
`br` and `span`. The only attributes are `href` and `data-bukit-id` on `a`,
`src` and `alt` on `img`, and `colspan`/`rowspan` on cells. There are no `class`
or `style` attributes, and no paragraph contains a block element.

### 6.1 With `rich-text`

The built-in `rich-text` component renders the string directly. Because there
are no styles, add your own before rendering:

```js
// bukit/body.js
const styles = {
  img: 'max-width:100%;height:auto;display:block;margin:12px 0;',
  p: 'margin:0 0 12px;line-height:1.75;',
  h2: 'font-size:20px;font-weight:600;margin:20px 0 12px;',
  h3: 'font-size:17px;font-weight:600;margin:16px 0 10px;',
  pre: 'background:#f6f8fa;padding:12px;overflow-x:auto;font-size:13px;',
  blockquote: 'border-left:4px solid #ddd;padding-left:12px;color:#666;',
  table: 'border-collapse:collapse;width:100%;',
  td: 'border:1px solid #ddd;padding:6px;',
  th: 'border:1px solid #ddd;padding:6px;background:#f6f8fa;'
}

function prepareBody(html) {
  if (!html) return ''
  // The input never carries a style attribute, so a plain insert is safe.
  return html.replace(/<(img|p|h2|h3|pre|blockquote|table|td|th)(?=[\s>])/g,
    (match, tag) => `<${tag} style="${styles[tag]}"`)
}

module.exports = { prepareBody }
```

```xml
<rich-text nodes="{{html}}" user-select />
```

`rich-text` ignores taps on the elements inside it, so links in the body cannot
be opened and images cannot be previewed. If that is acceptable, you are done.

### 6.2 Opening Links And Images

To handle taps, render `body` with an HTML parser component that reports link
and image taps (several open-source parsers for mini-programs do), or convert
it to your own WXML nodes. Then route taps as follows:

| Tap target | Action |
|---|---|
| `a` with `data-bukit-id` | A page of a `collection` dataset, which may be another one than the current page's: find the dataset whose detail catalog lists the id, then open its detail page. |
| `a` whose `href` is on your site, without `data-bukit-id` | A website page that has no mini-program page: copy the link, or open it in a `web-view` if your account has that business domain. |
| Other `a` | External: copy the link with `wx.setClipboardData()` and tell the user. |
| `img` | `wx.previewImage()` with the `src` of every image in the body. |

```js
const store = require('./store')

// Detail page of each collection dataset.
const detailPages = { articles: '/pages/article/index', products: '/pages/product/index' }

function onLinkTap(attrs, lang) {
  const id = attrs['data-bukit-id']
  const catalog = id && store.read(lang, 'catalog')
  const name = catalog && Object.keys(detailPages).find(dataset => {
    const details = catalog.datasets[dataset] && store.read(lang, `details:${dataset}`)
    return details && details.items.some(item => item.id === id)
  })
  if (name) return wx.navigateTo({ url: `${detailPages[name]}?id=${encodeURIComponent(id)}` })
  wx.setClipboardData({ data: attrs.href })
}
```

`data-bukit-id` links pages of every `collection` dataset of the same
language. Ids are unique within a dataset; if two collections can share an id
(for example a canonical key reused across them), check the current dataset
first.

## 7. Menus And Site Links

`menu` items and `url` fields point to the website, as absolute URLs. Map
them to mini-program pages with a route table owned by the client:

```js
const routes = [
  { match: /^\/$/, page: '/pages/home/index' },
  { match: /^\/posts\/$/, page: '/pages/articles/index' }
]

function openSiteUrl(url, siteRoot) {
  const path = url.startsWith(siteRoot) ? '/' + url.slice(siteRoot.length) : null
  const route = path && routes.find(r => r.match.test(path))
  if (route) return wx.navigateTo({ url: route.page })
  wx.setClipboardData({ data: url })
}
```

`siteRoot` is the `url` of the `site` dataset. Pages that are tab-bar pages
need `wx.switchTab()` instead of `wx.navigateTo()`.

## 8. First Launch And Offline Use

- With no local data and no network, the app has nothing to show. To avoid an
  empty first screen, bundle a snapshot: copy a built `mp/` directory into the
  package, and load it into storage on first launch before the first sync. The
  sync then downloads only what changed since the snapshot.
- Keep the package within the size limit: bundle the catalog, list files and
  small datasets, not every detail file.
- After a failed sync, the app keeps working from the local copy; the next
  launch retries.

## 9. Testing

- **Against a test site**: build and deploy the site to a staging host with
  its own `https://` `site.url`, and point `catalogs` in `config.js` at it. Dataset
  URLs in a catalog always use the `site.url` it was built with.
- **In the developer tools**: you can turn off domain checking in the local
  project settings while testing against a domain that is not yet in the
  admin console. Production and preview builds on a phone still need the
  domain configured.
- **Locally**: `bukit build` writes `dist/mp/`; `bukit dev` skips `mp/` when
  `site.url` is unset. The catalog in `dist/` points to the production
  `site.url`, so test the files themselves (shapes, fields) rather than
  serving them locally.
- **Contract checks**: validate the catalog against
  `docs/schemas/miniprogram-catalog.v1.schema.json` in CI, and check that each
  dataset you depend on is present with the fields you read.
- **Sync cases to try**: no change (only the catalog is requested); one record
  edited; a record unpublished; a dataset removed from `site.yaml`; a deploy
  between catalog and file requests (404 then retry); offline launch.

## 10. Versioning

- The current catalog format is `schemaVersion` `1.0`, with `minClientSchema`
  `1.0`. Ignore keys you do not know, so a later addition does not break an
  older client.
- `1.1` adds `object` and `object[]` fields: a field's value can be a JSON
  object or an array of objects, with keys in ordinal order. A catalog is `1.1`
  (with `minClientSchema` `1.1`) only when the site declares such a field, so
  set `clientSchema: '1.1'` once the client renders them. Nested values may be
  `null`, and nested strings are plain text: show them with `text`, never with
  `rich-text`. For example, a route detail with `days: object[]` reads
  `detail.days[0].sights` as an array of strings.
- `minClientSchema` is the signal for an incompatible change: when it is newer
  than the client's `clientSchema`, the client keeps its local copy and asks
  the user to update.
- Adding, removing or retyping fields in `site.yaml` changes the data without
  changing the schema version. Agree such changes with the client developer
  first, and treat every field you read as possibly `null` unless it is
  `required`.

## 11. Checklist

- [ ] Site domain is HTTPS, ICP-registered and added as a request domain.
- [ ] `catalog.json` is served without caching.
- [ ] Catalog URL configured for every supported language.
- [ ] Sync compares `catalogHash`, honors `minClientSchema`, and saves the
      catalog last.
- [ ] 404 on a data file triggers one catalog refetch.
- [ ] Details load lazily and fall back to the stale copy offline.
- [ ] Storage stays under 10 MB; details are evicted least recently used first.
- [ ] Optional fields handle `null`; calendar dates are parsed as local dates.
- [ ] `body` images are styled to fit the screen; link and image taps are
      handled or knowingly ignored.
- [ ] Menu and site URLs map to mini-program pages.
