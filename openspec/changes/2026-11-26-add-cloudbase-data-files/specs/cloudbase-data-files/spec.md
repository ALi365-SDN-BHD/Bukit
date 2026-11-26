## ADDED Requirements

### Requirement: Data-mode CloudBase sources resolve file fields
A CloudBase source with `mode: data` SHALL accept `fileFields` under the same preconditions as
content mode. Each file field value SHALL be signed and replaced with the signed link, or with the
media placeholder when it cannot be resolved, before media localization. Other fields containing
`cloud://` SHALL still be dropped with `BKT-0C0B`.

#### Scenario: Gallery photo
- **WHEN** a `mode: data` row has `photo: cloud://env.media/gallery/p1.jpg` and `photo` is a file field
- **THEN** the data document's `photo` is the signed link, which media localization then downloads

#### Scenario: Missing preconditions
- **WHEN** a `mode: data` source sets `fileFields` without the media preconditions
- **THEN** config loading fails with `BKT-0C01`
