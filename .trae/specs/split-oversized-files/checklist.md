# Checklist

- [x] TemplateContextBuilder is created in `src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs` and encapsulates TemplateContext setup logic
- [x] PageRenderHandler: intentionally skipped — `RenderPage`/`RenderList` are already 2-3 line thin wrappers; scribanTemplateRenderer is 422 lines without it
- [x] `ScribanTemplateRenderer.cs` is below 600 lines after split (422 lines)
- [x] SiteDefaultsApplier is created in `src/Bukit.Config/SiteDefaultsApplier.cs` (515 lines) as internal static helper class for ConfigLoader
- [x] ConfigYamlHelpers created (268 lines) and ConfigCollectionReader created (267 lines) — all below 600
- [x] `ConfigLoader.cs` is below 600 lines after split (167 lines)
- [x] CollectionsValidator is created in `src/Bukit.Config/CollectionsValidator.cs` with `ValidateCollections`, `ValidateFilteredLists`, `ValidateSourcesToCollections`
- [x] I18nValidator is created in `src/Bukit.Config/I18nValidator.cs` with `ValidateSite(SiteConfig)`
- [x] `ConfigValidator.Validate()` delegates to CollectionsValidator/I18nValidator/ExternalPluginsValidator/ProviderValidators
- [x] `ConfigValidator.cs` is below 600 lines after split (368 lines)
- [x] Full `dotnet build` passes with 0 warnings (treat-warnings-as-errors)
- [x] Full test suite passes (2361 passed, 0 failed, 0 skipped)
- [x] `scripts/.oversized-baseline.txt` no longer contains the 3 split files
- [x] No new oversized files (≥600 lines) were introduced by this change — all 7 new files are below 600 lines
