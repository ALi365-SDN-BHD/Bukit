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
can follow symlinks only in supported copy paths when `build.followSymlinks`
allows it. Other default recursive scanners continue to skip directory symlinks
and reparse points.

Before render/copy writes, `AssetOutputPlan` combines static, assets, media,
generated tokens, and render destinations. Parent and site files retain
same-category override order; cross-category and file/descendant conflicts fail
instead of relying on copy timing. Third-party after-build plugin outputs are
outside this plan.

The resolved layouts directory may contain `bukit.templates.yaml`. Capability
manifests are cached by current content fingerprint, and static root/include/
layout analysis is scoped to one decision call. A subsequent build observes a
changed, created, deleted, or corrected manifest/template input.
