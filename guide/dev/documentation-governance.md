# Documentation Governance

## Directory Responsibilities

| Directory | Purpose |
|---|---|
| `README.*` | Public project landing pages |
| `guide/user/*` | User-facing operating manual |
| `guide/dev/*` | Maintainer and contributor reference |
| `guide/ai/*` | Human-facing AI prompt packs |
| `src/skills/*` | AI Agent knowledge layer |
| `docs/*` | Product proposals, audit reports, governance notes, long-form analysis |

## Rules

1. **README must stay concise.** It is the project entry, not the manual.
2. **Full CLI reference belongs in `guide/user` or `guide/dev`.** Do not replicate in README.
3. **Full config schema belongs in `guide/dev`.** Do not replicate in README or `guide/user`.
4. **Skills documentation must not be duplicated in README or guide.** `src/skills/*` is the single source of truth for agent knowledge.
5. **All root README language versions must share the same section order.**
6. **All guide README language versions should share the same information hierarchy.**
7. **Secret values must never appear in documentation examples.** Always use placeholder names like `NOTION_TOKEN` or `YOUR_KEY`.
8. **Notion token must always be documented as `NOTION_TOKEN`.** Never show a real token value.

## Language Fallback Rules

When a localized document does not exist:

- **English**: "Currently available in [language] only"
- **Chinese (zh-CN)**: No fallback note needed unless linking to non-Chinese material
- **Malay (ms)**: "Pada masa ini hanya tersedia dalam bahasa [language]"

Use consistent wording. Do not use ad-hoc labels like "(Chinese)" in navigation titles.

## Cross-Reference Principles

- `guide/user` may reference `guide/dev` for authoritative field/contract details
- `guide/dev` may reference `docs/` for product-level context
- `guide/ai` should reference `guide/user` and `guide/dev` for validation workflows
- `src/skills` should never reference temporary/provisional documentation
