# Stage 9 evidence (2026-10-29)

This directory keeps the complete stage 9 test and I-01 fix evidence in one compressed archive, so it no longer depends on the system temporary directory or on one machine's user state.

## Scope

- Branch: `codex/stage9`
- Base commit: `7a8b71350501ded16a5268b6bb7cef23afd6aa67` (merged stage 8 `main`)
- Stage 9 scope: `Bukit.PluginHost` + `Bukit.Plugin.Abstractions` coverage and protocol exceptions.
- Separate authorized Core task: I-01 / KD-013, masking `COOKIE` and `AUTHORIZATION` values in plugin execution reports.

## Final results

| Item | Result |
|---|---|
| PluginHost tests (after I-01) | 277 / 277 passed, 0 failed, 0 skipped |
| PluginHost tests (batch 1) | 271 / 271 passed |
| Abstractions tests | 20 / 20 passed |
| PluginHost line coverage | 1521 / 1779 = 85.50% |
| Abstractions coverage (own) | 4986 / 5664 = 88.03% |
| Abstractions coverage (merged) | 4989 / 5664 = 88.08% |
| Abstractions handwritten source | 204 / 204 = 100% |
| I-01 RED | 7 failed / 21 passed / 0 skipped (focused) |
| I-01 focused GREEN | 28 / 28 passed |
| Audit-patch validator focus | 12 / 12 passed |
| Audit-patch full PluginHost | 277 / 277 passed, 0 failed, 0 skipped |
| Reviews | Critical 0 / Important 0; remaining Minor items are recorded in the plan |

The coverage denominator follows `scripts/checks/coverage/summarize.py`, which currently includes source-generated `obj/.../*.g.cs` files. `coverage-breakdown.json` inside the archive separates handwritten and generated lines.

## Archive contents

`stage9-evidence-20261029.tar.gz` contains three roots:

- `stage9-plugin-batch1-20261029/` — baseline and final TRX, coverage XML, closure, classification, cache records, reviews, metrics, and coverage summaries for the stage 9 test batch.
- `stage9-plugin-secret-masker-20261029/` — I-01 RED/focused/full GREEN TRX, coverage, cache records, OpenSpec validation log, review reports, review scope, and completion record.
- `stage9-audit-patch-20261029/` — post-verdict validator-assertion rerun: focused `PluginCommandManifestValidatorTests` 12/12 and full PluginHost 277/277 TRX/logs.

Verify the archive:

```sh
shasum -a 256 -c stage9-evidence-20261029.tar.gz.sha256
tar -tzf stage9-evidence-20261029.tar.gz | head
```

The archive contains synthetic test values such as `secret-token` and `session-cookie`; it does not contain real credentials. Cache records preserve the original execution output path, so a fresh run against a different path must record new cache evidence rather than claim the old hit.

The plan record is `docs/plans/bukit-core-automated-test-plan-2026-10-24.zh-CN.md` (stage 9 sections). The I-01 OpenSpec change is `openspec/changes/fix-plugin-secret-masker-cookie-authorization/`.

## Open follow-ups

S-01 (plugin-controlled report fields can echo a granted secret), M-01/M-02/M-03, the real abnormal-termination crash probe, and the Windows/Linux process-tree branches remain open and are recorded in the plan. They are not stage 9 blockers.
