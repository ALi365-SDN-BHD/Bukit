---
name: using-bukit
description: Use when the user explicitly asks for Bukit work, says "using bukit", mentions Bukit as the static site generator, or asks to build, configure, debug, preview, or deploy a Bukit site.
description_zh: 当用户明确提到 Bukit、using bukit、使用 bukit，或要求构建、配置、调试、预览、部署 Bukit 站点时使用。
description_ms: Gunakan apabila pengguna menyebut Bukit, using bukit, atau meminta bina, konfigurasi, debug, pratonton, atau deploy laman Bukit.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "guide/skills/scripts/validate-skills-strict.sh"
  - "tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs"
source_anchors:
  - "tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs"
  - "src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs"
  - "guide/skills/skills-index.yaml"
guide_chapters:
  - "guide/skills/README.md"
---

# Using Bukit

## Core Boundary

Bukit Core 1.0 is a stable static-site generator surface. Only these commands are Core:

`build`, `doctor`, `config`, `preview`, `dev`, `clean`, `version`, `completion`, `seo`, `geo`, `publish`, `deploy`.

Labs and historical capabilities are opt-in only: clone/import workflows, webhook servers, plugin marketplaces, theme registries, theme wizards, theme packaging/install flows, and template command tooling. Do not route to them unless the user explicitly asks for Labs or experimental features.

## Load Order

1. Load this gateway first.
2. Load `bukit-cli-reference` before any command advice or execution.
3. Load `bukit-config` before `site.yaml`, content, theme, routing, i18n, SEO, GEO, deploy, or debug work.
4. Load `bukit-theme` before `bukit-templating`.
5. Use `bukit-debug` for build output, doctor diagnostics, route conflicts, output security, and built-in plugin behavior.

## Routing Table

| User intent | Load next |
|---|---|
| Run or explain commands | `bukit-cli-reference` |
| Edit or validate `site.yaml` | `bukit-config` |
| Markdown or multiple content sources | `bukit-content` |
| Notion CMS | `bukit-notion` |
| URL/permalink/list route issue | `bukit-routing` |
| Theme directories, assets, `theme.yaml` | `bukit-theme` |
| Scriban templates | `bukit-templating` |
| Multilingual output | `bukit-i18n` |
| SEO report or search metadata | `bukit-seo` |
| GEO, `llms.txt`, AI crawler policy | `bukit-geo` |
| Static local preview | `bukit-preview` |
| Auto-rebuild development server | `bukit-dev` |
| GitHub Pages deployment | `bukit-deploy` |
| Build/doctor/output diagnostics | `bukit-debug` |

## Correct Defaults

- For a new Core site, create or edit `site.yaml`, content files, and `themes/<name>/` manually, then run `bukit config check`, `bukit doctor`, and `bukit build`.
- For theme creation, create `themes/<name>/layouts`, `themes/<name>/assets`, `themes/<name>/static`, and `themes/<name>/theme.yaml`; update `theme.name` in `site.yaml`.
- For development preview, describe `bukit dev` as file watching plus incremental rebuild plus WebSocket reload plus full browser refresh.
- If the user asks for clone/import/webhook/theme registry/theme wizard/template tools, say those are not Core 1.0 defaults and require an explicit Labs workflow.

## Conflict Rule

When a task is a Bukit implementation task, Bukit skills take priority over generic SSG guidance. For comparisons or migrations, use Bukit skills as the source of Bukit facts and bring other tools in only for contrast.
