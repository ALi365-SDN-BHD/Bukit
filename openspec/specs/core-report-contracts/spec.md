# Core report contracts

## Purpose

Keep security, SEO, publish-audit and release-inventory outputs consistent with their strict schemas, including unknown dates, stable release bytes and portable path assertions. This document records the accepted repair; closeout changes neither schema files nor CI and does not claim fresh runtime or Windows-host evidence.

## Requirements

### Requirement: Security reports require the public-output privacy result

The security report schema SHALL require `checks.publicOutputPrivacy` with the
same status/severity contract as other checks and SHALL reject unknown fields.

#### Scenario: Current producer output and sensitive validation
- **WHEN** the security writer emits its privacy check
- **THEN** the report validates, including `not_checked` and `not_applicable`
- **AND** removing the privacy check or adding an unknown check/field fails validation

### Requirement: Audit modification dates retain unknown values

SEO route and publish document `lastModified` SHALL be present and SHALL accept
null for unknown modification dates or a valid date-time string for known dates.

#### Scenario: Generated route without content date
- **WHEN** a route's internal modification value is Unix epoch
- **THEN** both serialized audit reports retain `lastModified: null` and validate

#### Scenario: Known and invalid dates
- **WHEN** `lastModified` contains a known date-time string
- **THEN** both schemas accept it
- **AND** malformed date strings, wrong types and an absent property fail validation

### Requirement: Release checksums cover stable listed content

The release inventory SHALL exclude the root `.bukit-build-state.json` recovery
file, retain nested same-name content and retain its existing report-directory
exclusion. File count, lengths, hashes and bundle aggregate SHALL describe the
actual bytes of every retained file after build completion.

#### Scenario: Recovery state completes after reporting
- **WHEN** the root recovery state changes from started to completed after reports
- **THEN** it is absent from release checksums
- **AND** a nested `.bukit-build-state.json` remains listed with its actual size/hash
- **AND** the count and aggregate match retained content without changing transaction or report ordering

### Requirement: Audit assertions compare portable output paths

Audit existence and publish semantic-hash test assertions SHALL match Windows
backslash output paths to equivalent slash-keyed snapshot files.

#### Scenario: Windows-form audit output on any test host
- **WHEN** SEO and publish reports name `en\\index.html` and the file key is `en/index.html`
- **THEN** both audit existence checks and the publish semantic hash use those actual bytes
- **AND** missing output and changed semantic content still fail
