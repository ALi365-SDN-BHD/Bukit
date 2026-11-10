---
name: bukit-analytics
description: Use when adding or debugging web analytics in a Bukit site — site.analytics providers (Google Analytics, Google Tag Manager, Plausible, Umami), Google consent mode defaults, production-only injection, CSP requirements report, and `.bukit/analytics-report.json`.
---

# Bukit Analytics

Core injects analytics snippets into generated HTML during the build. Themes do not
need to include tracking code; do not hand-write provider snippets in templates.

## Configuration

```yaml
site:
  analytics:
    enabled: true            # default true
    productionOnly: true     # default true: no injection in `bukit dev`
    providers:
      - type: google-analytics
        measurementId: G-XXXXXXXXXX
      - type: google-tag-manager
        containerId: GTM-XXXXXXX
      - type: plausible
        domain: example.com
        snippetMode: site-specific        # site-specific|legacy
        scriptUrl: https://plausible.io/js/pa-EXAMPLE123.js
      - type: umami
        websiteId: 00000000-0000-0000-0000-000000000000
        scriptUrl: https://analytics.example.com/script.js
```

| `type` | Required fields | Validation |
|---|---|---|
| `google-analytics` | `measurementId` | `G-` followed by uppercase letters/digits |
| `google-tag-manager` | `containerId` | `GTM-` followed by uppercase letters/digits |
| `plausible` | `domain`, `snippetMode`, `scriptUrl` | DNS host; on `plausible.io`, site-specific needs `/js/pa-<id>.js` and legacy must not use it |
| `umami` | `websiteId`, `scriptUrl` | UUID |

Every `scriptUrl` must be an absolute HTTPS `.js` URL without credentials, fragment, or
custom port. `type` is case-sensitive. Fields from another provider type and duplicate
providers are rejected. Old keys `googleAnalyticsId` and `disableInPreview` no longer
exist and fail strict validation.

The built-in plugin switch `site.plugins.analytics: false` disables the feature as well.

## Consent mode (Google)

```yaml
site:
  analytics:
    consent:
      google:
        mode: advanced                 # only accepted value
        defaults:                      # all four required: granted|denied
          adStorage: denied
          analyticsStorage: denied
          adUserData: denied
          adPersonalization: denied
        waitForUpdateMs: 500           # optional, 0..5000
```

Requires at least one Google provider. The consent default block is injected first.
Your site still needs its own consent banner that calls `gtag('consent', 'update', ...)`.
The GTM `<noscript>` iframe is only emitted when all four defaults are `granted` or no
consent is configured.

## CSP requirements

```yaml
site:
  analytics:
    csp:
      mode: requirements-report   # only accepted value
build:
  report:
    enabled: true                 # required for csp
```

Bukit does not write a Content-Security-Policy header. It reports what the policy
needs (`script-src`, `frame-src`, `connect-src`, `img-src` origins and inline script
hashes) in `.bukit/analytics-report.json` so you can configure the host.

## Injection

- Head-start fragments (consent, GA, GTM) go after the `<meta charset>`; Plausible and
  Umami go before `</head>`; the GTM noscript goes after `<body>`.
- Each block is wrapped in `<!-- bukit:analytics:<type>:<id>:<head|body>:start -->` (e.g.
  `google-analytics:G-XXXX`, `google-consent:default`) /
  `:end -->` markers; Bukit removes and re-inserts its own blocks on every build.
- Pages without `<head>` (or `<body>` when needed) are skipped and counted.
- `bukit dev` builds skip injection when `productionOnly` is true. `bukit preview`
  strips managed blocks from served HTML in that case when it can load the site config
  (`--config`/`--site`, or a `site.yaml` found from the served directory); otherwise
  it keeps them and warns. Files on disk always keep them.

## Report

`.bukit/analytics-report.json` (when `build.report.enabled: true`; disabling reports
deletes an existing file) records plugin/feature state, execution mode, provider
types, consent defaults, CSP requirements, `processedHtml`, `injectedHtml`, and
`skippedByReason` (`analytics_disabled`, `no_providers`, `development_mode`,
`head_missing`, `body_missing`, `incremental_unchanged`, ...).

## Verify

```bash
bukit build
grep -l "bukit:analytics" dist/index.html
cat dist/.bukit/analytics-report.json
```
