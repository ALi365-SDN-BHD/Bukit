## ADDED Requirements

### Requirement: CloudBase temporary links are rejected in mini-program data
A written image- or URL-typed field, or a body image, link or embed, that still carries a CloudBase
temporary link or a `cloud://` id SHALL fail the build with `SchemaValidationFailed` naming the
dataset, record id and field, in the same way as the existing expiring-signature checks. Localized
CloudBase images SHALL be written as absolute URLs under the site URL.

#### Scenario: Unlocalized CloudBase temporary link
- **WHEN** a `records` dataset backed by a `cloudbase` source has an `image` field whose value is still a CloudBase temporary link
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field

#### Scenario: Localized CloudBase cover
- **WHEN** a CloudBase `cover` file field was downloaded to `/assets/media/…`
- **THEN** the list and detail files contain `https://<site host>/assets/media/…`
