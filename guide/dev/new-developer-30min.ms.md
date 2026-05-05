# Panduan Permulaan 30 Minit untuk Pembangun Baharu

Panduan ini membantu penyumbang baharu menjadi produktif dalam 30 minit.

## 1. Prasyarat (5 min)
- .NET 10 SDK, Git, PowerShell (Windows) atau bash (Linux/macOS)

## 2. Klon dan Bina (5 min)
```bash
git clone <repo-url> bukit && cd bukit
dotnet build bukit.slnx -c Release
```

## 3. Jalankan Tapak Contoh (5 min)
```bash
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

## 4. Jalankan Ujian (5 min)
```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
```

## 5. Model Mental (5 min)
Tiga perkara untuk diingat:
1. **Kandungan datang dari suatu tempat** (Markdown/Notion/sources)
2. **Setiap item kandungan dioutputkan ke suatu tempat** (Penghalaan → URL + templat)
3. **Templat menghasilkannya** (Scriban → HTML)

Saluran paip binaan:
```
site.yaml → Kandungan → Penghalaan → Rendering → Plugin → dist/
```

## 6. Fail Utama untuk Dibaca (5 min)
1. `src/Bukit.Cli/Program.cs` - Titik masuk
2. `src/Bukit.Engine/SiteEngine.cs` - Orkestrasi binaan
3. `src/Bukit.Routing/RouteGenerator.cs` - Penjanaan URL
4. `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs` - Pemuatan kandungan
5. `src/Bukit.Engine/Plugins/PluginRunner.cs` - Pelaksanaan plugin

## 7. Membuat Perubahan Pertama Anda
1. Jalankan ujian untuk menetapkan garis dasar
2. Buat perubahan
3. Jalankan ujian berkaitan
4. Jalankan asap: `pwsh ./scripts/smoke.ps1`
5. Bina tapak contoh untuk mengesahkan

## 8. Aliran Kerja Lazim
### Tambah parameter CLI: Tambah ke `BukitCliSpecs.cs`, kemudian `BuildCommand.cs`
### Tambah medan kandungan: Tambah ke `ContentItem.Fields` semasa pemuatan kandungan
### Tambah plugin: Laksana `IBukitPlugin` + `[BukitPlugin]`, letak dalam `plugins/`

## 9. Pergi Lebih Mendalam
- [architecture.md](./architecture.md) - [code-wiki.md](./code-wiki.md)
- [maintainer-entrypoints.md](./maintainer-entrypoints.md)
