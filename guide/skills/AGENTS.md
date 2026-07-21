# Agent Rules For Bukit Core Skills

- This file applies only to `guide/skills/` and its descendants. It supplements
  the root `AGENTS.md` and may add stricter requirements, but it does not weaken
  root-level strict prohibitions.
- Treat `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs` as the command source.
- Treat `src/Bukit-Core/Bukit.Config/AppConfig.cs` as the config field source.
- Do not teach non-Core command families as stable Core commands.
- Use Labs skills only after an explicit Labs or experimental request.
- For docs changes, run the focused docs and skills scripts before finalizing.
- For rule changes under `guide/skills/` or changes to this nested `AGENTS.md`,
  run `bash scripts/checks/docs-consistency.sh`,
  `bash scripts/checks/skills-schema.sh`, and
  `bash guide/skills/scripts/validate-skills-strict.sh`.
