## ADDED Requirements

### Requirement: Starter theme links honor the base URL

Starter theme links built from root-relative URLs SHALL include
`site.base_url`. This covers navigation module links that start with a single
`/` and taxonomy index term links. Absolute and protocol-relative navigation
links SHALL be left unchanged.

#### Scenario: Navigation module link under a sub-path
- **WHEN** `site.base_url` is `/docs` and a navigation module link is `/about/`
- **THEN** the header renders `href="/docs/about/"`

#### Scenario: External navigation link
- **WHEN** a navigation module link is `https://example.com/`
- **THEN** the header renders `href="https://example.com/"`

#### Scenario: Taxonomy term link under a sub-path
- **WHEN** `site.base_url` is `/docs` and a term URL is `/tags/news/`
- **THEN** the taxonomy index renders `href="/docs/tags/news/"`

### Requirement: Starter theme escapes content text

Starter theme templates SHALL HTML-escape content-derived text: page and item
titles and summaries, cover image attributes, categories, tags, navigation
titles, feature text, taxonomy names and descriptions. Rendered page content
SHALL stay unescaped.

#### Scenario: Title with markup
- **WHEN** an item title is `A <b> & C`
- **THEN** the list card renders `A &lt;b&gt; &amp; C`

### Requirement: Starter theme has no business-specific fields

The list card SHALL NOT render `country` or `industry` fields.

#### Scenario: Item with country field
- **WHEN** an item has a `country` field
- **THEN** the list card does not render its value

### Requirement: Starter theme labels come from site params

Starter theme templates SHALL read interface text from
`site.params.labels.<key>` and SHALL fall back to English when a key is
missing. `bukit new` SHALL write `theme.params.labels` in Simplified Chinese
when the primary language subtag is `zh` and the tag has no `Hant`, `TW`,
`HK`, or `MO` subtag, and in English otherwise.

#### Scenario: Chinese starter
- **WHEN** the user runs `bukit new my-site --language zh-CN` and builds it
- **THEN** the home page navigation shows `首页`

#### Scenario: Traditional Chinese starter
- **WHEN** the user runs `bukit new my-site --language zh-TW`
- **THEN** `theme.params.labels.home` is `Home`

#### Scenario: Missing label
- **WHEN** `site.params.labels` is absent
- **THEN** the header renders `Home`

### Requirement: Starter theme presentation

The starter stylesheet SHALL support `prefers-color-scheme: dark` and SHALL
provide a collapsible navigation on narrow screens whose links stay visible
without JavaScript. The theme SHALL NOT load external fonts or scripts. The
stylesheet SHALL keep the `--primary: #0b5fff;` and `--accent: #0f7b6c;`
declarations.

#### Scenario: Stylesheet contract
- **WHEN** `StyleCss.css` is inspected
- **THEN** it contains `prefers-color-scheme: dark`, `--primary: #0b5fff;`,
  and `--accent: #0f7b6c;`
- **AND** no theme resource references an `http://` or `https://` stylesheet,
  font, or script

#### Scenario: Navigation without JavaScript
- **WHEN** the header is rendered
- **THEN** the menu toggle button is `hidden` and the navigation links are in
  the markup
