# Maintenance Governance Checklist (P2)

Converts architecture review conclusions into executable actions on a periodic basis.

## 1) Body Read and Cache Baseline

### Frequency: Monthly + before Content/Engine/Rendering changes

```bash
dotnet build bukit.slnx -c Release
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --no-clean --incremental
```

Record: total build time (clean + incremental), `rendered/skipped` in logs, manifest behavior.

## 2) Collections and Compatibility Layer Governance

- Primary path: `site.collections`
- Compatibility path: `post/page` default rules (compatibility only, not the long-term extension model)

Pre-change checks: Can goal be achieved via `collections`? Will it affect existing post/page theme behavior?
Post-change validation: `dotnet test ... --filter RouteGenerator`

## 3) Documentation–Asset Consistency Check

Monthly + before releases:
```bash
pwsh ./scripts/check-doc-asset-consistency.ps1
```

Check: whether solution files mentioned in docs exist, whether directories exist, whether embedded workflows exist.

## 4) Cadence: Monthly execute sections 1+3; Quarterly review collections strategy and architecture-review scores.
