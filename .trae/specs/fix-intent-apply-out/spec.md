# Fix intent apply --out Being Ignored

## Why
`bukit intent apply <intent.yaml> --out <path>` silently ignores `--out` and writes to `site.yaml` in the current directory. This causes `smoke.sh` to fail and is a data-loss risk: users passing `--out` expect the output at the specified path, but the tool writes to a default location instead.

## Root Cause Analysis
The `--out` and `--root-dir` options are defined only on the parent `intent` command spec, not on the `apply`/`validate` subcommand specs. In `CliParser.Parse`, when a subcommand is detected:

1. `parentBound` is created with **empty options** — parent options are never bound
2. The recursive `Parse(subSpec, remainingArgs)` passes only the sub-spec
3. `CliBoundCommandFactory.Create(remainingArgs, applySubSpec)` builds an option map from `apply`'s spec only — which has no `--out`
4. `--out <path>` is treated as two unknown positional arguments, silently dropped

`MergeForSubcommand` later merges parent options dict (empty) with inner options dict (also empty for `--out`), so `--out` is always `null` → defaults to `"site.yaml"`.

The same issue affects `intent validate --out <path> --root-dir <dir>`.

## What Changes
- Add `--out` and `--root-dir` option specs to the `apply` and `validate` subcommand specs in `BukitCliSpecs.cs`
- Add `test -f "$intent_out"` verification after `intent apply` in `smoke.sh`

## Impact
- Affected specs: intent command, CLI spec definitions, smoke test
- Affected code: `src/Bukit.Cli/Cli/BukitCliSpecs.cs`, `scripts/smoke.sh`

## ADDED Requirements
### Requirement: intent apply --out writes to specified path
The system SHALL write generated config to the path specified by `--out` when provided.

#### Scenario: intent apply with explicit --out
- **GIVEN** an intent file `samples/intent/markdown_blog.yaml`
- **WHEN** `bukit intent apply samples/intent/markdown_blog.yaml --out /tmp/custom-site.yaml`
- **THEN** the config is written to `/tmp/custom-site.yaml`
- **AND** `site.yaml` in the current directory is NOT created or modified
- **AND** the output message contains `/tmp/custom-site.yaml`

#### Scenario: intent apply without --out
- **GIVEN** an intent file `samples/intent/markdown_blog.yaml`
- **WHEN** `bukit intent apply samples/intent/markdown_blog.yaml`
- **THEN** the config is written to `site.yaml` in the current directory (default behavior preserved)

### Requirement: intent validate --out resolves root directory
The system SHALL use `--out` to resolve the root directory for template/content path validation.

#### Scenario: intent validate with explicit --out
- **GIVEN** an intent file `samples/intent/markdown_blog.yaml`
- **WHEN** `bukit intent validate samples/intent/markdown_blog.yaml --out /path/to/site.yaml`
- **THEN** the validation uses `/path/to/` as the root directory for resolving relative paths
