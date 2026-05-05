# Testing and Smoke

This repository's testing strategy leans toward "runnable acceptance", covering core paths suitable for system-type projects like static site engines.

## Existing Entry Points

- One-click smoke: `scripts/smoke.ps1`, `scripts/smoke.sh`
- Itemized acceptance: "v2 Acceptance and Testing" in `README.md`

## When to Add Smoke / Acceptance

- Changes affecting core end-to-end paths: add smoke
- Changes adding/enhancing external stable contracts: supplement acceptance docs
- Internal refactors with no external impact: ensure smoke does not regress

## Minimum Structure for New Acceptance Cases

1. Prerequisites (environment variables, sample config)
2. Steps (build/doctor/preview commands)
3. Assertions (output structure, key files exist, routes accessible)
4. Cleanup (clean and cache handling)
