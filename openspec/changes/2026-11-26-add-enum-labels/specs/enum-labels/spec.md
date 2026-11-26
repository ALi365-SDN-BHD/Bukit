## ADDED Requirements

### Requirement: Enum items can carry labels
An `enum` item in `content.modelSchema` SHALL be a value or `{ value, label }`. Validation SHALL use
the values. Templates SHALL read every enum field's options in definition order as
`site.field_enums.<field>` (scoped fields under `site.field_enums.scopes.<collection>.<field>`),
each with `value` and `label`, where `label` defaults to the value.

#### Scenario: Labeled audience
- **WHEN** `audience` has `enum: [{ value: family, label: Family trips }, couple]`
- **THEN** `site.field_enums.audience` is `[{ value: family, label: Family trips }, { value: couple, label: couple }]`

#### Scenario: Duplicate value
- **WHEN** an `enum` lists `family` twice
- **THEN** the config fails to load
