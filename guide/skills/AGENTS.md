# Bukit Core Skill Red Lines

- This file applies only below `guide/skills/` and may tighten, but never weaken, the root red lines.
- Treat `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs` as the Core command source and `src/Bukit-Core/Bukit.Config/AppConfig.cs` plus the config validators as the config-field source. Source code wins over guide prose.
- Do not teach non-Core command families as stable Core commands, and do not write `bukit <name>` examples for commands that are not registered.
- Do not present accepted-but-inert keys (`site.menus`, `site.pagination`, `site.sitemapDetail`, `site.plugins.<name>.options`, `site.collections.<name>.output.archiveDetail`,
  `content.modelSchema.media.requireAlt|requireDescription|requireLicense`) as working features.
- Do not recommend front matter `aliases` until the Core alias build failure is fixed
  (see `bukit-outputs`).
- Do not use Labs skills without an explicit Labs or experimental request.
- Follow `MAINTENANCE.md` when a Core contract changes.
