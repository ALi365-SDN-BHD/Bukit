## ADDED Requirements

### Requirement: Backrefs list the pages that link to a target
When a `content.links` entry has `backref.name`, every routed page of the target collection SHALL get
a field `<name>` of type `list` and a field `<name>_count` of type `number`. `<name>` SHALL hold one
ordinal, key-sorted map per content page whose link resolved to that target in the same language,
with `key` (the linking page slug), `title`, `slug`, `url`, `collection` and each `backref.select`
field of the linking page. Each linking page SHALL appear once. Entries SHALL follow the collection
list order: publish time descending, then `content.ordering`, then URL and id. When `limit` is
greater than 0, only the first `limit` entries SHALL be kept. `<name>_count` SHALL hold the number of
linking pages before the limit.

#### Scenario: Route listed on a city
- **WHEN** routes `r1` (older) and `r2` (newer) both name city `kl`, and `r2` names it twice
- **THEN** the `kl` page has `routes` with `r2` then `r1`, and `routes_count` is 2

#### Scenario: Limit
- **WHEN** the backref has `limit: 1` and two routes name `kl`
- **THEN** `routes` holds only the newer route and `routes_count` is 2

#### Scenario: No linking pages
- **WHEN** no page names city `melaka`
- **THEN** the `melaka` page has an empty `routes` list and `routes_count` 0

### Requirement: Backref scope
Only routed target pages SHALL get backrefs; data documents SHALL NOT. When a key names more than one
target, only the first target SHALL list the linking page. Backref entries SHALL NOT include other
backrefs.

#### Scenario: Repeated key
- **WHEN** two city pages share slug `kl`
- **THEN** only the first one lists the routes that name `kl`

### Requirement: Backref conflicts
Config validation SHALL reject a backref whose name is not a valid field name, whose limit is
negative, whose `select` names a reserved or repeated field, or whose `<name>` or `<name>_count`
another link or backref already writes in the target collection. At build time a target page that
already has `<name>` or `<name>_count` SHALL keep its fields, and the conflict SHALL be reported as
`content.link_output_conflict` on that target page under its collection's `schemaFailMode`.

#### Scenario: Existing field
- **WHEN** a city page already has a `routes` field
- **THEN** the field is unchanged and the city page reports `content.link_output_conflict`

### Requirement: Backref changes rebuild target pages
Backrefs SHALL be part of the target page fields, so its incremental metadata hash SHALL change when a
linking page is added, removed or changes its title, URL or selected fields.

#### Scenario: Linking page title changes
- **WHEN** a route that names `kl` changes its title
- **THEN** the `kl` page's metadata hash changes
