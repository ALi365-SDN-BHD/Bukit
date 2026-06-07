# Graf Panggilan Modul Bukit / BukitJalil

Dokumen ini menggambarkan hubungan kebergantungan dan panggilan modul.

## Graf Kebergantungan Peringkat Atas
```text
Bukit.Cli → Bukit.Engine → Bukit.Config + Content + Rendering + Routing + Shared + Abstractions
```

## Urutan Panggilan Saluran Paip Binaan
```text
Program.Main
  → BuildCommand.RunAsync
    → ConfigPathResolver → ConfigLoader → ConfigValidator
    → SiteEngine.BuildAsync
      → ContentProviderFactory → LoadAsync
      → BuildVariantAsync (setiap bahasa)
        → RouteGenerator → DataModuleBuilder
        → PluginRunner (DerivePages + AfterBuild)
        → PageRenderDispatcher → ITemplateRenderer
      → I18nOutputMerger → MetricsWriter
```

## Graf Pemuatan Plugin
```text
PluginRegistry.GetAllPlugins
  → BuiltInPluginSource + GeneratedPluginSource
  → ExternalPluginSource (Non-AOT sahaja)
  → ExternalProtocolPluginSource (process/wasm)
```

## Graf Pembekal Kandungan
```text
ContentProviderFactory.Create
  → markdown: MarkdownFolderProvider
  → notion: NotionContentProvider
  → sources: CompositeContentProvider
```

## Aliran Model Data
```text
Markdown/Notion/sources → ContentDocument
  → RouteGenerator → RouteInfo
    → PageRenderDispatcher → ITemplateRenderer → HTML
```

## Struktur Data Utama
| Struktur | Ditakrifkan Dalam | Digunakan Oleh |
|---|---|---|
| `ContentDocument` | `Engine.Abstractions` | Content, Routing, Rendering, Plugins |
| `RouteInfo` | `Engine.Abstractions` | Routing, Rendering, Plugins |
| `BuildContext` | `Engine.Abstractions` | Plugins |
| `AppConfig` | `Config` | Config, Engine |
