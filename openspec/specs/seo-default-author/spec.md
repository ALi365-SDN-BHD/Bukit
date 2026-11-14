# seo-default-author Specification

## Purpose
定义 `site.seo.defaultAuthor`：页面没有作者时，JSON-LD 的 `author` 最后回退到站点默认作者，适合由同一机构发布全部内容的站点。

## Requirements

### Requirement: Site default author as the last SEO author fallback
When `site.seo.defaultAuthor` is configured, Bukit SHALL use it as the SEO
author of a content page only when the page has no author relation, no content
`author` field, and no GEO author. The author type SHALL be
`site.seo.defaultAuthorType`, `Person` or `Organization`, defaulting to
`Person`. Configuring `defaultAuthorType` without `defaultAuthor`, a blank
`defaultAuthor`, or any other type SHALL fail configuration validation.

#### Scenario: Article without any author
- **WHEN** `defaultAuthor` is "Example Society" with type `Organization` and an article has no author
- **THEN** the article JSON-LD author is an Organization named "Example Society"

#### Scenario: Content author wins
- **WHEN** `defaultAuthor` is configured and the article has an `author` field
- **THEN** the article keeps its own author

#### Scenario: Type without author
- **WHEN** only `defaultAuthorType` is configured
- **THEN** configuration validation fails

### Requirement: Default author changes invalidate incremental output
Changing `site.seo.defaultAuthor` or `site.seo.defaultAuthorType` SHALL change
the incremental SEO dependency hash.

#### Scenario: Default author edited
- **WHEN** an incremental build runs after `defaultAuthor` changes
- **THEN** pages are re-rendered with the new author
