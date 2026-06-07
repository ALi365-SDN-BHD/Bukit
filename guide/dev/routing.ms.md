# Penghalaan (Laluan Utama Koleksi dan Peraturan Keserasian)

Memetakan `ContentDocument` kepada `RouteInfo(url, outputPath, template)`.

Pelaksanaan: `src/Bukit.Routing/RouteGenerator.cs`, `src/Bukit.Routing/RoutePathBuilder.cs`, `src/Bukit.Engine/RouteInventoryValidator.cs`

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

Keutamaan (tinggi ke rendah):
1. Tindihan penuh (url + outputPath + template)
2. Tindihan separa (url sahaja atau url + template)
3. Peraturan Koleksi (`site.collections`)
4. Corak Permalink (`site.permalinks`)
5. Penghalaan lalai dibuang kerana laluan kini dikawal oleh peraturan koleksi/permalink/rute terarah.

## Tindihan Laluan

### Tindihan Penuh
Apabila ketiga-tiga `url`, `outputPath`, `template` hadir dalam meta — penghalaan lalai ditindih sepenuhnya.

### Tindihan Separa (url sahaja)
Apabila hanya `url` disediakan, `outputPath` diterbitkan secara automatik daripada URL. `template` diwarisi daripada peraturan koleksi/permalinks/lalai.
`outputPath` sahaja **tidak disokong**.

## Pengekodan outputPath: `none`/`slug`/`urlencode`/`sanitize`

`site.outputPathEncoding` digunakan untuk kedua-dua halaman kandungan dan halaman terbitan (pagination, arkib, taksonomi).

## Utiliti Laluan (`RoutePathBuilder`)

| Kaedah | Tujuan |
|--------|---------|
| `NormalizeUrl(url)` | Pastikan garis miring depan/belakang |
| `NormalizeListRoute(url)` | Normalisasi laluan senarai (lalai `/`) |
| `BuildOutputPathFromUrl(url, encoding)` | URL → laluan output dengan `index.html` |
| `NormalizeOutputPath(path, encoding)` | Guna pengekodan ke segmen laluan |

## Pengesanan Konflik Laluan

`RouteInventoryValidator` mengesahkan keunikan laluan pada dua titik:
1. **Selepas penjanaan laluan kandungan** — semak konflik URL/outputPath halaman kandungan
2. **Sebelum rendering** — semak inventori lengkap (kandungan + terbitan + senarai)

`bukit doctor` juga menjalankan pengesahan laluan kandungan tanpa binaan penuh.

Konflik halaman terbitan dikawal oleh `site.deriveConflictPolicy`: `fail` (lalai), `warn` (langkau + log), `last-wins` (terima halaman terbitan). Konflik antara halaman kandungan sentiasa gagal.
