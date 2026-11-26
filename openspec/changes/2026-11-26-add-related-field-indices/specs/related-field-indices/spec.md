## ADDED Requirements

### Requirement: Related pages can score custom fields per collection
A related index with `field` SHALL add its weight for each value two pages share in that field.
For a collection listed in `site.related.collections`, its pages SHALL be scored with that
collection's rules against pages of the same collection only.

#### Scenario: Shared cities and audiences
- **WHEN** route rules are `indices: [{ field: cities, weight: 2 }, { field: audiences, weight: 1 }]` and two routes share one city and one audience
- **THEN** their score is 3

### Requirement: Related order is stable
Related entries SHALL be sorted by score descending, and entries with the same score SHALL keep
the collection list order. Each entry SHALL include `id`, `slug`, `title`, `url` and `score`.

#### Scenario: Equal scores
- **WHEN** two candidates score the same
- **THEN** the newer one comes first
