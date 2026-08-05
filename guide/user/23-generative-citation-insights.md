# 23 Generative Citation Insights

`bukit seo generative-insights` joins local generative answer observations
with the route map and writes an offline citation report. It helps a site
team review how often generative engines mention the brand and cite site
routes; it is not a provider connector, a model API client, a ranking
guarantee, or an automatic edit mechanism.

## Purpose And Boundary

The operating flow is:

```text
build -> route map -> local generative observations -> generative citation report
```

1. `bukit build` writes `.bukit/seo-route-map.json` using the
   `seo-route-map.v1` contract.
2. An explicitly authorized collector runs a fixed question set against one or
   more generative engines outside Core and writes one or more local
   `generative-answer-observation.v1` JSON files.
3. `bukit seo generative-insights` joins the cited URLs to the route map and
   writes the offline `generative-citation-report.v1` report.

Core never receives raw answers or prompts. Question identities are hashed
`question:sha256:` keys and each observed answer is represented only by an
`answer:sha256:` hash; the raw answer text, prompt text, and any user or
account identity stay in the external collector. Core does not authenticate
to any provider, does not access the network, and does not store credentials,
schedule, or notify.

The report prioritizes human review. It does not prove demand,
and it does not prove causation; it does not authorize an automatic edit.
An absent citation is not evidence that an engine never cites the site.

## Local Input Contract

An observation dataset records one engine, one versioned prompt set, one
locale, a collection timestamp, a `collectionMethod` of `api`,
`browser-export`, or `manual`, and one row per observed run:

```json
{
  "schema": "https://bukit.dev/schemas/generative-answer-observation.v1.json",
  "schemaVersion": "1.0",
  "engine": "engine-one",
  "promptSetVersion": "2026-08-v1",
  "locale": "en",
  "collectedAt": "2026-08-05T03:00:00Z",
  "collectionMethod": "manual",
  "rows": [
    {
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "promptVariant": 0,
      "runIndex": 0,
      "brandMentioned": true,
      "siteCited": true,
      "citedUrls": [
        "https://www.example.com/guides/tea/"
      ],
      "citationPosition": 1,
      "answerHash": "answer:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789"
    }
  ]
}
```

`engine` is a free-form collector-supplied identifier; Core does not hardcode
any provider. `promptVariant` and `runIndex` both range from `0` to `9999`,
and each `(questionKey, promptVariant, runIndex)` tuple must be unique within
one dataset. `citationPosition` is a positive integer only when `siteCited`
is true, otherwise null. Unknown or duplicate fields are rejected; the row
list is capped at 100000 entries.

The validator checks that `siteCited` agrees with the presence of at least
one cited URL whose host is `siteHost` or `hostAliases` from the shared
`seo-insights-rules.v1` rules profile. Third-party HTTP(S) URLs remain valid
external evidence; they enter a separate `externalCitedUrls` array and never
count as unmatched site routes.

## Run Generative Insights

Build first, then supply local files only:

```bash
bukit build --clean
bukit seo generative-insights \
  --dir dist \
  --observations observations/generative-runs.json \
  --rules seo-insights-rules.json
```

| Option | Required | Default | Meaning |
|---|---:|---|---|
| `--dir` | No | `dist` | Build output directory. |
| `--routes` | No | `<dir>/.bukit/seo-route-map.json` | Local route-map path. |
| `--observations` | Yes | — | Comma-separated list of 1-10 local `generative-answer-observation.v1` JSON files. |
| `--rules` | Yes | — | Local rules JSON path supplying the host allowlist. |
| `--out` | No | `<dir>/.bukit/generative-citation-report.json` | Local report output path. |
| `--strict-join` | No | off | Return a nonzero result for unmatched or ambiguous cited URLs. |

| Exit | Meaning |
|---:|---|
| `0` | Report was written; complete joins and allowed join gaps both succeed. |
| `1` | `--strict-join` found an unmatched or ambiguous cited URL. The report is written before returning exit code 1. |
| `2` | Input, schema, local-path, or read/write failure; inspect the stable error code. |

On success, stdout reports only aggregate source/match counts, the local
report path, and a classification. It does not echo raw answers, prompts, or
credentials.

## Aggregation And Join Quality

For every group the report outputs `runs`, `brandMentions`,
`brandMentionRate`, `siteCitations`, and `siteCitationRate`; each ratio is
null when its run count is zero. Groups cover the overall result, each
engine, and each question. A route cited more than once inside a single run
is counted once for that run. Datasets with contradictory prompt-set
versions remain separate `sources` entries instead of being merged.

Allowed-host cited URLs enter route matching with the same host allowlist
and URL normalization as `seo insights`. An allowed-host URL that matches no
route remains in `unmatchedCitedUrls`; a URL whose canonical maps to several
routes remains in `ambiguousCitedUrls` and ambiguity never chooses a winner.
`joinQuality` counts the allowed-host cited URLs as source, matched,
unmatched, and ambiguous rows.

## Repeatability Boundary

Generative engines vary their answers between runs, so comparisons are only
meaningful when the method is held constant: use a fixed question set, a
versioned prompt set, multiple prompt phrasings, and repeated runs per
question. Record the engine and model context whenever the collector can
provide it. These numbers describe observed samples only;
observed changes do not prove causation, and they do not justify automatic
content edits.

## Privacy And Plugin Handoff

Within this citation workflow, outputs and logs use hashed
`question:sha256:`, `answer:sha256:`, and `route:sha256:` values plus cited
URLs only. Core never receives raw answers or prompts, and the report is not
publishable or website content. `.bukit` is internal diagnostics output.

An external collector/plugin must request explicit network and environment
permission before it contacts a provider or reads credentials. Its Core
handoff writes only local `generative-answer-observation.v1` JSON. Provider
authentication, retries, and caching stay outside Core; do not add a provider
token, raw answer text, or user identity to the observation or report.
