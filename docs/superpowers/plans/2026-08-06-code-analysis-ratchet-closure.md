# Code-analysis ratchet closure plan

## Evidence and decision

- The committed baseline was produced at `cdab94b4` and exactly matches fresh
  style/analyzer reports from that commit: 593 style and 326 analyzer findings.
- Clean `HEAD` (`97d1801e`) reports 630 style and 438 analyzer findings. The
  current working-tree release candidate reports the same style inventory and
  one additional `CA1849` finding from an already-present Core change.
- The interval contains 843 changed files and about 24,000 added C# lines; no
  analyzer policy or SDK pin changed. This is stale inventory after a large code
  wave, not malformed baseline data or an analyzer-version wave.
- New `CA2000` instances were reviewed as ownership transfers or guarded
  exception paths. `CA1806`, `CA2101`, and `SYSLIB1054` identify deliberate
  best-effort process termination or P/Invoke modernization debt. Complexity,
  async, performance, and style findings remain report-only debt.
- `CA2208` is a confirmed correctness defect: `ValidateRequestTarget` exposes
  the local name `requestUri` as `ArgumentException.ParamName` instead of its
  public method parameter `request`. Fix it before accepting a new baseline.

## Implementation

1. Add a failing Notion client assertion for the public exception parameter
   contract, then change `nameof(requestUri)` to `nameof(request)`.
2. Add verification-closure ownership for the code-analysis scripts, baseline,
   and code-analysis plans. Prove the new paths are mapped before changing the
   workflow policy.
3. Generate a fresh candidate with the repository snapshot command after the
   correctness fix, review every per-diagnostic delta, and replace only
   `scripts/checks/baselines/code-analysis.v1.json`.
4. Do not bulk-format, mechanically refactor complexity findings, or suppress
   diagnostics in source/configuration as part of this closure.

## Verification closure

Changed files:

- `src/Bukit-Core/Bukit.Notion/Transport/NotionClient.cs`
- `tests/Bukit.Notion.Tests/NotionClientTests.cs`
- `scripts/checks/baselines/code-analysis.v1.json`
- `scripts/checks/codex-workflow-policy.v1.json`
- `scripts/checks/codex-workflow-self-test.d/closure-basic.sh`
- this plan

Exact specialty and final commands:

```bash
bash scripts/checks/codex-workflow-self-test.sh
dotnet test tests/Bukit.Notion.Tests/Bukit.Notion.Tests.csproj
bash scripts/checks/code-analysis-ratchet-self-test.sh
bash scripts/checks/code-analysis-ratchet.sh check
bash scripts/gates/ci-fast.sh Release
git diff --check
```

Run static self-tests separately from the serialized .NET tests and ratchet
scan. Run the user-authorized `ci-fast` gate only after the candidate baseline
and specialty evidence are green.
