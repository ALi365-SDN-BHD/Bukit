## ADDED Requirements

### Requirement: Content links resolve page keys into documents
When `content.links` names a page collection, a key field and a target collection, each content page
in that collection SHALL get a field named after the key field with a `_links` suffix. The field
SHALL be of type `list` and SHALL hold one ordinal, key-sorted map per resolved key, in key order.
Each map SHALL hold `key`, `title`, `slug`, `url` and `collection`, plus each `select`ed top-level
target field (null when the target lacks it). `url` SHALL be the target's route URL, or null for a
data document. Targets SHALL match on `slug`, or on the configured `target.key` field,
case-insensitively. A page SHALL link only to target documents built for its language. The key field
itself SHALL NOT change.

#### Scenario: Top-level keys
- **WHEN** a `route` page has `cities: [penang, kl]` and a link names `field: cities` with target collection `city`
- **THEN** the page has `cities_links` with the Penang and Kuala Lumpur documents in that order, and `cities` is unchanged

#### Scenario: Data target
- **WHEN** the target document is a data document
- **THEN** its link has a null `url`

### Requirement: Nested link paths
A link field that is a nested path (`a[].b`, where `name[]` walks every object of a list and `name`
enters an object, at most 8 names) SHALL write `<last name>_links` into each object that holds the
key field. It SHALL also write one top-level field, named from the path names joined by `_` with a
`_links` suffix, that lists the first occurrence of each target. The source document's structured
values SHALL NOT be modified.

#### Scenario: Itinerary days
- **WHEN** a link names `days[].attractions` and two days share attraction `a2`
- **THEN** each day has `attractions_links`, and `days_attractions_links` lists `a2` once

### Requirement: Link problems follow schemaFailMode
Each of the following SHALL be reported as a content validation issue under the page collection's
`schemaFailMode`:
Each problem SHALL be reported on the page that names the key, with the page's language as the scope
of the check.
- A key that no target document of the page's language has, as `content.link_target_unresolved`. The
  link SHALL be left out.
- A key held by more than one target document of the page's language, as
  `content.link_target_duplicate`. The first document SHALL win.
- A key that is not text, or a path that does not match the field's shape, as
  `content.link_value_invalid`.
- An output field that already exists, as `content.link_output_conflict`. The existing field SHALL
  NOT be overwritten.

#### Scenario: Key only in another language
- **WHEN** an `ms` page names a key that only an `en` document has
- **THEN** the `ms` page reports `content.link_target_unresolved`

#### Scenario: Strict collection with an unknown key
- **WHEN** a page in a collection with `schemaFailMode: strict` names a key no target has
- **THEN** content validation fails the build with `SchemaStrictModeBlocked`

### Requirement: Link changes rebuild linking pages
Links SHALL be part of the page fields, so the page's incremental metadata hash SHALL change when a
target's title, URL or selected fields change. It SHALL stay the same when nothing changes.

#### Scenario: Target title changes
- **WHEN** a linked city's title changes between builds
- **THEN** the linking route page's metadata hash changes
