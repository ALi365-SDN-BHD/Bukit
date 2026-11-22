## Purpose

Lets a site read one Tencent CloudBase data model as a read-only content source with
`content.sources[].type: cloudbase`, map its records to Bukit content documents, render Markdown
bodies safely, and publish CloudBase-hosted images only after local download, without leaking
CloudBase identifiers, credentials or temporary links.

## ADDED Requirements

### Requirement: CloudBase source configuration is validated before any request
`config check`, `doctor` and `build` SHALL accept a `content.sources[]` entry with `type: cloudbase`
and a `cloudbase` section. Validation SHALL fail with `CloudBaseConfigInvalid` naming the offending
path when a required field is missing, a value is out of range, an unknown field is present,
`kind` is not `model`, `cacheMode` is not `off`, `bodyFormat` is not `markdown`, `region` is not
`cn` or `intl`, `filter` contains a key other than `field` and `value`, or `envId` does not match
`^[a-z0-9][a-z0-9-]{0,62}$`. A credential field written in YAML SHALL fail validation; credentials
SHALL come only from the environment or `--secrets-stdin`. The configuration types SHALL be
internal and SHALL NOT add public API.

#### Scenario: Valid source
- **WHEN** a site declares a `cloudbase` source with `envId: prod-abc`, `kind: model`, `model: article` and `bodyField: content`
- **THEN** `config check` passes and the source uses region `cn`

#### Scenario: Unsafe envId
- **WHEN** `envId` contains `/`, `@`, `#`, `?`, `.` or an uppercase letter, or is longer than 63 characters
- **THEN** validation fails with `CloudBaseConfigInvalid` naming `cloudbase.envId` and no request is sent

#### Scenario: Credential in YAML
- **WHEN** the `cloudbase` section contains an API key field
- **THEN** validation fails with `CloudBaseConfigInvalid`

#### Scenario: Unsupported options
- **WHEN** `cacheMode: readonly`, `bodyFormat: html` or `filter: { field: status, op: ne, value: x }` is configured
- **THEN** validation fails with `CloudBaseConfigInvalid` naming that path

### Requirement: File fields require guaranteed local media download
When `fileFields` is non-empty, configuration validation SHALL fail with `CloudBaseConfigInvalid`
unless `content.media.downloadToLocal` is not `false`, every file field is listed in
`content.media.fieldKeys`, `content.media.onFailure` is `placeholder`, and every file field is an
image field. When the placeholder image is missing, `build` SHALL fail with
`CloudBaseConfigInvalid`, and `config check` and `doctor` SHALL report a warning. When
`content.media.downloadHosts` or `skipHosts` would exclude the CloudBase temporary-link host,
validation SHALL fail with `CloudBaseMediaLocalizationRequired`.

#### Scenario: Keep on failure
- **WHEN** `fileFields: [cover]` is configured with `content.media.onFailure: keep`
- **THEN** validation fails with `CloudBaseConfigInvalid`

#### Scenario: Field not localized
- **WHEN** `fileFields: [cover]` is configured and `cover` is not in `content.media.fieldKeys`
- **THEN** validation fails with `CloudBaseConfigInvalid`

#### Scenario: Missing placeholder
- **WHEN** `fileFields` is non-empty and the media placeholder file does not exist
- **THEN** `build` fails with `CloudBaseConfigInvalid` and `config check` reports a warning

### Requirement: Records are read completely, filtered, sorted and bounded
The source SHALL read every page of matching records until the remote result is exhausted, SHALL
stop at `maxItems` records when it is set, and SHALL send the configured single-field equality
filter and single-field sort with every query. Responses that exceed the size limit or do not
match the expected structure SHALL fail with `CloudBaseResponseInvalid`. Automated tests SHALL use
a fake HTTP handler and SHALL NOT contact the network.

#### Scenario: Multi-page read
- **WHEN** the fake remote holds 250 matching records and the page size is 100
- **THEN** the build produces 250 documents from three requests

#### Scenario: maxItems truncation
- **WHEN** `maxItems: 120` is configured and 250 records match
- **THEN** exactly 120 documents are produced

#### Scenario: Filter and sort sent
- **WHEN** `filter: { field: status, value: published }` and `sort: { field: publishAt, direction: desc }` are configured
- **THEN** every query request carries that equality filter and that sort

#### Scenario: Oversized response
- **WHEN** a response body exceeds the size limit or is malformed JSON
- **THEN** the build fails with `CloudBaseResponseInvalid`

### Requirement: Field projection never publishes system or personal fields
Field projection SHALL default to `whitelist` with an empty allowed list. System and personal
fields (including the record creator's open id, owner, creator and updater) SHALL be removed in
every `fieldPolicy` mode, including `all`. The record id SHALL be used only as an internal source
identifier; it SHALL NOT be projected in `all` mode and SHALL appear in public fields only when
listed explicitly in `allowed`, in which case `doctor` SHALL warn.

#### Scenario: All mode still strips personal fields
- **WHEN** `fieldPolicy: { mode: all }` is configured and a record carries `_openid` and `createBy`
- **THEN** neither field appears in any public output

#### Scenario: Record id not projected by default
- **WHEN** `fieldPolicy: { mode: all }` is configured
- **THEN** the record id is not a public field and the document identity is still stable across builds

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

### Requirement: CloudBase file ids are resolved only where they are localized
`cloud://` file ids in `fileFields` and in body image positions (inline images and image reference
definitions) SHALL be resolved in batches to temporary links and downloaded locally within the same
build. A `cloud://` id in any other position SHALL NOT be resolved: a non-image body link SHALL be
rendered as plain text, and a public field outside `fileFields` whose value contains `cloud://`
SHALL be dropped, both with the warning `CloudBaseUnsupportedFileReference`. A failed resolution
SHALL report `CloudBaseFileResolveFailed` and identify the failing entry. Published output SHALL
NOT contain `cloud://`, the envId, or a temporary-link host or signature.

#### Scenario: Cover image localized
- **WHEN** a record's `cover` file field holds a `cloud://` id that resolves and downloads
- **THEN** the page references the local media file and no published file contains `cloud://` or the temporary link

#### Scenario: Non-image body link degraded
- **WHEN** a body contains `[pdf](cloud://env.bucket/a.pdf)`
- **THEN** the link is rendered as plain text and `CloudBaseUnsupportedFileReference` is reported

#### Scenario: Partial resolution failure
- **WHEN** one id in a batch fails to resolve
- **THEN** `CloudBaseFileResolveFailed` is reported for that entry and its image falls back to the placeholder

### Requirement: Public output privacy detects CloudBase identifiers
The build-time public output privacy check SHALL fail when public output contains the configured
envId, a `cloud://` id, or a CloudBase temporary-link host or signature parameter. The deployment
privacy validator SHALL detect `cloud://` ids and temporary-link hosts or signature parameters
without configuration, and SHALL detect the envId when the deploy command passes it.

#### Scenario: envId leaks into a page
- **WHEN** a public HTML page contains the configured envId
- **THEN** the build privacy check fails

#### Scenario: Temporary link in deploy staging
- **WHEN** a staged deployment file contains a CloudBase temporary link
- **THEN** deployment fails before any Git write without reading site configuration

### Requirement: Credentials, signatures and temporary links never reach logs
Logs, diagnostics and exception messages SHALL NOT contain the credential, request headers, URL
query strings, temporary links or signatures. Internal diagnostics SHALL log only a hash of the
record id, the model name and the HTTP status code. Before sending a credential the transport
SHALL assert that the scheme is `https` and the host is exactly `{envId}.api.tcloudbasegateway.com`
or `{envId}.api.intl.tcloudbasegateway.com`.

#### Scenario: Authentication failure message
- **WHEN** the remote returns 401 for a request carrying the credential
- **THEN** the build fails with `CloudBaseAuthFailed` and neither the credential nor the request URL query appears in any log or exception text

#### Scenario: Host assertion
- **WHEN** a gateway URL would resolve to any host other than the expected gateway host
- **THEN** no request is sent and no credential is attached

### Requirement: Doctor reports CloudBase readiness
`doctor` SHALL report connectivity, permission, model existence and whether mapped fields exist in
a sample record, using the CloudBase diagnostic codes. It SHALL warn with
`CloudBaseAdminCredentialInUse` when an administrator-level credential is in use, and its output
SHALL NOT contain the credential.

#### Scenario: Admin credential warning
- **WHEN** `doctor` runs with the API key credential configured
- **THEN** it reports `CloudBaseAdminCredentialInUse` as a warning

#### Scenario: Missing model
- **WHEN** the configured model does not exist
- **THEN** `doctor` reports `CloudBaseNotFound`

### Requirement: Delivery passes the scoped gates and syncs documentation
The change SHALL pass every specialty test emitted by the verification closure, `dotnet-format.sh`,
`code-analysis-ratchet.sh check` without raising CA1502 or CA1506 counts, and
`public-api-drift.sh check Debug` reporting only new `DiagnosticCode` members and the empty
governed `Bukit.CloudBase` assembly. `Bukit.CloudBase` line coverage SHALL be at least 70%. User,
developer, skill and troubleshooting documentation, the BukitJalil consumer contract table in
`guide/dev/cli.md`, the three CHANGELOGs and these OpenSpec specs SHALL be updated. A site without a
CloudBase source SHALL produce byte-identical output.

#### Scenario: Site without CloudBase
- **WHEN** an existing site without a `cloudbase` source is built before and after this change
- **THEN** the outputs are byte-identical

#### Scenario: Public API drift
- **WHEN** `public-api-drift.sh check Debug` runs against the updated baseline
- **THEN** it reports no change beyond the new `DiagnosticCode` members and the empty `Bukit.CloudBase` entry
