# Merged search with a root default language

## Why

`add-i18n-default-language-root` rejected `site.search.mode: merged` when the default language lives at the output root (`site.defaultLanguageInSubdir: false`), because the default language's own index and the merged index would both be written to `/search.json`. Sites that want cross-language search had to choose between the root layout and merged search.

Decisions (2026-11-13, the recommended options of the merged-search analysis): Q1 keep the merged index at `/search.json` and move the default language's own index; Q2 add `language` to every record; Q3 fix the search UI index URL first (done separately); Q4 the built-in UI searches the current language without a toggle; Q5 plan and own the root-language search UI (done with Q3); Q6 implement through this change.

## What Changes

- Configuration validation no longer rejects the root layout with `search.mode: merged`.
- With the root layout and merged search, the merged index stays at `/search.json`, and the default language's own index moves to `/<defaultLang>/search.json` (Q1, option B). Other languages keep `/<lang>/search.json`. The plan, the writer, the default-language audit and the search UI all use one helper, `I18nOutputLayout.SearchRelativePath`.
- Every search record carries a non-empty `language` (Q2 b). Records that previously had no language (list routes without dated items) or `und` take the language of the variant that wrote them in a multilingual build, and `und` in a single-language build.
- The built-in search UI of each language loads that language's own index (already the E1 behaviour), so in merged mode it shows only the current language without a toggle (Q4). The default language's fragment at the root points at `/<defaultLang>/search.json`.
- User guide, i18n skill, built-in outputs page and the three CHANGELOGs are updated.

## Impact

- Code: `Bukit.Config/SiteConfigValidator`, `Bukit.Engine` (`I18nOutputLayout`, `PublicOutputLifecycle`, `PublishProjectionContract`, `PublishAggregateProjectionWriters`, `SearchIndexBuilder`, `Plugins/BuiltIn/SearchIndexPlugin`).
- Specs: `i18n-output-layout` replaces the rejection requirement; `i18n-worktree-build` adds an equivalence requirement.
- Compatibility: the combination was rejected before, so no published site changes paths. List-route search records that had no `language` gain one in every mode.
