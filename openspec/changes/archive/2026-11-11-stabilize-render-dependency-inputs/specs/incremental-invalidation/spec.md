## ADDED Requirements

### Requirement: Signed links in shared render data are reported

When incremental rendering is enabled, the engine MUST report links of a recognized signing scheme that remain in the shared render data (`site.modules`, `site.data`, `site.data_index`, `site.related_pages`, `site.data_files`) with one summary warning that names each affected read key and its link count. The render dependency hash MUST keep such links unchanged, so that a page whose links were re-signed is rendered again rather than skipped with an expired link.

#### Scenario: Re-signed link in shared data
- **WHEN** a shared data collection contains a signed link and the next incremental build sees it re-signed
- **THEN** the build logs `event=incremental.signed_links` naming that read key, and the routes that read it are re-rendered with reason `data_dependency_changed`

#### Scenario: Unchanged inputs without signed links
- **WHEN** the same inputs without signed links are built twice incrementally
- **THEN** every route keeps its render dependency hash, every page is skipped, and no signed-link warning is logged
