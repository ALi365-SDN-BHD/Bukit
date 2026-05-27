# Contributing to Bukit

Thank you for your interest in contributing to Bukit.

## Getting Started

1. Install [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or later
2. Clone the repository and build:

```bash
git clone <repo-url>
cd Bukit
dotnet build bukit.slnx -c Release
```

3. Run tests:

```bash
dotnet test bukit.slnx -c Release
```

4. Run the smoke test (Windows):

```powershell
powershell -ExecutionPolicy Bypass -File scripts/smoke.ps1
```

For a new developer walkthrough, see [guide/dev/new-developer-30min.md](guide/dev/new-developer-30min.md).

## Code Style

- This project enforces `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild`
- Format your code before committing:

```bash
dotnet format bukit.slnx --verify-no-changes
```

- C# code follows the conventions in [.editorconfig](.editorconfig)
- Markdown, YAML, JSON, Shell, and PowerShell files use UTF-8 with LF line endings

## Architecture

The main entry points by change type are documented in [guide/dev/maintainer-entrypoints.md](guide/dev/maintainer-entrypoints.md).

Key architectural documents:
- [guide/dev/architecture.md](guide/dev/architecture.md) — module responsibilities and dependencies
- [guide/dev/code-wiki.md](guide/dev/code-wiki.md) — repository structure and key classes
- [guide/dev/governance-checklist.md](guide/dev/governance-checklist.md) — pre-release checklist

## Testing

- Unit tests are in `tests/` and use xUnit
- Smoke tests are in `scripts/smoke.ps1` and `scripts/smoke.sh`
- See [guide/dev/testing-smoke.md](guide/dev/testing-smoke.md) for testing strategy

## AOT Compatibility

This project publishes as Native AOT. All new code must be AOT-compatible:
- Avoid reflection on trim-affected types
- For Scriban changes, see the AOT adaption notes in [guide/dev/aot.md](guide/dev/aot.md)
- Run `scripts/check-aot-warnings.sh` to verify zero AOT warnings

## Pull Request Process

1. Update documentation if your change affects user-facing behavior
2. Run `bash scripts/quality-gate.sh` locally and ensure it passes (build + test + coverage + format + smoke)
3. Rebase onto the main branch before creating a PR

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
