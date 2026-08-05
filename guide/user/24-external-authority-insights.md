# 24 External Authority Insights

`bukit seo authority-insights` joins local external authority observations
with the route map and writes an offline citation evidence report. It helps a
site team see which external pages cite site routes and how those citations
change over time; it is not a provider connector, a crawler, an authority
scorer, or a publishing mechanism.

## Purpose And Boundary

The operating flow is:

```text
build -> route map -> local external observations -> external authority report
```

1. `bukit build` writes `.bukit/seo-route-map.json` using the
   `seo-route-map.v1` contract.
2. An explicitly authorized collector gathers external citation evidence
   outside Core and writes one or more local
   `external-authority-observation.v1` JSON files.
3. `bukit seo authority-insights` joins the cited URLs to the route map and
   writes the offline `external-authority-report.v1` report.

Core never receives raw page content, comment text, usernames, user IDs, or
private messages. Each source page is represented by its URL plus a
`context:sha256:` context hash, and question, topic, and entity identities
are hashed `question:sha256:`, `topic:sha256:`, and `entity:sha256:` keys.
Core does not authenticate to any provider, does not access the network, and
does not store credentials, schedule, or notify.

The report is evidence for human review. It counts observed citations only;
it does not score authority, rank sources, judge factual accuracy, or
recommend edits. Automated posting, commenting, voting, messaging, and
account creation are forbidden in this workflow.

## Local Input Contract

An observation dataset records one provider, a collection timestamp, a
`collectionMethod` of `api`, `export`, or `manual`, and one row per observed
source page:

```json
{
  "schema": "https://bukit.dev/schemas/external-authority-observation.v1.json",
  "schemaVersion": "1.0",
  "provider": "approved-provider",
  "collectedAt": "2026-08-05T03:00:00Z",
  "collectionMethod": "manual",
  "rows": [
    {
      "sourceUrl": "https://source.example/discussion/1",
      "sourceType": "forum",
      "observedAt": "2026-08-05T03:00:00Z",
      "status": "active",
      "questionKey": "question:sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "topicKey": null,
      "entityKey": null,
      "contextHash": "context:sha256:abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "citedUrls": [
        "https://www.example.com/guides/tea/"
      ]
    }
  ]
}
```

`provider` is a free-form collector-supplied identifier; Core does not map
provider names to trust levels. `sourceType` is one of the fixed categories
`official`, `regulator`, `research`, `news`, `association`, `repository`,
`forum`, or `other`. `status` is `active`, `deleted`, or `unavailable`; at
least one of `questionKey`, `topicKey`, and `entityKey` must be non-null,
and every row requires a `contextHash`. A row `observedAt` later than the
dataset `collectedAt` is rejected. Unknown or duplicate fields are rejected;
the row list is capped at 100000 entries.

## Run Authority Insights

Build first, then supply local files only:

```bash
bukit build --clean
bukit seo authority-insights \
  --dir dist \
  --observations observations/external-authority.json \
  --rules seo-insights-rules.json
```

| Option | Required | Default | Meaning |
|---|---:|---|---|
| `--dir` | No | `dist` | Build output directory. |
| `--routes` | No | `<dir>/.bukit/seo-route-map.json` | Local route-map path. |
| `--observations` | Yes | — | Comma-separated list of 1-10 local `external-authority-observation.v1` JSON files. |
| `--rules` | Yes | — | Local rules JSON path supplying the host allowlist. |
| `--out` | No | `<dir>/.bukit/external-authority-report.json` | Local report output path. |
| `--strict-join` | No | off | Return a nonzero result for unmatched or ambiguous cited URLs. |

| Exit | Meaning |
|---:|---|
| `0` | Report was written; complete joins and allowed join gaps both succeed. |
| `1` | `--strict-join` found an unmatched or ambiguous cited URL. The report is written before returning exit code 1. |
| `2` | Input, schema, local-path, or read/write failure; inspect the stable error code. |

On success, stdout reports only aggregate source/match counts, the local
report path, and a classification. It does not echo raw content, usernames,
or credentials.

## Lifecycle Accounting

Every source row stays in the report `sources` evidence array with its
provider, source type, status, last `observedAt`, source URL, context hash,
and matched route keys. Only rows with `status` equal to `active` contribute
to current citation totals: the `activeSources` value in `overall`, per-route
`activeSources`, and the `activeCitedRoutes` value in `overall`. Rows with
`deleted` or `unavailable` status keep their history so consumers can explain
citation declines without erasing evidence. A route cited more than once by
one source row is counted once for that source, and duplicate URLs that
normalize to the same canonical are joined once.

Allowed-host cited URLs enter route matching with the same host allowlist
and URL normalization as `seo insights`. Third-party HTTP(S) URLs remain
valid external context; they are not joined and never count as unmatched
site routes. An allowed-host URL that matches no route remains in
`unmatchedCitedUrls`; a URL whose canonical maps to several routes remains in
`ambiguousCitedUrls` and ambiguity never chooses a winner. `joinQuality`
counts the joined cited URLs as source, matched, unmatched, and ambiguous
rows. Provider and source-type totals are reported separately and are never
merged into a single score.

## Reddit Decision Gate

No Reddit adapter ships with Core. A future `Bukit.Plugin.RedditObserve`
may be proposed only after:

```text
1. approved API use case and credentials boundary;
2. measured incremental value over GSC/GA4/generative observations;
3. fixed subreddit/query scope, rate and retention policy;
4. deletion/unavailable synchronization;
5. read-only commands only;
6. output validates against external-authority-observation.v1.
```

Any such plugin stays external and read-only. Automated posting, commenting,
voting, messaging, and account creation remain out of scope permanently.

## Privacy And Plugin Handoff

Within this citation workflow, outputs and logs use hashed
`question:sha256:`, `topic:sha256:`, `entity:sha256:`, `context:sha256:`,
and `route:sha256:` values plus cited URLs only. Core never receives raw
content or user identity, and the report is not publishable or website
content. `.bukit` is internal diagnostics output.

An external collector/plugin must request explicit network and environment
permission before it contacts a provider or reads credentials. Its Core
handoff writes only local `external-authority-observation.v1` JSON. Provider
authentication, retries, and caching stay outside Core; do not add a provider
token, raw page text, or user identity to the observation or report.
