# 生成 intent.yaml（推荐路线）

把本文件从“用户需求”开始整段复制给 ChatGPT，并按占位符填写。规则：信息不足必须先提问；一旦信息齐全，只输出 YAML（不要输出 ```）。

## 用户需求（把占位符替换为你的真实信息）

我要用 Bukit v2 建一个静态站点，请先用 5-10 个问题把缺失信息问清楚（不要输出 YAML）。当我回答完毕后，请只输出 `intent.yaml`（纯 YAML，不要解释），并确保可通过 `bukit intent validate`。

需求概述：
- 站点类型：{blog/docs/company/landing/others}
- 语言：{单语言/多语言}
  - 单语言：site.language = {zh-CN/en-US/...}
  - 多语言：languages.default = {zh-CN/en-US/...}，languages.supported = {zh-CN,en-US,...}
- 部署：{GitHub Pages/自建域名/others}
- base_url：{"/" 或 "/my-repo"}
- site.url（可选，sitemap/rss 绝对 URL）：{https://example.com 或留空}
- 内容源（Intent 仅支持二选一）：{markdown/notion}
  - markdown：内容目录 {content}，默认 type {page/post}
  - notion：database_id {xxxx}，field_policy.mode {whitelist/all}，allowed {可选}
  - 注意：Intent 暂不支持 filter/sort 等高级查询，生成后可手动修改 site.yaml。
- 如果你需要多源（content.sources[]）或 Modules（mode=data），请改用 [prompt_site_yaml.md](./prompt_site_yaml.md) 直接生成 site.yaml。
- 主题：theme.name {starter/alt/...}，是否需要 theme.params {是/否}

输出要求：
- 首选输出 intent.yaml（snake_case）
- 只输出 YAML；不要输出 Markdown 围栏（```）或解释文字
- 不要发明仓库不存在的字段

## intent.yaml 模板（信息齐全后再填充并输出）

规则：
- 单语言：保留 `site.language`，删除整个 `languages` section
- 多语言：删除 `site.language`，并填写 `languages.default/languages.supported`

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

## 安全约束（必须遵守）

- 绝不输出 shell 命令、部署脚本或绝对文件路径。
- 绝不索要或接受 token、密钥或 secret。Notion 访问必须使用 `NOTION_TOKEN` 环境变量。
- 如果用户要求提供命令，引导至：`guide/user/12-命令行参考.md`
