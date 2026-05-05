# bukit (Enjin Tapak Statik .NET 10 Native AOT)

Versi bahasa: [English](./README.md) | [简体中文](./README.zh-CN.md) | Bahasa Melayu (semasa)

Enjin tapak statik berasaskan aliran kerja "nota sebagai CMS". Kandungan boleh datang daripada Notion (atau Markdown tempatan), kemudian dibina dan dideploy ke GitHub Pages melalui GitHub Actions.

## Dokumen

- Panduan pengguna: [`guide/user`](guide/user/README.ms.md)
- Panduan pembangun: [`guide/dev`](guide/dev/README.ms.md)
- Nota tadbir urus: [`guide/dev/perf-aot-governance.md`](guide/dev/perf-aot-governance.md)
- Rujukan penuh dalam bahasa Cina: [`README.zh-CN.md`](README.zh-CN.md)

## Mula Pantas (guna contoh tapak dalam repositori ini)

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

## Perintah CLI Teras

### Cipta tapak

```bash
dotnet run --project src/Bukit.Cli -c Release -- create my-site
dotnet run --project src/Bukit.Cli -c Release -- create my-site --provider notion
```

### Bina

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean
dotnet run --project src/Bukit.Cli -c Release -- build --clean --metrics metrics.json --log-format json
dotnet run --project src/Bukit.Cli -c Release -- build --clean --jobs 8
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

### Semak / Bersih / Tema

```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- clean --dir dist
dotnet run --project src/Bukit.Cli -c Release -- theme list --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- theme use alt --config site.yaml
```

## Medan Penting `site.yaml`

- `site.collections`: model utama yang disyorkan untuk organisasi kandungan dan routing (isytihar `permalink`, `template`, dan `listRoute` pilihan setiap koleksi). Peraturan lalai `post/page` kekal sebagai lapisan keserasian.
- `site.baseUrl`: sublaluan GitHub Pages (`/my-repo`) atau `/` untuk root.
- `site.url`: URL kanonik tapak (sitemap/rss); boleh ditindih dengan `--site-url`.
- `content.provider`: `markdown` atau `notion`.
- `content.markdown.maxItems`: jumlah maksimum item Markdown.
- `content.notion.maxItems`: jumlah maksimum halaman Notion.
- `content.notion.cacheMode/cacheDir`: pilihan cache render Notion.
- `build.output`: direktori output.
- `theme.layouts/assets/static`: direktori tema.

## Bina Tapak dengan AI (v2)

- Panduan: [`guide/ai/chatgpt/README.ms.md`](guide/ai/chatgpt/README.ms.md)
- Kontrak Intent: [`guide/dev/intent-cli.md`](guide/dev/intent-cli.md)
- Prompt Pack ChatGPT: [`guide/ai/chatgpt`](guide/ai/chatgpt/README.ms.md)

## Sumber Kandungan Notion

- Token mesti dibekalkan melalui pemboleh ubah persekitaran sahaja: `NOTION_TOKEN`.
- Rujukan skema v1: [`guide/dev/content.md`](guide/dev/content.md)

## GitHub Actions + GitHub Pages

Templat aliran kerja disediakan di [`.github/workflows/pages.yml`](.github/workflows/pages.yml).
Salin ke repositori anda dan ubah suai mengikut keperluan. Lihat [`guide/user/13-部署-GitHub-Pages.md`](guide/user/13-部署-GitHub-Pages.md) untuk panduan terperinci.

Langkah biasa:

1. Di GitHub Settings → Pages, pilih "GitHub Actions".
2. Jika guna Notion, tambah `NOTION_TOKEN` dalam repository secrets.
3. Push ke `main` selepas aliran kerja anda disediakan untuk bina dan terbitkan tapak.

## Penerbitan AOT

```bash
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit
dotnet publish src/Bukit.Cli -c AOT -r linux-x64 -o out/bukit -p:BukitStripSymbols=true
dotnet publish src/Bukit.Cli -c AOT -r win-x64 -o out/bukit
```

## Matriks Pengesahan

```bash
dotnet build bukit.slnx -c Release -warnaserror
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
dotnet format bukit.slnx --verify-no-changes
```
