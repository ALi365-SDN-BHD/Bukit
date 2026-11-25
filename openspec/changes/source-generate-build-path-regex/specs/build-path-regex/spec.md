## ADDED Requirements

### Requirement: Static build-path patterns are source-generated

Every regular expression with a compile-time constant pattern that Bukit Core evaluates per page, per document or per attribute value during a build or publish audit MUST be declared with `[GeneratedRegex]`. Such a pattern MUST NOT be constructed at run time and MUST NOT rely on `RegexOptions.Compiled`.

#### Scenario: Publish audit semantic rules
- **WHEN** the publish audit evaluates the semantic HTML rules for a page
- **THEN** every pattern it uses is a source-generated regular expression

#### Scenario: Pattern assembled at run time
- **WHEN** a pattern is built from a run-time value such as an attribute name, a route or a glob
- **THEN** it is outside this requirement and stays a run-time constructed regular expression

### Requirement: Conversion preserves matching behaviour

A pattern converted to `[GeneratedRegex]` MUST keep its pattern text and its options other than `RegexOptions.Compiled` unchanged, and MUST return the same matches as the previous run-time constructed pattern for the same input, in both the test runtime and the Native AOT binary.

#### Scenario: Case-insensitive pattern
- **WHEN** a converted case-insensitive pattern is applied to input that differs from the pattern only in letter case
- **THEN** it matches exactly where the previous pattern matched

#### Scenario: Equivalence inputs
- **WHEN** a converted pattern and its previous run-time form are applied to the equivalence test inputs
- **THEN** both return identical match positions, values and groups

### Requirement: Build output and audit reports are unchanged

For the same inputs, a build MUST produce identical published file paths and bytes before and after the conversion. Internal reports MUST match after normalizing only the documented run timestamps, durations, and inventory digests and sizes derived from those fields. Derived values MUST be verified against their original inputs before normalization.

#### Scenario: Reference site compared
- **WHEN** the reference site is built and audited with the binary before and after this change
- **THEN** each published file has the same relative path and bytes, and the normalized audit reports retain the same issues, counts, and other contract fields

### Requirement: Public API is unchanged

The conversion MUST NOT add, remove or change any public member.

#### Scenario: Public API drift check
- **WHEN** the public API drift check runs after the conversion
- **THEN** it reports no change
