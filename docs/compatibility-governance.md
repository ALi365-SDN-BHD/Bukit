# Bukit Compatibility Governance

This document tracks Bukit's active compatibility behaviors, deprecation paths,
and removal candidates. It is intended to keep code, docs, CLI messaging, and
release planning aligned.

## Purpose

Use this document to answer four governance questions consistently:

1. Which compatibility behaviors are intentionally supported?
2. Which legacy behaviors are explicitly rejected for 1.0 users (with or without migration messages)?
3. Which items only emit warnings and are not true runtime compatibility?
4. Which legacy paths should be removed in a future major version?

## Status Vocabulary

Every compatibility item should use one of the statuses below.

| Status | Meaning |
|---|---|
| `supported` | Officially supported behavior. Short-term removal is not planned. |
| `removed` | Not part of the 1.0 public contract. Runtime support is not promised and should be rejected by default unless explicitly documented as an exception. |
| `warned-only` | The system warns about the old shape, but does not guarantee runtime compatibility. |
| `rejected` | No longer supported; the system rejects it explicitly. |
| `rejected-with-message` | Rejected, with a targeted migration error message. |
| `supported-by-policy` | Not a compatibility layer; this is a current platform/product boundary that must be documented clearly. |
| `deprecated-behavior` | Legacy behavior still exists, but it is not a formal compatibility promise and should be narrowed or removed. |

## Correctness And Safety Tightening (2026-07-19)

The following fixes enforce existing 1.0 intent. They do not add compatibility
aliases, schema versions, plugin protocol fields, or persistence migrations.

| Finding | Observable change | Compatibility classification |
|---|---|---|
| F-01 | Dangerous, outside-root, symlinked, `.git`, and non-empty unmarked clean targets are refused consistently. | Intentional security tightening; previously unsafe deletion is not preserved. |
| F-02 | HTML embedded in default search title/snippet data is displayed as text. | Security fix; generated DOM construction is not a public ABI. |
| F-03 | Cross-category and structural output collisions fail deterministically before publication writes. | Correctness tightening; previous timing-dependent overwrite is not supported compatibility. |
| F-04 | Default publication walkers consistently skip directory symlinks/reparse points. | Enforcement of existing `followSymlinks: false` policy; no new global follow behavior. |
| F-05 | Same-process template decisions observe current manifest/root/include/layout content. | Cache correctness fix; manifest shape and public capability model are unchanged. |
| F-06 | Existing `site.search.maxContentLength` now applies to every Core search representation. | Existing field semantics enforced; default `8000` and schema minimum `1` are unchanged. |
| F-07 | Existing `content.media.maxConcurrency` now limits active localization downloads in its operation/store scope. | Existing field semantics enforced; default and YAML shape are unchanged. |
| F-08 | Existing build-report fields contain current diagnostic counts and public output inventory. | Value correctness fix; frozen `build-report.v1` shape is unchanged. |

These tightenings are patch-compatible bug fixes. A site that depended on
dangerous deletion, non-deterministic output overwrites, followed directory
links under the default false policy, stale template decisions, or ignored
configuration was depending on behavior outside the documented contract.

## Governance Table

| ID | Compatibility Item | Current Status | Code Location | Risk | Recommended Action | Target Version | Suggested Owner |
|---|---|---|---|---|---|---|---|
| `CG-001` | `content.provider` removed; `content.sources[]` is the only content source entry | `rejected-with-message` | [ConfigLoader.cs](../src/Bukit-Core/Bukit.Config/ConfigLoader.cs), [ContentProviderFactory.cs](../src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs) | Medium | Keep rejection. Documentation and AI prompts must generate only `content.sources[]`; tests must assert `content.provider` fails with migration guidance. | `current` | Config / Engine |
| `CG-002` | SEO audit no longer discovers root `dist/seo-report.json` | `rejected-with-message` | [SeoCommand.cs](../src/Bukit-Core/Bukit.Cli/Commands/SeoCommand.cs) | Low | Keep default discovery limited to `.bukit/seo-report.json`, with `.bukit/publish-audit-report.json` as secondary compatible input. Run a fresh build instead of relying on root output. | `current` | CLI |
| `CG-003` | GEO audit no longer discovers root `dist/seo-report.json` | `rejected-with-message` | [GeoCommand.cs](../src/Bukit-Core/Bukit.Cli/Commands/GeoCommand.cs) | Low | Keep default discovery limited to `.bukit/seo-report.json`, with `.bukit/publish-audit-report.json` as secondary compatible input. Run a fresh build instead of relying on root output. | `current` | CLI |
| `CG-004` | Themes without `theme.yaml` are rejected | `rejected-with-message` | [ThemeManifestLoader.cs](../src/Bukit-Core/Bukit.Theme/ThemeManifestLoader.cs), [ThemeBootstrapper.cs](../src/Bukit-Core/Bukit.Engine/ThemeBootstrapper.cs), [BuildCompatibilityTests.cs](../tests/Bukit.Theme.Tests/BuildCompatibilityTests.cs) | High | Require `theme.yaml` for build and doctor; keep migration guidance to generate or restore manifest. | `current` | Theme |
| `CG-005` | Theme template fallback chain via `fallbackDir` and default home template | `supported` | [FileTemplateLoader.cs](../src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs), [ThemeTemplateResolver.cs](../src/Bukit-Core/Bukit.Engine/ThemeTemplateResolver.cs) | Medium | Keep. Regression coverage 已补齐：`FileTemplateLoaderTests` 覆盖 override/child/parent 回退优先级。 | `v1.x` | Rendering / Theme |
| `CG-006` | Taxonomy `kinds[]` coexisting with legacy `tags/categories` template config | `removed` | [TaxonomyTemplateResolver.cs](../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/TaxonomyTemplateResolver.cs) | Medium | 1.0 docs and starters should use `taxonomy.kinds[]` as the only documented path; legacy fallback remains migration-only. | `current` | Engine |
| `CG-007` | External protocol plugin `v1` handshake fallback | `rejected-with-message` | [PluginProtocolClient.cs](../src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs), [PluginProtocolConstants.cs](../src/Bukit-Core/Bukit.Plugin.Abstractions/Protocol/PluginProtocolConstants.cs) | Medium | Enforce `bukit-plugin-v1` protocol messages and reject unsupported protocol responses with migration guidance. | `current` | Plugin |
| `CG-008` | External plugin command metadata omitted from manifest | `rejected-with-message` | [PluginCommandManifestValidator.cs](../src/Bukit-Core/Bukit.PluginHost/PluginCommandManifestValidator.cs), [PluginSchemaContractTests.cs](../tests/Bukit.PluginHost.Tests/PluginSchemaContractTests.cs) | High | Runtime command metadata must be declared in `plugin.yaml`; undeclared runtime commands, aliases, arguments, and options fail validation. | `current` | Plugin / Security |
| `CG-009` | Legacy plugin option key `options.arguments` | `rejected` | [PluginManifestLoader.cs](../src/Bukit-Core/Bukit.PluginHost/PluginManifestLoader.cs), [PluginCommandManifestValidator.cs](../src/Bukit-Core/Bukit.PluginHost/PluginCommandManifestValidator.cs) | Low | Keep rejected. Document `commands[].arguments` and `commands[].options`, not legacy `options.arguments`. | `current` | Plugin |
| `CG-010` | `site.rssMode` still affects feed behavior | `rejected-with-message` | [ConfigLoader.cs](../src/Bukit-Core/Bukit.Config/ConfigLoader.cs), [FeedPlugin.cs](../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/FeedPlugin.cs) | Medium | Keep rejected for 1.0; migration guidance points to `site.feed.formats` and feed plugin defaults. | `current` | Config / Engine |
| `CG-011` | `site.plugins.<name>` remains a Core built-in plugin toggle | `supported` | [SiteDefaultsApplier.Theme.cs](../src/Bukit-Core/Bukit.Config/SiteDefaultsApplier.Theme.cs), [built-in-plugins.md](../guide/dev/built-in-plugins.md) | Medium | Keep documented as a Core built-in plugin toggle only. Do not describe it as external process plugin configuration; that belongs in `.bukit/plugins.yaml`. | `current` | Config / Engine |
| `CG-012` | Legacy `site.collections.*.rss` shortcut | `rejected` | [ConfigStrictFieldValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs), [ConfigLoaderTests.cs](../tests/Bukit.Config.Tests/ConfigLoaderTests.cs) | Medium | Keep rejected by strict config field validation. Use `site.collections.*.output.rss` for collection feed output. | `current` | Config |
| `CG-013` | Singular `site.collection` config | `rejected` | [ConfigStrictFieldValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs), [ConfigLoader.cs](../src/Bukit-Core/Bukit.Config/ConfigLoader.cs) | Medium | Keep rejected by strict config field validation. Use `site.collections`. | `current` | Config |
| `CG-014` | Legacy Notion page-root fields such as `rootPageId`/`rootBlockId` | `rejected` | [ConfigStrictFieldValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs), [SiteDefaultsApplier.Content.cs](../src/Bukit-Core/Bukit.Config/SiteDefaultsApplier.Content.cs), [ProviderValidators.cs](../src/Bukit-Core/Bukit.Config/ProviderValidators.cs) | Medium | Keep current `content.sources[].notion.databaseId` contract. Do not document page-root aliases as warning-only compatibility. | `current` | Config / Notion |
| `CG-015` | Top-level front matter `outputPath` | `rejected-with-message` | [RouteGenerator.cs](../src/Bukit-Core/Bukit.Routing/RouteGenerator.cs) | Low | Keep rejected with a targeted migration error. List as a breaking rule in routing docs. | `current` | Routing |
| `CG-016` | Legacy SEO field name `seodesc` fallback | `removed` | [LlmsTxtPlugin.cs](../src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs) | Low | Move docs/examples to `summary` and `seo_desc` as primary 1.0 fields. | `current` | SEO |
| `CG-017` | Windows time zone fallback table for IANA to Windows IDs | `supported` | [ConfigValidator.cs](../src/Bukit-Core/Bukit.Config/ConfigValidator.cs), [TimeZoneCompatibility.cs](../src/Bukit-Core/Bukit.Config/TimeZoneCompatibility.cs) | Low | Keep. Add parameterized tests and review the table periodically. | `v1.x` | Config |
| `CG-018` | Obsolete sync body resolver API still used internally | `deprecated-behavior` | [ContentBodyResolver.cs](../src/Bukit-Core/Bukit.Engine.Abstractions/ContentBodyResolver.cs), [DataModuleBuilder.cs](../src/Bukit-Core/Bukit.Engine/DataModuleBuilder.cs), [SearchIndexBuilder.cs](../src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs) | High | Replace internal sync call sites with async flows first, then review public removal. | Internal cleanup in `v1.2`, remove in `v2.0` if feasible | Engine |
| `CG-019` | AOT builds disable dynamic assembly plugins and converge on process protocol plugins | `supported-by-policy` | [PluginRegistry.cs](../src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs), [Bukit.Engine.csproj](../src/Bukit-Core/Bukit.Engine/Bukit.Engine.csproj) | Medium | Document this as a product boundary, not as a compatibility layer. | `v1.1` docs cleanup | Engine / Docs |
| `CG-020` | Import workflow defaults to a broad `pageTypes` set when input is missing | `deprecated-behavior` | [SiteConfigGenerator.cs](../src/Bukit-Plugins/Bukit.Importing/SiteConfigGenerator.cs) | Medium | Narrow defaults or make strategy explicit after fixture review. | `v1.3` | Import |

## Current Governance Priorities

### P0: Fix code-doc truth mismatches

These items should be clarified first because they create the most confusion for
users and maintainers:

- `CG-012` legacy `site.collections.*.rss`
- `CG-013` singular `site.collection`
- `CG-014` legacy Notion page-root fields
- `CG-004` themes without `theme.yaml`
- `CG-007` protocol `v1` handshake fallback
- `CG-008` `capabilities` omitted on external plugins

Expected outcome:

- Docs stop describing warning-only items as runtime-compatible.
- Migration guidance matches actual parser behavior.
- `site.plugins.<name>` stays clearly scoped to Core built-in plugin toggles, not external process plugin configuration.

### P1: Add missing regression coverage

The highest-value compatibility test additions are:

1. `content.provider` rejection and `content.sources[]` acceptance matrix
2. SEO report path discovery without root report fallback
3. GEO report path discovery without root legacy fallback
4. Protocol handshake `v1` rejection cases
5. Missing `capabilities` behavior
6. Windows time zone fallback table

### P2: Prepare removal plans

These items should move toward explicit sunset planning:

- `CG-006` taxonomy legacy template config
- `CG-010` `site.rssMode`
- `CG-018` obsolete sync body resolver API
- `CG-020` broad import defaults

## Documentation Rules

When updating Bukit docs, use the following rules:

1. Do not call an item "compatible" unless runtime behavior truly supports it.
2. If the code only emits warnings, mark the item as `warned-only`.
3. If the code rejects an old shape but gives guidance, mark it as `rejected-with-message`.
4. If an old path remains for migration-only contexts, document the fallback boundary and keep user-facing 1.0 guidance to avoid relying on it.

## Suggested Issue Checklist

- [ ] Add or link this document from the maintainer docs index
- [ ] Align config and routing docs with the status vocabulary above
- [ ] Add rejection tests for `content.provider` and acceptance tests for `content.sources[]`
- [ ] Add path-discovery tests for SEO and GEO audit commands
- [ ] Add protocol handshake rejection tests (`version` not `2`, `ok=false`, invalid JSON, empty stdout)
- [ ] Add tests for omitted plugin `capabilities`
- [ ] Add parameterized tests for Windows time zone fallback mappings
- [ ] Confirm legacy collection feed shortcuts remain rejected by strict field validation
- [ ] Confirm singular `site.collection` remains rejected by strict field validation
- [ ] Confirm legacy Notion page-root fields remain rejected in favor of `databaseId`
- [ ] Publish a sunset target for `site.rssMode`
- [ ] Replace internal sync `ContentBodyResolver.GetHtml()` call sites

## Review Cadence

Review this table whenever one of the following happens:

- a deprecation warning is added or removed
- a parser starts accepting or rejecting a legacy field
- a major release plan is drafted
- docs are updated for config, themes, routing, plugins, or import behavior
