# Split 3 Oversized Files Spec

## Why

The quality gate `.oversized-baseline.txt` lists 18 files that exceed the 600-line cohesion limit. Splitting them incrementally reduces technical debt at zero risk. This spec targets the three easiest files: `ScribanTemplateRenderer.cs`, `ConfigLoader.cs`, and `ConfigValidator.cs`.

## What Changes

### 1. ScribanTemplateRenderer.cs → Extract TemplateContextBuilder / PageRenderHandler

- **BREAKING**: Add new internal classes `TemplateContextBuilder` and `PageRenderHandler` in separate files under `src/Bukit.Rendering/Scriban/`
- Move `RenderTemplate`'s TemplateContext setup logic (lines 83-153 in original) into `TemplateContextBuilder.BuildContext(globals)`
- Move public `RenderPage`/`RenderList` entry points into `PageRenderHandler` which delegates to the main `ScribanTemplateRenderer`
- No change to public API surface — `ScribanTemplateRenderer` still exists and is the entry point
- Remove `ScribanTemplateRenderer.cs` from the baseline when done

### 2. ConfigLoader.cs → Extract SiteDefaultsApplier

- **BREAKING**: Add new internal class `SiteDefaultsApplier` in `src/Bukit.Config/SiteDefaultsApplier.cs`
- Move the default-value-filling logic scattered across `Load()` into `SiteDefaultsApplier.ApplyDefaults(AppConfig)`
- Specifically: site defaults (BaseUrl, Language, SitemapMode, etc.), build defaults, theme defaults, taxonomy defaults
- `ConfigLoader.Load()` calls `SiteDefaultsApplier.ApplyDefaults()` at the end before returning
- No change to public API — `ConfigLoader.Load()` is still the entry point
- Remove `ConfigLoader.cs` from the baseline when done

### 3. ConfigValidator.cs → Extract CollectionsValidator / I18nValidator

- **BREAKING**: Add new internal classes `CollectionsValidator` and `I18nValidator` in `src/Bukit.Config/`
- `CollectionsValidator.ValidateCollections(...)` handles:
  - `ValidateCollections` (collection permalink, template, pagination, filteredLists)
  - `ValidateFilteredLists`
  - `ValidateSourcesToCollections`
- `I18nValidator.ValidateI18n(...)` handles:
  - Language list validation (duplicates, non-empty)
  - DefaultLanguage validation
  - All enum-value validation for SitemapMode, RssMode, SearchMode, etc.
- `ConfigValidator.Validate()` delegates to these new classes
- No change to public API — `ConfigValidator.Validate()` is the entry point
- Remove `ConfigValidator.cs` from the baseline when done

## Impact

- Affected specs: Quality Gate (oversized baseline), Config, Rendering
- Affected code:
  - `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs` (target)
  - `src/Bukit.Config/ConfigLoader.cs` (target)
  - `src/Bukit.Config/ConfigValidator.cs` (target)
  - New files in `src/Bukit.Rendering/Scriban/` and `src/Bukit.Config/`
  - No test files need modification (same public API, same behavior)

## ADDED Requirements

### Requirement: TemplateContextBuilder

The system SHALL provide a `TemplateContextBuilder` internal class that encapsulates TemplateContext creation with shortcodes, components, theme sections, image helpers, and utility functions.

#### Scenario: Build contexts with all features
- **WHEN** `BuildContext` is called with a `ScriptObject` globals
- **THEN** the returned `TemplateContext` has shortcode/component/section/image/util globals pushed

### Requirement: PageRenderHandler

The system SHALL provide a `PageRenderHandler` internal class that wraps the public render entry points (`RenderPage`, `RenderList`).

#### Scenario: Render page through handler
- **WHEN** `RenderPage` is called on the handler
- **THEN** it delegates to `ScribanTemplateRenderer.Render` with the correct globals

### Requirement: SiteDefaultsApplier

The system SHALL provide a `SiteDefaultsApplier` internal class that applies default values to all config sections after YAML deserialization.

#### Scenario: Apply defaults after load
- **WHEN** `ApplyDefaults` is called on a partially-populated `AppConfig`
- **THEN** all missing values receive their documented defaults

### Requirement: CollectionsValidator

The system SHALL provide a `CollectionsValidator` internal class that validates collection-related config.

#### Scenario: Validate collections
- **WHEN** collections are present in config
- **THEN** `ValidateCollections` validates each collection's permalink, template, pagination, filteredLists

### Requirement: I18nValidator

The system SHALL provide an `I18nValidator` internal class that validates i18n-related config.

#### Scenario: Validate languages
- **WHEN** site.languages is configured
- **THEN** `ValidateI18n` validates language uniqueness, default language inclusion

## REMOVED Requirements

None — all changes are internal refactoring. Public API unchanged.

## Baseline Removal

After each file is split below 600 lines, its entry shall be removed from `scripts/.oversized-baseline.txt`.
