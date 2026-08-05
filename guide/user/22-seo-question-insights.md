# 22 SEO Question Insights

`bukit seo question-insights` joins a local question target map with local
Search Console question observations and the route map, then writes an offline
coverage report. It helps a site team see which planned questions already have
observable search traffic; it is not a Google connector, a keyword research
tool, a ranking guarantee, or an automatic edit mechanism.

## Purpose And Boundary

The operating flow is:

```text
build -> route map -> local question target map -> local observations -> question insights
```

1. `bukit build` writes `.bukit/seo-route-map.json` using the
   `seo-route-map.v1` contract.
2. A human or an explicitly authorized tool prepares a local
   `seo-question-target-map.v1` file: the questions the site intends to cover
   and the `route:sha256:` route keys each question should land on.
3. An explicitly authorized collector or plugin exports Search Console
   question rows outside Core and writes one or more local
   `search-question-observation.v1` JSON files.
4. `bukit seo question-insights` joins both inputs to the route map and writes
   the offline `seo-question-insights-report.v1` report.

Core never receives raw queries. Question identities are hashed
`question:sha256:` keys, grouped by hashed `topic:sha256:` keys, and joined to
hashed `route:sha256:` route keys. The raw query text stays in the external
collector and its provider account. Core does not authenticate to Google, does
not access the network, and does not store OAuth or service credentials,
schedule, or notify.

The report prioritizes human review. It does not prove demand,
and it does not prove causation; it does not authorize an automatic edit.
A question absent from the observations is not evidence that nobody searches
for it.

For Search Console specifically, the
[Search Analytics query reference](https://developers.google.com/webmaster-tools/v1/searchanalytics/query)
returns top rows and does not guarantee all rows. GSC top-row behavior can
omit low-volume queries, so a valid export may still be incomplete. Treat the
report as evidence for a review, never as proof of completeness, demand, or
root cause.

## Local Input Contracts

### Question Target Map

The target map is authored offline. Every question row carries a hashed
identity, an intent, a locale, a priority, and the route keys it is supposed
to cover:

```json
{
  "schema": "https://bukit.dev/schemas/seo-question-target-map.v1.json",
  "schemaVersion": "1.0",
  "generatedAt": "2026-08-05T00:00:00Z",
  "questions": [
    {
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "topicKey": "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "intent": "informational",
      "locale": "en",
      "priority": "P1",
      "coveredRouteKeys": [
        "route:sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
      ]
    }
  ]
}
```

`intent` is one of `informational`, `navigational`, `commercial`,
`transactional`, `other`; `priority` is `P0`, `P1`, or `P2`. Unknown or
duplicate fields are rejected. The question list is capped at 100000 entries.

### Search Question Observations

An observation dataset has a fixed `provider` of `google-search-console`, a
fixed `scope` of `google-organic`, a reporting window, a `collectionMethod`
(`api`, `export`, or `manual`), and per-question rows:

```json
{
  "schema": "https://bukit.dev/schemas/search-question-observation.v1.json",
  "schemaVersion": "1.0",
  "provider": "google-search-console",
  "scope": "google-organic",
  "collectedAt": "2026-08-05T03:00:00Z",
  "collectionMethod": "export",
  "window": {
    "startDate": "2026-07-01",
    "endDate": "2026-07-31",
    "timeZone": "Asia/Kuala_Lumpur"
  },
  "rows": [
    {
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "topicKey": "topic:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "url": "https://www.example.com/guides/tea/",
      "locale": "en",
      "device": "mobile",
      "impressions": 320,
      "clicks": 9,
      "averagePosition": 11.6
    }
  ]
}
```

Rows use GSC-only metrics (`impressions`, `clicks`, `averagePosition`). Row
`url` values must be absolute HTTP(S) URLs whose host is `siteHost` or
`hostAliases` from the shared `seo-insights-rules.v1` rules profile; relative
values are `invalid_url` and remain unmatched.

## Run Question Insights

Build first, then supply local files only:

```bash
bukit build --clean
bukit seo question-insights \
  --dir dist \
  --targets observations/question-targets.json \
  --observations observations/gsc-questions.json \
  --rules seo-insights-rules.json
```

| Option | Required | Default | Meaning |
|---|---:|---|---|
| `--dir` | No | `dist` | Build output directory. |
| `--routes` | No | `<dir>/.bukit/seo-route-map.json` | Local route-map path. |
| `--targets` | Yes | — | Local `seo-question-target-map.v1` JSON path. |
| `--observations` | Yes | — | Comma-separated list of 1-10 local `search-question-observation.v1` JSON files. |
| `--rules` | Yes | — | Local rules JSON path supplying the host allowlist. |
| `--out` | No | `<dir>/.bukit/seo-question-insights-report.json` | Local report output path. |
| `--strict-join` | No | off | Return a nonzero result for unmatched or ambiguous rows. |

| Exit | Meaning |
|---:|---|
| `0` | Report was written; complete joins and allowed join gaps both succeed. |
| `1` | `--strict-join` found an unmatched or ambiguous row. The report is written before returning exit code 1. |
| `2` | Input, schema, local-path, or read/write failure; inspect the stable error code. |

On success, stdout reports only aggregate source/match counts, the local
report path, and a classification. It does not echo raw observation rows,
query text, or credentials.

## Join Behavior And Join Quality

The join has two stages, both local and deterministic:

1. **Targets.** Each `coveredRouteKeys` entry is looked up in the route map.
   A route key missing from the route map becomes an `unmatchedTargets` entry
   with the fixed error code `route_key_not_found`.
2. **Observations.** Each row `url` is canonicalized and matched to a route
   using the same host allowlist and URL normalization as `seo insights`. A
   row without a matching canonical remains in `unmatchedObservations`;
   duplicate canonical entries remain in `ambiguousObservations` and ambiguity
   never chooses a winner.

All observation datasets must share the same reporting window; a mismatched
window fails with a stable error code instead of silently mixing periods.
Matched observations are aggregated per question and per route: totals for
impressions and clicks, CTR as `clicks / impressions`, and an
impressions-weighted average position. The report records `joinQuality`
counts overall, for targets, and for observations, and preserves every
unmatched and ambiguous record for human review.

## Privacy And Plugin Handoff

Within this question coverage workflow, outputs and logs use hashed
`question:sha256:`, `topic:sha256:`, and `route:sha256:` values only. Core
never receives raw queries, and the report is not publishable or website
content. `.bukit` is internal diagnostics output.

An external collector/plugin must request explicit network and environment
permission before it contacts Google or reads credentials. Its Core handoff
writes only local `search-question-observation.v1` JSON. Google
authentication, pagination, and caching stay outside Core; do not add an
OAuth secret, a provider token, or raw query text to the target map,
observation, rules, or insights report.
