## ADDED Requirements

### Requirement: Stable initial Google Analytics page title
When the final HTML head has a title, Bukit SHALL provide its normalized text
as the page_title parameter of every Google Analytics config command. Bukit
SHALL keep one config command per destination and SHALL NOT add a manual page
view. Consent defaults SHALL remain before Google loaders and config commands.

#### Scenario: Final static title is available
- **WHEN** Analytics transforms final HTML with a primary head title
- **THEN** the generated Google Analytics config reads the encoded normalized
  title without waiting for browser parsing

#### Scenario: No final title is available
- **WHEN** the final HTML has no head title
- **THEN** Google Analytics retains its document-title default

### Requirement: Stable Analytics CSP and incremental output
Page-specific title text SHALL NOT change generated inline script bodies.
Changing the renderer behavior SHALL invalidate prior incremental output.

#### Scenario: Two pages have different titles
- **WHEN** CSP requirements are generated without rendering those pages first
- **THEN** their generated inline Analytics script bodies remain covered by the
  reported CSP hashes

#### Scenario: Renderer contract changes
- **WHEN** an incremental build compares output rendered by the prior contract
- **THEN** the Analytics dependency hash changes
