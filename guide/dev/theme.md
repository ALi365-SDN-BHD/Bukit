# Theme Runtime

Theme runtime is split between config path resolution, manifest validation,
component registration, section rendering, token processing, assets, and static
files.

## Key Files

| File | Role |
|---|---|
| `ThemePathResolver` | Resolves layouts, assets, static, parent, and user layout paths. |
| `ThemeBootstrapper` | Loads theme manifest and runtime helpers. |
| `ThemeManifestLoader` | Reads `theme.yaml`. |
| `ThemeManifestStrictValidator` | Rejects invalid theme manifest fields and paths. |
| `ThemeComponentRegistry` | Loads component definitions. |
| `SectionSchemaValidator` | Validates section data. |
| `ThemeTokensProcessor` | Converts tokens into CSS variables. |

## Manifest Areas

`theme.yaml` can define capabilities, layouts, templates, page templates,
sections, components, assets, tokens, and `extends`.

## Asset Flow

`AssetPipeline` copies static and asset files, processes SCSS when configured,
generates image variants when configured, honors `build.publishDotFiles`, and
can follow symlinks only when `build.followSymlinks` allows it.
