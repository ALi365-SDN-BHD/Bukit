# KD-004 implementation checklist

- [x] 1. Freeze the current baseline and mapped closure; activate KD-004 and add whole-input preflight, path/symlink and normal-behavior regressions; observe behavioral RED before production edits. Original writer: 19 expected failures / 33 passed / 0 Skip, actual exit 1; no build/environment error.
- [x] 2. Repair the writer only using the existing path policy; pass the complete planned three-class specialty without KD-004 Skip and cache the GREEN evidence. Final `review-green`: writer 27 + page creator 23 + ensure terms 3 = 53 passed / 0 failed / 0 Skip; runtime cache HIT.
- [x] 3. Accept one specialty review and one delta-only final review; backfill KD-004 status, verify unrelated bytes, complete static checks and release the writer. Initial specialty Important I01 was reproduced (1 RED / 52 passed), repaired by validating before self-skip, and closed by I01-only re-review. Final remaining findings: Critical 0 / Important 0 / Minor 0; single final review scope, no unchanged fixture rerun.

Evidence: `/Users/ali/.local/state/bukit-verification/kd004-redirect-boundary-bh6Eqt`. No KD-003, SCSS, alias-ingestion feature, shared resolver change, full/release gate or commit.
