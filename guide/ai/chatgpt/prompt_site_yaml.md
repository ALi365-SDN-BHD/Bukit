# Generate site.yaml (use only when you explicitly want "direct config generation")

Copy this entire file starting from "User Requirements" to ChatGPT, filling in the placeholders. Rule: if information is insufficient, ask questions first; once complete, output only YAML (no ```). Fields must strictly align with `guide/dev/config-site-yaml.md`.

## User Requirements (replace placeholders with your real info)

I want to build a static site using Bukit v2. Please ask questions first to fill in missing info (do not output YAML). When info is complete, output only `site.yaml` (pure YAML, no explanations), ensuring it passes `bukit doctor --config site.yaml`.

Summary:
- site.name: {starter}
- site.title: {My Site}
- site.baseUrl: {"/" or "/my-repo"}
- site.url (optional, for sitemap/rss): {https://example.com or leave empty}
- Content source:
  - Single: {markdown|notion}
  - Multi: use `content.sources[]`: {yes/no}; need Modules (mode=data): {yes/no}
- Multilingual: {yes/no} (if yes: languages list and defaultLanguage)
- Theme: theme.name {alt/...}, theme.params {optional}

Output requirements:
- Output only YAML; no Markdown fences (```) or explanations
- Do not invent fields not present in the repo
- Notion token must not appear in config or conversation; must come from env var `NOTION_TOKEN`

## site.yaml Template (fill in and output after info is complete)

Rules:
- Single language: keep `site.language`, delete `site.languages` and `site.defaultLanguage`
- Multilingual: fill in `site.languages` and `site.defaultLanguage`, set `site.language` to default language
- Single source: use `content.provider: markdown|notion` with corresponding section
- Multi-source/Modules: use `content.provider: sources` + `content.sources[]` (see snippet at end)

site:
  name: "{site_name}"
  title: "{site_title}"
  url: "{optional_site_url}"
  baseUrl: "{base_url}"
  language: "{language}"
  languages: [{optional_languages}]
  defaultLanguage: "{optional_default_language}"
  pluginFailMode: strict
  timezone: Asia/Shanghai

content:
  provider: "{markdown|notion|sources}"
  markdown:
    dir: "{content_dir}"
    defaultType: page
  notion:
    databaseId: "{database_id}"
    pageSize: 50
    # sort/filter (optional, uncomment as needed)
    # sortProperty: "Date"
    # sortDirection: "descending" # ascending | descending
    # filterProperty: "Status"
    # filterType: "checkbox_true" # checkbox_true | none
    fieldPolicy:
      mode: whitelist
      allowed: [{allowed_fields}]

build:
  output: dist
  clean: true
  draft: false

theme:
  name: "{theme_name}"
  layouts: layouts
  assets: assets
  static: static
  params: {}

logging:
  level: info

## content.sources[] snippet (for multi-source / Modules, replace the content section above)

content:
  provider: sources
  sources:
    # 1. Page content source (generates routes)
    - type: markdown
      name: content
      mode: content
      markdown:
        dir: content
        defaultType: page
    # 2. Structured data source (Modules, no routes)
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
    # 3. Notion supplementary source (example: as news section)
    # - type: notion
    #   name: news
    #   mode: content
    #   notion:
    #     databaseId: "xxxx"
    #     fieldPolicy: { mode: whitelist, allowed: [title, date] }

## Safety Constraints (Must Follow)

- Never output shell commands, deployment scripts, or absolute file paths.
- Never ask for or accept tokens, keys, or secrets. Notion access must always use the `NOTION_TOKEN` environment variable.
- If the user asks for commands, direct them to: `guide/user/12-cli-reference.md`
