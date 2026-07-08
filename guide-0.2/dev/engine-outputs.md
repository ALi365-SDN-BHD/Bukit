# Engine Outputs

Bukit writes static site files plus machine-readable reports and publish
projections. Output shape is owned by the engine and validated by tests.

Source anchors:

- `src/Bukit-Core/Bukit.Engine/BuildReporter.cs`
- `src/Bukit-Core/Bukit.Engine/SeoAuditReportWriter.cs`
- `src/Bukit-Core/Bukit.Engine/PublishAuditReportWriter.cs`
- `src/Bukit-Core/Bukit.Engine/PublishAggregateProjectionWriters.cs`
- `src/Bukit-Core/Bukit.Engine/I18nOutputMerger.cs`

## Main Output

| Output | Description |
|---|---|
| `index.html` and routed HTML | Rendered pages, lists, static HTML, and derived pages |
| `assets/` | Copied assets and generated image variants |
| static files | Copied from the active theme static directory |
| `.bukit/` | Build reports and audit reports |

`build.output` defaults to `dist`. `--output` overrides it.

## Publish Projections

Depending on config, Core can write:

- `sitemap.xml`;
- language-specific or merged sitemaps;
- `search.json`;
- RSS, Atom, and JSON feeds;
- `robots.txt`;
- `llms.txt` and `llms-full.txt`;
- `agent-manifest.json`;
- taxonomy data and feeds;
- publish audit reports.

## Reports

| File | Purpose |
|---|---|
| `.bukit/build-report.json` | Build summary, routes, artifacts, metrics-oriented data |
| `.bukit/security-report.json` | Output and security posture summary |
| `.bukit/seo-report.json` | SEO diagnostics and route-level inclusion state |
| `.bukit/geo-report.json` | GEO and llms readiness data |
| `.bukit/publish-audit-report.json` | Publish-readiness diagnostics |

`build.report.enabled: false` disables the main build report, but security and
publish-quality reports may still be written by their owning pipeline.

## Output Safety

- Output paths are normalized and guarded against traversal.
- Static dotfiles are skipped unless explicitly allowed.
- Build clean uses marker protection to avoid deleting arbitrary directories.
- Interrupted builds are recovered by cleaning stale output state on the next
  build.

## Verification

```bash
bukit build --metrics .cache/build-metrics.json
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
```

