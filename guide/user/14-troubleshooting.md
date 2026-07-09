# 14 Troubleshooting

Use the smallest command that isolates the failure.

## Config Fails

```bash
bukit config check --config site.yaml
```

Common causes:

| Message Pattern | Meaning |
|---|---|
| `Unknown config field` | The YAML field is not in `ConfigStrictFieldValidator`. |
| `content.sources is required` | Bukit 1.0 does not use legacy top-level provider config. |
| `NOTION_TOKEN is required` | Notion provider validation needs the token in the environment. |
| `deploy.provider must be 'github-pages'` | Core has one deploy provider. |

## Build Fails Before Rendering

- Run `doctor` to inspect templates and providers.
- Check `site.collections` and `content.sources[].collection`.
- Check that route patterns include `{slug}` where required.
- Keep `build.output` inside a dedicated output directory.

## Route Conflicts

Route conflicts are usually caused by repeated slugs in the same collection,
manual route URL overrides, or list routes colliding with content routes. Adjust
the slug, collection permalink, or list route.

## Template Fails

- Confirm the template path exists under the resolved layouts directory.
- Check layout directives are at the start of the file.
- Use `page`, `site`, `pages`, `items`, `pagination`, `collection`, `taxonomy`,
  and `filter` according to template type.

## Slow Builds

Use `--metrics` to inspect stage timing, `--jobs` to limit or increase render
parallelism, and `--incremental` to reuse unchanged render results.
