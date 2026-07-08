---
name: bukit-preview
description: Use when starting or troubleshooting Bukit static preview, serving existing build output, handling preview ports, MIME types, analytics suppression, or local pre-deploy checks.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Cli.Tests/PreviewCommandTests.cs"
  - "tests/Bukit.Cli.Tests/PreviewCommandExtendedTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Cli/Commands/PreviewCommand.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Preview

`preview` serves existing output. It does not rebuild.

## Usage

```bash
bukit build
bukit preview --dir dist
bukit preview --dir dist --port auto
bukit preview --config site.yaml
```

Defaults:

| Option | Default |
|---|---|
| `--dir` | `dist` |
| `--host` | `localhost` |
| `--port` | `4173` |
| `--strict-port` | false |

If a port is busy and strict mode is off, preview tries the next ports. `--port auto` asks the OS for a free port.

## Behavior

- Serves `index.html` for directory URLs.
- Blocks path traversal outside the preview root.
- Supports HTML, CSS, JavaScript, JSON, XML, SVG, PNG, JPG/JPEG, GIF, and TXT MIME types.
- Removes analytics snippets when `site.analytics.disableInPreview` is true and a Google Analytics id exists.

## Debugging

| Symptom | Action |
|---|---|
| Directory not found | Run `bukit build` or pass the correct `--dir` |
| Port conflict | Use `--port auto` or another port |
| 404 | Check generated output path and route URL |
| Analytics still visible | Confirm preview can locate `site.yaml` above the preview directory |
