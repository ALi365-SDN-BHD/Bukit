## MODIFIED Requirements

### Requirement: Media and links are absolute and stable
Image- and URL-typed fields and `src` and `href` attributes in bodies SHALL be absolute http(s)
URLs under the site URL or the original external host. When a written image- or URL-typed field, or a body image, link or embed,
still carries an expiring signature (`X-Amz-Signature`, `X-Amz-Credential`, `Signature` or
`Expires` query parameter), is a CloudBase PG storage signed link (a host matching
`*.api.tcloudbasegateway.com` or `*.api.intl.tcloudbasegateway.com` with a path that starts with
`/v1/storages/object/sign/`), or is a CloudBase `cloud://` reference, the build SHALL fail with
`SchemaValidationFailed` naming the dataset, record id and field. Links to pages that have
mini-program detail records SHALL carry `data-bukit-id` with the target id.

#### Scenario: Localized Notion cover
- **WHEN** an article's `cover` is a Notion file that image localization downloaded to `/assets/media/…`
- **THEN** the list and detail files contain `https://<site host>/assets/media/…`

#### Scenario: Unlocalized signed URL
- **WHEN** a `records` dataset has an `image` field whose value is still an `*.amazonaws.com` signed URL
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field

#### Scenario: Unlocalized signed file link
- **WHEN** a `module` dataset has a `url`-typed field whose value is a signed Notion PDF URL
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field

#### Scenario: Internal link
- **WHEN** an article body links to another article in the same `collection` dataset
- **THEN** the link has an absolute `href` and a `data-bukit-id` equal to the target article's id

#### Scenario: Unlocalized CloudBase signed link
- **WHEN** a `records` dataset backed by a `cloudbase` source has an `image` field whose value is still `https://env-abc.api.tcloudbasegateway.com/v1/storages/object/sign/media/a.png?token=…`
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field

#### Scenario: CloudBase compat reference in a body
- **WHEN** a detail `body` still contains an image or link whose URL is `cloud://env-abc.media/a.png`
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field
