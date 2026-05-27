# Tasks

- [ ] Task 1: Extract TemplateContextBuilder from ScribanTemplateRenderer.cs
  - [ ] SubTask 1.1: Create `src/Bukit.Rendering/Scriban/TemplateContextBuilder.cs` with `BuildContext(ScriptObject globals)` that encapsulates TemplateContext setup (shortcodes, components, theme sections, image helpers, util functions) from `RenderTemplate` lines 83-153
  - [ ] SubTask 1.2: Update `ScribanTemplateRenderer.cs` to use `TemplateContextBuilder` instead of inline setup
  - [ ] SubTask 1.3: Verify original file shrunk below 600 lines; remove from baseline if so

- [ ] Task 2: Extract PageRenderHandler from ScribanTemplateRenderer.cs
  - [ ] SubTask 2.1: Create `src/Bukit.Rendering/Scriban/PageRenderHandler.cs` that wraps `RenderPage` and `RenderList` entry points, delegating to `ScribanTemplateRenderer`
  - [ ] SubTask 2.2: Update `ScribanTemplateRenderer` to use `PageRenderHandler` internally (if needed) — OR keep `ScribanTemplateRenderer` as the public API and `PageRenderHandler` as the delegation layer
  - [ ] SubTask 2.3: Run all tests to verify nothing broke

- [ ] Task 3: Extract SiteDefaultsApplier from ConfigLoader.cs
  - [ ] SubTask 3.1: Create `src/Bukit.Config/SiteDefaultsApplier.cs` with `ApplyDefaults(AppConfig)` that consolidates all default-value assignments from `ConfigLoader.Load()`
  - [ ] SubTask 3.2: Update `ConfigLoader.Load()` to call `SiteDefaultsApplier.ApplyDefaults(config)` after constructing `AppConfig`
  - [ ] SubTask 3.3: Run all tests to verify nothing broke; remove from baseline

- [ ] Task 4: Extract CollectionsValidator from ConfigValidator.cs
  - [ ] SubTask 4.1: Create `src/Bukit.Config/CollectionsValidator.cs` with static methods: `ValidateCollections(...)`, `ValidateFilteredLists(...)`, `ValidateSourcesToCollections(...)`
  - [ ] SubTask 4.2: Update `ConfigValidator.Validate()` to delegate to `CollectionsValidator` instead of inline code
  - [ ] SubTask 4.3: Run all tests to verify nothing broke

- [ ] Task 5: Extract I18nValidator from ConfigValidator.cs
  - [ ] SubTask 5.1: Create `src/Bukit.Config/I18nValidator.cs` with static method `ValidateI18n(...)` that handles language list, defaultLanguage, sitemapMode, rssMode, searchMode validation
  - [ ] SubTask 5.2: Update `ConfigValidator.Validate()` to delegate to `I18nValidator` instead of inline code
  - [ ] SubTask 5.3: Run all tests to verify nothing broke; remove from baseline

- [ ] Task 6: Run full build and test suite
  - [ ] SubTask 6.1: Run `dotnet build` with treat-warnings-as-errors
  - [ ] SubTask 6.2: Run full test suite
  - [ ] SubTask 6.3: Update `scripts/.oversized-baseline.txt` to remove the 3 split files
  - [ ] SubTask 6.4: Verify no new oversized files were introduced

# Task Dependencies

- [Task 1] depends on none
- [Task 2] depends on [Task 1] (may use TemplateContextBuilder)
- [Task 3] depends on none
- [Task 4] depends on none
- [Task 5] depends on none
- [Task 6] depends on [Task 1], [Task 2], [Task 3], [Task 4], [Task 5]
