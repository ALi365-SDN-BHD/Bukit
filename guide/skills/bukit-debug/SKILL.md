---
name: bukit-debug
description: Use when diagnosing Bukit build failures, doctor output, route conflicts, output security, incremental build behavior, built-in derived pages, reports, or unexpected generated files.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Engine.Tests/PluginRegistryTests.cs"
  - "tests/Bukit.Engine.Tests/BuildReporterTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Cli/Commands/BuildCommand.cs"
  - "src/Bukit-Core/Bukit.Cli/Commands/DoctorCommand.cs"
  - "src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs"
  - "src/Bukit-Core/Bukit.Engine/BuildReporter.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Debug

This replaces the old plugin-centered debug skill for Core 1.0. The Core registry loads built-in plugins only.

## Built-In Plugin Surface

The Core built-in source includes:

- data files
- pages index
- taxonomy
- pagination
- archive
- related content
- aliases
- menus
- image processing

Do not imply a Core external plugin marketplace.

## First Checks

```bash
bukit config check
bukit doctor
bukit build --metrics .cache/build-metrics.json
```

## Common Diagnostic Areas

| Area | What to inspect |
|---|---|
| Config failure | Required fields, invalid enums, path traversal, Notion token |
| Missing pages | Content source filters, collection mapping, draft handling |
| Route conflict | Duplicate slug, collection permalink, list route, unsafe output path |
| Missing template | `theme.yaml.templates`, content template field, layout/include path |
| Missing derived pages | Built-in feature config, taxonomy, pagination, archives |
| Missing media | `content.media`, private-network blocking, max file size, download failures |
| Output safety | Build output marker, dotfile rules, path traversal guards |
| SEO/GEO reports | `.bukit/seo-report.json`, `.bukit/geo-report.json`, `.bukit/publish-audit-report.json` |

## Escalation Order

1. Reproduce with `config check` if the error is config-shaped.
2. Use `doctor` for template, route, and provider diagnostics.
3. Use `build --metrics` for performance or stage timing.
4. Use `seo audit`, `geo audit`, or `publish audit` for report quality gates.
5. Narrow to source files or tests after the failing layer is identified.
