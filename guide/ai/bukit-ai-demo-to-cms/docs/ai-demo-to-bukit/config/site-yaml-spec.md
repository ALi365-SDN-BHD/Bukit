# Bukit `site.yaml` 配置规范

## 1. 目标

本规范定义 AI 在生成 Bukit `site.yaml` 时必须遵守的配置合同。

AI 不得自行发明字段、层级或组合方式。所有 `site.yaml` 必须从本规范和 `site-yaml-profiles.md` 中选择合法结构生成。

适用范围：

```text
AI Demo-to-Bukit
直接生成 Bukit 工程
HTML Demo import 后配置修正
Notion-only 构建配置
Markdown 本地预览配置
```

---

## 2. 基本原则

### 2.1 不得自由拼装配置

AI 生成 `site.yaml` 前，必须先选择一种标准 Profile：

```text
Profile A：Markdown 本地预览模式
Profile B：Notion 单数据库模式
Profile C：Notion 多数据库模式
Profile D：JSON/YAML seed + Markdown 构建模式
```

### 2.2 不得生成未知字段

禁止生成 Bukit 未定义字段，例如：

```yaml
base_url: https://example.com
themePath: themes/demo
notionDatabase:
  id: xxx
```

### 2.3 不得混用旧新配置

禁止同时出现：

```yaml
content:
  provider: notion
  sources: []
```

`content.provider` 与 `content.sources` 只能二选一。

### 2.4 构建数据源必须与内容输出匹配

合法组合：

```text
--content-source notion + --build-source markdown
--content-source notion + --build-source notion
--content-source json   + --build-source markdown
--content-source yaml   + --build-source markdown
```

非法组合：

```text
--content-source json + --build-source notion
--content-source yaml + --build-source notion
```

---

## 3. 顶层结构

允许的顶层字段：

```yaml
site:
content:
collections:
build:
theme:
```

常用字段：

```yaml
site:
  title:
  baseUrl:
  language:

content:
  provider:
  markdown:
  notion:
  sources:

collections:

build:
  output:
  clean:

theme:
  name:
```

---

## 4. `site` 节点

| 字段 | 类型 | 必需 | 示例 | 说明 |
|---|---|---:|---|---|
| `site.title` | string | 是 | `丝路商讯` | 站点名称 |
| `site.baseUrl` | string | 否 | `https://example.com` | 正式 URL |
| `site.language` | string | 否 | `zh` | 默认语言 |

示例：

```yaml
site:
  title: 丝路商讯
  baseUrl: https://example.com
  language: zh
```

禁止：

```yaml
site:
  base_url: https://example.com
  lang: zh
  theme: silkroadbiz
```

---

## 5. `content` 节点

`content` 必须使用以下三种模式之一：

```text
Markdown Provider
Notion Single Database Provider
Notion Multi-source Provider
```

---

## 5.1 Markdown Provider 模式

用于本地预览与早期验证。

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
```

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `content.provider` | string | 是 | 必须为 `markdown` |
| `content.markdown.dir` | string | 是 | 相对于站点目录的内容目录 |
| `content.markdown.defaultType` | string | 否 | 默认内容类型 |

路径规则：

```text
content.markdown.dir: content
```

表示：

```text
sites/<site-name>/content/
```

不要生成：

```yaml
dir: sites/silkroadbiz/content
dir: ./sites/silkroadbiz/content
dir: ../content
```

---

## 5.2 Notion 单数据库模式

用于小型站点或统一内容库。

```yaml
content:
  provider: notion
  notion:
    databaseId: ${NOTION_DATABASE_ID}
    tokenEnv: NOTION_TOKEN
    filterProperty: Published
    filterType: checkbox_true
    sortProperty: Title
    sortDirection: ascending
```

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `content.provider` | string | 是 | 必须为 `notion` |
| `content.notion.databaseId` | string | 是 | Notion database ID 或环境变量 |
| `content.notion.tokenEnv` | string | 是 | Notion token 环境变量名 |
| `content.notion.filterProperty` | string | 否 | 默认 `Published` |
| `content.notion.filterType` | string | 否 | 默认 `checkbox_true` |
| `content.notion.sortProperty` | string | 否 | 默认 `Title` |
| `content.notion.sortDirection` | string | 否 | `ascending` 或 `descending` |

禁止：

```yaml
content:
  notionDatabaseId: xxx
```

```yaml
content:
  provider: notion
  database:
    id: xxx
```

---

## 5.3 Notion 多数据库模式

用于正式 CMS 化。

```yaml
content:
  sources:
    - type: notion
      name: pages
      mode: content
      collection: page
      notion:
        databaseId: ${NOTION_PAGES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - type: notion
      name: posts
      mode: content
      collection: post
      notion:
        databaseId: ${NOTION_POSTS_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending
```

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `content.sources` | array | 是 | 多内容源数组 |
| `sources[].type` | string | 是 | 当前使用 `notion` |
| `sources[].name` | string | 是 | 唯一源名称 |
| `sources[].mode` | string | 否 | 默认 `content` |
| `sources[].collection` | string | 是 | Bukit collection |
| `sources[].notion.databaseId` | string | 是 | Notion database ID |
| `sources[].notion.tokenEnv` | string | 是 | Token 环境变量 |

推荐 source 名称：

```text
pages
posts
companies
services
```

推荐 collection：

```text
page
post
company
service
```

禁止：

```yaml
content:
  provider: notion
  sources:
    - type: notion
```

---

## 6. `collections` 节点

`collections` 用于声明集合路由、模板与 permalink 规则。只有当项目需要显式集合配置时才生成。

示例：

```yaml
collections:
  post:
    listRoute: /insights/
    listTemplate: pages/insights.html
    detailTemplate: pages/article.html
    permalink: /insights/{slug}/

  company:
    listRoute: /companies/
    listTemplate: pages/companies.html
    detailTemplate: pages/company.html
    permalink: /companies/{slug}/
```

规则：

- `permalink` 必须包含 `{slug}`
- `listTemplate` 与 `detailTemplate` 必须指向真实模板
- collection 名称必须与内容数据一致

---

## 7. `build` 节点

```yaml
build:
  output: dist
  clean: true
```

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `build.output` | string | 否 | 输出目录 |
| `build.clean` | boolean | 否 | 构建前是否清理输出目录 |

禁止：

```yaml
build:
  outDir: dist
  cleanOutput: yes
```

---

## 8. `theme` 节点

```yaml
theme:
  name: silkroadbiz
```

| 字段 | 类型 | 必需 | 说明 |
|---|---|---:|---|
| `theme.name` | string | 是 | 主题名称 |

禁止：

```yaml
theme:
  path: themes/silkroadbiz
```

---

## 9. 合法组合矩阵

| 场景 | content-source | build-source | site.yaml |
|---|---|---|---|
| 本地预览 | notion | markdown | `content.provider: markdown` |
| 本地预览 | json | markdown | `content.provider: markdown` |
| 本地预览 | yaml | markdown | `content.provider: markdown` |
| Notion 单库 | notion | notion | `content.provider: notion` |
| Notion 多库 | notion | notion | `content.sources` |
| 非法 | json | notion | 不允许 |
| 非法 | yaml | notion | 不允许 |

---

## 10. AI 生成前必须确认

AI 生成 `site.yaml` 前必须说明：

```text
当前选择哪个 Profile？
构建源是 markdown 还是 notion？
内容 seed 是 notion/json/yaml？
是否单数据库 Notion？
是否多数据库 Notion？
是否需要 content.sources？
是否需要 collections？
theme.name 是什么？
content 路径是否相对于 site 目录？
```

---

## 11. 验证命令

生成后必须运行：

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

如支持配置校验，可运行：

```bash
bukit config validate --config sites/<site-name>/site.yaml
```

---

## 12. 常见错误

### 错误 1：路径写成绝对路径

```yaml
markdown:
  dir: /Users/demo/sites/silkroadbiz/content
```

应改为：

```yaml
markdown:
  dir: content
```

### 错误 2：混用 provider 和 sources

```yaml
content:
  provider: notion
  sources: []
```

应二选一。

### 错误 3：collection 名称错误

```yaml
collection: articles
```

应使用：

```yaml
collection: post
```

### 错误 4：Notion 环境变量乱命名

```yaml
databaseId: ${PAGES_DB}
```

应使用：

```yaml
databaseId: ${NOTION_PAGES_DATABASE_ID}
```

### 错误 5：模板路径错误

```yaml
listTemplate: insights.html
```

建议使用：

```yaml
listTemplate: pages/insights.html
```
