## 1. Implementation

- [x] 1.1 Remove the root layout + merged search rejection from configuration validation.
- [x] 1.2 Add `I18nOutputLayout.SearchRelativePath` and use it in the projection plan, the search projection writer, the default-language audit read paths and the search UI.
- [x] 1.3 Give every search record a non-empty `language`.

## 2. Tests

- [x] 2.1 Configuration accepts the combination.
- [x] 2.2 Root layout + merged: merged `/search.json` holds every language; `/<defaultLang>/search.json` holds only the default language; other languages unchanged; plan, ownership and UI agree.
- [x] 2.3 The default-language audit reads its own index and reports no missing search routes.
- [x] 2.4 Switching between split and merged, and between layouts, leaves no stale index.
- [x] 2.5 A default-language `static/search.json` fails with `i18n-root-output-conflict`.
- [x] 2.6 Worktree and in-process builds produce the same search files.

## 3. Documentation

- [x] 3.1 User guide, i18n skill, built-in outputs, CHANGELOG (en, zh-CN, ms).
