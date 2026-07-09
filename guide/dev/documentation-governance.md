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

## Script Rule

Scripts must stay small. A gate script composes checks; it does not contain
large inline scanners. A checker should own one concern and fail fast.
