# Tasks

- [x] Task 1: Add `--out` and `--root-dir` options to `apply` and `validate` subcommand specs
  - [x] Update `apply` subcommand spec in `BukitCliSpecs.cs` to include `--out` and `--root-dir` options
  - [x] Update `validate` subcommand spec in `BukitCliSpecs.cs` to include `--out` and `--root-dir` options

- [x] Task 2: Add CLI tests for intent apply --out
  - [x] Test: `intent apply input.yaml --out custom.yaml` writes to custom.yaml
  - [x] Test: `intent apply input.yaml --out custom.yaml` does NOT create root site.yaml
  - [x] Test: output message contains the custom path
  - [x] Test: `intent apply input.yaml` without --out writes to site.yaml (default preserved)

- [x] Task 3: Update smoke.sh with verification
  - [x] Add `test -f "$intent_out"` after `intent apply --out "$intent_out"`

- [x] Task 4: Verify fix
  - [x] CLI intent tests: 12 passed, 0 failed
  - [x] `bash scripts/smoke.sh Release` passes — **Smoke OK**

# Task Dependencies
- Task 2 depends on Task 1
- Task 3 depends on Task 1
- Task 4 depends on Task 1, 2, 3
