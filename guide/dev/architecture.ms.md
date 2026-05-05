# Seni Bina dan Sempadan Modul

Menerangkan saluran paip binaan hujung-ke-hujung Bukit, sempadan modul, dan struktur data utama.

## Aliran Data Hujung-ke-Hujung
```text
CLI → Config → SiteEngine.BuildAsync
  → IContentProviderFactory (Markdown/Notion/sources)
  → BuildVariantAsync setiap bahasa
    → RouteGenerator → DataModuleBuilder → PluginRunner → PageRenderDispatcher
  → I18nOutputMerger → MetricsWriter
```

## Pembahagian Modul
| Modul | Tanggungjawab |
|---|---|
| `Bukit.Cli` | Penghuraian perintah, resolusi konfigurasi |
| `Bukit.Config` | Penghuraian `site.yaml`, lalai, pengesahan |
| `Bukit.Content` | Pemuatan kandungan → ContentItem |
| `Bukit.Routing` | ContentItem → RouteInfo |
| `Bukit.Rendering` | Model rendering, pengikatan Scriban |
| `Bukit.Engine` | Orkestrasi binaan, tokokan, plugin |

## Komponen Dalaman Enjin
| Komponen | Tanggungjawab |
|---|---|
| `SiteEngine` | Orkestrator nipis |
| `PageRenderDispatcher` | Rendering halaman selari dengan tokokan |
| `DataModuleBuilder` | `mode=data` → `site.modules` |
| `I18nOutputMerger` | Orkestrasi pelbagai bahasa |

## Prinsip Penyelenggaraan
- Kontrak luaran dahulu: Medan konfigurasi, parameter CLI adalah antara muka stabil
- Kebergantungan sehala: Cli → Config/Engine; Engine → Content/Routing/Rendering
- Sempadan tanggungjawab jelas
