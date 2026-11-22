## ADDED Requirements

### Requirement: CloudBase sources provide no incremental fingerprint in P1
A `cloudbase` content source SHALL NOT declare a `bodyFingerprint` or any other source-specific
incremental fingerprint in P1. Pages from a CloudBase source SHALL be invalidated only by the
existing metadata, content, route, template and render dependency hashes. Because CloudBase PG
storage signed links are re-signed on every build, a CloudBase page whose body references a
resolved file MAY be re-rendered on every incremental build; this SHALL NOT be reported as an
error. Signed-link reporting for shared render data SHALL include CloudBase signed links (gateway
host, `/v1/storages/object/sign/` path and `token` parameter, or the storage CDN host
`*.storage.tcloudbase.com` with `token`) as a recognized signing scheme.
Incremental behaviour of sites without a CloudBase source MUST NOT change.

#### Scenario: Re-signed body image
- **WHEN** a CloudBase page body image resolves to a newly signed link on the next incremental build
- **THEN** the page is re-rendered under the existing render reasons and no error is reported

#### Scenario: Site without CloudBase
- **WHEN** a site without a `cloudbase` source runs the same incremental build twice with unchanged inputs
- **THEN** every page is skipped as `unchanged`, as before this change
