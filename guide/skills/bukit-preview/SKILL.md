---
name: bukit-preview
description: Use for serving existing build output with the stable preview command.
---

# Bukit Preview

`preview` serves an existing output directory. It does not watch or rebuild.
Use `--dir`, `--host`, `--port`, `--strict-port`, `--config`, and `--site`.
Binding to a non-loopback host requires `--allow-lan` (or `--public`); use it only
on trusted networks. Internal artifacts (`.bukit/`, `.bukit-build-state.json`,
`.bukit-output-marker`) are never served.
