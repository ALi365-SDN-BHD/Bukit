## ADDED Requirements

### Requirement: The CloudBase filter value can read environment variables
`cloudbase.filter.value` SHALL replace each `${env:NAME}` reference with the trimmed value of
environment variable NAME when the build creates the content source. An unset or empty variable
SHALL fail the build with `ConfigRequiredFieldMissing`; a `${` that does not start a valid
reference SHALL fail validation.

#### Scenario: Build one release
- **WHEN** the filter is `{ field: release_id, value: "${env:RELEASE_ID}" }` and `RELEASE_ID=r42`
- **THEN** the build queries rows whose `release_id` is `r42`

#### Scenario: Variable not set
- **WHEN** `RELEASE_ID` is not set
- **THEN** the build fails naming `RELEASE_ID` and does not query the table
