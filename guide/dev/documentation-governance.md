# Documentation Governance

Documentation is generated from source contracts, not from historical guide
text.

## Source Priority

1. Current `src/Bukit-Core` code and tests.
2. Current lightweight script gates.
3. README entry points.
4. Historical docs, only as context.

## Update Rule

When a public command, config field, template object, output report, or plugin
boundary changes, update:

- `guide/user`.
- `guide/dev`.
- `guide/skills`.
- README links if entry points change.
- Focused scripts if a new invariant needs checking.

## Checked Surfaces

The active documentation gate blocks drift in these public surfaces:

- Root public docs: `README*.md`, `CONTRIBUTING*.md`, and `SECURITY*.md`.
- GitHub reader surfaces: `.github/PULL_REQUEST_TEMPLATE.md` and active workflow YAML.
- Active guide content under `guide/`.
- Active script surfaces under `scripts/`.
- Compatibility governance docs promoted at `docs/compatibility-governance*.md`.
- `docs/governance/` when that directory exists.

Historical analysis, plans, and backup directories remain reference-only unless
they are promoted into one of the active surfaces above.

## Script Rule

Scripts must stay small. A gate script composes checks; it does not contain
large inline scanners. A checker should own one concern and fail fast.
