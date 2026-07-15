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
    providers:
      - type: google-analytics
        measurementId: G-XXXXXXXXXX

      - type: google-tag-manager
        containerId: GTM-XXXXXXX

      - type: plausible
        domain: example.com
        scriptUrl: https://plausible.io/js/script.js

      - type: umami
        websiteId: 00000000-0000-0000-0000-000000000000
        scriptUrl: https://analytics.example.com/script.js
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
| `google-analytics` | `measurementId` | none | end of `<head>` |
| `google-tag-manager` | `containerId` | none | end of `<head>` and start of `<body>` |
| `plausible` | `domain` | `scriptUrl` | end of `<head>` |
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
- Umami website IDs are UUIDs.
- Script URLs are absolute HTTPS URLs ending in `.js`, with no credentials,
  fragment, or non-default port.
- Plausible defaults to `https://plausible.io/js/script.js` when `scriptUrl` is
  omitted. Umami requires an explicit script URL.

Providers generate fixed templates. They cannot read files, access the
network, or accept arbitrary JavaScript, head HTML, or body HTML. Bukit encodes
configured values at the HTML or JavaScript boundary.

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
provider types, processed/injected counts, and fixed skip reasons. It never
includes measurement IDs, container IDs, domains, website IDs, or script URLs.
The frozen `build-report.v1` shape is unchanged.

## Breaking Removal

The former googleAnalyticsId and disableInPreview configuration keys have been
removed. They are not deprecated and have no compatibility mapping, warning
path, environment fallback, or theme fallback. If either key appears in YAML,
strict field validation reports it as unknown. Re-express the desired provider
and environment policy with `providers` and `productionOnly`.
