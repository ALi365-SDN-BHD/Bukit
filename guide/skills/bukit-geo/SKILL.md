---
name: bukit-geo
description: Use for Bukit generative-engine optimization — site.seo.geo, llms.txt and llms-full.txt, per-page geo front matter (llms curation, FAQ, HowTo, citations, author, speakable), AI crawler policy in robots.txt, agent-manifest.json, and `bukit geo audit`.
---

# Bukit GEO

GEO makes a site easy for AI systems to read and cite: `llms.txt` summaries,
structured data, and an explicit AI crawler policy.

## Configuration

```yaml
site:
  url: https://example.com        # absolute llms links and robots.txt need it
  title: Example
  description: One-line site summary   # becomes the llms.txt blockquote
  seo:
    robotsTxt:
      enabled: true               # default false; AI policy is written only when true
    geo:
      enabled: true               # default true
      llmsTxt: true               # default true
      llmsFullTxt: false          # default false
      llmsTxtMaxArticles: 20      # per-collection cap; 0 = unlimited; negative is invalid
      aiBotMode: allow            # allow|block-training|block|selective
      aiBotAllowList: [OAI-SearchBot]
      aiBotBlockList: [Bytespider]
      llmsTxtOptionalLinks:
        - title: Documentation
          url: https://example.com/docs/   # written exactly as given
          description: Public docs
```

`llms.txt` is on by default, so most sites already produce one. Disable with
`geo.llmsTxt: false` or `geo.enabled: false`.

## llms.txt layout

1. `# <site.title>` and `> <site.description>`.
2. Pages without a collection under `## Documentation` (if the home page is among them)
   or `## Pages`. Not capped.
3. One `## <Collection Name>` section per collection: every `visibility: include`
   page plus the top `llmsTxtMaxArticles` `auto` pages, ordered by priority, then
   publish date.
4. `## Optional`: optional-tier pages, `llmsTxtOptionalLinks`, and a link to
   `agent-manifest.json` (always present).

Each entry is a Markdown list item: a link with the page title as text and the page URL
as target, followed by `: description`. `llms-full.txt` contains every primary and
optional page in full (title, URL, description, author, body as Markdown), ordered by
URL only and never capped.

Only indexable pages are candidates. Noindex pages never appear.

## Per-page curation

```yaml
---
title: Pricing
geo:
  llms:
    visibility: include   # auto (default) | include | exclude
    tier: optional        # primary (default) | optional
    priority: 10          # integer -100..100, default 0
---
```

Only these three keys are allowed, and `priority` must be a YAML integer. Invalid
curation (unknown key, bad value, `llms: false`, or a `geo:` value that is not a
mapping) excludes the page from llms output and, when `llmsTxt` or `llmsFullTxt` is on,
logs `geo.llms_field_unknown`, `geo.llms_visibility_invalid`, `geo.llms_tier_invalid`,
or `geo.llms_priority_invalid`, each with `route=<output path>`, for every routed page
(noindex pages included). These follow `site.seo.diagnostics`: a warning by default,
a build failure (exit 2) under `strict`, silent under `off`.

These build diagnostics are new (CHANGELOG "Unreleased"). Bukit 2.0.11 and earlier drop
such pages silently, even under `strict`; there the only signal is the publish audit
warning `publish.llms_missing_route route=<url>` (and `publish.llms_full_missing_route`).
Use `visibility: exclude` to remove a page on purpose.

## Structured data front matter

```yaml
geo:
  schema_type: FAQPage
  about: Tea brewing
  date_reviewed: 2026-10-01
  author: { name: Jane Doe, url: https://example.com/jane/, same_as: [https://x.com/jane] }
  same_as: [https://en.wikipedia.org/wiki/Tea]
  citations:
    - { title: Study, url: https://example.org/study, relation: citation }   # citation (default) | based-on
  faq:
    - { question: How hot?, answer: 80-85 °C. }
  steps:                       # for HowTo
    - { name: Heat water, text: Bring water to 85 °C. }
  speakable_xpath: /html/body/main/h1
```

Build diagnostics follow `site.seo.diagnostics` (warn by default, strict fails the
build): `geo.faq_items_missing` / `geo.howto_steps_missing` (schema type without
items), `geo.faq_empty_question`, `geo.faq_empty_answer`, `geo.howto_step_empty_name`,
`geo.howto_step_empty_text`, `geo.citation_url_invalid` (not absolute),
`geo.citation_relation_invalid` (not `citation` or `based-on`), `geo.author_no_sameas`,
and `geo.speakable_path_invalid` (must start with `/`).

## AI crawler policy

Written into robots.txt only when `robotsTxt.enabled: true`, `geo.enabled: true`,
`site.url` is set, and there is no static `robots.txt`. Otherwise, if a non-default AI
policy is configured (`aiBotMode` other than `allow`, or an allow/block list), the
build logs `event=robots.ai_policy_inactive reason=robots_txt_disabled|geo_disabled|site_url_missing|static_robots_txt`.

| Mode | Effect |
|---|---|
| `allow` | All known crawlers allowed. |
| `block-training` | Blocks training crawlers (GPTBot, ClaudeBot, CCBot, Google-Extended, Applebot-Extended, Meta-ExternalAgent, Bytespider, Amazonbot, ...). Search and user-fetch bots stay allowed. |
| `block` | Blocks all known AI crawlers. |
| `selective` | With an allow list, only listed crawlers are allowed; with only a block list, just those are blocked. |

The block list always beats the allow list; unknown names get their own groups. Tokens
may contain letters, digits, `-`, `_`, `.`. Blocking search bots (OAI-SearchBot,
Claude-SearchBot, PerplexityBot) or user-fetch bots (ChatGPT-User, Claude-User,
Perplexity-User) in any mode produces the publish audit warning
`publish.ai_crawler_policy_conflict` on indexable routes. In `selective` mode the build
also logs `event=robots.ai_search_blocked` for such bots left off the allow list.
Blocking `Google-Extended` (as `block-training` does) logs
`event=robots.ai_grounding_blocked`, because that token also controls grounding in
Gemini answers.

For the `Content-Signal` line (`search`, `aiInput`, `aiTrain`), see `bukit-outputs`.

## agent-manifest.json

Always written, even with `geo.enabled: false`. It lists indexable documents with their
HTML, JSON, Markdown, and JSON-LD representations.

## Audit

```bash
bukit build
bukit geo audit --dir dist
```

Prints whether `llms.txt`, `llms-full.txt`, and `robots.txt` exist, geo-enhanced routes,
and `GEO Score: N/100` from `.bukit/geo-report.json`. It exits 0 regardless of score,
so it is informational, not a gate (exit 1 when `.bukit/geo-report.json` is missing or
invalid, exit 2 when `--dir` does not exist). Use `bukit seo audit` and `bukit publish audit`
for pass/fail checks (`geo.llms_txt_missing`, `publish.llms_missing_route`, ...);
these are warnings, so use `--strict` to fail on them.

Score points: llms.txt +25, llms-full.txt +15, any geo-enhanced route +10, article
coverage up to +15, any FAQPage/HowTo +15, a valid article author +10, Speakable +5,
a WebPage schema on a geo route when there are two or more geo routes +5 (geo routes
include any article, FAQPage, HowTo, Person, or Speakable page).
