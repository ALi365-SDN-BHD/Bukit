# Troubleshooting

Start with the shortest command that proves the failing layer.

```bash
bukit config check
bukit doctor
bukit build
```

## Config Fails

Unknown fields mean `site.yaml` contains a key outside the Core contract. Remove the field or move the workflow to Labs documentation if it is experimental.

Common removed fields include remote theme source fields and non-built-in plugin config fields.

## Notion Fails

Check:

- `NOTION_TOKEN` exists in the environment.
- `content.sources[].notion.databaseId` is correct.
- The integration can access the database.
- `filterType`, `filterValue`, and property names match Notion.

## Template Fails

Check:

- `theme.name` points at an existing `themes/<name>/`.
- `theme.yaml` exists and uses known fields.
- `layouts/base.html` contains `{{ content }}`.
- Template paths in `site.collections` match files under the theme layouts directory.

## Output Is Missing

Run `bukit clean`, then rebuild.

```bash
bukit clean
bukit build
```

If a page is missing, check `draft`, source filters, collection assignment, route conflicts, and build diagnostics.

## Local Server Issues

Use `preview` for existing output and `dev` for file watching.

```bash
bukit preview --dir dist --port auto
bukit dev --port 5173
```

Use `--allow-lan` or `--public` only when you intentionally expose the dev server on a non-localhost address.
