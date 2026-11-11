## ADDED Requirements

### Requirement: Media output validation computes the effective media configuration once

Media output validation MUST compute the effective media configuration once per variant and reuse it for every rewritten URL, without filesystem access per link. Validation results, collected references, fallback handling and exceptions MUST be unchanged.

#### Scenario: Many links validated
- **WHEN** rendered HTML with many non-media links is validated
- **THEN** the effective media configuration is computed once and validation reports the same missing outputs as before
