## ADDED Requirements

### Requirement: CloudBase temporary links are a recognized signing scheme
The localizer SHALL recognize CloudBase temporary download links as a signing scheme, detected by
the CloudBase temporary-link host, and SHALL remove only that scheme's signature parameters when
computing the media identity, under the same matching and removal rules as the existing schemes.
The host pattern and the parameter names SHALL be defined in one central place so they can be
corrected after verification on a real environment. URLs on other hosts SHALL keep their identity
unchanged.

#### Scenario: Same CloudBase object re-signed
- **WHEN** two builds reference the same CloudBase object through temporary links that differ only in their signature parameters
- **THEN** both resolve to the same local file

#### Scenario: Same parameter names on another host
- **WHEN** a URL on a non-CloudBase host carries query parameters with the CloudBase signature names
- **THEN** its identity and stored file name are unchanged from before this change

### Requirement: CloudBase file fields keep the localization path closed
For a `cloudbase` source with non-empty `fileFields`, media localization SHALL be the only path by
which a temporary link can reach output: `content.media.downloadToLocal` MUST NOT be `false`,
every file field MUST be listed in `content.media.fieldKeys`, `content.media.onFailure` MUST be
`placeholder` with an existing placeholder image, file fields MUST be image fields, and
`downloadHosts` or `skipHosts` MUST NOT exclude the CloudBase temporary-link host. A failed
download SHALL fall back to the placeholder and SHALL NOT keep the temporary link.

#### Scenario: Download failure
- **WHEN** a CloudBase temporary link for a file field returns HTTP 403 during download
- **THEN** the field references the placeholder image and the temporary link is not written to any output

#### Scenario: Skip rule excludes the temporary-link host
- **WHEN** `content.media.skipHosts` matches the CloudBase temporary-link host and `fileFields` is non-empty
- **THEN** configuration validation fails with `CloudBaseMediaLocalizationRequired`
