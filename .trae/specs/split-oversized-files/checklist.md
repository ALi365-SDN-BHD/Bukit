# Checklist

- [ ] TemplateContextBuilder is created in `src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs` and encapsulates TemplateContext setup logic
- [ ] PageRenderHandler is created in `src/Bukit.Rendering/Scriban/PageRenderHandler.cs` and wraps `RenderPage`/`RenderList`
- [ ] `ScribanTemplateRenderer.cs` is below 600 lines after split
- [ ] SiteDefaultsApplier is created in `src/Bukit.Config/SiteDefaultsApplier.cs` with `ApplyDefaults(AppConfig)`
- [ ] `ConfigLoader.Load()` calls `SiteDefaultsApplier.ApplyDefaults(config)` after constructing AppConfig
- [ ] `ConfigLoader.cs` is below 600 lines after split
- [ ] CollectionsValidator is created in `src/Bukit.Config/CollectionsValidator.cs` with `ValidateCollections`, `ValidateFilteredLists`, `ValidateSourcesToCollections`
- [ ] I18nValidator is created in `src/Bukit.Config/I18nValidator.cs` with `ValidateI18n`
- [ ] `ConfigValidator.Validate()` delegates to CollectionsValidator/I18nValidator instead of inline code
- [ ] `ConfigValidator.cs` is below 600 lines after split
- [ ] Full `dotnet build` passes with 0 warnings (treat-warnings-as-errors)
- [ ] Full test suite passes (all tests green)
- [ ] `scripts/.oversized-baseline.txt` no longer contains the 3 split files
- [ ] No new oversized files (≥600 lines) were introduced by this change
