# Bukit 修复完成后的下一步路线图与 Codex 执行文档

> 适用项目：`ALi365-SDN-BHD/bukit`  
> 使用场景：在 Build Core Hardening、路由安全、主题继承、增量构建、插件安全等 bug 修复完成后，继续推进 Bukit 的工程化、平台化和 AI 化能力。  
> 建议执行方式：交给 Codex 分阶段修复与实现，每个阶段单独创建分支、补测试、跑 CI、提交 PR。

---

## 1. 总体判断

当前 Bukit 已经具备静态站点生成器的核心能力，包括：

- Notion / Markdown 内容源
- 主题系统
- 模板渲染
- 静态文件输出
- Sitemap / RSS / SEO 基础能力
- 插件机制
- GitHub Pages / Cloudflare Pages 等部署方向
- AI Agent / GEO 优化的产品定位

前一阶段已经重点修复了：

- 路由安全问题
- Static HTML URL 错误
- 主题 parent / child 继承覆盖顺序
- 增量构建 stale file 残留
- media 嵌套文件复制问题
- remote theme checkout 失败问题
- git timeout 问题
- process plugin 安全边界问题
- output path traversal 问题

下一步不建议立刻继续堆功能，而是进入：

```text
Core 验收
  ↓
Build Report
  ↓
Pipeline 模块化
  ↓
Theme Componentization
  ↓
Content Schema Layer
  ↓
SEO / GEO Audit
  ↓
BukitJalil 本地 App
  ↓
AI Agent 自动建站
```

---

# 2. 第一阶段：Core Hardening 验收

## 2.1 目标

确认前一阶段 bug 修复没有引入回归问题，并建立长期可运行的回归测试矩阵。

## 2.2 必须验证的能力

```text
Route 安全
Static HTML 路由
Theme parent / child 继承
Asset / media 同步
Incremental build 删除清理
Plugin 安全边界
Remote theme lock
Native AOT publish
多语言构建
多 jobs 并发构建
```

## 2.3 建议测试命令

```bash
dotnet test

dotnet publish src/Bukit.Cli/Bukit.Cli.csproj \
  -c Release \
  -p:PublishAot=true

bukit build --clean
bukit build
bukit build --jobs 1
bukit build --jobs 8
```

## 2.4 建议新增 examples 测试站

```text
examples/
  blog-site/
  corporate-site/
  docs-site/
  notion-site/
  multilingual-site/
  plugin-site/
  theme-inheritance-site/
```

每个 example 都应该能执行：

```bash
bukit build --clean
bukit build
```

并通过 snapshot / file existence 检查。

## 2.5 验收标准

- `dotnet test` 全部通过
- Native AOT publish 成功
- 所有 example site 可构建
- 删除内容后 dist 中旧页面不会残留
- 修改 parent theme / child theme / root layout override 后增量构建能正确刷新
- 不安全 slug 会在 route validation 阶段失败
- 插件 timeout / stdout 超限 / 非法 outputPath 会被阻断

---

# 3. 第二阶段：Build Report 系统

## 3.1 目标

让 Bukit 每次构建都生成可审计、可被 UI 读取、可被 AI 判断的构建报告。

这是 Bukit 从 CLI 工具升级为平台内核的关键一步。

## 3.2 输出目录

建议在每次构建后生成：

```text
dist/.bukit/
  build-report.json
  routes.json
  assets.json
  security-report.json
  seo-report.json
  incremental-manifest.json
```

## 3.3 build-report.json 内容建议

```json
{
  "version": "0.1.0",
  "startedAt": "2026-05-25T00:00:00Z",
  "endedAt": "2026-05-25T00:00:02Z",
  "durationMs": 2100,
  "environment": {
    "os": "linux",
    "runtime": ".NET",
    "aot": true
  },
  "project": {
    "root": ".",
    "output": "dist",
    "contentSource": "notion",
    "theme": "silkroadbiz"
  },
  "summary": {
    "pageCount": 128,
    "routeCount": 128,
    "assetCount": 34,
    "mediaCount": 18,
    "pluginCount": 2,
    "warningCount": 1,
    "errorCount": 0
  },
  "incremental": {
    "enabled": true,
    "cacheHitCount": 112,
    "cacheMissCount": 16
  },
  "generatedFiles": [
    "index.html",
    "sitemap.xml",
    "rss.xml",
    "search-index.json"
  ]
}
```

## 3.4 routes.json 内容建议

```json
[
  {
    "url": "/",
    "outputPath": "index.html",
    "template": "index.sbn",
    "source": "content/pages/home.md",
    "kind": "page",
    "language": "en"
  },
  {
    "url": "/blog/hello/",
    "outputPath": "blog/hello/index.html",
    "template": "post.sbn",
    "source": "content/posts/hello.md",
    "kind": "post",
    "language": "en"
  }
]
```

## 3.5 assets.json 内容建议

```json
[
  {
    "path": "assets/css/main.css",
    "source": "themes/default/assets/css/main.css",
    "hash": "sha256:...",
    "size": 12345
  }
]
```

## 3.6 security-report.json 内容建议

```json
{
  "status": "passed",
  "warnings": [],
  "errors": [],
  "checks": {
    "routeTraversal": "passed",
    "unsafeSlug": "passed",
    "pluginOutputPath": "passed",
    "remoteThemeLock": "passed"
  }
}
```

## 3.7 seo-report.json 内容建议

```json
{
  "pages": 128,
  "missingTitle": 0,
  "missingDescription": 2,
  "missingCanonical": 0,
  "missingOgImage": 4,
  "duplicateCanonical": 0,
  "sitemapGenerated": true,
  "rssGenerated": true,
  "robotsGenerated": true,
  "llmsTxtGenerated": true
}
```

## 3.8 Codex 执行 Prompt

```markdown
# Codex Task: Implement Bukit Build Report System

## Goal

After build-core-hardening bug fixes, implement a formal build reporting system for Bukit.

## Requirements

1. Generate the following files after every successful build:

   - dist/.bukit/build-report.json
   - dist/.bukit/routes.json
   - dist/.bukit/assets.json
   - dist/.bukit/security-report.json
   - dist/.bukit/seo-report.json

2. Add a BuildResult model that contains:

   - startedAt
   - endedAt
   - durationMs
   - Bukit version
   - environment info
   - content source type
   - theme name
   - theme source
   - page count
   - route count
   - asset count
   - media count
   - plugin count
   - warning count
   - error count
   - incremental build enabled
   - incremental cache hit count
   - generated files

3. Refactor SiteEngine.BuildAsync so it returns BuildResult.

4. Do not break existing CLI behavior.

5. Add tests for:

   - build-report.json is generated
   - routes.json contains all rendered routes
   - assets.json contains copied assets
   - security-report.json contains route security results
   - seo-report.json contains SEO audit results
   - report output is deterministic enough for tests

6. Ensure Native AOT compatibility.

7. Avoid reflection-heavy JSON serialization patterns unless already supported by the project.
```

---

# 4. 第三阶段：Build Pipeline 模块化

## 4.1 目标

逐步拆分 `SiteEngine`，降低构建核心复杂度，为后续主题组件化、AI 控制面、可视化构建报告做准备。

## 4.2 推荐模块

```text
Bukit.Engine/
  BuildPlanner.cs
  BuildPipeline.cs
  ContentPipeline.cs
  ThemePipeline.cs
  RoutePipeline.cs
  RenderPipeline.cs
  AssetPipeline.cs
  SeoPipeline.cs
  PluginPipeline.cs
  BuildReporter.cs
```

## 4.3 推荐流程

```text
BuildPlanner
  ↓
ContentPipeline
  ↓
ThemePipeline
  ↓
RoutePipeline
  ↓
RenderPipeline
  ↓
AssetPipeline
  ↓
SeoPipeline
  ↓
PluginPipeline
  ↓
BuildReporter
```

## 4.4 每个模块职责

| 模块 | 职责 |
|---|---|
| BuildPlanner | 合并 CLI 参数、site.yaml、默认配置 |
| ContentPipeline | 加载 Notion / Markdown / JSON 内容 |
| ThemePipeline | 解析主题、本地主题、远程主题、parent theme |
| RoutePipeline | 生成 URL / outputPath，并进行安全校验 |
| RenderPipeline | 渲染页面，处理模板、layout、section |
| AssetPipeline | 复制 assets/static/media，清理 stale files |
| SeoPipeline | 生成 sitemap、rss、robots、llms.txt、SEO 报告 |
| PluginPipeline | 执行插件 hooks，收集插件输出 |
| BuildReporter | 生成 build-report 和相关 JSON 报告 |

## 4.5 Codex 执行 Prompt

```markdown
# Codex Task: Refactor Bukit SiteEngine into Build Pipeline

## Goal

Refactor the current SiteEngine into a modular build pipeline without changing external CLI behavior.

## Requirements

1. Keep existing build behavior compatible.
2. Introduce BuildContext and BuildResult if not already present.
3. Split SiteEngine responsibilities into pipeline stages:

   - BuildPlanner
   - ContentPipeline
   - ThemePipeline
   - RoutePipeline
   - RenderPipeline
   - AssetPipeline
   - SeoPipeline
   - PluginPipeline
   - BuildReporter

4. Each pipeline stage should be independently testable.
5. Do not introduce unnecessary abstractions.
6. Do not break Native AOT compatibility.
7. Add tests for pipeline stage execution order.
8. Existing tests must continue to pass.
9. Avoid broad rewrites. Prefer incremental extraction.
```

---

# 5. 第四阶段：Theme Componentization

## 5.1 目标

将 Bukit 主题系统从传统的 `layouts + assets + static`，升级为支持 section schema、组件化、AI 可编辑的主题系统。

## 5.2 推荐主题结构

```text
themes/silkroadbiz/
  theme.yaml
  tokens.yaml

  layouts/
    base.sbn
    index.sbn
    page.sbn
    post.sbn

  sections/
    hero/
      hero.sbn
      hero.schema.yaml
      hero.preview.json

    features/
      features.sbn
      features.schema.yaml
      features.preview.json

    cta/
      cta.sbn
      cta.schema.yaml
      cta.preview.json

  components/
    button.sbn
    card.sbn
    nav.sbn
    footer.sbn

  assets/
    css/
    js/
    images/

  static/
```

## 5.3 section schema 示例

```yaml
name: hero
label: Hero Section

props:
  title:
    type: string
    required: true

  subtitle:
    type: string
    required: false

  primaryButtonText:
    type: string

  primaryButtonUrl:
    type: url

  backgroundImage:
    type: image
```

## 5.4 section preview 示例

```json
{
  "title": "Professional ESD Services in Malaysia",
  "subtitle": "End-to-end expatriate service support for growing companies.",
  "primaryButtonText": "Get Consultation",
  "primaryButtonUrl": "/contact/",
  "backgroundImage": "/assets/images/hero.jpg"
}
```

## 5.5 设计原则

AI 不应该直接修改 HTML / Scriban 模板。

AI 应该优先修改：

```text
site.yaml
tokens.yaml
page structure
section props
collections.yaml
notion mapping
```

这样可以保证：

- 渲染结果可控
- 模板可维护
- AI 输出可校验
- BukitJalil UI 可视化编辑更容易
- 主题市场更容易建立

## 5.6 Codex 执行 Prompt

```markdown
# Codex Task: Implement Theme Componentization Foundation

## Goal

Add section-based theme component support to Bukit.

## Requirements

1. Support theme sections under:

   themes/{themeName}/sections/{sectionName}/

2. Each section may include:

   - {sectionName}.sbn
   - {sectionName}.schema.yaml
   - {sectionName}.preview.json

3. Add a SectionDefinition model.

4. Add a ThemeComponentRegistry that discovers available sections.

5. Validate section schemas during build.

6. Allow pages to reference sections through structured config.

7. Do not remove existing layout behavior.

8. Existing themes must continue to work.

9. Add tests for:

   - section discovery
   - section schema validation
   - missing section template error
   - invalid section props error
   - parent theme section inheritance
   - child theme section override

10. Ensure Native AOT compatibility.
```

---

# 6. 第五阶段：Content Schema Layer

## 6.1 目标

统一 Notion、Markdown、JSON、YAML、AI 生成内容的字段定义和校验逻辑。

## 6.2 推荐新增文件

```text
collections.yaml
```

## 6.3 collections.yaml 示例

```yaml
collections:
  posts:
    label: Articles
    source: notion

    fields:
      title:
        type: string
        required: true

      slug:
        type: slug
        required: true

      summary:
        type: text

      cover:
        type: image

      publishAt:
        type: date

      tags:
        type: string[]

      author:
        type: string

  companies:
    label: Companies
    source: notion

    fields:
      name:
        type: string
        required: true

      country:
        type: enum
        options:
          - China
          - Malaysia

      industry:
        type: string

      logo:
        type: image

      description:
        type: text
```

## 6.4 核心价值

| 能力 | 作用 |
|---|---|
| 字段校验 | 防止 Notion 缺字段导致构建失败 |
| AI 内容生成 | AI 可以按 schema 生成内容 |
| UI 表单生成 | BukitJalil 可以自动生成编辑界面 |
| 模板安全 | 模板知道字段类型 |
| SEO 自动化 | title、summary、cover、date 可统一处理 |
| 多语言 | 每个字段可以支持 i18n |

## 6.5 Codex 执行 Prompt

```markdown
# Codex Task: Add Content Schema Layer

## Goal

Introduce collections.yaml to define structured content schemas for Bukit.

## Requirements

1. Add support for collections.yaml at project root.

2. Define models:

   - CollectionDefinition
   - FieldDefinition
   - FieldType
   - ContentValidationResult

3. Supported field types:

   - string
   - text
   - slug
   - url
   - image
   - date
   - boolean
   - number
   - enum
   - string[]

4. Validate content items against collection schemas.

5. Support both Notion and Markdown content items.

6. Build should fail on required field missing.

7. Build should warn on unknown fields unless configured otherwise.

8. Add content validation output to:

   dist/.bukit/build-report.json

9. Add tests for:

   - missing required field
   - invalid slug field
   - invalid enum value
   - unknown field warning
   - Notion field mapping
   - Markdown frontmatter validation

10. Keep existing content behavior backward-compatible when collections.yaml is absent.
```

---

# 7. 第六阶段：SEO / GEO / LLMs 优化系统

## 7.1 目标

将 Bukit 的 SEO / GEO 能力产品化，变成构建过程中的一等公民。

## 7.2 推荐模块

```text
Bukit.Seo/
  SitemapGenerator
  RssGenerator
  RobotsGenerator
  LlmTxtGenerator
  JsonLdGenerator
  SeoAuditor
  GeoAuditor
```

## 7.3 推荐支持文件

```text
sitemap.xml
rss.xml
robots.txt
llms.txt
llms-full.txt
search-index.json
schema-org.json
```

## 7.4 SEO Audit 检查项

```text
missing title
missing description
missing canonical
missing og:title
missing og:description
missing og:image
duplicate slug
duplicate canonical
invalid sitemap URL
invalid robots.txt
missing lang
missing hreflang
invalid JSON-LD
```

## 7.5 GEO Audit 检查项

```text
是否存在 llms.txt
是否存在 llms-full.txt
页面是否有清晰摘要
页面是否有结构化 FAQ
页面是否有 Organization / Article / Breadcrumb JSON-LD
页面是否有明确更新时间
页面是否有作者或组织实体
重要页面是否可被 AI 摘要
```

## 7.6 Codex 执行 Prompt

```markdown
# Codex Task: Implement SEO / GEO Audit System

## Goal

Make SEO and GEO audit a first-class part of Bukit build.

## Requirements

1. Generate or validate:

   - sitemap.xml
   - rss.xml
   - robots.txt
   - llms.txt
   - llms-full.txt
   - search-index.json

2. Add SeoAuditResult and GeoAuditResult models.

3. Output reports to:

   - dist/.bukit/seo-report.json
   - dist/.bukit/geo-report.json

4. Add checks for:

   - missing title
   - missing description
   - missing canonical
   - missing og image
   - duplicate canonical
   - invalid sitemap URL
   - missing lang
   - missing hreflang
   - invalid JSON-LD
   - missing llms.txt
   - missing page summary

5. Add config options:

   seo:
     failOnError: true
     failOnWarning: false

   geo:
     enabled: true
     generateLlmsTxt: true
     generateLlmsFullTxt: true

6. Add tests for all major audit checks.

7. Keep existing SEO output backward-compatible.
```

---

# 8. 第七阶段：BukitJalil 本地 App

## 8.1 目标

在 Bukit Core 稳定、Build Report 完成、主题组件化和 Content Schema 完成后，再启动 BukitJalil。

BukitJalil 不建议一开始做 SaaS，建议先做本地运行 App。

## 8.2 MVP 功能

```text
1. 打开 Bukit 项目
2. 读取 site.yaml
3. 读取 collections.yaml
4. 读取 theme.yaml
5. 读取 build-report.json
6. 选择主题
7. 修改项目配置
8. 运行 bukit build
9. 本地 preview
10. 查看 routes / SEO / errors
11. 一键发布 GitHub Pages / Cloudflare Pages
```

## 8.3 本地 App 架构

```text
BukitJalil App
  ↓
Project Manager
  ↓
Theme Manager
  ↓
Content Schema Viewer
  ↓
AI Command Panel
  ↓
Bukit CLI Runner
  ↓
Build Report Viewer
  ↓
Preview Server
  ↓
Deploy Wizard
```

## 8.4 AI 操作原则

AI 优先修改结构化文件：

```text
site.yaml
collections.yaml
theme.yaml
tokens.yaml
page.schema.json
section props json
notion mapping
```

AI 不应优先修改：

```text
*.sbn
*.cs
*.css
*.js
```

除非进入高级开发模式。

---

# 9. 第八阶段：AI Agent 自动建站

## 9.1 目标

建立一个 AI 可控、可验证、可回滚的自动建站流程。

## 9.2 推荐 Workflow

```text
用户输入需求
  ↓
AI 生成 Site Intent
  ↓
AI 生成 collections.yaml
  ↓
AI 选择 theme
  ↓
AI 生成 page structure
  ↓
AI 生成 section props
  ↓
Bukit build
  ↓
Playwright 截图
  ↓
AI 根据截图调整配置
  ↓
输出静态网站
```

## 9.3 Site Intent 示例

```yaml
site:
  name: Silkroad Business
  type: business-news
  languages:
    - zh
    - en

goals:
  - publish business news
  - list China and Malaysia companies
  - support GEO optimization

pages:
  - home
  - insights
  - companies
  - china-companies
  - malaysia-companies
  - about
  - contact

collections:
  - insights
  - companies

theme:
  style: modern-business
  density: high
  colors:
    primary: warm-red
    accent: gold
```

---

# 10. 推荐分支与 Commit 规划

## 10.1 分支规划

```text
feature/build-report
feature/build-pipeline
feature/theme-componentization
feature/content-schema
feature/seo-geo-audit
feature/bukitjalil-local-app
feature/ai-site-intent
```

## 10.2 Commit 建议

```text
feat(engine): add BuildResult model
feat(engine): generate build-report output
feat(engine): generate route and asset reports
refactor(engine): extract build planner from SiteEngine
refactor(engine): extract route pipeline
refactor(engine): extract asset pipeline
feat(theme): add section discovery
feat(theme): add section schema validation
feat(content): add collections schema layer
feat(seo): add SEO audit report
feat(geo): add llms.txt and geo audit report
```

---

# 11. 总体验收标准

完成以上阶段后，Bukit 应该具备以下能力：

## 11.1 工程稳定性

- Build core 可测试
- Pipeline 可拆分
- BuildResult 可审计
- Native AOT 可发布
- CI 可稳定运行
- Example sites 可持续验证

## 11.2 主题能力

- 支持主题继承
- 支持 section component
- 支持 section schema
- 支持 section preview
- 支持 child theme override
- 支持 theme lock

## 11.3 内容能力

- 支持 collections.yaml
- 支持 Notion 字段映射
- 支持 Markdown frontmatter 校验
- 支持内容字段类型校验
- 支持内容错误报告

## 11.4 SEO / GEO 能力

- 自动生成 sitemap
- 自动生成 rss
- 自动生成 robots
- 自动生成 llms.txt
- 自动生成 llms-full.txt
- 输出 SEO / GEO audit report

## 11.5 AI 友好性

- AI 可读取 build-report
- AI 可读取 routes.json
- AI 可读取 schema
- AI 可修改 section props
- AI 可通过结构化配置生成网站
- AI 不需要直接修改模板代码

---

# 12. 最终建议

Bukit 的下一步不应该是简单增加更多模板或更多内容源，而是先把它变成一个：

```text
可审计
可测试
可扩展
可被 AI 操作
可做主题市场
可做本地 App 控制面
```

的静态网站生成平台内核。

建议按以下顺序推进：

```text
1. Core Hardening 验收
2. Build Report
3. Build Pipeline 模块化
4. Theme Componentization
5. Content Schema Layer
6. SEO / GEO Audit
7. BukitJalil 本地 App
8. AI Agent 自动建站
```

只要这个顺序稳定执行，Bukit 就可以从一个静态站点生成 CLI，升级为面向 AI Agent 与 GEO 优化的新一代 .NET Native AOT 静态网站生成平台。
