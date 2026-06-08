# Antarabangsa dan SEO (mod sitemap/rss/search)

Pelaksanaan: `src/Bukit.Config/AppConfig.cs`, `src/Bukit.Engine/SiteEngine.cs`

## Struktur Output Pelbagai Bahasa
Apabila `site.languages` ditetapkan: output menggunakan subdirektori per-bahasa: `dist/<lang>/...`

## sitemapMode: `split` (setiap bahasa), `merged` (gabungan dengan hreflang), `index` (indeks menunjuk ke setiap bahasa)
## feedMode (legasi/compat)

`site.rssMode` telah dibuang daripada konfigurasi pengguna dalam Bukit 1.0. Strategi feed dikawal melalui `site.feed` (terutamanya `site.feed.formats`) dan pilihan plugin feed. Perilaku merged/split lama hanya kekal pada laluan keserasian migrasi.
## searchMode: `split`, `merged`, atau `index`

## Sempadan baseUrl dan site.url
- `site.baseUrl`: pautan relatif dalaman
- `site.url`: URL mutlak untuk sitemap/rss
