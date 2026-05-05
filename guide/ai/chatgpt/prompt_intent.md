# Generate intent.yaml (recommended route)

Copy this entire file starting from "User Requirements" to ChatGPT, filling in the placeholders. Rule: if information is insufficient, ask questions first; once complete, output only YAML (no ```).

## User Requirements (replace placeholders with your real info)

I want to build a static site using Bukit v2. Please start by asking 5–10 questions to clarify missing info (do not output YAML). After I answer, output only `intent.yaml` (pure YAML, no explanations), ensuring it passes `bukit intent validate`.

Summary:
- Site type: {blog/docs/company/landing/others}
- Language: {single/multilingual}
  - Single: site.language = {zh-CN/en-US/...}
  - Multilingual: languages.default = {zh-CN/en-US/...}, languages.supported = {zh-CN,en-US,...}
- Deployment: {GitHub Pages/custom domain/others}
- base_url: {"/" or "/my-repo"}
- site.url (optional, sitemap/rss absolute URL): {https://example.com or leave empty}
- Content source (Intent only supports one of two): {markdown/notion}
  - markdown: content dir {content}, default type {page/post}
  - notion: database_id {xxxx}, field_policy.mode {whitelist/all}, allowed {optional}
  - Note: Intent does not yet support advanced queries like filter/sort; manually edit site.yaml after generation.
- If you need multi-source (content.sources[]) or Modules (mode=data), use [prompt_site_yaml.md](./prompt_site_yaml.md) to directly generate site.yaml.
- Theme: theme.name {starter/alt/...}, need theme.params: {yes/no}

Output requirements:
- Prefer outputting intent.yaml (snake_case)
- Output only YAML; no Markdown fences (```) or explanations
- Do not invent fields not present in the repo

## intent.yaml Template (fill in and output after info is complete)

Rules:
- Single language: keep `site.language`, delete entire `languages` section
- Multilingual: delete `site.language`, fill in `languages.default/languages.supported`

site:
  name: "{site_name}"
  title: "{site_title}"
  base_url: "{base_url}"
  url: "{optional_site_url}"
  language: "{optional_single_language}"

languages:
  default: "{default_language}"
  supported: [{supported_languages}]

content:
  provider: "{markdown|notion}"
  markdown:
    dir: "{content_dir}"
  notion:
    database_id: "{database_id}"
    field_policy:
      mode: "{whitelist|all}"
      allowed: [{allowed_fields}]

theme:
  name: "{theme_name}"
  params: {}
