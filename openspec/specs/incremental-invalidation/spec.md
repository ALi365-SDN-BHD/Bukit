# incremental-invalidation Specification

## Purpose
Defines which inputs invalidate incrementally rendered output: the engine identity in the template hash, enabled built-in plugin options in the render dependencies, how changed inputs are reported as render reasons, and the diagnostic for signed links left in shared render data.

## Requirements

### Requirement: Engine identity invalidates rendered output

The composite template hash used for incremental reuse MUST include the Bukit engine identity (the engine assembly's informational version, including the commit identifier when available). When the engine identity changes, every page, list and special-list output MUST be re-rendered on the next incremental build. The manifest format and the public API MUST NOT change for this purpose.

#### Scenario: Bukit upgrade with preserved output
- **WHEN** a site built with one engine identity is rebuilt with `--no-clean` by a different engine identity and no content changed
- **THEN** every page, list and special list is re-rendered

#### Scenario: Same engine, unchanged inputs
- **WHEN** the same engine rebuilds unchanged inputs with `--no-clean`
- **THEN** every page is skipped as `unchanged`

#### Scenario: Multilingual worktree build after an upgrade
- **WHEN** a multilingual worktree build runs with a new engine identity and then again with the same identity and unchanged inputs
- **THEN** the first build re-renders every page and the second skips them as `unchanged`

### Requirement: Built-in plugin options are render dependencies

For every enabled built-in plugin other than analytics, the render dependency hash MUST include its options in canonical form. Changing any option value MUST invalidate the outputs that depend on the render dependency hash.

#### Scenario: Plugin option changed
- **WHEN** a built-in plugin option value changes and an incremental build runs
- **THEN** pages are re-rendered with reason `render_dependency_changed`

#### Scenario: Option order does not matter
- **WHEN** the same plugin options are written in a different key order
- **THEN** the render dependency hash is unchanged

### Requirement: Metadata changes have their own render reason

When an incremental build re-renders a page because its metadata hash (page record and custom fields) changed, the page MUST be counted under the render reason `metadata_changed`. The `bodyFingerprint` field identifies the body, not metadata: it MUST NOT enter the metadata hash, it MUST still enter the page content hash, and list pages that do not include content MUST still change when it changes. The reason `content_changed` MUST be used only when the metadata, route, template and render dependency hashes match and the body content hash differs. The order in which reasons are evaluated MUST NOT change.

#### Scenario: Custom field changed
- **WHEN** only a page's custom field changes and an incremental build runs
- **THEN** the page is re-rendered and counted as `metadata_changed`

#### Scenario: Body changed
- **WHEN** only a page's body changes and an incremental build runs
- **THEN** the page is re-rendered and counted as `content_changed`

#### Scenario: Declared body fingerprint changed
- **WHEN** only a page's `bodyFingerprint` field changes (for example a Markdown body edit)
- **THEN** the page is counted as `content_changed`, and list pages that show the page without its content are rendered again
