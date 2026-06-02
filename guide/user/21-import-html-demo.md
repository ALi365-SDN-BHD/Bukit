# 21 Import HTML Demo: Convert a Local Demo into a Bukit Site Draft

Use `bukit import html-demo` when you already have a local HTML demo directory and want Bukit to generate a maintainable theme/site draft from it.

If you want to copy a live website by URL, use [18 Clone Website](./18-clone-website.md) instead. `clone` starts from browser extraction; `import html-demo` starts from local `.html`, CSS, image, and asset files.

## What It Generates

```bash
bukit import html-demo ./demo --theme silkroadbiz --force --verify
```

Default output:

- `themes/silkroadbiz/` — generated layouts, partials, assets, static files, and `bukit.templates.yaml`
- `sites/silkroadbiz/site.yaml` — generated site config
- `sites/silkroadbiz/content/` — Markdown drafts when the build source is Markdown
- `sites/silkroadbiz/notion-seed/` or `sites/silkroadbiz/data/` — seed files for review/import
- `sites/silkroadbiz/original-demo/` — preserved original HTML demo
- `sites/silkroadbiz/import-report.md` — conversion report and review checklist

`--verify` runs `bukit doctor` and `bukit build` against the generated `site.yaml`.

## Recommended First Pass

```bash
# 1. Analyze counts and diagnostics without writing files
bukit import html-demo ./demo --theme silkroadbiz --dry-run

# 2. Generate a buildable local draft
bukit import html-demo ./demo --theme silkroadbiz --force --verify

# 3. Review the report
cat sites/silkroadbiz/import-report.md
```

After the first pass, open the generated site in preview/dev mode and compare key pages at desktop, tablet, and mobile widths.

## Content Source vs Build Source

`--content-source` controls generated seed files. It accepts `notion`, `json`, or `yaml`.

`--build-source` controls how the generated site builds. It accepts `markdown` or `notion`.

The default is:

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source markdown
```

This creates Notion seed files for review, but the generated site still builds from local Markdown. That keeps `--verify` offline and avoids requiring `NOTION_TOKEN`.

Use direct Notion build only when the generated site should read from Notion at build time:

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source notion
```

`--build-source notion` can only be used with `--content-source notion`.

## Seed Import

Use `import seed` when you already have generated JSON/YAML seed files and want Markdown content:

```bash
bukit import seed sites/silkroadbiz/data \
  --output sites/silkroadbiz/content \
  --force
```

`import seed` does not write to Notion. It is a local build adapter.

## Notion Push

`import html-demo` does not write to Notion by default. Review seed files first:

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --dry-run
```

Push to one database:

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-id <notion-database-id>
```

Push with a multi-database map:

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-map sites/silkroadbiz/notion-seed/notion-database-map.yaml
```

Create missing mapped databases explicitly:

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --create-missing-databases \
  --parent-page-id <notion-parent-page-id>
```

You can also push immediately after import, but this is intentionally opt-in:

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --push-notion \
  --notion-database-id <notion-database-id>
```

`--push-notion` cannot be combined with `--dry-run`.

## Important Options

| Option | Meaning |
|---|---|
| `--theme <name>` | Required target theme name |
| `--site-path <dir>` | Override generated site directory, default `sites/<theme>` |
| `--content-source <notion|json|yaml>` | Seed output type |
| `--build-source <markdown|notion>` | Generated site build provider |
| `--route-map <file>` | Optional route/template override map resolved from the demo directory |
| `--strict` | Fail on strict import diagnostics |
| `--strict warn` | Report strict diagnostics without failing |
| `--no-extract-content` | Generate theme/config without content extraction |
| `--no-seed` | Skip seed files |
| `--no-preserve-html` | Do not copy original HTML demo to `original-demo/` |
| `--no-report` | Skip `import-report.md` |
| `--push-notion` | Push generated seed records to Notion after import |

## Review Checklist

- `import-report.md` has expected page routes, templates, components, and seed files.
- `bukit doctor --config sites/<theme>/site.yaml` is clean.
- `bukit build --config sites/<theme>/site.yaml` succeeds.
- `site.url` is replaced before publishing.
- Preserved source HTML lives under `original-demo/`, not theme `static/`.
- Visual parity is manually reviewed before treating the import as finished.

Related agent skill: [bukit-import](../../src/skills/bukit-import/SKILL.md).
