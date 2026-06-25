# Bukit Import Content Extraction Contract

This document defines the `content extraction` module for the external Import
process plugin.

## Scope

`bukit import html-demo <demo-dir> --theme <name>` extracts maintainable
Markdown content from local HTML pages and writes it under:

```text
sites/<theme-name>/content/
```

The generated Markdown contains front matter with at least:

- `title`
- `slug`
- `collection`
- `published`

The body comes from the page's unique main/article content after removing the
top-level heading when possible.

## Command Contract

The static and runtime manifests expose:

```text
--no-extract-content
```

When the flag is present, the importer still generates the local theme and
`site.yaml`, but it skips component/content extraction and does not return a
`content` artifact.

## Deferred Scope

This module does not add seed-output controls, `--content-source`, `--no-seed`,
Notion handoff, report/security scanning, `--use`, `--verify`, strict mode, or
Clone migration.

The plugin continues to force the local-import path to:

- `ContentSource = json`
- `BuildSource = markdown`
- `GenerateSeed = false`
- `GenerateReport = false`

## Artifacts

When extraction is enabled and content is written, invoke responses include:

```text
type: content
path: sites/<theme-name>/content
```

When `--no-extract-content` is used, the content artifact is omitted.
