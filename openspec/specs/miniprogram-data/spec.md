# miniprogram-data Specification

## Purpose
Lets a site publish the data a mini-program consumes, as deterministic static JSON files with a
per-language version catalog, so the mini-program requests only the catalog on launch and fetches
only the datasets whose version changed. Bukit does not generate the mini-program project.

## Requirements

### Requirement: Mini-program output is opt-in and declared by datasets
The build SHALL write mini-program data only when `miniprogram.enabled` is `true`. Each entry of
`miniprogram.datasets` SHALL declare a unique `name` matching `^[a-z0-9][a-z0-9-]*$` and a `kind`
of `collection`, `module`, `dataIndex`, `records`, `taxonomy` or `menu`. A `source` SHALL name an
existing `content.sources[].name`; a `collection` source SHALL use `mode: content`, and a `module`,
`dataIndex` or `records` source SHALL use `mode: data`. A `menu` dataset SHALL have no `source` and
SHALL name an existing `site.yaml` menu with `menu`. Configuration validation SHALL fail and
name the offending path when any of these rules is broken. Only declared datasets and declared
fields SHALL be written.

#### Scenario: Disabled by default
- **WHEN** a site without a `miniprogram` section is built
- **THEN** no `mp/` directory is written and all other outputs are byte-identical to a build before this change

#### Scenario: Unknown source
- **WHEN** a dataset declares `source: notion-shop` and no content source has that name
- **THEN** configuration validation fails naming `miniprogram.datasets[0].source`

#### Scenario: Undeclared field
- **WHEN** a `records` dataset declares `fields: [name, phone]` and the Notion rows also have an `internalNote` property
- **THEN** the dataset file contains `name` and `phone` for each record and no `internalNote`

#### Scenario: Unknown menu
- **WHEN** a dataset declares `kind: menu` and `menu: footer`, and `site.yaml` has no `footer` menu
- **THEN** configuration validation fails naming `miniprogram.datasets[0].menu`

### Requirement: Navigation can come from Notion or from site menus
A `module` dataset SHALL be able to carry navigation records maintained in a Notion data source.
A `menu` dataset SHALL write the entries of the named `site.yaml` menu, ordered by `weight` like
the website's `menus.json` (declaration order breaking ties) and keeping their nesting, with each entry's `title`, absolute `url` and `children`. A site MAY
declare both kinds as separate datasets. The website's own navigation SHALL be unaffected by
either.

#### Scenario: Menu from site.yaml
- **WHEN** `site.yaml` defines `menus.main` with `Home` and `Docs`, where `Docs` has a child `Guide`, and a dataset declares `kind: menu`, `menu: main`
- **THEN** the dataset file lists `Home` then `Docs`, `Docs` has one child `Guide`, and every `url` is an absolute `https://` URL

#### Scenario: Navigation from Notion
- **WHEN** a dataset declares `kind: module`, `module: nav`, `sort: { field: order }` and an editor changes a nav row's `order` in Notion
- **THEN** after a rebuild the dataset lists the rows in the new order and its hash changes

### Requirement: Mini-program data requires an HTTPS site URL
When mini-program output is enabled, the effective site URL SHALL start with `https://`. A URL
that is set but malformed, not `https://`, or carrying user info, a query or a fragment SHALL fail
with `ConfigInvalidValue`, and the message SHALL not repeat the user info. A missing URL
SHALL fail a production build with `ConfigRequiredFieldMissing`; a development build SHALL skip
mini-program output instead, so a local preview works without a site URL.

#### Scenario: Non-HTTPS site URL
- **WHEN** `miniprogram.enabled` is `true` and the effective site URL is `http://localhost:4000`
- **THEN** the build fails with `ConfigInvalidValue`

#### Scenario: Development build without a site URL
- **WHEN** `miniprogram.enabled` is `true`, no site URL is set, and the build runs in development mode
- **THEN** the build succeeds, logs that mini-program output was skipped, and writes no file under the output directory

### Requirement: Dataset files are deterministic
Each dataset file SHALL be UTF-8 without BOM, end with exactly one `\n`, write fields in their
declared order, and SHALL NOT contain the build time or source metadata such as Notion
`last_edited_time` unless that field is declared. Records SHALL be ordered by the dataset `sort`
and then by `id` ascending. Building the same content twice, including on different operating
systems, SHALL produce byte-identical mini-program files.

#### Scenario: Rebuild without changes
- **WHEN** a site is built twice from the same content snapshot
- **THEN** every file under `mp/` is byte-identical between the two builds

#### Scenario: Source returns rows in a different order
- **WHEN** the Notion provider returns the rows of a `module` dataset sorted by `order` in a different sequence than the previous build, with the same values
- **THEN** the dataset file and its hash are unchanged

### Requirement: Files are content-addressed and listed in a version catalog
For each language the build SHALL write `catalog.json` in the mini-program directory (`mp/` by
default) under that language's output root. Every other mini-program file
SHALL be named with the first 16 hexadecimal characters of the SHA-256 of its bytes. The catalog
SHALL list each dataset with its `kind`, full `hash` (`sha256:<hex>`), absolute `url` and `size`;
a `collection` dataset SHALL also list a `detailCatalog` whose items give each detail file's
`id`, `hash`, `url`, `size` and `updatedAt`. The catalog SHALL contain `schemaVersion`,
`minClientSchema`, `language`, `generatedAt` (the latest `updatedAt` or `publishedAt` of the
written records, or the Unix epoch when there are none) and `catalogHash` computed from the
serialized catalog without `generatedAt` and `catalogHash`, so it changes whenever a version,
the language, a dataset hash or a file URL changes. A `site` dataset SHALL always be present.

#### Scenario: One record changes
- **WHEN** only the title of one banner record changes between two builds
- **THEN** the `banners` hash and `catalogHash` change, and every other dataset hash and detail hash is unchanged

#### Scenario: Article body changes
- **WHEN** only the body of one article changes
- **THEN** that article's detail hash, the `articles` detail catalog hash and `catalogHash` change, while the `articles` list hash is unchanged

#### Scenario: Multilingual site
- **WHEN** `languages` is `[en, zh-CN]`
- **THEN** with `defaultLanguage: en` in the root layout, `dist/mp/catalog.json` lists only `en` records and `dist/zh-CN/mp/catalog.json` lists only `zh-CN` records

### Requirement: Only public records are written
A dataset SHALL exclude records with `enabled: false`, drafts (also when `build.draft` keeps them
on the website), and content pages that are not indexable. An excluded record SHALL NOT appear in
any list, detail catalog or detail file.

#### Scenario: Record disabled in Notion
- **WHEN** an editor unchecks `enabled` on a banner row and the site is rebuilt
- **THEN** that banner no longer appears in the `banners` file and the `banners` hash changes

### Requirement: Media and links are absolute and stable
Image- and URL-typed fields and `src` and `href` attributes in bodies SHALL be absolute http(s)
URLs under the site URL or the original external host. When a written image- or URL-typed field, or a body image, link or embed,
still carries an expiring signature (`X-Amz-Signature`, `X-Amz-Credential`, `Signature` or
`Expires` query parameter), the build SHALL fail with `SchemaValidationFailed` naming
the dataset, record id and field. Links to pages that have mini-program detail records SHALL carry
`data-bukit-id` with the target id.

#### Scenario: Localized Notion cover
- **WHEN** an article's `cover` is a Notion file that image localization downloaded to `/assets/media/…`
- **THEN** the list and detail files contain `https://<site host>/assets/media/…`

#### Scenario: Unlocalized signed URL
- **WHEN** a `records` dataset has an `image` field whose value is still an `*.amazonaws.com` signed URL
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field

#### Scenario: Unlocalized signed file link
- **WHEN** a `module` dataset has a `url`-typed field whose value is a signed Notion PDF URL
- **THEN** the build fails with `SchemaValidationFailed` naming that dataset, record and field

#### Scenario: Internal link
- **WHEN** an article body links to another article in the same `collection` dataset
- **THEN** the link has an absolute `href` and a `data-bukit-id` equal to the target article's id

### Requirement: Bodies use a mini-program safe HTML subset
Detail and module `body` fields SHALL contain only the allowed tags and attributes defined by the
change design, with embedded frames replaced by links, collapsible blocks expanded and unknown
containers unwrapped; text directly inside an unwrapped block container SHALL become its own
paragraph, or be separated from visible content by line breaks inside a table cell or list item, a
paragraph SHALL NOT contain a block (so an inline element that wraps a block is written beside the
paragraphs of its siblings, an embed inside a paragraph becomes an inline link, and block markup
inside a paragraph, heading or summary is reduced to its content separated by line breaks), and a table caption SHALL become a paragraph before its table. A body
image, link or embed whose URL is still signed SHALL fail the build with `SchemaValidationFailed`. Scripts, styles, `class` and `style` attributes, event attributes, links
with schemes other than http(s), `mailto:` and `tel:`, and disallowed tags SHALL NOT be written.

#### Scenario: Embedded video
- **WHEN** an article body contains a Notion video embed rendered as an `<iframe>`
- **THEN** the detail body contains a link to the video URL and no `<iframe>`

### Requirement: Dataset contents are validated at build time
A dataset MAY declare `required` fields and field `types`; except for `dataIndex`, each of them
SHALL name a declared field, otherwise configuration validation SHALL fail. A record that misses a required field,
or whose value cannot be read as the declared type (for example a list under `string`), SHALL fail the build with
`SchemaValidationFailed` naming the dataset, record id and field. A `collection` dataset
whose list exceeds `maxListItems` SHALL fail the build naming the dataset.

A `module` value or `dataIndex` scope that no row of its source uses in any site language, enabled
or not, SHALL fail the build; a row whose `locale` names no site language SHALL not count; rows that exist but are all disabled SHALL give an empty dataset. A
language variant without rows of its own, because the matching rows carry another `locale`,
SHALL log a warning and write an empty dataset.

#### Scenario: Language variant without rows
- **WHEN** a site declares the languages `en` and `zh-CN` and its only `banner` row has `locale: en`
- **THEN** the `zh-CN` variant logs a warning and writes an empty `banners` dataset

#### Scenario: Module typo on a multilingual site
- **WHEN** a site declares the languages `en` and `zh-CN` and a dataset declares `module: banners` while every row has `type: banner`
- **THEN** the build fails naming the dataset and `module`

#### Scenario: Wrong type in Notion
- **WHEN** a `stores` row has `lat` set to `abc` and the dataset declares `lat: number`
- **THEN** the build fails with `SchemaValidationFailed` naming `stores`, that row's id and `lat`

### Requirement: Mini-program files follow the output lifecycle
The build manifest's plugin-output tracking and stale output removal SHALL own every file under
the mini-program output directory. Files no longer referenced by the current catalog SHALL be
removed on the next build, and disabling mini-program output SHALL remove the whole directory.
Multilingual builds that run each language in a worktree worker SHALL write the same output as an
in-process build. Each file SHALL be tracked as it is written, so files written before a failing dataset are still
owned and removed later.
A static file colliding with a mini-program output path SHALL fail the build through the existing
output collision check.

#### Scenario: Output disabled
- **WHEN** a site that wrote `mp/` is rebuilt with `miniprogram.enabled: false`
- **THEN** no file under `mp/` remains in the output

### Requirement: Object and object-array fields are opt-in, bounded and deterministic
A dataset field whose value is a mapping or a list of mappings SHALL be written only when `types`
declares it `object` or `object[]`, optionally with nested `fields` declared the same way. Declared
nested fields SHALL project each object to exactly those keys; undeclared objects SHALL keep every
key with inferred values. Object keys SHALL be written in ordinal order. Nested `image` and `url`
values SHALL follow the top-level URL rules, nested strings SHALL be plain text, and objects nested
deeper than 8 levels SHALL fail the build with `SchemaValidationFailed` naming the dataset, record
and field path. A catalog SHALL use `schemaVersion` and `minClientSchema` `1.1` only when an object
field is declared; other sites SHALL keep `1.0` and byte-identical output.

#### Scenario: Route days
- **WHEN** a route record has `days: [{title, text, stay, meals, sights, exps}]` and the dataset declares `days: object[]`
- **THEN** the detail file contains every day with its keys in ordinal order and the build succeeds

#### Scenario: Site without object fields
- **WHEN** a site declares no `object` or `object[]` type
- **THEN** its catalog stays `1.0` and every file under `mp/` is byte-identical to the previous release

### Requirement: Data index rows can hold object values
A data index row SHALL accept the value types `object` (one mapping) and `object[]` (a list of
mappings) besides `text`, `multiline`, `email`, `phone` and `url`. A Markdown row SHALL give the
value as YAML; a Notion row SHALL give it as JSON text, parsed without reflection. Every scalar in an
object value SHALL be stored as text (JSON numbers keep their source digits, booleans become `true`
or `false`, null stays null), so equal values from either source SHALL build the same model and render
dependency hash. Keys SHALL be ordered ordinally, nesting SHALL be limited to 8 levels with every
mapping and list counting as one, and `NaN`, infinity, invalid JSON, duplicate JSON keys or a value of
the wrong shape SHALL fail the build naming the source, content id and `scope.key`. A text value type
whose value is a mapping or a list SHALL fail the build instead of storing the .NET type name. An
empty object value SHALL be an empty mapping or list and SHALL count as empty for `requiredKeys`.

#### Scenario: Object row read by a template and a dataset
- **WHEN** a Markdown row `trip.info` has `value_type: object` and `value: {days: 5, stops: [{name: George Town}]}`
- **THEN** `site.data_index.settings.trip.info.days` renders `5`, and a `dataIndex` dataset that declares `info: object` writes `{"days":"5","stops":[{"name":"George Town"}]}`

#### Scenario: Text row given a mapping
- **WHEN** a row has `value_type: text` and its `value` is a YAML mapping
- **THEN** the build fails naming the source, content id and `scope.key`
