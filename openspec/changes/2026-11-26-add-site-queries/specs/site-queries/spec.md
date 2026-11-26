## ADDED Requirements

### Requirement: Named queries list one collection's entries
For each `site.queries.<name>`, the build SHALL take the current language's entries of collection
`from` (pages and entries without a page) in collection list order, keep those matching every
`where` filter, apply `sort` stably, and expose the result to templates as `site.queries.<name>`.

#### Scenario: Cities with routes
- **WHEN** the query is `{ from: city, where: [{ hasBackref: routes }] }` and only `kl` is linked by a route
- **THEN** `site.queries.<name>` holds one entry with key `kl`

#### Scenario: Missing sort value
- **WHEN** sorting by `featured` descending and one entry has no `featured`
- **THEN** that entry comes last

### Requirement: Queries can group or return distinct values
A query with `group` SHALL return one `{ value, items }` per listed value, holding the entries whose
field (or any list item) equals it, with `limit` per group. A query with `distinct` SHALL return
the field's distinct values in entry order.

#### Scenario: Routes by month
- **WHEN** `group: { field: best_months, values: [1, 2] }` and a route lists months 1 and 2
- **THEN** the route appears in both groups
