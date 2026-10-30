# Stage 6 output validation

## Requirement: Ordered, evidence-based completion

The implementation MUST complete the five user-authorized work packages in order and MUST NOT treat skipped or withdrawn-candidate results as current GREEN evidence.

### Scenario: A later repair depends on a contract decision

WHEN a contract is ambiguous, THEN its decision and compatibility implications SHALL be recorded before its implementation, and the next work package SHALL NOT begin until the current package is verified or explicitly blocked.

## Requirement: Real report contracts

Each of the 24 current report records MUST have a local binding, a real generated instance accepted by its schema, and a wrong-field or wrong-type instance rejected by that schema.

### Scenario: Optional production fields are emitted

WHEN a current producer emits supported optional metadata, THEN its report SHALL remain valid and wrong metadata types and unrelated unknown fields SHALL still be rejected.

## Requirement: Safe public text

Search, document Markdown and llms-full text MUST preserve visible content without exposing script/style bodies or extracting their hidden links.

### Scenario: Adversarial HTML boundaries

WHEN input contains quoted attributes, similar closing tags, script escapes, raw-text examples or SVG CDATA, THEN each public text representation SHALL be checked independently against the intended visible-content and hidden-content expectations.

### Scenario: Inert and literal parsing contexts

WHEN an outer anchor spans an inert HTML template, THEN omitted template contents SHALL NOT erase its valid target; nested/unclosed HTML templates and foreign/self-closing controls SHALL respect parser context. WHEN scripting-enabled noscript contains literal anchor markup, THEN its text SHALL remain literal without creating a Markdown link. Normalized xmp/iframe/noembed/noframes text SHALL preserve word boundaries in search, Markdown and llms-full. The existing malformed-anchor and unsafe-URI policies SHALL remain unchanged.

## Requirement: Diagnose incomplete raw GEO items

FAQ/HowTo entries from actual field maps MUST remain observable by existing per-field warn/strict/off diagnostics even when incomplete or not maps. Only complete entries SHALL be emitted in specialized JSON-LD; no valid entries means no specialized node and no fabricated Article fallback. Valid HowTo positions SHALL be contiguous. Public model declarations, citations/mentions and author/publisher behavior SHALL remain compatible.

### Scenario: Mixed or all-invalid required items

WHEN raw FAQ question/answer or HowTo name/text is missing or blank, THEN warn SHALL report, strict SHALL reject, and off SHALL suppress diagnostics without emitting invalid items. Missing/empty list controls SHALL retain their existing missing-list diagnostic behavior.
