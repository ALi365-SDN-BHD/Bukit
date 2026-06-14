# Clone Website Labs Workflow

Website cloning is not part of Bukit Core 1.0.

The Core CLI does not include a clone command, theme wizard, theme registry, or theme package installer. Core theme work is manual filesystem authoring under `themes/<name>/` plus `theme.yaml`.

Use this page only for future Labs planning. A Labs implementation must remain outside the default Core user guide until the code, tests, and architecture boundary allow it.

## Core Alternative

1. Create a theme directory manually.
2. Write `theme.yaml`.
3. Move inspected layout ideas into Scriban templates yourself.
4. Configure `theme.name`.
5. Run:

```bash
bukit doctor
bukit build
```
