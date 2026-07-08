# Bukit Archive Boundary

Archive is for retired, historical, or non-buildable material. It is not part
of the Core 1.0 developer path and should not be used as a source of current
behavior without source verification.

## Use Archive For

- old release notes that describe removed command surfaces;
- design proposals not implemented in the current branch;
- generated examples that no longer pass strict config validation;
- historical plugin/theme/import/clone workflows with no current owner;
- documents kept for migration context only.

## Promotion Rule

Archived material can move to Labs only when it has an active experimental
owner. It can move to Core only after source, tests, skills, and guide docs are
updated together.

## Reader Warning

If an archived document mentions a command or config field not present in
`BukitCliSpecs.cs` or `ConfigStrictFieldValidator.cs`, treat the archive as
historical context, not usage guidance.

