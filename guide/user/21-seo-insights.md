# 21 SEO Insights

`bukit seo insights` turns local, already-collected search and analytics
observations into an offline report. It helps a site team prioritize pages for
review; it is not a Google connector, a ranking guarantee, or an automatic
edit mechanism.

## Purpose And Boundary

The operating flow is:

```text
build -> route map -> external collector/plugin -> observations -> insights
```

1. `bukit build` writes `.bukit/seo-route-map.json` using the
   `seo-route-map.v1` contract.
2. An explicitly authorized collector or plugin reads that local map and,
   outside Core, obtains provider data.
3. The collector writes one or more local `seo-observation.v1` JSON files.
4. `bukit seo insights` joins those files to the route map and writes the
   offline `seo-insights-report.v1` report.

Core does not authenticate to Google, does not access the network, and does not
store OAuth or service credentials, schedule, or notify. It does not prove causation
or ranking, and it does not make an automatic edit. Authentication, API
pagination, retry/cache policy, and provider-specific collection remain outside
Core.

For Search Console specifically, the
[Search Analytics query reference](https://developers.google.com/webmaster-tools/v1/searchanalytics/query)
states that the API returns top rows and does not guarantee all rows. A
collector can therefore have valid data while low-volume rows are absent.
Treat this report as evidence for a review, never as proof of completeness,
ranking, or root cause.

## Local Input Contracts

Every observation has the same reporting window, a provider, and the
`google-organic` scope. A provider accepts only its own metrics: Search
Console rows use `impressions`, `clicks`, and `averagePosition`; GA4 rows use
`sessions`, `engagedSessions`, and `keyEvents`.

`gsc.json`:

```json
{
  "schema": "https://bukit.dev/schemas/seo-observation.v1.json",
  "schemaVersion": "1.0",
  "provider": "google-search-console",
  "scope": "google-organic",
  "collectedAt": "2026-08-03T03:00:00Z",
  "window": {
    "startDate": "2026-07-01",
    "endDate": "2026-07-31",
    "timeZone": "Asia/Kuala_Lumpur"
  },
  "rows": [
    {
      "url": "https://www.example.com/guides/tea/",
      "impressions": 1200,
      "clicks": 24,
      "averagePosition": 8.4
    }
  ]
}
```

`ga4.json` uses the identical window, but contains GA4-only metrics:

```json
{
  "schema": "https://bukit.dev/schemas/seo-observation.v1.json",
  "schemaVersion": "1.0",
  "provider": "google-analytics-4",
  "scope": "google-organic",
  "collectedAt": "2026-08-03T03:10:00Z",
  "window": {
    "startDate": "2026-07-01",
    "endDate": "2026-07-31",
    "timeZone": "Asia/Kuala_Lumpur"
  },
  "rows": [
    {
      "url": "https://www.example.com/guides/tea/",
      "sessions": 8,
      "engagedSessions": 6,
      "keyEvents": 13
    }
  ]
}
```

`keyEvents` may exceed `sessions`; consequently `keyEventRate` may exceed 1.
That relationship is valid and must not be treated as a malformed GA4 row.

The route map and result use privacy-safe `routeKey` values and optional
`contentKey` values. A route key identifies a current route/canonical pair;
a content key can preserve continuity when the route changes.

Create a local rules file such as `seo-insights-rules.json`:

```json
{
  "schema": "https://bukit.dev/schemas/seo-insights-rules.v1.json",
  "schemaVersion": "1.0",
  "siteHost": "www.example.com",
  "hostAliases": ["example.com"],
  "ignoredQueryParameters": ["utm_source", "utm_medium", "gclid"],
  "thresholds": {
    "minimumSearchImpressions": 100,
    "maximumLowImpressions": 20,
    "minimumAnalyticsSessions": 10,
    "lowCtr": 0.03,
    "lowEngagementRate": 0.4,
    "highEngagementRate": 0.7,
    "opportunityPositionMinimum": 4,
    "opportunityPositionMaximum": 20
  },
  "priorities": {
    "snippetMismatch": "P1",
    "landingQuality": "P0",
    "discoverability": "P2",
    "positionOpportunity": "P1"
  }
}
```

This is one complete valid `seo-insights-rules.v1` profile. Its thresholds and
priorities are examples, not Core defaults. Choose values that reflect the
site's review capacity and measurement practices.

## Run Insights

Build first, then supply local files only:

```bash
bukit build --clean
bukit seo insights \
  --dir dist \
  --observations incoming/gsc.json,incoming/ga4.json \
  --rules seo-insights-rules.json
```

| Option | Required | Default | Meaning |
|---|---:|---|---|
| `--dir` | No | `dist` | Build output directory. |
| `--routes` | No | `<dir>/.bukit/seo-route-map.json` | Local route-map path. |
| `--observations` | Yes | — | Comma-separated list of 1-10 local observation JSON files. |
| `--rules` | Yes | — | Local rules JSON path. |
| `--out` | No | `<dir>/.bukit/seo-insights-report.json` | Local report output path. |
| `--strict-join` | No | off | Return a nonzero result for unmatched or ambiguous rows. |

| Exit | Meaning |
|---:|---|
| `0` | Report was written; complete joins and allowed join gaps both succeed. |
| `1` | `--strict-join` found an unmatched or ambiguous row. The report is written before returning exit code 1. |
| `2` | Input, schema, local-path, or read/write failure; inspect the stable error code. |

On success, stdout reports only aggregate source/match/finding counts, the
local report path, and a classification. It does not echo raw observation rows
or credentials. `strict-join-failed` is an integrity signal, not an absence of
the local report.

## URL Joining And Join Quality

The rules profile supplies the host allowlist: `siteHost` plus `hostAliases`.
Observation-row `url` values must be absolute HTTP(S) URLs whose host is
`siteHost` or `hostAliases`; relative observation values are `invalid_url` and remain `unmatched`.
Route-map `canonical` values may be a leading-slash relative path or an absolute HTTP(S) URL. Accepted observation URLs are
canonicalized to their route form. Normalization removes a fragment and a
default HTTP/HTTPS port. It ignores only the explicit tracking parameter names
in `ignoredQueryParameters`; all other query parameters are retained and
sorted. Malformed percent encoding is rejected rather than silently repaired.

An observation without a matching canonical remains `unmatched`. Duplicate
canonical entries remain `ambiguous`; ambiguity never chooses a winner. Both
are preserved in the report alongside `joinQuality` counts overall and by
provider. Review those records before interpreting an apparent performance
change. `--strict-join` promotes either condition to exit 1 but does not erase
the evidence.

## Candidate Diagnostics

The report can emit these four configured finding codes. Each is a candidate
diagnosis with threshold evidence and a suggested action, never a root cause.

| Code | Evidence pattern | Suggested review action |
|---|---|---|
| `seo.insights.snippet_mismatch` | All four conditions: `impressions` >= `minimumSearchImpressions`; `ctr` < `lowCtr`; sessions >= `minimumAnalyticsSessions`; engagement rate >= `highEngagementRate`. | Compare title, description, query intent, and SERP presentation. |
| `seo.insights.landing_quality` | Sufficient sessions with engagement rate below the configured low-engagement threshold. | Inspect landing-page relevance, clarity, speed, and conversion path. |
| `"seo.insights.discoverability"` | Low impressions with sufficient sessions and high engagement. | Check discoverability, internal linking, index coverage, and measurement scope. |
| `seo.insights.position_opportunity` | Sufficient impressions with average position inside the configured opportunity range. | Review intent coverage, content quality, and competing search results. |

Thresholds turn observations into repeatable hypotheses; they do not establish
causation, guarantee a ranking change, or authorize a content change.

## Privacy And Plugin Handoff

Within this SEO route/insights workflow, outputs and logs use hashed
`routeKey`/`contentKey` values and do not expose raw CMS or Notion IDs or
credentials. This scoped statement does not mean every `.bukit` artifact lacks
trace IDs. `.bukit` is internal diagnostics output and is not publishable or
website content.

An external collector/plugin must request explicit network and environment
permission before it contacts Google or reads credentials. Its Core handoff
writes only local `seo-observation.v1` JSON. Google authentication,
pagination, and caching stay outside Core; do not add an OAuth secret or a
provider token to the route map, observation, rules, or insights report.
