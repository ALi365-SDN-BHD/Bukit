# Import + Notion Plugins 1.0.0-rc.1 Validation

Date: 2026-06-28

## Candidate

- Import runtime/static manifest: `1.0.0-rc.1`
- Notion runtime/static/template manifest: `1.0.0-rc.1`
- Source branch during preparation: `main`
- Tag: not created; pre-tag evidence is incomplete

## Local Evidence

| Check | Status |
| --- | --- |
| Import plugin tests | PASS (40/40) |
| Importing tests | PASS (174/174) |
| Homepage seed slug regression tests | PASS (2/2, empty homepage slug serializes as `index`) |
| Notion plugin tests | PASS (31/31) |
| Architecture tests | PASS (42/42) |
| RC release contract tests | PASS (3/3) |
| Repository quality gate | PASS (`bash scripts/quality-gate.sh Release`) |
| Official plugin package contract check | PASS |
| Documentation consistency check | PASS (0 warnings) |
| Import package build | PASS (`win-x64`, `linux-x64`, `osx-arm64`, after homepage slug fix) |
| Import package smoke | BLOCKED: non-sandbox approval quota was reached after the latest package build |
| Notion package build/smoke | PASS (`win-x64`, `linux-x64`, `osx-arm64`) |
| Packaged Import -> Notion dry-run | BLOCKED: pre-fix run exposed empty homepage slugs; fixed build awaits package smoke rerun |
| Release gate | BLOCKED: the previous pass predates the homepage slug fix and must be rerun |
| `git diff --check` | PASS |
| Boundary audit | PASS |

## External Evidence

| Check | Status |
| --- | --- |
| Dedicated Notion sandbox live create | BLOCKED: `NOTION_TOKEN` is not set in the current environment |
| Live report contains remote page IDs | BLOCKED: live create has not run |
| Token absent from live reports | BLOCKED: live create has not run |
| Same-commit `ci.yml` success on `main`/`master` | BLOCKED: RC changes are uncommitted and GitHub CLI is not installed |

## Manual Acceptance Command

Run from a project that has both RC plugin packages enabled:

```bash
export NOTION_TOKEN='...'
export NOTION_DATA_SOURCE_ID='...'
export BUKIT_NOTION_RC_CONFIRM=YES
bash scripts/smoke/import-notion-rc-manual.sh ./demo demo
```

The script writes:

```text
.bukit/tmp/notion/rc-manual-database-map.yaml
.bukit/reports/plugin-output/notion/rc-manual-dry-run.json
.bukit/reports/plugin-output/notion/rc-manual-live.json
.bukit/reports/plugin-output/notion/rc-acceptance-summary.json
.bukit/reports/plugin-output/notion/rc-acceptance-summary.md
```

## Release Decision

**BLOCKED**

The source, repository quality gate, documentation, and plugin boundaries are
locally green. The candidate remains blocked on current Import package smoke,
the post-fix release gate, live Notion sandbox acceptance, and same-commit
GitHub Actions evidence. Do not create or push an RC tag until all requirements
pass against the same candidate revision.
