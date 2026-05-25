---
title: About Plugins
slug: about
---

## Bukit Plugin System

Bukit supports a plugin system with multiple hook points:

- **AfterBuild** - Runs after all pages are rendered
- **DerivePages** - Can generate additional pages
- **BeforeRender** / **AfterRender** - Section-level hooks

Plugins can be:
1. Built-in (C#)
2. External (process/WASM)
3. Custom section plugins
