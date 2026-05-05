# Wiki Kod Bukit

Struktur repositori, seni bina teras, tanggungjawab modul, kelas dan fungsi utama.

## Struktur Repositori
```text
Bukit
├─ src/
│  ├─ Bukit.Cli/                 # Masuk CLI, penghantaran perintah
│  ├─ Bukit.Config/              # Penghuraian site.yaml, pengesahan
│  ├─ Bukit.Content/             # Pemuatan Markdown/Notion/sumber
│  ├─ Bukit.Engine.Abstractions/ # ContentItem, RouteInfo, kontrak plugin
│  ├─ Bukit.Engine/              # Orkestrasi binaan, tokokan, plugin
│  ├─ Bukit.Rendering/           # Model templat, pengikatan Scriban
│  ├─ Bukit.Routing/             # Pemetaan kandungan-ke-laluan
│  └─ Bukit.Shared/              # Pengelogan, pengecualian
├─ tests/  ├─ examples/starter/  ├─ guide/
├─ scripts/  ├─ tools/scriban/  └─ docs/
```

## Tanggungjawab Modul Teras
| Modul | Titik Masuk Utama |
|---|---|
| `Bukit.Cli` | `Program.cs`, `Commands/*` |
| `Bukit.Config` | `ConfigLoader.cs`, `ConfigValidator.cs` |
| `Bukit.Content` | `MarkdownFolderProvider.cs`, `NotionContentProvider.cs` |
| `Bukit.Engine` | `SiteEngine.cs`, `PageRenderDispatcher.cs` |
| `Bukit.Routing` | `RouteGenerator.cs` |

## Kelas dan Fungsi Utama
### CLI/Config
- `ConfigLoader.Load` — YAML → AppConfig
- `ConfigValidator.Validate` — Pengesahan konfigurasi penuh

### Kandungan
- `MarkdownFolderProvider.LoadAsync` — Imbas Markdown
- `NotionContentProvider.LoadAsync` — Ambil halaman Notion

### Enjin
- `SiteEngine.BuildAsync` — Masuk binaan utama
- `PageRenderDispatcher.RenderPages` — Rendering halaman selari
- `DataModuleBuilder.BuildModules` — `mode=data` → `site.modules`

### Penghalaan/Rendering/Plugin
- `RouteGenerator.Generate` — ContentItem → RouteInfo
- `ScribanModelBinder` — Model C# → Scriban

## Jalankan Setempat
```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
```

## Ujian dan Asap
```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
pwsh ./scripts/smoke.ps1
```
