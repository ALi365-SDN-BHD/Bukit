## ADDED Requirements

### Requirement: Root error document identity
The static render route SHALL preserve a root `404.html` input as URL `/404.html` and output `404.html`. Both static route entry points SHALL agree. Ordinary and nested static documents SHALL retain existing routing.

#### Scenario: Root error document
- **WHEN** a theme renders root `404.html` through its static template
- **THEN** the output is root `404.html`, no `404/index.html` is created, and the document passes through the existing HTML transform pipeline.

#### Scenario: Nested ordinary document
- **WHEN** a theme renders `docs/404.html`
- **THEN** existing URL `/docs/404/` and output `docs/404/index.html` are retained.
