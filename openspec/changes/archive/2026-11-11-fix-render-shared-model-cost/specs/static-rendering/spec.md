## ADDED Requirements

### Requirement: Shared template models are converted once per render pass

Within one render pass, the Scriban renderer MUST convert the site page index exposed as `pages` and the site model exposed as `site` at most once per model instance, identified by reference equality, and MUST reuse the converted values for every page that receives the same instance; the cached key and converted value MUST be published together. Values that differ per page (`page`, `seo`, `content` and other root entries) MUST still be created for each page. The shared values and every nested object or array inside them MUST be read-only, so a template that adds a new key to a shared object or assigns an element of a shared array fails with a read-only error and cannot affect another page; assigning an existing member MUST keep failing as it does today. Template-visible keys, values and ordering of `pages` and `site` MUST stay unchanged, derived list pages MUST keep exposing their list items as `pages` and `items`, and for templates that perform no such writes the rendered output MUST be byte-identical to the output produced before this change. The public `ITemplateRenderer` interface MUST NOT change. The renderer MUST release the cached values when the render dispatch ends.

#### Scenario: Large site renders without per-page index conversion
- **WHEN** a site with thousands of pages is rendered and its templates do not use `pages`
- **THEN** the page index and the site model are each converted at most once for the render pass rather than once per page

#### Scenario: New key on a shared object is rejected
- **WHEN** a template adds a key that does not exist to `site`, to `pages[0]` or to a nested `site.data` object
- **THEN** rendering that page fails with a read-only error and other pages do not see the key

#### Scenario: Shared array element assignment is rejected
- **WHEN** a template assigns `pages[0]` or an element of a nested array inside `site`
- **THEN** rendering that page fails with a read-only error and other pages see the original value

#### Scenario: Concurrent renders share safely
- **WHEN** many pages that read the same shared values are rendered in parallel
- **THEN** the output equals the output of rendering them one by one

#### Scenario: Output unchanged
- **WHEN** the same site is built before and after this change with the same inputs and templates that do not write to shared values
- **THEN** every output file is byte-identical

#### Scenario: Derived list aliases preserved
- **WHEN** a derived list page is rendered
- **THEN** its `pages` and `items` still refer to the list items

### Requirement: List page sources are converted once per list page

When a list page's `items` (or `pages` when `items` is absent) refers to the same instance as its `pages`, the renderer MUST convert it once and expose the same read-only array under both names, with unchanged template-visible content.

#### Scenario: Same instance for pages and items
- **WHEN** a list page model sets `Pages` and `Items` to the same list instance
- **THEN** `pages` and `items` are the same read-only array and the rendered output is unchanged

#### Scenario: Items absent
- **WHEN** a list page model has no `Items`
- **THEN** `items` is the same read-only array as `pages`

### Requirement: Render stage timings are measured

When build metrics are requested, the render dispatcher MUST record `pageRender`, `listBuild` and `staticRender` as the elapsed time of each content, list and static page render, measured from after the body is loaded to after the output file is written, and MUST record, for all three kinds, `renderCall` (the template renderer call), `htmlTransform` (HTML transforms) and `renderWrite` (writing the output file). When the built-in Scriban renderer is used, it MUST also record `renderModelBind` (template model conversion, a subset of `renderCall`) through an internal channel that does not change the public `ITemplateRenderer` interface. The dispatcher MUST record `renderDispatch` as the wall-clock time of the whole render dispatch. The variant pipeline MUST record the wall-clock time of its pre-render steps that have no other stage as `variantSetup`, `mediaResolve` (only when media localization is active), `renderPlan`, `assetPrepare` and `seoPrepare`, so that the wall-clock stages, excluding the per-page render stages, add up to `variantTotal`. Per-page durations MUST be accumulated with sub-millisecond precision and converted to milliseconds once, and concurrent renders MUST NOT lose recorded time. The metrics file structure MUST stay unchanged; only stage names are added.

#### Scenario: Page render time reported
- **WHEN** a build with `--metrics` renders content pages and at least one list page with the Scriban renderer
- **THEN** `stages.durationsMs` reports `pageRender`, `listBuild`, `renderCall`, `htmlTransform`, `renderWrite`, `renderModelBind` and `renderDispatch` with values greater than zero

#### Scenario: Variant total is accounted for
- **WHEN** a build with `--metrics` completes a variant
- **THEN** the sum of its wall-clock stages, excluding per-page render stages that run inside `renderDispatch`, is within 10% of `variantTotal`, or within 50 milliseconds when `variantTotal` is shorter than 500 milliseconds

#### Scenario: Sub-millisecond pages are not lost
- **WHEN** many pages each take less than one millisecond to write
- **THEN** `renderWrite` reports their accumulated time rather than zero
