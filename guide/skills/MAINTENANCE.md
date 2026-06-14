# Maintenance

The source of truth for Core command availability is `src/Bukit.Cli/Cli/BukitCliSpecs.cs`, backed by `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs`.

When the Core CLI registry changes:

1. Update `bukit-cli-reference/SKILL.md`.
2. Update `skills-index.yaml` only if the skill surface changes.
3. Regenerate `skills-index.json`.
4. Run `bash guide/skills/scripts/validate-skills-strict.sh`.

Do not reintroduce historical Labs commands into the Core gateway. Move Labs guidance under `guide/labs-skills/` and keep it opt-in.
