---
alwaysApply: false
description: 
---
# Bukit Project Rules

## Lint & TypeCheck

```bash
dotnet build bukit.slnx -c Release -warnaserror
```

## Test Commands

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
```

## Format Checks

```bash
dotnet format bukit.slnx --verify-no-changes
```

## Conventions

- All CLI commands go in `src/Bukit.Cli/Commands/` with namespace `Bukit.Cli.Commands`
- New commands must be registered in both `Program.cs` (fallback switch) and `BukitCliSpecs.cs` (spec-based registry)
- Theme/template scaffolding uses `ThemeTemplateResource.Get("Name")` for template loading
- Wizard presets defined in `WizardPresets.cs` as `public static readonly WizardPreset` fields
- Agent skills in `src/skills/<skill-name>/SKILL.md` — 18 skills covering all Bukit subsystems:
  - Gateway: `using-bukit` routes to sub-skills via Skill tool
  - Reference: `bukit-cli-reference` (CLI single source of truth), `bukit-notion` (Notion content)
  - Technique: `bukit-config`, `bukit-templating`, `bukit-routing`, `bukit-plugins-debug`, `bukit-seo`, `bukit-geo`, `bukit-content-to-template`
  - Pattern: `bukit-theme`, `bukit-design-tokens`, `bukit-i18n`
  - Operation: `bukit-deploy`, `bukit-clone`, `bukit-preview`, `bukit-dev`, `bukit-webhook`
  - CLI operations: always load `bukit-cli-reference` first
  - See `src/skills/skills-index.yaml` for complete skill catalog with triggers, dependencies, and platform loading instructions
- User docs in `guide/user/` — three languages: `.md` (EN), `.zh-CN.md` (CN), `.ms.md` (MS)
- Developer docs in `guide/dev/` — maintainer-facing contracts and implementation reference
- No TODO/FIXME/HACK comments in production code
