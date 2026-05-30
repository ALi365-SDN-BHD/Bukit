# Bukit Full Automated Test System

## Goal

Build a release-grade automated testing system for the whole Bukit repository, covering all modules, all major risk areas, and all project-level validation flows. This work must not be limited to recently fixed bugs. It should establish a complete testing foundation for daily development, CI validation, release candidates, and formal releases.

## Repository Context

Bukit contains multiple source modules:

* `Bukit.Cli`
* `Bukit.Config`
* `Bukit.Content`
* `Bukit.Engine.Abstractions`
* `Bukit.Engine`
* `Bukit.Rendering`
* `Bukit.Routing`
* `Bukit.Shared`
* built-in/sample plugins

Bukit also contains multiple test projects:

* `Bukit.Engine.Tests`
* `Bukit.Content.Tests`
* `Bukit.Rendering.Tests`
* `Bukit.Cli.Tests`
* `Bukit.Config.Tests`
* `Bukit.Engine.Abstractions.Tests`
* `Bukit.Shared.Tests`
* `Bukit.Architecture.Tests`
* `ThrowingPlugin`
* `ProtocolEchoPlugin`

The final testing system must validate:

* build correctness
* unit test correctness
* integration behavior
* CLI behavior
* fixture-based end-to-end site generation
* Native AOT publish compatibility
* cross-platform behavior
* security regressions
* plugin behavior
* output safety
* CWD/environment isolation
* repeated-run stability

---

## 1. Add Top-Level Full Test Script

Create:

```text
scripts/test-all.sh
```

The script must be executable and must run from the repository root.

It must execute:

```bash
#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

echo "=== restore ==="
dotnet restore bukit.slnx

echo "=== build ==="
dotnet build bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false

echo "=== test ==="
dotnet test bukit.slnx -c "$configuration" --no-build -maxcpucount:1 -nodeReuse:false

echo "=== quality gate ==="
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-65}" bash scripts/quality-gate.sh "$configuration"

echo "=== smoke ==="
bash scripts/smoke.sh "$configuration"

echo "=== smoke all ==="
bash scripts/smoke-all.sh "$configuration"

echo "=== native aot publish ==="
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c "$configuration" -p:PublishAot=true

echo "=== test-all OK ==="
```

Acceptance requirements:

* Script exits non-zero on any failure.
* Script works from repository root.
* Script does not leave temporary output directories behind unless explicitly documented.
* Script must be safe to run repeatedly.

---

## 2. Add Stress Test Script

Create:

```text
scripts/stress-test.sh
```

It must run the full test suite repeatedly to detect intermittent failures.

Required behavior:

```bash
#!/usr/bin/env bash
set -euo pipefail

runs="${1:-20}"
configuration="${2:-Release}"

for i in $(seq 1 "$runs"); do
  echo "=== stress test run $i / $runs ==="
  dotnet test bukit.slnx -c "$configuration" -maxcpucount:1 -nodeReuse:false || exit 1
done

echo "=== stress-test OK ==="
```

Acceptance requirements:

* `bash scripts/stress-test.sh 20 Release` must pass.
* No test should leave CWD, environment variables, temp files, or background tasks mutated.
* Intermittent failures must be treated as release blockers.

---

## 3. Add Security Regression Script

Create:

```text
scripts/security-regression.sh
```

It must run focused security-related tests for:

* `SafeUrl`
* route security
* output path security
* Notion block renderer URL sanitization
* plugin policy
* external plugin `sha256`
* CI external plugin restriction
* dotfile/secret leakage prevention

Suggested structure:

```bash
#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"

echo "=== SafeUrl tests ==="
dotnet test tests/Bukit.Shared.Tests/Bukit.Shared.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl"

echo "=== Config security tests ==="
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~ExternalPluginPolicy|FullyQualifiedName~Path|FullyQualifiedName~Traversal"

echo "=== CLI security tests ==="
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~CIEnv|FullyQualifiedName~PathTraversal|FullyQualifiedName~NoConfig"

echo "=== Engine security tests ==="
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~Security|FullyQualifiedName~SafePath|FullyQualifiedName~Output|FullyQualifiedName~Plugin"

echo "=== Content security tests ==="
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c "$configuration" --filter "FullyQualifiedName~SafeUrl|FullyQualifiedName~Renderer|FullyQualifiedName~Audio|FullyQualifiedName~Notion"

echo "=== security-regression OK ==="
```

Adjust filters to match actual test class names after adding/renaming tests.

---

## 4. Add Missing Renderer-Level URL Sanitization Tests

Add tests under:

```text
tests/Bukit.Content.Tests
```

Add renderer-level tests for `AudioBlockRenderer`.

Required test cases:

* `AudioBlockRenderer` rejects `javascript:alert(1)`
* `AudioBlockRenderer` rejects `data:text/html,...`
* `AudioBlockRenderer` rejects `file:///etc/passwd`
* `AudioBlockRenderer` rejects `//evil.com/audio.mp3`
* `AudioBlockRenderer` allows `https://example.com/audio.mp3`
* `AudioBlockRenderer` allows `/assets/audio.mp3`

Also add similar renderer-level URL sanitization tests for:

* image block
* video block
* embed block
* bookmark block
* file block
* pdf block
* rich text links
* mention links

Required unsafe URL matrix:

```text
javascript:alert(1)
data:text/html,<script>alert(1)</script>
file:///etc/passwd
vbscript:msgbox(1)
//evil.com
//evil.com/x.js
//cdn.evil.com/audio.mp3
```

Required safe URL matrix:

```text
https://example.com/resource
http://example.com/resource
/assets/local-file.png
/audio/local.mp3
mailto:user@example.com
tel:+1234567890
```

Rules:

* Link-like renderers may allow `http`, `https`, `mailto`, `tel`, and internal `/path`.
* Media renderers may allow `http`, `https`, and internal `/path`.
* Embed renderers may allow `https` and internal `/path`.
* Protocol-relative URLs beginning with `//` must be rejected.
* Generated HTML must never contain unsafe URL values.

---

## 5. Add Fixture-Based End-to-End Sites

Create fixtures under:

```text
tests/fixtures/
  basic-markdown-site/
  route-security-site/
  safe-url-content-site/
  plugin-policy-site/
  output-safety-site/
  incremental-site/
  i18n-site/
  taxonomy-site/
  component-validation-site/
  dotfile-leak-site/
```

Each fixture must include:

```text
site.yaml
content/
layouts/
static/ or assets/ when relevant
expected output checks
```

### 5.1 basic-markdown-site

Purpose:

* Verify minimum Markdown site build.
* Verify `index.html` generation.
* Verify normal content rendering.

Expected checks:

* `dist/index.html` exists.
* Main content appears in output.
* No unexpected build errors.

### 5.2 route-security-site

Purpose:

* Verify unsafe routes are rejected.

Must include cases for:

```text
../x
../../x
/absolute/path
C:\Windows
\\server\share
CON
PRN
AUX
NUL
COM1
LPT1
%2F
%5C
//evil.com
https://evil.com
```

Expected behavior:

* Unsafe route configs fail with `ConfigException`.
* Safe route configs build successfully.

### 5.3 safe-url-content-site

Purpose:

* Verify generated output does not contain malicious URLs.

Must include content with:

```text
javascript:
data:
file:
vbscript:
//evil.com
https://safe.example.com
/assets/local.png
```

Expected checks:

```bash
! grep -R "javascript:" dist
! grep -R "data:text/html" dist
! grep -R "file:///etc/passwd" dist
! grep -R "vbscript:" dist
! grep -R "//evil.com" dist
```

### 5.4 plugin-policy-site

Purpose:

* Verify external plugin trust policy.

Must cover:

```yaml
externalPluginPolicy: deny
externalPluginPolicy: warn
externalPluginPolicy: allow
externalPluginPolicy: alow
```

Expected behavior:

* `deny` does not execute plugin.
* `warn` executes plugin and logs warning.
* `allow` executes plugin.
* invalid policy fails fast with `ConfigException`.

### 5.5 output-safety-site

Purpose:

* Verify output directory safety.

Must test:

```yaml
build:
  output: dist
```

and unsafe outputs:

```yaml
build:
  output: ..
```

```yaml
build:
  output: /
```

```yaml
build:
  output: C:\Users
```

Expected behavior:

* Safe output builds.
* Unsafe outputs fail.

### 5.6 incremental-site

Purpose:

* Verify incremental build behavior.

Expected checks:

* First build succeeds.
* Second build succeeds.
* Manifest/cache is created.
* Unchanged files are skipped or reported as cached where applicable.
* Modified file triggers rebuild.

### 5.7 i18n-site

Purpose:

* Verify multilingual build.

Expected checks:

* default language output exists.
* secondary language output exists.
* alternate links exist where configured.
* sitemap/rss/search modes behave as expected.

### 5.8 taxonomy-site

Purpose:

* Verify taxonomy generation.

Expected checks:

* taxonomy list pages exist.
* taxonomy item pages exist.
* disabled taxonomy config does not generate taxonomy outputs.
* taxonomy JSON is valid if enabled.

### 5.9 component-validation-site

Purpose:

* Verify component/theme validation.

Expected checks:

* valid components render.
* invalid component props warn or fail according to config.
* strict mode fails invalid component usage.

### 5.10 dotfile-leak-site

Purpose:

* Verify sensitive files are not copied to output.

Create source files:

```text
static/.env
static/.npmrc
static/.yarnrc
static/private.key
static/cert.pfx
static/cert.p12
static/.git/config
```

Expected output checks:

```bash
test ! -f dist/.env
test ! -f dist/.npmrc
test ! -f dist/.yarnrc
test ! -f dist/private.key
test ! -f dist/cert.pfx
test ! -f dist/cert.p12
test ! -d dist/.git
```

---

## 6. Extend `scripts/smoke-all.sh`

Update:

```text
scripts/smoke-all.sh
```

It must continue to build all existing example sites and also build/validate the new fixtures.

Existing examples must remain covered:

* `examples/blog-site`
* `examples/corporate-site`
* `examples/docs-site`
* `examples/plugin-site`
* `examples/theme-inheritance-site`
* `examples/component-theme`
* `examples/multilingual-site`

Add fixture checks for:

* `tests/fixtures/basic-markdown-site`
* `tests/fixtures/safe-url-content-site`
* `tests/fixtures/plugin-policy-site`
* `tests/fixtures/output-safety-site`
* `tests/fixtures/incremental-site`
* `tests/fixtures/i18n-site`
* `tests/fixtures/taxonomy-site`
* `tests/fixtures/component-validation-site`
* `tests/fixtures/dotfile-leak-site`

For each successful build, verify:

* `index.html` exists where expected.
* `sitemap.xml` is valid if enabled.
* `rss.xml` is valid if enabled.
* `search.json` is valid JSON if enabled.
* no `.env`, `.npmrc`, `.key`, `.pfx`, `.p12`, `.git` is copied.
* malicious URLs are not present in generated HTML.
* incremental second build succeeds where applicable.

Also add expected-failure fixture checks:

* unsafe route config must fail.
* unsafe output config must fail.
* invalid `externalPluginPolicy` must fail.

Expected-failure checks must fail the smoke script if the unsafe fixture unexpectedly succeeds.

---

## 7. Upgrade GitHub Actions

Refactor CI into separate jobs:

* `quality-gate`
* `cross-platform-tests`
* `smoke-examples`
* `native-aot`
* `stress-cli`, manually triggered

Use OS matrix:

```yaml
os: [ubuntu-latest, windows-latest, macos-latest]
```

Suggested workflow structure:

```yaml
name: Full Validation

on:
  push:
    branches: [main, master]
  pull_request:
  workflow_dispatch:

permissions:
  contents: read

jobs:
  quality-gate:
    runs-on: ubuntu-latest
    env:
      COVERAGE_THRESHOLD: "65"
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: bash scripts/quality-gate.sh Release

  cross-platform-tests:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore bukit.slnx
      - run: dotnet build bukit.slnx -c Release
      - run: dotnet test bukit.slnx -c Release --no-build

  smoke-examples:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: bash scripts/smoke.sh Release
      - run: bash scripts/smoke-all.sh Release

  native-aot:
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -p:PublishAot=true

  stress-cli:
    if: github.event_name == 'workflow_dispatch'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
      - run: bash scripts/stress-test.sh 20 Release
```

If shell scripts are not portable on Windows/macOS, use `dotnet` commands directly for cross-platform jobs and keep bash-based smoke tests on Ubuntu.

---

## 8. Acceptance Criteria

The work is complete only when all of the following are true:

* `bash scripts/test-all.sh` passes locally.
* `bash scripts/stress-test.sh 20 Release` passes.
* GitHub Actions passes on Ubuntu, Windows, and macOS.
* Native AOT publish passes.
* All example sites build.
* All fixture sites build or fail as expected.
* Coverage remains above configured threshold.
* No test leaves CWD mutated.
* No test leaves environment variables mutated.
* No test leaves background tasks running.
* No generated output leaks dotfiles or secret-like files.
* No generated output contains unsafe URLs.
* CI clearly separates quality gate, cross-platform tests, smoke examples, AOT, and stress jobs.
* Expected-failure fixtures fail when they are supposed to fail.
* Security regression tests can be run independently.

---

## Daily Development Gate

For normal development, run:

```bash
dotnet build bukit.slnx -c Release
dotnet test bukit.slnx -c Release
bash scripts/smoke.sh Release
```

---

## Release Candidate Gate

Before public testing or release candidate builds, run:

```bash
bash scripts/test-all.sh
bash scripts/stress-test.sh 20 Release
bash scripts/security-regression.sh Release
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release -p:PublishAot=true
```

---

## Final Release Gate

Before formal release, additionally verify:

* GitHub Actions matrix passes on Ubuntu, Windows, and macOS.
* AOT publish passes on Ubuntu, Windows, and macOS.
* All examples and fixtures pass.
* Coverage is at or above the release threshold.
* No known intermittent failures remain.
* No security regression tests are skipped.
* No fixture expected-failure checks are disabled.
