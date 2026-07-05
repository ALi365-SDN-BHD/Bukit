# Bukit 1.0 Core CLI Labs Boundary Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

> Historical status note (2026-07-05): This is a historical implementation plan and keeps the migration paths and legacy mechanism names from that point in time. Mentions of `experimental/Bukit.Labs.Protocol`, `site.externalPlugins`, `ExternalProtocolPluginSource`, `ProtocolEchoPlugin`, or sample plugins do not describe the current implementation. The current formal external plugin path is `Bukit.PluginHost` + `bukit-plugin-v1`; legacy Labs Protocol source, sample plugins, and protocol echo fixtures have been removed.

**Goal:** Freeze the Bukit 1.0 Core CLI contract while physically isolating migration, debugging, and experimental tooling behind a Labs boundary.

**Architecture:** `bukit` remains the stable Core binary with only build, diagnostics, preview, cleanup, quality-gate, and deploy commands. `bukit-labs` owns clone/import/notion-push/plugin-external/intent/visual/webhook/data/theme/docs/route tooling and may depend on Core, but Core must not depend on Labs or `Bukit.Importing`.

**Tech Stack:** .NET 10, custom CLI metadata/parser in `src/Bukit.Cli/Cli`, xUnit tests, NetArchTest architecture guards.

---

### Task 1: Register Core Stable Quality-Gate Commands

**Files:**
- Modify: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`
- Modify: `src/Bukit.Cli/Commands/SeoCommand.cs`
- Modify: `src/Bukit.Cli/Commands/GeoCommand.cs`
- Test: `tests/Bukit.Cli.Tests/CliProgramFlowTests.cs`
- Test: `tests/Bukit.Cli.Tests/SeoReportValidatorTests.cs`

**Step 1: Add failing command registry tests**

Assert that `seo`, `geo`, and `publish` resolve in `BukitCliSpecs.CreateRegistry()`, and that `clone`, `import`, `notion`, `plugin`, `intent`, `visual`, `webhook`, `data`, `theme`, `docs`, and `route` remain unresolved.

Run:

```bash
dotnet test --no-restore tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliProgramFlowTests
```

Expected before implementation: tests for `seo`, `geo`, and `publish` fail.

**Step 2: Register stable specs and descriptors**

Add `seo audit`, `seo diff`, `geo audit`, `publish audit`, and `publish diff` specs to `BukitCliSpecs.CreateRegistry()`. Add matching dispatchers in `BukitCliDescriptors.CreateDescriptors()`.

**Step 3: Tighten audit report contracts**

`SeoCommand.ResolveAuditReportPath()` must only auto-resolve `.bukit/seo-report.json`; it must not fall back to `.bukit/publish-audit-report.json`.

`GeoCommand` must auto-resolve `.bukit/geo-report.json` and validate the `https://bukit.dev/schemas/geo-report.v1.json` contract directly; it must not parse SEO or Publish reports as a fallback.

`PublishCommand` remains tied to `.bukit/publish-audit-report.json`.

**Step 4: Verify**

Run:

```bash
dotnet test --no-restore tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
```

Expected: pass.

### Task 2: Create the Labs CLI Project Boundary

**Files:**
- Create: `experimental/Bukit.Labs.Cli/Bukit.Labs.Cli.csproj`
- Create: `experimental/Bukit.Labs.Cli/Program.cs`
- Create: `experimental/Bukit.Labs.Cli/LabsCliAssemblyMarker.cs`
- Modify: `bukit.slnx`
- Modify: `tests/Bukit.Architecture.Tests/DependencyMatrixTests.cs`
- Modify: `tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj`

**Step 1: Add failing architecture guard**

Add a test proving `Bukit.Cli` must not depend on `Bukit.Importing` or any `Bukit.Labs.*` assembly.

Run:

```bash
dotnet test --no-restore tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj --filter FullyQualifiedName~DependencyMatrixTests
```

Expected before migration: fail because `src/Bukit.Cli/Bukit.Cli.csproj` references `src/Bukit.Importing/Bukit.Importing.csproj`.

**Step 2: Add Labs CLI shell**

Create `experimental/Bukit.Labs.Cli` with `AssemblyName` `bukit-labs`. It may reference `Bukit.Cli`, `Bukit.Engine`, `Bukit.Config`, `Bukit.Shared`, and `Bukit.Importing`. Core projects must not reference it.

**Step 3: Wire minimal Labs dispatch**

Start with `import` because it is the current direct reason Core references `Bukit.Importing`. The initial implementation may link the existing import command file and minimal helper files into Labs while Core excludes `ImportCommand.cs`; a later cleanup should move those files physically.

Do not treat import as fully isolated: `ImportCommand` calls clone models, `ThemeCommand.SetThemeAsync`, `NotionCommand.RunAsync`, `DoctorCommand`, `BuildCommand`, and `ConfigPathResolver`. The next migration batch should handle `import + clone + theme tooling + notion seed/push`, or first extract shared CLI helpers so Labs can call them without making Core depend on Labs.

**Step 4: Verify**

Run:

```bash
dotnet test --no-restore tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
dotnet test --no-restore tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet build --no-restore experimental/Bukit.Labs.Cli/Bukit.Labs.Cli.csproj
```

Expected: pass.

### Task 3: Remove Core CLI Importing Dependency

**Files:**
- Modify: `src/Bukit.Cli/Bukit.Cli.csproj`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/ImportCommand.cs`
- Move or share as needed: `src/Bukit.Cli/Commands/ImportSeedContentWriter.cs`
- Move or share as needed: `src/Bukit.Cli/Commands/ImportSeedRecords.cs`
- Move or share as needed: `src/Bukit.Cli/Commands/NotionSeedPusher.cs`
- Move or keep shared as needed: `src/Bukit.Cli/Commands/ThemeCommand.cs`
- Move or keep shared as needed: `src/Bukit.Cli/Commands/CloneModels.cs`
- Test: `tests/Bukit.Architecture.Tests/DependencyMatrixTests.cs`

**Step 1: Remove the ProjectReference**

Delete the `Bukit.Importing` reference from `src/Bukit.Cli/Bukit.Cli.csproj`.

**Step 2: Move import-only command code behind Labs**

Move import command code to `experimental/Bukit.Labs.Cli` or exclude it from Core compilation and link it into Labs as an interim step. The current low-risk bridge is:

```text
src/Bukit.Cli/Bukit.Cli.csproj removes Bukit.Importing ProjectReference.
src/Bukit.Cli/Bukit.Cli.csproj excludes Commands/ImportCommand.cs.
experimental/Bukit.Labs.Cli links ImportCommand.cs plus CloneModels.cs, ImportSeedRecords.cs, and ImportSeedContentWriter.cs.
ThemeCommand.SetThemeAsync is public so Labs can reuse the existing theme write helper.
```

Prefer a real physical move once the first Labs batch is ready.

**Step 3: Keep Core behavior unchanged**

`bukit import` must remain unknown in Core. `bukit-labs import html-demo` and `bukit-labs import seed` own the experimental behavior.

**Step 4: Verify**

Run:

```bash
dotnet test --no-restore tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
dotnet test --no-restore tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj
dotnet run --no-build --project experimental/Bukit.Labs.Cli/Bukit.Labs.Cli.csproj -- import
```

Expected: pass.

### Task 4: Move Remaining Experimental CLI Commands

**Files:**
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/CloneCommand*.cs`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/PluginCommand.cs`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/IntentCommand.cs`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/VisualCommand.cs`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/WebhookCommand.cs`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/DataCommand.cs`
- Move or exclude from Core compile: `src/Bukit.Cli/Commands/ThemeCommand.cs`, `ThemeInstallCommand.cs`, `ThemeRegistryCommand.cs`, `ThemePackCommand.cs`, `ThemeWizardCommand.cs`

**Step 1: Move one family at a time**

Use separate commits for the following groups:

1. `import + clone + theme tooling + notion seed/push`, because these files are mutually coupled.
2. `plugin external`.
3. `intent`.
4. `visual`.
5. `webhook`.
6. `docs tooling`.
7. `data inspect/dump`.

`route` currently has no `src/Bukit.Cli/Commands/RouteCommand.cs`; decide whether it is a future Labs command or a stale docs-only concept before creating migration work for it.

**Step 2: Keep Core runtime helpers if needed**

Do not move runtime code required by build, doctor, config, preview, clean, version, completion, seo, geo, publish, or deploy.

`DataCommand.cs` is not fully Labs-only today: `DoctorCommand` calls `DataCommand.PrintModuleSummary`. Before moving `data inspect/dump`, extract `PrintModuleSummary` into a Core helper or keep that summary-only code in Core.

`DocsCheckCommand.cs` reads `BukitCliSpecs.CreateRegistry()`. If docs checks move to Labs, decide whether they validate Core registry, Labs registry, or both.

**Step 3: Verify after each family**

Run:

```bash
dotnet test --no-restore tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj
dotnet test --no-restore tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj
```

Expected: pass after each family.

### Task 5: Harden and Register Core Deploy

**Files:**
- Modify: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `src/Bukit.Cli/Cli/BukitCliDescriptors.cs`
- Modify: `src/Bukit.Cli/Commands/DeployCommand.cs`
- Modify: `src/Bukit.Cli/Deploy/IDeployProvider.cs`
- Modify: `src/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs`
- Modify as needed: `src/Bukit.Engine/DeployConfig*.cs`
- Test: `tests/Bukit.Engine.Tests/DeployConfigTests.cs`
- Test: `tests/Bukit.Engine.Tests/DeployConfigLoaderTests.cs`
- Test: `tests/Bukit.Cli.Tests/*Deploy*Tests.cs`

**Step 1: Add failing deploy safety tests**

Cover `provider: github-pages` only, dry-run, missing git, missing `GITHUB_TOKEN`, non-fast-forward default failure, explicit `--force`, and empty dist.

**Step 2: Implement deploy contract**

Register `deploy` only after default force-push behavior is removed. Non-fast-forward must fail unless `--force` is explicitly present.

The current implementation adds `DeployContext.Force`, reads `bukit deploy --force`, and makes `GitHubPagesDeployProvider` return a failed deploy result for non-fast-forward pushes unless `Force` is true.

The first registered spec must match current `DeployCommand` inputs unless the command is refactored in the same task:

```text
--config
--site
--dry-run
--skip-build
--base-url
--site-url
--output
--branch
--message
--ci
--force
```

Also update `CompletionCommandTests` and `HelpPrinterTests` when deploy becomes public.

**Step 3: Verify**

Run:

```bash
dotnet test --no-restore tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~Deploy
dotnet test --no-restore tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~Deploy
```

Expected: pass.

### Task 6: Remove External Protocol Plugins From Core Contract

**Files:**
- Modify: `src/Bukit.Config/AppConfig.cs`
- Modify: `src/Bukit.Config/ConfigLoader.cs`
- Modify: `src/Bukit.Config/ConfigStrictFieldValidator.cs`
- Modify: `src/Bukit.Config/ConfigJsonSchemaGenerator.cs`
- Modify: `src/Bukit.Engine/Plugins/PluginRegistry.cs`
- Move or isolate: `src/Bukit.Engine/Plugins/Protocol/*`
- Test: `tests/Bukit.Config.Tests/*`
- Test: `tests/Bukit.Engine.Tests/PluginRegistryTests.cs`
- Test: `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs`

**Step 1: Add failing config contract tests**

`site.externalPlugins` and `site.externalPluginPolicy` should be rejected by Core config once this phase starts.

**Step 2: Move external protocol runtime**

Core `PluginRegistry` should load built-in plugin sources only. External protocol loading belongs to Labs.

**Step 3: Verify**

Run:

```bash
dotnet test --no-restore tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj
dotnet test --no-restore tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~Plugin
```

Expected: pass after tests are updated to the new 1.0 contract.

**Current status**

- Core config model no longer exposes `ExternalPluginConfig`, `ExternalPluginPolicy`, `ExternalPlugins`, `externalProtocolIncludeRoutedPages`, or `AllowExternalPlugins`.
- Core config strict validation rejects `site.externalPlugins`, `site.externalPluginPolicy`, and `site.externalProtocolIncludeRoutedPages`.
- Core CLI no longer exposes `--allow-external-plugins` and the old `plugin` command source was removed from Core CLI.
- Core `PluginRegistry` no longer registers `ExternalProtocolPluginSource`; protocol runtime files were moved from `src/Bukit.Engine/Plugins/Protocol` to `experimental/Bukit.Labs.Protocol/EngineProtocol`.
- Legacy protocol DTO/host files were moved from `src/Bukit.Engine.Abstractions/Plugins/Protocol` to `experimental/Bukit.Labs.Protocol/AbstractionsProtocol`.
- Legacy process sample plugins were moved from `src/plugins/*` to `experimental/Bukit.Labs.Protocol/SamplePlugins`.
- Legacy `ProtocolEchoPlugin` fixture was moved from `tests/ProtocolEchoPlugin` to `experimental/Bukit.Labs.Protocol.Tests/Fixtures/ProtocolEchoPlugin`.
- Legacy protocol tests were moved from Core Engine/Abstractions tests to `experimental/Bukit.Labs.Protocol.Tests/Legacy*Tests` and are not wired into Core verification.
- `src/skills` references were updated so agent-facing docs describe external protocol plugins as Labs/legacy, not Bukit 1.0 Core.
