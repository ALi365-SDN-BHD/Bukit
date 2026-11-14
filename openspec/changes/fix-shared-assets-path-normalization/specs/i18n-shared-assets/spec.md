## MODIFIED Requirements

### Requirement: The effective shared set is a set of output prefixes
Each entry in `sharedAssets.paths` SHALL be an output path prefix relative to a
language output directory. Entries SHALL be normalized by converting `\` to
`/`, dropping `.` segments and empty segments, and appending a trailing `/` when
missing. A leading `/` or `\` SHALL denote an absolute path and SHALL fail
validation rather than be stripped. Configuration validation SHALL fail for an
empty list, a blank entry, an absolute path (a leading `/` or `\`, a drive
letter, or a UNC prefix), a path containing `..`, or an entry that normalizes to
the output root. Prefix matching and de-duplication SHALL use the same case
rules as output destination identity. When `paths` is not set, it SHALL default
to `assets/`, plus the SCSS output directory when `theme.scss.enabled` is
`true`, de-duplicated, omitting with a warning an SCSS output directory that
normalizes to the output root. A configured list SHALL replace the default. When
sharing is enabled with two or more languages and every default prefix is inside
the media prefix, configuration validation SHALL fail because the effective
shared set would be empty. The content media prefix, derived from
`content.media.urlBase`, SHALL always be excluded: an entry equal to or inside
the media prefix SHALL fail configuration validation, and an entry containing
the media prefix SHALL have it excluded internally. A path SHALL belong to the
effective shared set only when it matches at least one configured or default
prefix and does not match the media prefix. Every rule that refers to shared
paths SHALL apply to the effective shared set through a single implementation.

#### Scenario: Root prefix rejected
- **WHEN** `sharedAssets.paths` is `[/]`
- **THEN** configuration validation fails naming the entry

#### Scenario: Leading slash rejected
- **WHEN** `sharedAssets.paths` is `[/assets/]`
- **THEN** configuration validation fails naming the entry as an absolute path

#### Scenario: Windows-style absolute prefix rejected
- **WHEN** `sharedAssets.paths` is `[\assets\]` or `[C:\assets]`
- **THEN** configuration validation fails naming the entry on every platform

#### Scenario: Dot segment normalized
- **WHEN** `sharedAssets.paths` is `[./fonts/]`
- **THEN** the effective shared set contains `fonts/` and `dist/zh-CN/fonts/` is not written

#### Scenario: Blank entry rejected
- **WHEN** `sharedAssets.paths` is `[assets/, ""]`
- **THEN** configuration validation fails naming `site.sharedAssets.paths[1]`

#### Scenario: SCSS output at the root
- **WHEN** `paths` is not set, SCSS is enabled, and the SCSS output directory is the output root
- **THEN** the SCSS output directory is not a default shared prefix and a warning is logged

#### Scenario: SCSS disabled
- **WHEN** `paths` is not set, `theme.scss.enabled` is `false`, and the SCSS output directory is `css/`
- **THEN** the effective shared set contains only `assets/`

#### Scenario: Media path under a shared prefix
- **WHEN** sharing uses the default paths and `content.media.urlBase` is `/assets/uploads`
- **THEN** `assets/css/a.css` belongs to the effective shared set and `assets/uploads/x.jpg` does not

#### Scenario: Default prefixes covered by the media prefix
- **WHEN** sharing is enabled with two or more languages, `sharedAssets.paths` is not set, and `content.media.urlBase` is `/assets` or `/`
- **THEN** configuration validation fails because no default shared prefix remains outside the media prefix

#### Scenario: Explicit media prefix rejected
- **WHEN** `content.media.urlBase` is `/assets/uploads` and `sharedAssets.paths` is `[assets/uploads/]`
- **THEN** configuration validation fails stating that media sharing is not supported

#### Scenario: Media stays per language
- **WHEN** sharing uses the default paths and a Chinese post references a media image
- **THEN** the image is written under `dist/zh-CN/assets/uploads/` and its URL keeps the Chinese base URL
