# Titik Masuk Sumber Mengikut Jenis Perubahan

Membantu penyelenggara mencari titik masuk sumber **mengikut jenis perubahan**.

## 1. Menukar CLI / Parameter / Konfigurasi
- `src/Bukit.Cli/Program.cs`, `src/Bukit.Cli/Commands/BuildCommand.cs`
- `src/Bukit.Config/ConfigLoader.cs`, `src/Bukit.Config/ConfigValidator.cs`

## 2. Menukar Pengambilan Sumber Kandungan
- `src/Bukit.Engine/ContentProviderFactory.cs`
- `src/Bukit.Content/Markdown/MarkdownFolderProvider.cs`
- `src/Bukit.Content/Notion/NotionContentProvider.cs`

## 3. Menukar Penghalaan / URL / Laluan Output
- `src/Bukit.Routing/RouteGenerator.cs`

## 4. Menukar Rendering / Tema / Pembolehubah Templat
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- `src/Bukit.Rendering/Scriban/ScribanModelBinder.cs`
- `src/Bukit.Engine/PageRenderDispatcher.cs`

## 5. Menukar Plugin / Artifak Output
- `src/Bukit.Engine/Plugins/PluginRegistry.cs`
- `src/Bukit.Engine/Plugins/PluginRunner.cs`

## 6. Menukar Binaan Tokokan / Langkau Render / Caching
- `src/Bukit.Engine/SiteEngine.cs`, `src/Bukit.Engine/PageRenderDispatcher.cs`
- `src/Bukit.Engine/Incremental/BuildManifest.cs`

## Jadual Keputusan Pantas
| Apa yang Ingin Ditukar | Hentian Pertama |
|---|---|
| Perintah atau parameter | `Program.cs` / `BuildCommand.cs` |
| Medan atau pengesahan konfigurasi | `ConfigLoader.cs` / `ConfigValidator.cs` |
| URL halaman dan laluan output | `RouteGenerator.cs` |
| Pembolehubah templat dan rendering | `ScribanModelBinder.cs` |
| Carian, RSS, sitemap, taksonomi | `PluginRunner.cs` + `Plugins/BuiltIn/*` |
