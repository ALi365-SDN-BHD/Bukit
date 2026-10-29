## ADDED Requirements

### Requirement: Reports must not contain plaintext cookie or authorization values

Plugin execution reports MUST NOT contain plaintext values for environment keys containing `COOKIE` or `AUTHORIZATION`. The masker SHALL write `***` for those keys, case-insensitively, including `HTTP_COOKIE` and `HTTP_AUTHORIZATION`. Existing masking for `NOTION_TOKEN`, `API_KEY`, `PASSWORD`, `TOKEN` and `SECRET` SHALL remain unchanged, and non-secret keys SHALL remain visible.

#### Scenario: Granted cookie or authorization environment value

- **WHEN** a plugin is granted `environment.read: ["COOKIE"]` or `["AUTHORIZATION"]` and an execution report includes the environment
- **THEN** the report SHALL contain `***` for that value
- **AND** the report MUST NOT contain the original value

#### Scenario: Existing masking and non-secret visibility

- **WHEN** the environment contains `NOTION_TOKEN` and `PUBLIC_VALUE`
- **THEN** `NOTION_TOKEN` SHALL be `***`
- **AND** `PUBLIC_VALUE` SHALL remain unchanged
