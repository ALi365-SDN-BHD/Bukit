## ADDED Requirements

### Requirement: dataIndex keys allow dotted camelCase
A `dataIndex` row key SHALL match `^[A-Za-z0-9][A-Za-z0-9._+-]*$` and SHALL be stored as written.
Scopes, source names and field names SHALL keep `^[a-z][a-z0-9_]*$`.

#### Scenario: Copy key with a range
- **WHEN** a row has scope `copy` and key `custom.days.4-5.label`
- **THEN** templates read it as `site.data_index.<source>.copy["custom.days.4-5.label"]`

#### Scenario: Required dotted key
- **WHEN** `requiredKeys` lists `copy.custom.daysHint`
- **THEN** the scope is `copy` and the key is `custom.daysHint`
