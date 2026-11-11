## ADDED Requirements

### Requirement: Render dependencies are tracked per route

An incremental build MUST split render dependencies into a base part (every render input other than shared data collections) and shared data collections: each public `site.data`, `site.modules` and `site.data_index` key, `site.related_pages`, `site.data_files`, and `pages` (the all-pages list and pages read by section sources). For every rendered page, list and special list, Bukit MUST record the collections that route read, determined conservatively from every template file the render used (template, layout chain, includes, components and section templates), recorded as the render loads them so that a dynamic template name resolves to the file actually used. A use whose key cannot be determined statically, a whole `site`, `site.data`, `site.modules` or `site.data_index` value, a global name the analysis cannot account for, a template analysis failure, or a renderer that cannot report its templates MUST count as reading every collection in that scope. A route's render dependency hash MUST cover the base part and exactly the collections the route read in its previous render; when the previous manifest has no read record for the route, it MUST cover every collection. The public API MUST NOT change.

#### Scenario: One article edited
- **WHEN** an article's title changes on a site whose article template reads no shared collection and whose home page reads `site.data.pages_by_id`, and an incremental build runs
- **THEN** the edited article and the home page are rendered again and the other article pages are skipped

#### Scenario: Template reads the all-pages list
- **WHEN** a page template iterates `pages` and another page's title changes
- **THEN** that page is rendered again

#### Scenario: Conditional read
- **WHEN** a template reads `site.data.menu` only inside a branch controlled by a page field, and that field changes so the branch is taken
- **THEN** the page is rendered again because its own content changed, and its new read record includes `site.data.menu`

#### Scenario: Whole data object used
- **WHEN** a template passes `site.data` to a function or iterates it
- **THEN** the route depends on every `site.data` collection

#### Scenario: Manifest without read records
- **WHEN** the previous manifest was written without read records
- **THEN** every route depends on every collection for that build, and the new manifest records reads

#### Scenario: Base input changed
- **WHEN** a site-wide input in the base part changes
- **THEN** every route is rendered again

### Requirement: Data dependency changes have their own render reason

When a page is rendered again because a shared collection it read changed while the base part is unchanged, it MUST be counted as `data_dependency_changed`. A change of the base part MUST still be counted as `render_dependency_changed`, and `render_dependency_changed` MUST be evaluated before `data_dependency_changed`.

#### Scenario: Collection changed
- **WHEN** only `site.data.pages_by_id` changes and a page read it
- **THEN** the page is counted as `data_dependency_changed`

#### Scenario: Site setting changed
- **WHEN** a site setting in the base part changes
- **THEN** pages are counted as `render_dependency_changed`
