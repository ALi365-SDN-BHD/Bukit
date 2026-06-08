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

## Governance Table

| ID | Compatibility Item | Current Status | Code Location | Risk | Recommended Action | Target Version | Suggested Owner |
|---|---|---|---|---|---|---|---|
| `CG-001` | `content.provider` removed; `content.sources[]` is the only content source entry | `rejected-with-message` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:82), [ContentProviderFactory.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ContentProviderFactory.cs:15) | Medium | Keep rejection. Documentation and AI prompts must generate only `content.sources[]`; tests must assert `content.provider` fails with migration guidance. | `current` | Config / Engine |
| `CG-002` | SEO audit no longer discovers root `dist/seo-report.json` | `rejected-with-message` | [SeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/SeoCommand.cs:8) | Low | Keep default discovery limited to `.bukit/seo-report.json`, with `.bukit/publish-audit-report.json` as secondary compatible input. Run a fresh build instead of relying on root output. | `current` | CLI |
| `CG-003` | GEO audit no longer discovers root `dist/seo-report.json` | `rejected-with-message` | [GeoCommand.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/GeoCommand.cs:26) | Low | Keep default discovery limited to `.bukit/seo-report.json`, with `.bukit/publish-audit-report.json` as secondary compatible input. Run a fresh build instead of relying on root output. | `current` | CLI |
| `CG-004` | Themes without `theme.yaml` are rejected | `rejected-with-message` | [ThemeManifestLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Theme/ThemeManifestLoader.cs:7), [ThemeBootstrapper.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeBootstrapper.cs:11), [BuildCompatibilityTests.cs](/Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Theme.Tests/BuildCompatibilityTests.cs:121) | High | Require `theme.yaml` for build and doctor; keep migration guidance to generate or restore manifest. | `current` | Theme |
| `CG-005` | Theme template fallback chain via `fallbackDir` and default home template | `supported` | [FileTemplateLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Rendering/Scriban/FileTemplateLoader.cs:15), [ThemeTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/ThemeTemplateResolver.cs:17) | Medium | Keep. Regression coverage 已补齐：`FileTemplateLoaderTests` 覆盖 override/child/parent 回退优先级。 | `v1.x` | Rendering / Theme |
| `CG-006` | Taxonomy `kinds[]` coexisting with legacy `tags/categories` template config | `removed` | [TaxonomyTemplateResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/TaxonomyTemplateResolver.cs:16) | Medium | 1.0 docs and starters should use `taxonomy.kinds[]` as the only documented path; legacy fallback remains migration-only. | `current` | Engine |
| `CG-007` | External protocol plugin `v1` handshake fallback | `rejected-with-message` | [ProtocolAfterBuildRunner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProtocolAfterBuildRunner.cs:92), [ProtocolHandshakeNegotiator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProtocolHandshakeNegotiator.cs:23) | Medium | Enforce schema v2-only handshake and reject v1 responses with migration guidance. | `current` | Plugin |
| `CG-008` | External plugin `capabilities` omitted metadata | `rejected-with-message` | [PluginCapabilityEnforcer.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/PluginCapabilityEnforcer.cs:10) | High | Missing capabilities now fail plugin execution (`site.pluginFailMode` controls strict vs warning behavior). | `current` | Plugin / Security |
| `CG-009` | Legacy plugin option key `options.arguments` | `rejected` | [ProcessArgumentsBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/Protocol/ProcessArgumentsBuilder.cs:16) | Low | Keep rejected. Do not describe this as compatibility in docs. | `current` | Plugin |
| `CG-010` | `site.rssMode` still affects feed behavior | `rejected-with-message` | [ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:68), [FeedPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/FeedPlugin.cs:24) | Medium | Keep rejected for 1.0; migration guidance points to `site.feed.formats` and feed plugin defaults. | `current` | Config / Engine |
| `CG-011` | `site.plugins.rss` deprecation warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:36) | Medium | Document clearly that this is warning-only, not automatic runtime compatibility. | `v1.1` docs cleanup | Config |
| `CG-012` | `collections.*.rss` deprecation warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:63) | Medium | Same treatment as `CG-011`. Avoid calling it supported unless runtime parsing is added. | `v1.1` docs cleanup | Config |
| `CG-013` | `site.collection` to `site.collections` migration warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:80) | Medium | Document as migration guidance only. If true runtime compatibility is desired, add parsing logic explicitly. | `v1.1` docs cleanup | Config |
| `CG-014` | `content.notion.rootPageId` to `rootBlockId` migration warning | `warned-only` | [ConfigDeprecationScanner.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigDeprecationScanner.cs:89), [SiteDefaultsApplier.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/SiteDefaultsApplier.cs:77) | Medium | Clarify that warning does not imply runtime support. Decide whether alias parsing is worth adding for stored configs. | Decision in `v1.2` | Config / Notion |
| `CG-015` | Top-level front matter `outputPath` | `rejected-with-message` | [RouteGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Routing/RouteGenerator.cs:41) | Low | Keep rejected with a targeted migration error. List as a breaking rule in routing docs. | `current` | Routing |
| `CG-016` | Legacy SEO field name `seodesc` fallback | `removed` | [LlmsTxtPlugin.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/Plugins/BuiltIn/LlmsTxtPlugin.cs:300) | Low | Move docs/examples to `summary` and `seo_desc` as primary 1.0 fields. | `current` | SEO |
| `CG-017` | Windows time zone fallback table for IANA to Windows IDs | `supported` | [ConfigValidator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigValidator.cs:323), [TimeZoneCompatibility.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/TimeZoneCompatibility.cs:3) | Low | Keep. Add parameterized tests and review the table periodically. | `v1.x` | Config |
| `CG-018` | Obsolete sync body resolver API still used internally | `deprecated-behavior` | [ContentBodyResolver.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine.Abstractions/ContentBodyResolver.cs:18), [DataModuleBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/DataModuleBuilder.cs:43), [SearchIndexBuilder.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Engine/SearchIndexBuilder.cs:65) | High | Replace internal sync call sites with async flows first, then review public removal. | Internal cleanup in `v1.2`, remove in `v2.0` if feasible | Engine |
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
- `CG-004` themes without `theme.yaml`
- `CG-007` protocol `v1` handshake fallback
- `CG-008` `capabilities` omitted on external plugins

Expected outcome:

- Docs stop describing warning-only items as runtime-compatible.
- Migration guidance matches actual parser behavior.

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
- [ ] Decide whether `rootPageId` should stay warning-only or gain alias parsing
- [ ] Publish a sunset target for `site.rssMode`
- [ ] Replace internal sync `ContentBodyResolver.GetHtml()` call sites

## Review Cadence

Review this table whenever one of the following happens:

- a deprecation warning is added or removed
- a parser starts accepting or rejecting a legacy field
- a major release plan is drafted
- docs are updated for config, themes, routing, plugins, or import behavior
