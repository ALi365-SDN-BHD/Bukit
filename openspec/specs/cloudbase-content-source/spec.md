# cloudbase-content-source Specification

## Purpose
Lets a site read one PostgreSQL table or view in the `public` schema of a Tencent CloudBase PG-mode
environment as a read-only content source with `content.sources[].type: cloudbase`, map its rows to
Bukit content documents, render Markdown bodies safely, and publish CloudBase-hosted images only
after local download, without leaking CloudBase identifiers, credentials or signed links.

## Requirements

### Requirement: CloudBase source configuration is validated before any request
`config check`, `doctor` and `build` SHALL accept a `content.sources[]` entry with `type: cloudbase`
and a `cloudbase` section. Validation SHALL fail with `CloudBaseConfigInvalid` naming the offending
path when a required field is missing, a value is out of range, an unknown field is present,
`envId` does not match `^[a-z0-9][a-z0-9-]{0,62}$`, `region` is not `cn` or `intl`, `table` or
`idField` does not match `^[A-Za-z_][A-Za-z0-9_]{0,62}$`, `storageBucket` is missing while
`fileFields` is non-empty or does not match `^[a-z0-9][a-z0-9._-]{0,62}$`, `pageSize` is outside 1 to
1000, `signExpiresSeconds` is outside 60 to 3600, `cacheMode` is not `off`, `bodyFormat` is not
`markdown`, or `filter` contains a key other than `field` and `value`. The former `kind`, `model`
and `collectionName` keys are unknown fields. A credential field written in YAML SHALL fail
validation; the credential SHALL come only from the `CLOUDBASE_PUBLISHABLE_KEY` environment
variable or `--secrets-stdin`. The configuration types SHALL be internal and SHALL NOT add public
API.

#### Scenario: Valid source
- **WHEN** a site declares a `cloudbase` source with `envId: prod-abc`, `table: articles` and `bodyField: content`
- **THEN** `config check` passes, the source uses region `cn`, `idField` defaults to `id` and `pageSize` defaults to 100

#### Scenario: Unsafe envId or table
- **WHEN** `envId` contains `/`, `@`, `#`, `?`, `.` or an uppercase letter or is longer than 63 characters, or `table` contains `.`, `/`, `?` or a space
- **THEN** validation fails with `CloudBaseConfigInvalid` naming that path and no request is sent

#### Scenario: Credential in YAML
- **WHEN** the `cloudbase` section contains a key or token field
- **THEN** validation fails with `CloudBaseConfigInvalid`

#### Scenario: Unsupported options
- **WHEN** `cacheMode: readonly`, `bodyFormat: html`, `kind: model` or `filter: { field: status, op: ne, value: x }` is configured
- **THEN** validation fails with `CloudBaseConfigInvalid` naming that path

#### Scenario: File fields without a bucket
- **WHEN** `fileFields: [cover]` is configured without `storageBucket`
- **THEN** validation fails with `CloudBaseConfigInvalid` naming `cloudbase.storageBucket`

### Requirement: File fields require guaranteed local media download
When `fileFields` is non-empty, configuration validation SHALL fail with `CloudBaseConfigInvalid`
unless `content.media.downloadToLocal` is not `false`, every file field is listed in
`content.media.fieldKeys`, `content.media.onFailure` is `placeholder`, and every file field is an
image field. When the placeholder image is missing, `build` SHALL fail with
`CloudBaseConfigInvalid`, and `config check` and `doctor` SHALL report a warning. When
`content.media.downloadHosts` or `skipHosts` would exclude the environment's gateway host
(`{envId}.api.tcloudbasegateway.com` or `{envId}.api.intl.tcloudbasegateway.com`) or its storage CDN
host (`{envId}.storage.tcloudbase.com`), validation SHALL
fail with `CloudBaseMediaLocalizationRequired`.

#### Scenario: Keep on failure
- **WHEN** `fileFields: [cover]` is configured with `content.media.onFailure: keep`
- **THEN** validation fails with `CloudBaseConfigInvalid`

#### Scenario: Field not localized
- **WHEN** `fileFields: [cover]` is configured and `cover` is not in `content.media.fieldKeys`
- **THEN** validation fails with `CloudBaseConfigInvalid`

#### Scenario: Missing placeholder
- **WHEN** `fileFields` is non-empty and the media placeholder file does not exist
- **THEN** `build` fails with `CloudBaseConfigInvalid` and `config check` reports a warning

### Requirement: Rows are read completely, filtered, sorted and bounded
The source SHALL read the configured table or view through the PostgREST read interface, page by
page with `limit` and `offset`, requesting the exact total count. When the total is known it SHALL
stop when the offset reaches the total or a page is empty, so that a gateway row cap below the page
size does not truncate the result; without a total it SHALL stop when a page holds fewer rows than
the page size. Every request SHALL carry the
configured single-field equality filter as a URL-encoded `eq.` condition, and an order that ends
with the unique `idField` ascending as a tiebreak so that paging is stable. The source SHALL stop
at `maxItems` rows when it is set. Responses that exceed the size limit, do not match the expected
structure, or carry an unparseable count SHALL fail with `CloudBaseResponseInvalid`. Automated
tests SHALL use a fake HTTP handler and SHALL NOT contact the network.

#### Scenario: Multi-page read
- **WHEN** the fake remote holds 250 matching rows and the page size is 100
- **THEN** the build produces 250 documents from three requests

#### Scenario: maxItems truncation
- **WHEN** `maxItems: 120` is configured and 250 rows match
- **THEN** exactly 120 documents are produced

#### Scenario: Filter and sort sent
- **WHEN** `filter: { field: status, value: published }` and `sort: { field: publish_at, direction: desc }` are configured
- **THEN** every request carries the `status` equality filter for `published` and orders by `publish_at` descending, then by `id` ascending

#### Scenario: Oversized response
- **WHEN** a response body exceeds the size limit or is malformed JSON
- **THEN** the build fails with `CloudBaseResponseInvalid`

### Requirement: Field projection never publishes system or personal fields
Field projection SHALL default to `whitelist` with an empty allowed list. The columns `user_id`,
`owner`, `created_by`, `updated_by` and `_openid` SHALL be removed in every `fieldPolicy` mode,
including `all`; configuration MAY add columns to this list and SHALL NOT remove any. The `idField`
value SHALL be used only as an internal source identifier; it SHALL NOT be projected in `all` mode
and SHALL appear in public fields only when listed explicitly in `allowed`, in which case `doctor`
SHALL warn. Timestamp fields SHALL accept ISO-8601 strings and epoch numbers.

#### Scenario: All mode still strips personal fields
- **WHEN** `fieldPolicy: { mode: all }` is configured and a row carries `user_id` and `created_by`
- **THEN** neither field appears in any public output

#### Scenario: Row id not projected by default
- **WHEN** `fieldPolicy: { mode: all }` is configured
- **THEN** the `id` column is not a public field and the document identity is still stable across builds

### Requirement: Markdown bodies are rendered with untrusted-link policy
CloudBase Markdown bodies SHALL be rendered in untrusted-link mode. Links, autolinks, reference
definitions and images whose parsed URL uses the `javascript:`, `vbscript:` or `data:` scheme, in
any letter case or HTML-entity-encoded form, or that start with `//`, SHALL be dropped or rendered
as plain text. `#fragment` links and scheme-less relative links SHALL be kept, and `http`, `https`,
`mailto` and `tel` links SHALL be kept. A scheme SHALL be detected by RFC 3986 syntax before the
first `/`, `?` or `#`. Rendering with the option disabled SHALL be byte-identical to rendering
before this change.

#### Scenario: Encoded script link
- **WHEN** a body contains `[x](jav&#x61;script:alert(1))`, `<javascript:alert(1)>` or `[r]: JAVASCRIPT:alert(1)`
- **THEN** the rendered HTML contains no executable `javascript:` URL

#### Scenario: Relative and fragment links kept
- **WHEN** a body links to `#section`, `other-post/`, `../img.png` and `/abs`
- **THEN** all four links are kept unchanged

#### Scenario: Protocol-relative link dropped
- **WHEN** a body links to `//evil.example/x`
- **THEN** the link is dropped or rendered as plain text

### Requirement: CloudBase file references are resolved only where they are localized
File references SHALL be resolved only in `fileFields` and in body image positions (inline images
and image reference definitions). A `fileFields` value SHALL be an object name in the configured
`storageBucket` or a `cloud://<envId>.<bucket>/<path>` compat reference; a body image position SHALL
be resolved only when it holds a `cloud://` compat reference. Object names SHALL be non-empty, SHALL
NOT start with `/`, and SHALL NOT contain `//` or a `..` segment; a compat reference whose
environment or bucket does not match the configuration SHALL NOT be resolved. Resolution SHALL use
the PG storage batch-sign interface with at most 500 paths per request and an expiry of
`signExpiresSeconds`. The signed link SHALL be the returned `fullSignedURL` when present, otherwise
the environment's gateway origin followed by the returned relative `signedURL`; either way it SHALL be
used only when it is `https`, its host equals the environment's gateway host and its path starts with
`/v1/storages/object/sign/<storageBucket>/`. The CDN form returned as `fullSignedURLForCDN` SHALL NOT
be used, so that only one host has to be validated. Body https links already written in the gateway
or storage CDN signed form SHALL go through existing media localization. Downloading a signed link
SHALL NOT send the credential. Signed links SHALL be downloaded locally within
the same build. A `cloud://` reference in any other position SHALL NOT be resolved: a non-image body
link SHALL be rendered as plain text, and a public field outside `fileFields` whose value contains
`cloud://` SHALL be dropped, both with the warning `CloudBaseUnsupportedFileReference`. A failed
resolution (a per-item error in a batch response, a failed sign request, a signed link with the
wrong scheme, host or path, a compat reference whose environment or bucket does not match, or an
invalid object name) SHALL report `CloudBaseFileResolveFailed`, identify the failing entry by a hash of the row id and
the field name, and
replace the field value or body image URL with the configured media placeholder
(`content.media.defaultImageUrl`); the object name or `cloud://` reference SHALL NOT be kept. Published output SHALL NOT contain
`cloud://`, the gateway host, the storage CDN host or a signed link.

#### Scenario: Cover image localized
- **WHEN** a row's `cover` file field holds the object name `posts/a.png` that signs and downloads
- **THEN** the page references the local media file and no published file contains `cloud://`, the gateway host or the signed link

#### Scenario: Batch of more than 500 references
- **WHEN** a build collects 501 distinct file references
- **THEN** they are signed in two batch requests

#### Scenario: Signed URL on an unexpected host
- **WHEN** a batch-sign response returns a signed link whose host is not the environment's gateway host, or whose path is outside the configured bucket
- **THEN** that entry is not downloaded and `CloudBaseFileResolveFailed` is reported

#### Scenario: Relative signed link
- **WHEN** a batch-sign entry has no `fullSignedURL` and a relative `signedURL` of `/v1/storages/object/sign/media/posts/a.png?token=…`
- **THEN** the image is downloaded from the environment's gateway host at that path without an `Authorization` header

#### Scenario: CDN form ignored
- **WHEN** a batch-sign entry carries both `fullSignedURL` and `fullSignedURLForCDN`
- **THEN** the image is downloaded from the `fullSignedURL` gateway link and the CDN link is not used

#### Scenario: Non-image body link degraded
- **WHEN** a body contains `[pdf](cloud://env.bucket/a.pdf)`
- **THEN** the link is rendered as plain text and `CloudBaseUnsupportedFileReference` is reported

#### Scenario: Partial resolution failure
- **WHEN** one path in a batch comes back with an error
- **THEN** `CloudBaseFileResolveFailed` is reported for that entry and its image falls back to the placeholder

#### Scenario: Unresolvable references use the placeholder
- **WHEN** a `cover` file field holds the object name `posts/a.png` and a body image holds `cloud://other-env.media/a.png`, and neither can be resolved
- **THEN** both reference the media placeholder, and no published file contains `posts/a.png`, `cloud://` or the environment's gateway host

### Requirement: Public output privacy detects CloudBase identifiers
The build-time public output privacy check SHALL fail when public output contains a CloudBase
identifier: the gateway host `{envId}.api.tcloudbasegateway.com` or
`{envId}.api.intl.tcloudbasegateway.com`, the storage CDN host `{envId}.storage.tcloudbase.com` (hosts matched at a host-label boundary,
so `xprod-1.api.tcloudbasegateway.com` is not a match for envId `prod-1`), a `cloud://` compat reference, a URL on the PG storage signed-download path carrying a
`token` parameter, or any other `*.storage.tcloudbase.com` URL. It SHALL NOT fail for a bare
envId in ordinary text or in an output file path. The deployment privacy validator reads no
configuration and applies to every site, so it SHALL detect only (a) signed links anywhere in
the text of any scanned text file: a gateway-host URL on the signed path with a `token`
parameter, or a storage CDN host URL with a `token` parameter, in any `//` URL form (`https`, `http`, protocol-relative or another scheme); and (b) `cloud://` in URL
positions. URL positions are quoted or unquoted URL attribute values of any tag in HTML-like
files (`src`, `href`, each `srcset` candidate, `poster`, `data-src`, `action`, `formaction`,
`cite`, `data`, `xlink:href`, `url`, and `content` on `<meta>`) and CSS `url(...)` arguments in
`style` attributes, CSS `url(...)` arguments in `.css` files, and whole string values in JSON
files, each trimmed (CSS arguments also unquoted) and starting with `cloud://`
case-insensitively. Text nodes, code blocks and other text files SHALL NOT be checked for
`cloud://` at deploy time; the build-time check keeps full-text detection for sites with a
`cloudbase` source. A gateway or CDN host URL without `token` SHALL NOT fail deployment, so sites
without a CloudBase source keep their behaviour. When the deploy command passes CloudBase
envIds, it SHALL pass every configured source envId and match them only inside the gateway host,
storage CDN host or `cloud://` reference; a bare envId substring SHALL NOT fail. A JSON file
that contains `cloud://` but cannot be parsed SHALL be treated as a leak at deploy time.

#### Scenario: envId leaks into a page
- **WHEN** a public HTML page contains `https://env-abc.api.tcloudbasegateway.com/...` or `cloud://env-abc.media/a.png`
- **THEN** the build privacy check fails

#### Scenario: bare envId in prose or a path
- **WHEN** a site whose envId is `blog` publishes ordinary prose or a path containing `blog`
- **THEN** the build privacy check passes

#### Scenario: Signed link in deploy staging
- **WHEN** a staged deployment file contains a CloudBase signed link
- **THEN** deployment fails before any Git write without reading site configuration

#### Scenario: CDN signed link in deploy staging
- **WHEN** a staged deployment file contains `https://env-abc.storage.tcloudbase.com/media/a.png?token=…`
- **THEN** deployment fails before any Git write without reading site configuration

#### Scenario: CDN host in a CloudBase build
- **WHEN** a site with a `cloudbase` source writes any `*.storage.tcloudbase.com` URL into public output
- **THEN** the build privacy check fails

#### Scenario: Unsigned CDN link on a site without CloudBase
- **WHEN** a site without a `cloudbase` source links to `https://x.storage.tcloudbase.com/b/a.pdf` without a `token` parameter
- **THEN** deployment privacy validation passes, as before this change

#### Scenario: cloud:// image in deploy staging
- **WHEN** a staged HTML file contains `<img src="cloud://env-abc.media/a.png">` or `<img src=cloud://env-abc.media/a.png>`
- **THEN** deployment fails with `BKT-DEPLOY-PRIVACY-0001` before any Git write

#### Scenario: Signed link in a code block
- **WHEN** a staged page shows `https://env-abc.storage.tcloudbase.com/media/a.png?token=…` inside a `<code>` block
- **THEN** deployment fails with `BKT-DEPLOY-PRIVACY-0001`, because signed links are detected anywhere in text

#### Scenario: Tutorial mentions cloud:// in a code block
- **WHEN** a site without a `cloudbase` source publishes a page whose `<code>` block shows `cloud://env.bucket/a.png`
- **THEN** deployment privacy validation passes

#### Scenario: Every CloudBase envId is checked at deploy
- **WHEN** a site has two CloudBase sources and the second envId appears in a staged file's gateway host
- **THEN** deployment fails

#### Scenario: Malformed JSON with cloud://
- **WHEN** a staged `.json` file contains `cloud://` but is not valid JSON
- **THEN** deployment fails with `BKT-DEPLOY-PRIVACY-0001`

### Requirement: Credentials, signatures and signed links never reach logs
Logs, diagnostics and exception messages SHALL NOT contain the credential (even though the
publishable key is not a secret), request headers, URL query strings, signed links or tokens.
Internal diagnostics SHALL log only a hash of the row id, the table name and the HTTP status code.
Before sending a credential the transport SHALL assert that the scheme is `https` and the host is
exactly `{envId}.api.tcloudbasegateway.com` or `{envId}.api.intl.tcloudbasegateway.com`.

#### Scenario: Authentication failure message
- **WHEN** the remote returns 401 for a request carrying the credential
- **THEN** the build fails with `CloudBaseAuthFailed` and neither the credential nor the request URL query appears in any log or exception text

#### Scenario: Host assertion
- **WHEN** a gateway URL would resolve to any host other than the expected gateway host
- **THEN** no request is sent and no credential is attached

### Requirement: Doctor reports CloudBase readiness
`doctor` SHALL report connectivity, permission, whether the table or view is reachable, and whether
mapped fields exist in a sample row, using the CloudBase diagnostic codes. It SHALL decode the
credential's JWT `role` claim without verifying it, as a diagnostic only, and SHALL warn with
`CloudBaseAdminCredentialInUse` when the role is `service_role` or when the role is missing or
cannot be parsed; `build` SHALL continue in both cases. When the table is reachable but returns no
rows, `doctor` SHALL print a hint that a row-level security policy may be filtering rows, without a
new diagnostic code. When `fileFields` is non-empty and a sample row holds a file reference,
`doctor` SHALL sign that one object to check bucket access. Its output SHALL NOT contain the
credential.

#### Scenario: Service role credential warning
- **WHEN** `doctor` runs with a credential whose `role` claim is `service_role`
- **THEN** it reports `CloudBaseAdminCredentialInUse` as a warning

#### Scenario: Missing table
- **WHEN** the configured table does not exist
- **THEN** `doctor` reports `CloudBaseNotFound`

#### Scenario: Silent row-level security filter
- **WHEN** the table is reachable with the publishable key but returns an empty result
- **THEN** `doctor` prints a hint about row-level security and reports no error

### Requirement: Delivery passes the scoped gates and syncs documentation
The change SHALL pass every specialty test emitted by the verification closure, `dotnet-format.sh`,
`code-analysis-ratchet.sh check` without raising CA1502 or CA1506 counts, and
`public-api-drift.sh check Debug` reporting only new `DiagnosticCode` members and the empty
governed `Bukit.CloudBase` assembly. `Bukit.CloudBase` line coverage SHALL be at least 70%. User,
developer, skill and troubleshooting documentation (including an SQL setup example for the
read-only grant, the row-level security policy and the storage policy), the BukitJalil consumer
contract table in `guide/dev/cli.md`, the three CHANGELOGs and these OpenSpec specs SHALL be
updated. A site without a CloudBase source SHALL produce byte-identical output.

#### Scenario: Site without CloudBase
- **WHEN** an existing site without a `cloudbase` source is built before and after this change
- **THEN** the outputs are byte-identical

#### Scenario: Public API drift
- **WHEN** `public-api-drift.sh check Debug` runs against the updated baseline
- **THEN** it reports no change beyond the new `DiagnosticCode` members and the empty `Bukit.CloudBase` entry
