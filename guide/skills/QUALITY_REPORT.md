# Quality Report

Status: Core 1.0 aligned rewrite.

Implemented:

- Core gateway no longer routes to Labs or historical command skills.
- CLI reference is restricted to the current `BukitCliSpecs` command registry.
- Theme guidance is directory and `theme.yaml` focused.
- Development server wording uses LiveReload / auto reload, and avoids inaccurate module-replacement wording.
- Debug guidance focuses on build, doctor, route, output security, and built-in plugins.
- Validators fail on missing source anchors, missing guide chapters, non-Core command references, and inaccurate dev-server terminology in Core skills.

Known residual risk:

- Legacy human-facing guide cleanup may still be needed outside this Core skills pack.
