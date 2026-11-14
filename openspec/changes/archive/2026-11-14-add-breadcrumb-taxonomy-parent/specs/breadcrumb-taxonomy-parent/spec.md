## ADDED Requirements

### Requirement: Optional home node in BreadcrumbList
When `site.seo.schema.breadcrumbHome` is true, every BreadcrumbList SHALL start
with the home page, named by the home route title or `site.title` when no home
route exists. The home page itself SHALL still have no BreadcrumbList.

#### Scenario: Post with home enabled
- **WHEN** `breadcrumbHome` is true and a post lives at `/a/`
- **THEN** its BreadcrumbList is Home › post

### Requirement: Taxonomy term page as breadcrumb parent
When `site.seo.schema.breadcrumbParent` names a taxonomy kind, a content page
listed on a term page of that kind SHALL get the term's first page as parent,
preferring the document's own term order. Terms SHALL be identified by slug.
A blank value, or one that is not a taxonomy kind, SHALL fail validation.

#### Scenario: Flat post URL in a category
- **WHEN** `breadcrumbParent` is `categories` and post `/2596/` is listed under category "news"
- **THEN** its BreadcrumbList is news › post (Home › news › post with `breadcrumbHome`)

#### Scenario: Unset
- **WHEN** neither option is configured
- **THEN** BreadcrumbList output and incremental hashes are unchanged

### Requirement: Breadcrumb options invalidate incremental output
Changing either option SHALL change the incremental SEO dependency hash. With
either option on, a page's dependency hash SHALL include every breadcrumb node's
name and target.

#### Scenario: Option edited
- **WHEN** `breadcrumbParent` changes between incremental builds
- **THEN** pages are re-rendered

#### Scenario: Parent renamed
- **WHEN** a category's routeMetadata title changes between incremental builds
- **THEN** every post with that category as breadcrumb parent is re-rendered

#### Scenario: Unknown kind
- **WHEN** `breadcrumbParent` is `category` but only `categories` exists
- **THEN** configuration validation fails
