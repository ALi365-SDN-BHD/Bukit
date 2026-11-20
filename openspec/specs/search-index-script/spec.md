# search-index-script Specification

## Purpose
Lets the built-in search UIs load the search index on pages whose Content Security Policy blocks
`connect-src`, such as a sandboxed preview, by offering the index as a script next to every `search.json`.

## Requirements

### Requirement: Script form of every search index
Wherever a build writes a `search.json` (a single-language index, a language's index, the root
merged index, or the default language's own index in the root layout), it SHALL write
`search.json.js` in the same directory. Its content SHALL be exactly
`self.bukitSearchIndex=`, followed by the bytes of that `search.json`, followed by `;`. A build
SHALL NOT write `search.json.js` where it writes no `search.json`, and SHALL NOT write a script
form of `search.index.json`.

#### Scenario: Single-language site
- **WHEN** a single-language site is built
- **THEN** `dist/search.json.js` exists and evaluating it sets `self.bukitSearchIndex` to a value equal to the parsed `dist/search.json`

#### Scenario: Root layout with merged search
- **WHEN** `languages` is `[en, zh-CN]`, `defaultLanguage` is `en`, `defaultLanguageInSubdir` is `false`, and `search.mode` is `merged`
- **THEN** `dist/search.json.js`, `dist/en/search.json.js` and `dist/zh-CN/search.json.js` each match the `search.json` in the same directory

#### Scenario: Index mode
- **WHEN** a multilingual site is built with `search.mode: index`
- **THEN** each `<lang>/search.json.js` is written and no `search.index.json.js` is written

### Requirement: Script index follows its search index's lifecycle
The projection plan, the build manifest ownership, stale output removal and output conflict
checks SHALL treat each `search.json.js` the same as the `search.json` beside it. The publish
audit SHALL NOT treat `search.json.js` as an aggregate representation.

#### Scenario: Index no longer written
- **WHEN** an incremental build follows a build that wrote `<lang>/search.json` and `<lang>/search.json.js`, and the new configuration no longer writes `<lang>/search.json`
- **THEN** both files are removed

#### Scenario: Static file collides with the script index
- **WHEN** a site whose default language owns the root merged index has a default-language `static/search.json.js`
- **THEN** the build fails with `i18n-root-output-conflict` naming `static/search.json.js`

### Requirement: Built-in search UIs fall back to the script index
The `bukit-search.html` fragment and the starter theme's search page written by scaffolding SHALL
first request their index with `fetch`. When that request fails, returns a non-2xx status, or
does not yield a JSON array, they SHALL load `<index URL>.js` with a `<script>` element and use
`self.bukitSearchIndex` when it is an array. Only when both fail SHALL the fragment show
"Search is unavailable". The UIs SHALL NOT need `connect-src` to search when the script loads.
The fragment SHALL only load the script when the index URL resolves to the page's own origin;
an index on another origin is fetched but never executed, so a failing cross-origin index shows
"Search is unavailable".

#### Scenario: Page blocks connect-src
- **WHEN** a built site's search page is served with `Content-Security-Policy: sandbox allow-scripts allow-popups; connect-src 'none'; form-action 'none'` and the user searches for a word in a page title
- **THEN** the page lists that page in Chromium, Firefox and WebKit

#### Scenario: Normal hosting
- **WHEN** the same site is served without a Content Security Policy
- **THEN** the UIs load the index with `fetch` and do not request `search.json.js`

#### Scenario: Both forms unavailable
- **WHEN** neither `search.json` nor `search.json.js` can be loaded
- **THEN** the fragment shows "Search is unavailable"
