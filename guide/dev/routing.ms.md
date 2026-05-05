# Penghalaan (Laluan Utama Koleksi dan Peraturan Keserasian)

Memetakan `ContentItem` kepada `RouteInfo(url, outputPath, template)`.

Pelaksanaan: `src/Bukit.Routing/RouteGenerator.cs`

## Penghalaan Dipacu Koleksi (Model Utama)
```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
```

## Corak Permalink (Keserasian)
Pemegang tempat: `{slug}`, `{year}`, `{month}`, `{day}`, `{type}`

Keutamaan: Tindihan Laluan > Peraturan Koleksi > Corak Permalink > Penghalaan lalai

## Tindihan Laluan
Apabila meta mengandungi `url`, `outputPath`, `template` — penghalaan lalai ditindih.

## Pengekodan outputPath: `none`/`slug`/`urlencode`/`sanitize`
