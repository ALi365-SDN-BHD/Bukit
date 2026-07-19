# Engine Outputs

Outputs are written by render, asset, plugin, projection, SEO, and build report
stages.

## Output Ownership

Before publication writes, `AssetOutputPlan` claims destinations for static,
assets, media, generated theme tokens, content/list render entries, and rendered
static templates. Cross-category exact collisions and file/descendant
structural collisions fail with `BuildAssetOutputCollision`. Parent/site
same-category overrides remain valid.

`OutputDestinationIdentityComparer` probes the actual output filesystem and is
shared with `BuildManifestTracker`; path identity is not inferred only from the
operating system. Arbitrary after-build third-party plugin outputs are outside
this plan.

Default output inventory and covered source scans use `SafeFileEnumerator` and
do not descend through directory symlinks or reparse points.

## Projection Contract

`PublishProjectionContract` defines per-page and aggregate representations:
HTML, semantic HTML, JSON, Markdown, JSON-LD, feed, Atom, JSON Feed, sitemap,
search, llms, robots, and agent manifest.

## Reports

| Report | Writer |
|---|---|
| Build report | `BuildReporter` |
| SEO report | `SeoAuditReportWriter` |
| Publish audit report | `PublishAuditReportWriter` |
| Security report | `BuildReporter` security report data helper |
| Metrics | `MetricsWriter` |

Reports are not optional prose. CLI audit commands consume them directly.

`BuildDiagnosticLogger` counts Warn/Error events per build invocation and shares
the same counters across language-variant forwarders. `BuildOutputInventory`
captures sorted root-relative public files before the report is written and
excludes `.bukit`, state, marker, and symlink-only files. The frozen
`build-report.v1` shape is unchanged; artifact manifest hashing reads the final
report after it is written.

Search outputs receive `site.search.maxContentLength` through document/list
builders, the built-in plugin, publish projection, and i18n merge paths. The cap
applies only to `content` and preserves valid surrogate pairs.

Aggregate feed writers and machine-readability audit share
`FeedWindowSelector`, including canonical publish-time ordering, canonical URL
tie-breaking, deduplication, and limit fallback. Semantic heading inspection is
landmark-aware and produces the same primary-scope data used by heading issues,
JSON-LD visible-title comparison, and `semanticOutline`.
