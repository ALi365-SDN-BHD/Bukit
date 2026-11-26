## ADDED Requirements

### Requirement: Collections derive page fields from templates
For each page of a collection named in `content.derivedFields`, the build SHALL render each rule's
Scriban `value` over the page's top-level fields, in declaration order, and write the trimmed text
to the rule's field. In `default` mode it SHALL write only when the field is missing or empty and
the result is not empty; in `override` mode it SHALL always write.

#### Scenario: SEO title from two names
- **WHEN** the rule is `seo_title: { value: "{{ name }} {{ name_en }}" }` and the page has `name: 吉隆坡`, `name_en: Kuala Lumpur` and no `seo_title`
- **THEN** the page's `seo_title` is `吉隆坡 Kuala Lumpur`

#### Scenario: Default keeps a hand-written value
- **WHEN** the page already has `seo_title`
- **THEN** a `default` rule leaves it unchanged

### Requirement: Derived values are deterministic
Templates SHALL run with the invariant culture and a loop limit, and SHALL NOT have clock or random
functions.

#### Scenario: Runtime failure
- **WHEN** a template exceeds the loop limit on a page, including when only the page's link data makes it fail
- **THEN** the field is unchanged and `content.derived_field_failed` is reported under the collection's `schemaFailMode`

#### Scenario: Template does not parse
- **WHEN** a value is not valid Scriban
- **THEN** `bukit config check` and the build fail with `ConfigInvalidValue` before content is fetched
