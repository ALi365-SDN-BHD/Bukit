# analytics-rendering Specification

## Purpose
Provide stable, privacy-compatible Analytics output whose initial Google
Analytics page title comes from the final generated HTML.

## Requirements

### Requirement: Stable initial Google Analytics page title
When the final HTML head has a title, Bukit SHALL provide its normalized text
as the page_title parameter of every Google Analytics config command. Bukit
SHALL keep one config command per destination and SHALL NOT add a manual page
view. When Google consent is configured, consent defaults SHALL remain before
Google loaders and config commands.

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

### Requirement: Google providers without consent configuration
Bukit SHALL accept Google Analytics and Google Tag Manager providers without
`site.analytics.consent`. Without consent, Bukit SHALL NOT emit a consent
default command and SHALL emit the provider's own bootstrap. Configuring
`site.analytics.consent.google` without a Google provider SHALL remain invalid.

#### Scenario: Google Analytics without consent
- **WHEN** only a Google Analytics provider is configured and consent is absent
- **THEN** validation succeeds and the output contains the gtag bootstrap
  without a consent default command

#### Scenario: Consent without a Google provider
- **WHEN** consent.google is configured and no Google provider is configured
- **THEN** validation fails
