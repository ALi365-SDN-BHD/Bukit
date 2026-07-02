---
name: bukit-config
description: Use when creating, editing, validating, or explaining Bukit `site.yaml`, including content sources, theme settings, build settings, taxonomy, SEO, GEO, logging, and GitHub Pages deploy configuration.
status: stable
since: "v4.0.0-core1"
verified_by:
  - "tests/Bukit.Config.Tests/ConfigLoaderFullCoverageTests.cs"
  - "tests/Bukit.Engine.Tests/ConfigValidatorTests.cs"
source_anchors:
  - "src/Bukit-Core/Bukit.Config/AppConfig.cs"
  - "src/Bukit-Core/Bukit.Config/ConfigLoader.cs"
  - "src/Bukit-Core/Bukit.Config/ConfigValidator.cs"
  - "src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs"
guide_chapters:
  - "guide/skills/README.md"
---

# Bukit Config

`site.yaml` is the Core site contract. Bukit 1.0 requires `site`, `content`, and at least one `content.sources` entry.

## Top-Level Nodes

| Node | Purpose |
|---|---|
| `site` | Site identity, URLs, language, collections, menus, feed, sitemap, search, related content, SEO settings |
| `content` | Markdown/Notion sources, media handling, content model schema |
| `build` | Output, clean, draft, incremental/cache, report, security, fingerprint, language jobs |
| `theme` | Theme name, layouts/assets/static paths, params, components, SCSS, images, validation |
| `taxonomy` | Taxonomy kinds, output mode, indexes, pinned fields |
| `logging` | Log level |
| `deploy` | GitHub Pages deployment config |

## Minimal Core Config

```yaml
site:
  name: my-site
  title: My Site
  url: https://example.com
  baseUrl: /
content:
  sources:
    - type: markdown
      name: pages
      markdown:
        dir: content
build:
  output: dist
theme:
  name: starter
```

Validate it with:

```bash
bukit config check
```

## Required Rules

- `content.sources` is required.
- Supported source types are `markdown` and `notion`.
- `content.sources[].mode` is `content` or `data`.
- Source names are optional, but must be unique when set.
- Relative path fields must not be absolute and must not contain `..`.
- `build.output` is required and must be a relative safe path.
- `build.fingerprintMode` is `size-time` or `sha256`.
- `build.report.securityFailMode` is `auto`, `off`, `warn`, or `strict`.
- `theme.componentValidation` is `off`, `warn`, or `strict`.
- `deploy.provider` is required when `deploy` exists and must be `github-pages`.

## Notion Validation

Notion config validation requires `NOTION_TOKEN` unless a command intentionally disables provider-secret validation. A Notion source must set:

```yaml
content:
  sources:
    - type: notion
      name: cms
      notion:
        databaseId: "${NOTION_DATABASE_ID}"
```

## Deploy Config

```yaml
deploy:
  provider: github-pages
  branch: gh-pages
  message: "Deploy site"
  cname: example.com
  keepHistory: true
```

Only GitHub Pages is Core 1.0.

## Good Verification Chain

```bash
bukit config check
bukit doctor
bukit build
```
