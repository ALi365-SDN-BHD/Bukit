# 19 Analytics

Bukit owns Analytics output through the Core built-in `analytics` plugin. The
plugin is registered through `BuiltInPluginSource`, `PluginRegistry`, and
`PluginRunner`, then contributes an internal HTML transform before pages are
written. It is not an external protocol plugin, and external plugins do not
receive page HTML or Analytics provider APIs.

## Configuration

```text
site:
  plugins:
    analytics:
      enabled: true

  analytics:
    enabled: true
    productionOnly: true
    consent:
      google:
        mode: advanced
        defaults:
          adStorage: denied
          analyticsStorage: denied
          adUserData: denied
          adPersonalization: denied
        waitForUpdateMs: 500
    csp:
      mode: requirements-report
    providers:
      - type: google-analytics
        measurementId: G-XXXXXXXXXX

      - type: google-tag-manager
        containerId: GTM-XXXXXXX

      - type: plausible
        domain: example.com
        snippetMode: site-specific
        scriptUrl: https://plausible.io/js/pa-EXAMPLE123.js

      - type: umami
        websiteId: 00000000-0000-0000-0000-000000000000
        scriptUrl: https://analytics.example.com/script.js

build:
  report:
    enabled: true
```

The values above are placeholders on reserved example domains. Replace only
the fields required by providers you actually use; do not commit private keys
or unrelated secrets.

## Two Switches

Analytics uses two independent switches:

| Switch | Default | Responsibility |
|---|---:|---|
| `site.plugins.analytics.enabled` | `true` | Controls whether the built-in plugin participates in the plugin lifecycle. |
| `site.analytics.enabled` | `true` | Controls whether the enabled plugin is allowed to emit Analytics output. |

Both switches must be true. Bukit also requires at least one provider and an
execution mode allowed by `productionOnly`. Disabling the plugin switch means
no Analytics transform is created and no `html-transform` execution record is
produced. Disabling the feature switch keeps the plugin registered but emits
no provider blocks.

Defaults are intentionally inert: `enabled` and `productionOnly` default to
true, but `providers` defaults to an empty array, so a new site emits nothing.

## Providers

| Type | Required fields | Optional fields | Placement |
|---|---|---|---|
| `google-analytics` | `measurementId` | none | after opening `<head>` |
| `google-tag-manager` | `containerId` | none | after opening `<head>` and `<body>` |
| `plausible` | `domain`, `snippetMode`, `scriptUrl` | none | end of `<head>` |
| `umami` | `websiteId`, `scriptUrl` | none | end of `<head>` |

Provider types are exact, lowercase kebab-case values. Provider-specific
fields cannot be mixed: for example, a Google Analytics entry cannot carry a
container ID or script URL. Duplicate provider keys are rejected, including
Plausible domains that normalize to the same IDN ASCII host. Valid providers
are emitted in YAML order.

Validation rules are deliberately narrow:

- Google Analytics measurement IDs match `^G-[A-Z0-9]+$`.
- Google Tag Manager container IDs match `^GTM-[A-Z0-9]+$`.
- Plausible domains are DNS host names. Schemes, ports, paths, queries,
  fragments, credentials, and IP addresses are rejected.
- Plausible `snippetMode` is `site-specific` or `legacy`. Site-specific mode
  emits the current fixed `async` loader, queue bootstrap, and
  `plausible.init()` structure without `data-domain`; legacy mode emits the
  historical `defer + data-domain` tag.
- Umami website IDs are UUIDs.
- Script URLs are absolute HTTPS URLs ending in `.js`, with no credentials,
  fragment, or non-default port.
- Plausible and Umami require an explicit script URL. Plausible no longer
  defaults to the historical `https://plausible.io/js/script.js`; existing
  installations that need it must select `snippetMode: legacy` explicitly.
- A site-specific Plausible Cloud URL must use `/js/pa-<site-id>.js`; a `pa-*`
  URL cannot be labeled legacy.

Plausible assigns the site-specific URL in Site Installation settings. Copy
that URL exactly. Existing legacy Cloud or self-hosted installations remain
available explicitly:

```yaml
- type: plausible
  domain: example.com
  snippetMode: legacy
  scriptUrl: https://plausible.io/js/script.js
```

See the [Plausible script update guide](https://plausible.io/docs/script-update-guide)
for the upstream migration boundary.

For `site-specific`, Bukit emits the fixed loader/bootstrap only; it does not
configure a custom event endpoint or accept arbitrary init options. A
self-hosted or proxied script URL is therefore suitable only when that script
is already bound to the correct endpoint.

Providers generate fixed templates. They cannot read files, access the
network, or accept arbitrary JavaScript, head HTML, or body HTML. Bukit encodes
configured values at the HTML or JavaScript boundary.

## Google Consent Mode

Any `google-analytics` or `google-tag-manager` provider requires an explicit
`site.analytics.consent.google` policy. Bukit currently supports only Google
Consent Mode v2 `advanced` mode. All four defaults are required and each must
be `granted` or `denied`; `waitForUpdateMs` is optional from 0 through 5000.
The generated default command appears once, before every Google loader,
container bootstrap, and `config` command.

Bukit does not act as a CMP. For a GA-only site, the site CMP owns the standard
`gtag('consent', 'update', ...)` call. For a GTM site, consent updates belong in
a GTM consent template using `setDefaultConsentState` and
`updateConsentState`; Bukit does not add a competing update helper. Advanced
mode can still load Google resources and send cookieless pings while consent
is denied. This setting is therefore not a promise of zero network traffic or
legal compliance. See the [Google Consent Mode guide](https://developers.google.com/tag-platform/security/guides/consent).

## CSP Requirements Report

`site.analytics.csp.mode: requirements-report` is optional and requires
`build.report.enabled: true`. It extends `.bukit/analytics-report.json` with
the exact SHA-256 sources for Bukit-generated inline scripts, required
`script-src` and `frame-src` origins, and a flag when GTM may introduce
container-defined destinations. The report explicitly sets
`completePolicy: false`: it covers only Bukit Analytics fragments, not theme
scripts, content, headers, CMP code, or destinations configured inside GTM.

Bukit is a static generator and does not issue a fixed or placeholder CSP
nonce. A safe nonce must be random and unique per HTTP response, so it belongs
to the serving layer. Use the deterministic hashes from the report as inputs
to your deployment policy, then add all non-Analytics requirements yourself.
See the [Google CSP guide](https://developers.google.com/tag-platform/security/guides/csp).

## Build, Dev, And Preview

| Command or mode | `productionOnly: true` | `productionOnly: false` |
|---|---|---|
| `bukit build` | injects enabled providers | injects enabled providers |
| CI build | injects enabled providers | injects enabled providers |
| `bukit dev` | development build does not inject; served HTML also removes current Bukit-managed blocks | injects and serves enabled providers |
| `bukit preview` | removes current Bukit-managed blocks from the HTTP response when the active nearest config enables the policy | serves generated HTML unchanged |

Development and preview filtering never rewrites generated files. It removes
only well-formed blocks marked by the current `bukit:analytics` comments.
Unmarked third-party scripts and malformed or unmatched comments remain
unchanged. When preview cannot find or load an applicable `site.yaml`, it
serves the existing HTML unchanged.

## Theme And SEO Boundary

Themes and Scriban templates cannot read Analytics configuration: there is no
`site.analytics` template object and no Analytics rendering model. Do not add
provider scripts to a theme partial as a fallback. The Core transform injects
Analytics for content, list, and static HTML independently of
`site.seo.enabled` and `site.seo.renderMode`.

This separation means SEO can use `inject`, `theme`, or `off` without changing
the Analytics decision. A missing `<head>` skips head fragments; a missing
`<body>` skips the Google Tag Manager body fragment without synthesizing
markup.

## Build Report

When build reports are enabled, each build variant writes
`.bukit/analytics-report.json`. The report records switches, execution mode,
provider types, Google consent summary, optional CSP requirements,
processed/injected counts, and fixed skip reasons. It never includes
measurement IDs, container IDs, the Plausible tracking domain, website IDs,
or full script URLs. When CSP reporting is enabled, it intentionally includes
path-free scheme-and-authority origins such as `https://plausible.io`.
The report uses `analytics-report.v2`; the separate frozen `build-report.v1`
shape is unchanged.

## Breaking Removal

The former googleAnalyticsId and disableInPreview configuration keys have been
removed. They are not deprecated and have no compatibility mapping, warning
path, environment fallback, or theme fallback. If either key appears in YAML,
strict field validation reports it as unknown. Re-express the desired provider
and environment policy with `providers` and `productionOnly`.
