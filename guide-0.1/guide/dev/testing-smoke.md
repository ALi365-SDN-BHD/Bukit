# Testing System

Bukit's testing strategy combines unit tests, integration tests, smoke tests, security regression tests, and fixture-based acceptance tests. All entry points are designed to run in CI and locally with a single command.

## Script Entry Points

| Script | Purpose | Usage |
|--------|---------|-------|
| `scripts/test-all.sh` | One-click full pipeline: restore → build → unit tests → quality gate → smoke → smoke-all → AOT publish | `bash scripts/test-all.sh [Release]` |
| `scripts/quality-gate.sh` | Coverage threshold (65%), file size limits, encoding checks, dotnet format | `COVERAGE_THRESHOLD=65 bash scripts/quality-gate.sh [Release]` |
| `scripts/smoke.sh` | Build and validate the starter example site | `bash scripts/smoke.sh [Release]` |
| `scripts/smoke-all.sh` | Build all 7 example sites + 9 fixture sites, validate outputs | `bash scripts/smoke-all.sh [Release]` |
| `scripts/security-regression.sh` | Isolated security tests across 5 modules (Shared/Config/CLI/Engine/Content) | `bash scripts/security-regression.sh [Release]` |
| `scripts/stress-test.sh` | Repeat full test suite N times to catch intermittent failures | `bash scripts/stress-test.sh 20 [Release]` |

## CI Structure

GitHub Actions (`ci.yml`) runs 5 jobs:

| Job | OS Matrix | Trigger |
|-----|-----------|---------|
| `quality-gate` | ubuntu-latest | push, PR |
| `cross-platform-tests` | ubuntu, windows, macos | push, PR |
| `smoke-examples` | ubuntu-latest | push, PR |
| `native-aot` | ubuntu, windows, macos | push, PR |
| `stress-cli` | ubuntu-latest | `workflow_dispatch` only (manual) |

## Fixture Sites

10 fixture sites under `tests/fixtures/` provide deterministic end-to-end validation:

| Fixture | Validates |
|---------|-----------|
| `basic-markdown-site` | Minimal markdown site, index.html generation |
| `route-security-site` | Route safety configuration |
| `safe-url-content-site` | URL sanitization in output |
| `plugin-policy-site` | External plugin policy behavior |
| `output-safety-site` | Output directory safety |
| `incremental-site` | Incremental build (first + second build) |
| `i18n-site` | Multi-language build (en, zh-CN) |
| `taxonomy-site` | Taxonomy list/term page generation |
| `component-validation-site` | Component/theme validation |
| `dotfile-leak-site` | Sensitive files (.env, .key, .pfx, .git) not leaked to dist/ |

Each fixture contains a minimal `site.yaml`, `content/index.md`, `layouts/` directory, and optional `static/` files.

### Smoke Validation

`smoke-all.sh` performs these checks on every successful build:
- `index.html` exists (handles i18n subdirectories)
- `sitemap.xml` contains `<url>` entries
- `rss.xml` contains `<channel>` entries
- `search.json` is valid JSON
- No dotfiles leak (`.env`, `.npmrc`, `.key`, `.pfx`, `.p12`, `.git/`)
- No dangerous URLs in output (`javascript:`, `data:text/html`, `file://`, `vbscript:`, `//evil.com`)

## Security Regression Tests

`security-regression.sh` isolates security-related tests:

- **Shared**: `SafeUrl.ForLink/ForMedia/ForEmbed` unit tests and protocol-relative URL rejection
- **Config**: `ExternalPluginPolicy` validation, config exception paths
- **CLI**: Path traversal rejection, config exception handling
- **Engine**: Route security, external plugin security, plugin failure modes
- **Content**: Block renderer URL safety (86 tests across 8 renderers), Notion rich text sanitization

## Test Protocol Plugin

`ProtocolEchoPlugin` (`tests/ProtocolEchoPlugin/Program.cs`) provides deterministic modes for external plugin integration testing:

| Mode | Hook | Output |
|------|------|--------|
| `success` (default) | any | ok=true with sample output file |
| `derive-success` | derive-pages | 1 derived page at `/derived/derived-1/` |
| `derive-conflict` | derive-pages | 1 page at `/blog/post-1/` (conflicts with test content) |
| `derive-lastwins` | derive-pages | 1 page at `/derived/conflict/` (non-conflicting) |
| `derive-plugin-a` | derive-pages | 1 page at `/plugin-conflict/page/` (ID: plugin-a) |
| `derive-plugin-b` | derive-pages | 1 page at `/plugin-conflict/page/` (ID: plugin-b, conflicts with plugin-a) |
| `env` | after-build | Reports OPENAI_API_KEY, GITHUB_TOKEN, BUKIT_* vars to file |
| `env-allowlist` | after-build | Reports PATH, HOME, NOTION_TOKEN, OPENAI_API_KEY, BUKIT_* vars to env-report.json |
| `error` | after-build | ok=false with error message |
| `empty` | after-build | No output (empty stdin) |
| `sleep` | after-build | Sleeps 1s, exits 0 |
| `traversal` | after-build | Outputs file with `../escape.json` path (should be rejected) |
| `handshake-v2` | handshake | Negotiates schema version 2 |

## When to Add Tests

- **Unit tests**: New logic in `Bukit.Shared`, `Bukit.Config`, `Bukit.Content`, `Bukit.Engine`, `Bukit.Rendering`
- **Fixture sites**: New build-time behavior, output structure changes, security boundaries
- **Security regression**: Any change to SafeUrl, external plugin protocol, route/output path validation
- **Smoke**: Changes affecting example site builds or core end-to-end paths

## Architecture

```
scripts/
  test-all.sh           → one-click full pipeline
  quality-gate.sh        → coverage + format + encoding checks
  smoke.sh               → single-site smoke
  smoke-all.sh           → example sites + fixture sites
  security-regression.sh → isolated security tests
  stress-test.sh         → repeat N runs

tests/
  fixtures/              → 10 deterministic fixture sites
  ProtocolEchoPlugin/    → deterministic external plugin for integration tests
  Bukit.*.Tests/         → unit/integration test projects
```
