---
name: bukit-seo-insights
description: Use when joining external observation data to built Bukit routes with `bukit seo insights` (Search Console + GA4), `seo question-insights`, `seo generative-insights` (AI answer citations), or `seo authority-insights` — input file formats, rules profile, matching, exit codes, and report privacy.
---

# Bukit SEO Insights

The four insights commands join local JSON exports to the built route map
`<dir>/.bukit/seo-route-map.json` and write a JSON report. They never fetch data or
touch the network; you supply the files. Run `bukit build` first.

| Command | Inputs | Default report (`<dir>/.bukit/`) |
|---|---|---|
| `bukit seo insights` | GSC and/or GA4 observations | `seo-insights-report.json` |
| `bukit seo question-insights` | Question target map + GSC question observations | `seo-question-insights-report.json` |
| `bukit seo generative-insights` | AI answer observations | `generative-citation-report.json` |
| `bukit seo authority-insights` | External source observations | `external-authority-report.json` |

## Options

| Option | Notes |
|---|---|
| `--dir` | Build output, default `dist`. |
| `--routes` | Route map, default `<dir>/.bukit/seo-route-map.json`. Alias `--route-map` on the question, generative, and authority commands. |
| `--observations` | Required. Comma-separated list of 1–10 local files, no empty entries or duplicates. |
| `--rules` | Required. Rules profile (below). |
| `--targets` | Required for `question-insights`. |
| `--out` | Report path; must not be one of the input files. |
| `--strict-join` | Exit 1 on join gaps; the report is still written first. Gaps are unmatched or ambiguous observation rows for `insights` and `question-insights`, and unmatched or ambiguous cited URLs on allowed hosts for `generative-insights` and `authority-insights`. |

All paths must be local; URLs, `//host` paths, and schemes are rejected. Each input file
is capped at 50 MiB and 100,000 rows. Unknown or duplicate JSON fields are rejected.

Exit codes: 0 report written; 1 `--strict-join` with join gaps; 2 any input, schema,
path, or I/O error (stderr: `<Label> failed: <code>.`). A missing `--observations` or
`--rules` is rejected earlier by the argument parser (`Error: Missing required
option: ...`, exit 2). Paths are relative to the current directory.

Success prints `<Label>: sourceRows=N matched=N unmatched=N ambiguous=N` (plus
`findings=N` for `seo insights`), `<Label> report: <path>`, and `<Label>
classification: complete|join-gaps-allowed|strict-join-failed`. Labels: `SEO insights`,
`SEO question insights`, `SEO generative insights`, `SEO authority insights`.

## Rules profile (shared)

Every field is required; there are no defaults. The question, generative, and authority
commands only use the host fields and `ignoredQueryParameters`, but thresholds and priorities are still validated.

```json
{
  "schema": "https://bukit.dev/schemas/seo-insights-rules.v1.json",
  "schemaVersion": "1.0",
  "siteHost": "www.example.com",
  "hostAliases": ["example.com"],
  "ignoredQueryParameters": ["utm_source", "utm_medium", "gclid"],
  "thresholds": {
    "minimumSearchImpressions": 100, "maximumLowImpressions": 20,
    "minimumAnalyticsSessions": 10, "lowCtr": 0.03,
    "lowEngagementRate": 0.4, "highEngagementRate": 0.7,
    "opportunityPositionMinimum": 4, "opportunityPositionMaximum": 20
  },
  "priorities": {
    "snippetMismatch": "P1", "landingQuality": "P0",
    "discoverability": "P2", "positionOpportunity": "P1"
  }
}
```

## URL matching

Observed URLs must be absolute http(s), without credentials, on `siteHost` or a
`hostAliases` host. The match key is path plus normalized query only: fragments are
dropped, extensionless paths get a trailing `/`, ignored query parameters are removed,
the rest are sorted. Path matching is case-sensitive. Results are matched, unmatched
(with an error code such as `host_not_allowed` or `invalid_url`), or ambiguous.

## `seo insights` observations

```json
{"schema":"https://bukit.dev/schemas/seo-observation.v1.json","schemaVersion":"1.0",
 "provider":"google-search-console","scope":"google-organic",
 "collectedAt":"2026-08-03T03:00:00Z",
 "window":{"startDate":"2026-07-01","endDate":"2026-07-31","timeZone":"UTC"},
 "rows":[{"url":"https://www.example.com/guides/tea/","impressions":1200,"clicks":24,"averagePosition":8.4}]}
```

- `provider`: `google-search-console` rows need `impressions`, `clicks`
  (≤ impressions), `averagePosition`; `google-analytics-4` rows need `sessions`,
  `engagedSessions` (≤ sessions), `keyEvents`. Do not mix metric sets.
- All files must share the exact same `window`.
- Findings (codes prefixed `seo.insights.`): `snippet_mismatch`, `landing_quality`,
  `discoverability`, `position_opportunity`. Snippet and
  discoverability need both GSC and GA4 data for the route.

## Shared rules for the three advanced inputs

- Every listed field is required. Nullable fields must still be present (as `null`).
- Each root has `schema` set to the exact URL shown below and `"schemaVersion": "1.0"`.
- Identity keys use `<kind>:sha256:<64 lowercase hex>`, e.g. `question:sha256:`,
  `topic:sha256:`, `route:sha256:`, `entity:sha256:`, `answer:sha256:`,
  `context:sha256:`.

## `seo question-insights`

Targets root: `schema` (`https://bukit.dev/schemas/seo-question-target-map.v1.json`),
`schemaVersion`, `generatedAt` (ISO date-time), `questions[]` with `questionKey`
(`question:sha256:<64 hex>`), `topicKey` (`topic:sha256:...`), `intent`
(`informational|navigational|commercial|transactional|other`), `locale`, `priority`
(`P0|P1|P2`), `coveredRouteKeys` (`route:sha256:...` from the route map).

Observations root: `schema`
(`https://bukit.dev/schemas/search-question-observation.v1.json`), `schemaVersion`,
`provider: "google-search-console"`, `scope: "google-organic"`, `collectedAt`,
`collectionMethod` (`api|export|manual`), `window` (`startDate`, `endDate`,
`timeZone`; identical across files), and rows with all of `questionKey`, `topicKey`,
`url`, `locale`, `device` (`desktop|mobile|tablet|unknown`), `impressions`, `clicks`,
`averagePosition` (`clicks` ≤ `impressions`). A covered route key missing from the route map counts as a join gap.

## `seo generative-insights`

Observations root: `schema`
(`https://bukit.dev/schemas/generative-answer-observation.v1.json`), `schemaVersion`,
`engine`, `promptSetVersion`, `locale`, `collectedAt`, `collectionMethod`
(`api|browser-export|manual`), `rows`. Each row has all 8 fields: `questionKey`,
`promptVariant` and `runIndex` (integers 0–9999; the triple `(questionKey,
promptVariant, runIndex)` must be unique per file), `brandMentioned` and `siteCited`
(JSON booleans), `citedUrls` (≤ 100, no duplicates), `citationPosition` (null or ≥ 1, only
when `siteCited`, but always present), `answerHash` (`answer:sha256:...`). `siteCited` must be true exactly
when at least one cited URL is on an allowed host. Report: citation rates overall, per
engine and per question, plus external cited URLs.

Minimal valid file:

```json
{"schema":"https://bukit.dev/schemas/generative-answer-observation.v1.json",
 "schemaVersion":"1.0","engine":"example-engine","promptSetVersion":"2026-11",
 "locale":"en","collectedAt":"2026-11-01T00:00:00Z","collectionMethod":"manual",
 "rows":[{"questionKey":"question:sha256:0000000000000000000000000000000000000000000000000000000000000000","promptVariant":0,"runIndex":0,
   "brandMentioned":true,"siteCited":true,
   "citedUrls":["https://www.example.com/guides/tea/"],"citationPosition":1,
   "answerHash":"answer:sha256:0000000000000000000000000000000000000000000000000000000000000000"}]}
```

## `seo authority-insights`

Observations root: `schema`
(`https://bukit.dev/schemas/external-authority-observation.v1.json`), `schemaVersion`,
`provider`, `collectedAt`, `collectionMethod` (`api|export|manual`), `rows`. Each row
has all 9 fields: `sourceUrl`, `sourceType`
(`official|regulator|research|news|association|repository|forum|other`), `observedAt`
(≤ `collectedAt`), `status` (`active|deleted|unavailable`), `questionKey` / `topicKey` /
`entityKey` (each null or a hashed key; at least one non-null), `contextHash` (`context:sha256:...`), `citedUrls`
(≤ 100, all absolute http(s)). Cited URLs on other hosts are dropped from the report.

## Privacy

Reports are local files, but they are not anonymized: unmatched and ambiguous entries
keep the raw observed URL including query strings (only credentials are stripped),
authority reports keep `sourceUrl`, and the question and generative reports record the
absolute local path of each observation file. Do not publish these reports or commit
them to a public repository. Keep them out of the deployed output. The default `--out`
(`<dir>/.bukit/`) is deleted by the next clean build and is excluded only by
`bukit deploy`, not by other uploads of the output; pass `--out` outside the output
directory to keep a report.

## Workflow

```bash
bukit build
bukit seo insights --dir dist \
  --observations data/gsc.json,data/ga4.json \
  --rules data/seo-rules.json --strict-join
```
