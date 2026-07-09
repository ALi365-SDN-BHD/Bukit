# Contributing to Bukit

Thank you for your interest in contributing to Bukit.

## Getting Started

1. Install [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or later
2. Clone the repository and build:

```bash
git clone <repo-url>
cd Bukit
dotnet build bukit-core.slnx -c Release
```

3. Run the fast contribution gate:

```bash
bash scripts/quality-gate.sh Release
```

`scripts/quality-gate.sh` is a compatibility wrapper for
`scripts/gates/ci-fast.sh`. It checks documentation consistency, active workflow
boundaries, config documentation contracts, CLI documentation sync, skill
metadata, README sync, and the Core CLI script contract. It is intentionally
not a full release gate.

4. Run tests for code changes:

```bash
dotnet test bukit-test.slnx -c Release
```

For the current developer documentation map, see [guide/dev/README.md](guide/dev/README.md).

## Code Style

- This project enforces `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`
- Format your code before committing:

```bash
dotnet format bukit-core.slnx --verify-no-changes
```

- C# code follows the conventions in [.editorconfig](.editorconfig)
- Markdown, YAML, JSON, Shell, and PowerShell files use UTF-8 with LF line endings

## Architecture

The main developer entry points are documented in [guide/dev/README.md](guide/dev/README.md).

Key architectural documents:
- [guide/dev/architecture.md](guide/dev/architecture.md) — module responsibilities and dependencies
- [guide/dev/release.md](guide/dev/release.md) — CI, test, and release gate boundaries
- [guide/dev/release-checklist.md](guide/dev/release-checklist.md) — release-only checklist
- [guide/dev/documentation-governance.md](guide/dev/documentation-governance.md) — documentation governance

## Testing

- Unit tests are in `tests/` and use xUnit
- Core test projects are listed by `scripts/checks/core-tests.sh`
- Smoke entrypoints are `scripts/smoke.sh` and `scripts/smoke/core.sh`
- See [guide/dev/testing.md](guide/dev/testing.md) for testing strategy

## AOT Compatibility

This project publishes as Native AOT. All new code must be AOT-compatible:
- Avoid reflection on trim-affected types
- For Scriban changes, see the AOT adaption notes in [guide/dev/aot.md](guide/dev/aot.md)
- Release-owned Native AOT packaging uses `scripts/build/package-native-aot.sh`

## Pull Request Process

1. Update documentation if your change affects user-facing behavior
2. Run `bash scripts/quality-gate.sh Release` locally and ensure the fast documentation and contract gate passes.
3. For code changes, run the targeted tests first, then `BUKIT_CI_FULL_SKIP_FAST=1 bash scripts/gates/ci-full.sh Release` before handoff.
4. Release artifact, Native AOT, smoke, and security verification are release-owned checks. Run them only when your change touches that surface.
5. GitHub Actions uses `.github/workflows/ci.yaml` for pull requests and branch pushes.
6. Rebase onto the main branch before creating a PR

### Recommended Commit Sequence (TDD)

Follow the Red → Green → Refactor rhythm:

```text
test: add failing test for <feature|bug>    ← Red
feat: implement <feature>                   ← Green
refactor: extract <helper|method>           ← Refactor
```

For bug fixes, the first commit MUST reproduce the bug:

```text
test: reproduce <bug-description>           ← Red
fix: resolve <bug-description>              ← Green
```

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
