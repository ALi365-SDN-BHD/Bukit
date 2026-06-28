# Import + Notion Plugin v1 RC Closeout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mark Bukit.Plugin.Import and Bukit.Plugin.Notion as `1.0.0-rc.1`, document and reproduce the Import-to-Notion handoff, and make both plugin package smokes release-blocking without adding v1.1 commands or options.

**Architecture:** Keep Import local-only and Notion as the only network/token consumer. RC metadata is synchronized across runtime handshake and static manifests. The release gate builds and smokes both self-contained plugin packages, while a separate opt-in script performs the destructive live Notion sandbox acceptance and writes reviewer-visible evidence.

**Tech Stack:** .NET 10, xUnit, Bash, YAML plugin manifests, Markdown release documentation, Notion API through Bukit.Plugin.Notion.

---

### Task 1: Lock RC version contracts

**Files:**
- Modify: `tests/Bukit.Plugin.Import.Tests/ImportPluginSkeletonTests.cs`
- Modify: `tests/Bukit.Plugin.Import.Tests/ImportPluginManifestTests.cs`
- Modify: `tests/Bukit.Plugin.Notion.Tests/NotionPluginSkeletonTests.cs`
- Modify: `tests/Bukit.Plugin.Notion.Tests/NotionPluginManifestTests.cs`
- Modify: `plugins/Bukit.Plugin.Import/ImportPluginManifestProvider.cs`
- Modify: `plugins/Bukit.Plugin.Import/examples/minimal/plugins/import/plugin.yaml`
- Modify: `src/Bukit.Notion/NotionPluginConstants.cs`
- Modify: `plugins/Bukit.Plugin.Notion/examples/minimal/plugins/notion/plugin.yaml`
- Modify: `plugins/Bukit.Plugin.Notion/plugin.yaml.template`
- Modify: `docs/plugins/Bukit.Plugin.Notion 开发技术书.md`

- [ ] Add handshake and static-manifest assertions for exact version `1.0.0-rc.1`.
- [ ] Run both plugin test filters and confirm they fail on `0.1.0`.
- [ ] Change only version metadata; do not change command surfaces or permissions.
- [ ] Rerun both plugin suites and confirm zero failures.

### Task 2: Add release documentation

**Files:**
- Create: `docs/release/import-notion-plugins-1.0.0-rc.1-release-notes.md`
- Create: `docs/plugins/import-notion-handoff-usage.md`
- Modify: `docs/plugins/import-notion-handoff-contract.md`
- Modify: `docs/plugins/import-notion-push-design.md`
- Modify: `plugins/Bukit.Plugin.Notion/README.md`

- [ ] Document RC scope, fixed boundaries, supported commands, known non-atomic replace behavior, package locations, and deferred v1.1 items.
- [ ] Document the exact handoff sequence using the required `--theme` option:

```bash
bukit import html-demo ./demo --theme demo --content-source notion --build-source markdown --force
bukit notion validate-seed ./sites/demo/notion-seed
bukit notion validate-database-map ./sites/demo/notion-seed/notion-database-map.yaml
bukit notion push --seed ./sites/demo/notion-seed --database-map ./sites/demo/notion-seed/notion-database-map.yaml --mode create --dry-run
```

- [ ] Explain that `databaseId`/`dataSourceId` must be filled before database-map validation and that only `NOTION_TOKEN` is allowed for live push.
- [ ] Keep P1-001 through P1-006 explicitly deferred to v1.1; do not expose new CLI options.

### Task 3: Add reproducible manual sandbox acceptance

**Files:**
- Create: `scripts/smoke/import-notion-rc-manual.sh`
- Create: `docs/release/import-notion-plugins-1.0.0-rc.1-validation.md`
- Create: `tests/Bukit.Architecture.Tests/PluginRcReleaseContractTests.cs`

- [ ] Add a static contract test requiring the acceptance script to contain import, validate-seed, validate-database-map, dry-run push, and confirmed live create push commands.
- [ ] Run the test and confirm it fails while the script is absent.
- [ ] Implement an opt-in script requiring `NOTION_TOKEN`, `NOTION_DATA_SOURCE_ID`, `BUKIT_NOTION_RC_CONFIRM=YES`, a demo directory, and a theme name.
- [ ] Make the script write JSON/Markdown reports only under `.bukit/reports/plugin-output/notion` and never print the token.
- [ ] Record local gate/package evidence separately from live sandbox evidence. When credentials are absent, use `BLOCKED` rather than `PASS`.

### Task 4: Make plugin packages release-blocking

**Files:**
- Modify: `scripts/gates/release.sh`
- Test: `tests/Bukit.Architecture.Tests/PluginRcReleaseContractTests.cs`

- [ ] Add a failing static contract test requiring release.sh to invoke all four scripts:

```text
scripts/build/import-plugin-package.sh
scripts/smoke/import-plugin-package.sh
scripts/build/notion-plugin-package.sh
scripts/smoke/notion-plugin-package.sh
```

- [ ] Add a `release: official plugin packages` stage after `ci-full` and before workflow evidence.
- [ ] Store package roots under `TestResults/release-gate/plugin-packages/{import,notion}` so the existing release-gate artifact upload preserves them.
- [ ] Run architecture tests, direct package builds, and both package smokes.

### Task 5: Release proof and decision

**Files:**
- Update: `docs/release/import-notion-plugins-1.0.0-rc.1-validation.md`

- [ ] Run `git diff --check`.
- [ ] Run `bash scripts/quality-gate.sh Release`.
- [ ] Run `RELEASE_GATE_RIDS=<host-rid> bash scripts/gates/release.sh Release` when local release prerequisites permit it.
- [ ] Audit plugin references, Import `network=false`, Import `environment.read=[]`, Notion token allowlist, stdout/stderr protocol, report redaction, executable placement, and `.bukit/plugins.yaml` entry prohibition.
- [ ] Run the manual sandbox script only when all required environment variables are present; capture created page IDs and report paths, never the token.
- [ ] Query GitHub Actions for a completed successful `ci.yml` run on the exact RC commit before creating any tag.
- [ ] Keep the RC decision `BLOCKED` until both live Notion acceptance and same-commit GitHub CI evidence are present.

### Explicitly Deferred to v1.1

- `import validate-handoff`
- machine-readable `import-handoff-report.json`
- `--template full|bare|none`
- `--base-url`
- `--no-preserve-html`
- stronger route-map schema
