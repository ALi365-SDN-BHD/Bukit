# Plugin Terbina Dalam (BuiltIn) Artifak dan Sempadan

Halaman ini menerangkan kontrak output plugin terbina dalam.

Direktori pelaksanaan: `src/Bukit.Engine/Plugins/BuiltIn/`

## sitemap (IAfterBuildPlugin)
- Output: `sitemap.xml` (memerlukan `site.url`)
- Merangkumi: `/`, `/blog/`, `/pages/`, semua routed + derived
- Pelbagai bahasa: `merged`/`split` mengikut `site.sitemapMode`

## rss (IAfterBuildPlugin)
- Output: `rss.xml` (memerlukan `site.url`)
- Input: kandungan routed sahaja

## search-index (IAfterBuildPlugin)
- Output: `search.json` (tidak memerlukan `site.url`)
- `site.searchIncludeDerived` mengawal kemasukan halaman derived

## taxonomy (IDerivePagesPlugin + IAfterBuildPlugin)
- Halaman: `/tags/`, `/tags/<slug>/`, `/categories/`, `/categories/<slug>/`
- Templat: lalai `pages/page.html`, boleh dikonfigurasi

Lihat: [plugins.md](./plugins.md), [i18n-seo.md](./i18n-seo.md)
