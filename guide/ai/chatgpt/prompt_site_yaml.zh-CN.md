# 生成 site.yaml（仅当你明确要“直接生成配置”时使用）

把本文件从“用户需求”开始整段复制给 ChatGPT，并按占位符填写。规则：信息不足必须先提问；一旦信息齐全，只输出 YAML（不要输出 ```），字段必须严格对齐 `guide/dev/config-site-yaml.md`。

## 用户需求（把占位符替换为你的真实信息）

我要用 Bukit v2 建一个静态站点。请先提问补齐缺失信息（不要输出 YAML）。当信息齐全后，请只输出 `site.yaml`（纯 YAML，不要解释），并确保可通过 `bukit doctor --config site.yaml`。

需求概述：
- site.name：{starter}
- site.title：{My Site}
- site.baseUrl：{"/" 或 "/my-repo"}
- site.url（可选，用于 sitemap/rss）：{https://example.com 或留空}
- 内容源：
  - 单源：{markdown|notion}
  - 多源：是否使用 `content.sources[]`：{是/否}；是否需要 Modules（mode=data）：{是/否}
- 多语言：{是/否}（如果是：languages 列表与 defaultLanguage）
- 主题：theme.name {alt/...}，theme.params {可选}

输出要求：
- 只输出 YAML；不要输出 Markdown 围栏（```）或解释文字
- 不要发明仓库不存在的字段
- Notion token 不要出现在配置与对话里，必须来自环境变量 `NOTION_TOKEN`

## site.yaml 模板（信息齐全后再填充并输出）

规则：
- 单语言：保留 `site.language`，删除 `site.languages` 与 `site.defaultLanguage`
- 多语言：填写 `site.languages` 与 `site.defaultLanguage`，并把 `site.language` 设为默认语言
- 单源：使用 `content.provider: markdown|notion` 并保留对应 section
- 多源/Modules：使用 `content.provider: sources` + `content.sources[]`（见文末片段）

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
    # 排序与过滤（可选，按需取消注释）
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

## content.sources[] 片段（多源 / Modules 用，复制后替换上面的 content）

content:
  provider: sources
  sources:
    # 1. 页面内容源（生成路由）
    - type: markdown
      name: content
      mode: content
      markdown:
        dir: content
        defaultType: page
    # 2. 结构化数据源（Modules，不生成路由）
    - type: markdown
      name: modules
      mode: data
      markdown:
        dir: data
        defaultType: module
    # 3. Notion 补充源（示例：作为新闻版块）
    # - type: notion
    #   name: news
    #   mode: content
    #   notion:
    #     databaseId: "xxxx"
    #     fieldPolicy: { mode: whitelist, allowed: [title, date] }
