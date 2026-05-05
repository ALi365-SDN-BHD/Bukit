# Sistem Kandungan (Markdown / Notion / sources)

Rujukan pembangun untuk sistem kandungan, meliputi model seragam, pelaksanaan pembekal, dan peraturan penormalan medan.

Pelaksanaan: `src/Bukit.Content/*`, `src/Bukit.Engine.Abstractions/ContentItem.cs`

## Model Seragam: ContentItem
Semua sumber kandungan akhirnya mendarat pada `ContentItem`:
- `Id`, `Title`, `Slug`, `PublishAt`, `Language`
- `Meta`: Metadata yang mempengaruhi keputusan enjin
- `Fields`: Medan tersuai untuk penggunaan templat
- `ContentHtml`: Badan HTML (mungkin null dengan BodyKey)

## Pembahagian Meta vs Fields
- **Meta**: Keputusan enjin — `type`, `language`, `draft`, `route`, `sourceMode`, `tags`, `categories`, `collection`
- **Fields**: Penggunaan templat — medan SEO, medan perniagaan, imej, masa membaca

## Pembekal Markdown
`MarkdownFolderProvider.cs`: Membaca fail `*.md` secara rekursif, menghuraikan front matter YAML.

## Pembekal Notion
`NotionContentProvider.cs`: Mengambil halaman dari pangkalan data Notion, menghasilkan blok, memetakan sifat.

## Pembekal Komposit (mod sources)
`CompositeContentProvider.cs`: Mengagregatkan pelbagai sumber secara serentak.

## Penyetempatan Imej (`content.media`)
Disatukan merentasi semua pembekal: memuat turun imej jauh secara setempat, menggantikan URL.
