## ADDED Requirements

### Requirement: Aggregate projection and manifest checks scan each file once

When building publish projection results, the build MUST determine whether each route URL or canonical appears in an aggregate output file with the same result as a case-insensitive ordinal substring search, and MUST read each aggregate file and compute those results in a single pass per file rather than once per route. The publish audit MUST read and parse `agent-manifest.json` at most once per audit run and reuse it for every document. Projection results, audit issues (code, severity, route and message) and report files MUST be identical to a per-route evaluation.

#### Scenario: Route presence matches substring search
- **WHEN** an aggregate file is checked for a set of route URLs and canonicals that differ in letter case, overlap or are absent
- **THEN** each route's presence equals the result of a case-insensitive ordinal substring search for that route

#### Scenario: Manifest parsed once per audit
- **WHEN** a site with many indexable documents is audited
- **THEN** `agent-manifest.json` is read and parsed once, and every document reports the same issues as before

#### Scenario: Manifest problems reported unchanged
- **WHEN** `agent-manifest.json` is missing, unparsable or lacks a `documents` array
- **THEN** each indexable document reports exactly the issues it reported before this change
