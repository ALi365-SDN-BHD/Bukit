# SEC-GATE-01 Report

## Root Cause

`BlockRendererUrlSafetyTests` now lives in `Bukit.Notion.Tests`, but the
security selector still associated it with `Bukit.Content.Tests`. The release
restore solution also omitted `Bukit.Notion.Tests`, so the release job's
single restore could not support the selector's `--no-restore` execution path.

## RED

Before changing the selector mapping, the fake `dotnet` runner in
`security-regression-self-test.sh` was strengthened to model project
ownership:

- `Bukit.Content.Tests` emits only `ImageAssetLocalizerTests`.
- `Bukit.Notion.Tests` emits only `BlockRendererUrlSafetyTests`.

With the original production mapping, the self-test failed at the real script
boundary:

```text
==> Bukit.Content.Tests security
security TRX validation failed: security selectors have no executed result:
['FullyQualifiedName~BlockRendererUrlSafetyTests']
```

This proves the stale project-to-selector mapping is rejected by the existing
fail-closed TRX verification rather than by a string-only assertion.

## GREEN Changes

- Retained `ImageAssetLocalizerTests` as the sole Content selector.
- Added `BlockRendererUrlSafetyTests` under `Bukit.Notion.Tests`.
- Added `tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj` to
  `bukit-test.slnx`.
- Kept `verify-trx.py`, runtime code, workflows, and coverage unchanged.

## Verification

- `bash scripts/security/security-regression-self-test.sh`: passed. It checks
  fake project-owner behavior and each valid selector project/owner pair.
- `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release`:
  456/456 passed.
- `dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj -c Release`:
  376/376 passed.
- `dotnet restore bukit-test.slnx`: passed, including `Bukit.Notion.Tests`.
- `BUKIT_SECURITY_SKIP_RESTORE=1 bash scripts/security/security-regression.sh Release`:
  passed. The selector runs were Cli 4/4, Content 36/36, Notion 86/86,
  Engine 65/65, PluginHost 103/103, and Routing 8/8 (302/302 total).
- `bash scripts/security/security-regression.sh Release`: passed with the same
  302/302 selector-run total and successful TRX validation for all six projects.

## Scope and Concerns

No Architecture project changed, so no Architecture test project was required.
No unresolved concerns: this change is limited to the selector ownership
contract, its self-test, and release restore membership.
