# Bukit Compatibility Governance

This document tracks Bukit's active compatibility behaviors, deprecation paths,
and removal candidates. It is intended to keep code, docs, CLI messaging, and
release planning aligned.

## Purpose

Use this document to answer four governance questions consistently:

1. Which compatibility behaviors are intentionally supported?
2. Which legacy behaviors still work but should be migrated away?
3. Which items only emit warnings and are not true runtime compatibility?
4. Which legacy paths should be removed in a future major version?

## Status Vocabulary

Every compatibility item should use one of the statuses below.

| Status | Meaning |
|---|---|
| `supported` | Officially supported behavior. Short-term removal is not planned. |
| `deprecated-but-working` | Still works at runtime, but is in migration mode. |
| `warned-only` | The system warns about the old shape, but does not guarantee runtime compatibility. |
| `rejected` | No longer supported; the system rejects it explicitly. |
| `rejected-with-message` | Rejected, with a targeted migration error message. |
| `supported-by-policy` | Not a compatibility layer; this is a current platform/product boundary that must be documented clearly. |
| `deprecated-behavior` | Legacy behavior still exists, but it is not a formal compatibility promise and should be narrowed or removed. |

## Governance Table

| ID | Compatibility Item | Current Status | Code Location | Risk | Recommended Action | Target Version | Suggested Owner |
|---|---|---|---|---|---|---|---|
| `CG-001` | `content.provider` and `content.sources` dual-path loading | `supported` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:82), [ContentProviderFactory.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs:15) | Medium | Keep. Add explicit precedence tests and state that new projects should prefer `content.sources`. | `v1.x` | Config / Engine |
| `CG-002` | SEO audit report path fallback from `.bukit/seo-report.json` to legacy `dist/seo-report.json` | `deprecated-but-working` | [SeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/SeoCommand.cs:8) | Low | Keep for now. Add tests for new-only, old-only, and both-present precedence. Document legacy path sunset. | Review for `v2.0` | CLI |
| `CG-003` | GEO audit report lookup across publish audit, new SEO report, and legacy SEO report | `deprecated-but-working` | [GeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/GeoCommand.cs:26) | Low | Keep for now. Add precedence tests and document lookup order. | Review for `v2.0` | CLI |
| `CG-004` | Old themes without `theme.yaml` still render | `supported` | [ThemeManifestLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeManifestLoader.cs:7), [BuildCompatibilityTests.cs](/Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Theme.Tests/BuildCompatibilityTests.cs:41) | Medium | Keep as an explicit compatibility promise. Add more fixtures for inherited and mixed-mode themes. | `v1.x` | Theme |
| `CG-005` | Theme template fallback chain via `fallbackDir` and default home template | `supported` | [FileTemplateLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Rendering/Scriban/FileTemplateLoader.cs:15), [ThemeTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeTemplateResolver.cs:17) | Medium | Keep. Add tests for override, child, and parent precedence. | `v1.x` | Rendering / Theme |
| `CG-006` | Taxonomy `kinds[]` coexisting with legacy `tags/categories` template config | `deprecated-but-working` | [TaxonomyTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/TaxonomyTemplateResolver.cs:16) | Medium | Document as legacy-compatible, but steer users to `taxonomy.kinds[]`. Plan major-version cleanup. | `v2.0` | Engine |
| `CG-007` | External protocol plugin handshake `v2 -> v1` fallback | `supported` | [ProtocolAfterBuildRunner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProtocolAfterBuildRunner.cs:92) | Medium | Keep. Add tests for timeout, invalid JSON, `ok=false`, and empty stdout fallback cases. | `v1.x` | Plugin |
| `CG-008` | External plugin `capabilities` omitted means allow-all | `deprecated-but-working` | [PluginCapabilityEnforcer.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginCapabilityEnforcer.cs:10) | High | Clarify the status. Add warning for missing capabilities before tightening policy in a future major release. | Warn in `v1.1`, review strictness for `v2.0` | Plugin / Security |
| `CG-009` | Legacy plugin option key `options.arguments` | `rejected` | [ProcessArgumentsBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProcessArgumentsBuilder.cs:16) | Low | Keep rejected. Do not describe this as compatibility in docs. | Current | Plugin |
| `CG-010` | `site.rssMode` still affects feed behavior | `deprecated-but-working` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:68), [FeedPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/FeedPlugin.cs:24) | Medium | Publish a sunset plan. Keep until replacement guidance is fully documented. | `v2.0` | Config / Engine |
| `CG-011` | `site.plugins.rss` deprecation warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:36) | Medium | Document clearly that this is warning-only, not automatic runtime compatibility. | `v1.1` docs cleanup | Config |
| `CG-012` | `collections.*.rss` deprecation warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:63) | Medium | Same treatment as `CG-011`. Avoid calling it supported unless runtime parsing is added. | `v1.1` docs cleanup | Config |
| `CG-013` | `site.collection` to `site.collections` migration warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:80) | Medium | Document as migration guidance only. If true runtime compatibility is desired, add parsing logic explicitly. | `v1.1` docs cleanup | Config |
| `CG-014` | `content.notion.rootPageId` to `rootBlockId` migration warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:89), [SiteDefaultsApplier.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/SiteDefaultsApplier.cs:77) | Medium | Clarify that warning does not imply runtime support. Decide whether alias parsing is worth adding for stored configs. | Decision in `v1.2` | Config / Notion |
| `CG-015` | Top-level front matter `outputPath` | `rejected-with-message` | [RouteGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Routing/RouteGenerator.cs:41) | Low | Keep rejected with a targeted migration error. List as a breaking rule in routing docs. | Current | Routing |
| `CG-016` | Legacy SEO field name `seodesc` fallback | `deprecated-but-working` | [LlmsTxtPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs:300) | Low | Keep temporarily. Prefer `summary` and `seo_desc` in docs and examples. | Review for `v2.0` | SEO |
| `CG-017` | Windows time zone fallback table for IANA to Windows IDs | `supported` | [ConfigValidator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs:323), [TimeZoneCompatibility.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/TimeZoneCompatibility.cs:3) | Low | Keep. Add parameterized tests and review the table periodically. | `v1.x` | Config |
| `CG-018` | Obsolete sync body resolver API still used internally | `deprecated-but-working` | [ContentBodyResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine.Abstractions/ContentBodyResolver.cs:18), [DataModuleBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/DataModuleBuilder.cs:43), [SearchIndexBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SearchIndexBuilder.cs:65) | High | Replace internal sync call sites with async flows first, then review public removal. | Internal cleanup in `v1.2`, remove in `v2.0` if feasible | Engine |
| `CG-019` | AOT builds disable dynamic assembly plugins and converge on process protocol plugins | `supported-by-policy` | [PluginRegistry.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginRegistry.cs:1), [Bukit.Engine.csproj](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Bukit.Engine.csproj:17) | Medium | Document this as a product boundary, not as a compatibility layer. | `v1.1` docs cleanup | Engine / Docs |
| `CG-020` | Import workflow defaults to a broad `pageTypes` set when input is missing | `deprecated-behavior` | [SiteConfigGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Importing/SiteConfigGenerator.cs:28) | Medium | Narrow defaults or make strategy explicit after fixture review. | `v1.3` | Import |

## Current Governance Priorities

### P0: Fix code-doc truth mismatches

These items should be clarified first because they create the most confusion for
users and maintainers:

- `CG-011` `site.plugins.rss`
- `CG-012` `collections.*.rss`
- `CG-013` `site.collection`
- `CG-014` `content.notion.rootPageId`

Expected outcome:

- Docs stop describing warning-only items as runtime-compatible.
- Migration guidance matches actual parser behavior.

### P1: Add missing regression coverage

The highest-value compatibility test additions are:

1. `content.sources` vs `content.provider` precedence matrix
2. SEO report path fallback precedence
3. GEO report path fallback precedence
4. Plugin handshake `v2 -> v1` fallback cases
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
4. If an old path still runs, document both the preferred path and the sunset plan.

## Suggested Issue Checklist

- [ ] Add or link this document from the maintainer docs index
- [ ] Align config and routing docs with the status vocabulary above
- [ ] Add precedence tests for `content.sources` and `content.provider`
- [ ] Add fallback-path tests for SEO and GEO audit commands
- [ ] Add protocol handshake fallback tests
- [ ] Add tests for omitted plugin `capabilities`
- [ ] Add parameterized tests for Windows time zone fallback mappings
- [ ] Decide whether `rootPageId` should stay warning-only or gain alias parsing
- [ ] Publish a sunset target for `site.rssMode`
- [ ] Replace internal sync `ContentBodyResolver.GetHtml()` call sites

## Review Cadence

Review this table whenever one of the following happens:

- a deprecation warning is added or removed
- a parser starts accepting or rejecting a legacy field
- a major release plan is drafted
- docs are updated for config, themes, routing, plugins, or import behavior

