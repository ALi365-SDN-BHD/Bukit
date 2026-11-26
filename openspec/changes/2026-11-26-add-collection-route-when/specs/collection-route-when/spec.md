## ADDED Requirements

### Requirement: routeWhen limits which entries get pages
When a collection declares `routeWhen: { field, eq }`, only its content entries whose top-level field
equals `eq` SHALL get a page. Text SHALL compare ignoring case and surrounding spaces; boolean values
SHALL compare as `true` or `false`; whole numbers SHALL compare as their digits. An entry without the
field SHALL NOT get a page. Without `routeWhen`, every content entry SHALL get a page.

#### Scenario: Hidden city
- **WHEN** `city` declares `routeWhen: { field: has_page, eq: true }` and `ipoh` has `has_page: false`
- **THEN** `ipoh` has no page and is not in the city list

### Requirement: Entries without a page stay link targets
An entry without a page SHALL be available to `content.links` as a target with a null `url`. It SHALL
NOT resolve its own links, SHALL NOT be validated as a linking page and SHALL NOT get backrefs.

#### Scenario: Link to a hidden city
- **WHEN** a route names `kl` (has a page) and `ipoh` (hidden)
- **THEN** its `cities_links` lists both, and the `ipoh` link has a null `url`

### Requirement: routeWhen config validation
`routeWhen` SHALL require `field` (a valid field name) and `eq`, and SHALL reject other keys.

#### Scenario: Missing value
- **WHEN** `routeWhen` has only `field`
- **THEN** loading fails with `site.collections.<name>.routeWhen.eq is required.`
