# Labs: Init/Create Scaffolding

Status: not Core 1.0.

The old guide documented `bukit init` and `bukit create` as site scaffolding
commands. They are not in the Core 1.0 command registry.

## Core Boundary

Core commands are defined by `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`. The stable
registry does not include init or create scaffolding commands.

## Historical Shape

Older drafts described:

```bash
bukit init my-site
bukit create my-site
```

with optional provider/template presets. Do not present those commands as Core
1.0 behavior.

## Labs Re-Entry Requirements

To return as a supported workflow, scaffolding needs:

- command specs and handlers in the intended CLI assembly;
- architecture tests that decide whether it belongs to Core or Labs;
- starter fixture tests;
- generated `site.yaml` that passes strict validation;
- no dependency on removed theme wizard or registry flows;
- docs and skills updated in the same change.

