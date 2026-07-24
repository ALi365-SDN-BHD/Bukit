# Documentation Governance

Documentation is generated from source contracts, not from historical guide
text.

## Source Priority

1. Current `src/Bukit-Core` code and tests.
2. `docs/governance/bukit-core-product-positioning.md` for current product
   positioning questions.
3. Current lightweight script gates.
4. README entry points.
5. Historical docs, only as context.

## Update Rule

When a public command, config field, template object, output report, or plugin
boundary changes, update:

- `guide/user`.
- `guide/dev`.
- `guide/skills`.
- README links if entry points change.
- Focused scripts if a new invariant needs checking.

README, guide entry points, stability wording, contribution wording, and
release authorization wording must remain synchronized with the current product
positioning policy. Historical audit and plan wording is evidence of its time
and is not rewritten when the current product policy changes.

## Checked Surfaces

The active documentation gate blocks drift in these public surfaces:

- Root public docs: `README*.md`, `CONTRIBUTING*.md`, and `SECURITY*.md`.
- GitHub reader surfaces: `.github/PULL_REQUEST_TEMPLATE.md` and active workflow YAML.
- Active guide content under `guide/`.
- Active script surfaces under `scripts/`.
- Compatibility governance docs promoted at `docs/compatibility-governance*.md`.
- `docs/governance/` when that directory exists.
- CLR public/protected surface baseline and maintainer workflow under
  `docs/governance/bukit-core-public-api-baseline.v1.json` and
  `guide/dev/public-api-governance.md`.

Historical analysis, plans, and backup directories remain reference-only unless
they are promoted into one of the active surfaces above.

When a dated audit or remediation plan is superseded by verified implementation,
preserve its historical evidence and add a status banner linking the current
closure record. Do not rewrite old findings as though they were never present,
and do not leave a pre-fix document as the only discoverable current status.

## Script Rule

Scripts must stay small. A gate script composes checks; it does not contain
large inline scanners. A checker should own one concern and fail fast.
