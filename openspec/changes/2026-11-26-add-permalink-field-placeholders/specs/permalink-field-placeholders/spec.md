## ADDED Requirements

### Requirement: Permalinks expand field placeholders
A permalink pattern SHALL replace `{fields.<name>}` (case-insensitive) with the slugified value of the
page's top-level field `<name>` when it is text or a whole number. When the field is missing, empty,
another type, or slugifies to an empty string, routing SHALL fail with `RouteInvalidPattern` naming
the pattern, the page and the field.

#### Scenario: Attraction in a city
- **WHEN** the pattern is `/malaysia/{fields.city}/attractions/{slug}/` and `city` is `Kuala Lumpur`
- **THEN** the URL is `/malaysia/kuala-lumpur/attractions/batu-caves/`

#### Scenario: Missing field
- **WHEN** the page has no `city`
- **THEN** routing fails with `RouteInvalidPattern`
