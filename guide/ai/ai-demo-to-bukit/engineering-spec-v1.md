# Bukit AI Demo-to-CMS 工程化规范

## 1. 目标

本规范用于指导 ChatGPT、Codex、Cursor、Trae 或其他 AI Agent，根据用户的网站需求，先生成可视化 HTML Demo，待用户确认样式、页面结构、功能与文案方向后，再将最终 Demo 工程化转换为 Bukit 主题模板、内容数据、Notion seed、site.yaml、notion-database-map.yaml 等可构建文件。

目标不是生成一次性 HTML 页面，而是形成一套可持续维护、可数据化、可 CMS 化、可构建发布的 Bukit 网站工程。

标准流程如下：

```text
用户需求
→ AI 生成网站规划
→ AI 生成 HTML Demo
→ 用户确认视觉与功能
→ AI/Bukit 拆分为主题模板与数据
→ 生成 Notion seed 与 database map
→ Bukit build / doctor 验证
→ Notion push
→ build-source notion 正式 CMS 化
→ 静态发布
```

---

## 2. 适用场景

本规范适用于：

* 企业官网
* 行业资讯站
* 企业目录站
* 产品展示站
* 招商落地页
* 本地服务站
* 多语言内容站
* SEO/GEO 内容站
* Notion 作为 CMS 的静态网站

不适用于：

* 高度交互型 SaaS Web App
* 依赖复杂前端状态管理的系统
* 大量用户登录、后台权限、在线交易类系统
* 主要依赖客户端 JS 动态渲染的网站

---

## 3. 核心原则

### 3.1 先 Demo，后工程化

AI 不应一开始直接生成最终 Bukit 工程，除非用户明确要求。

推荐优先流程：

```text
先生成 HTML Demo
→ 用户确认设计
→ 再转换 Bukit 工程
```

这样可以降低返工成本，让用户先确认视觉风格、布局、栏目和内容方向。

---

### 3.2 Demo 必须可迁移

HTML Demo 不是一次性页面，而是 Bukit import / AI conversion 的输入。

Demo 必须满足：

* 页面文件独立
* HTML 结构语义化
* class 命名标准
* 列表和详情页分离
* 业务内容可抽取
* 图片、CSS、JS 本地化
* 必须生成 `demo.routes.yaml`
* 不依赖复杂运行时框架
* 不把业务文案写死在不可识别结构中

---

### 3.3 内容必须数据化

业务文案、文章、企业资料、服务信息、FAQ、SEO 信息，不应长期保留在模板中。

最终应拆分为：

```text
pages.json
posts.json
companies.json
services.json
sections.json
faqs.json
media.json
components.json
notion-database-map.yaml
```

其中默认 Notion push 范围为：

```text
pages
posts
companies
services
```

以下内容默认作为 review-only seed：

```text
sections
faqs
media
components
```

如需完整 CMS 化，应为这些集合设计独立 Notion schema。

---

### 3.4 主题只负责结构与表现

Bukit 主题模板中应保留：

* 页面布局
* 组件结构
* 样式 class
* 模板变量
* 循环逻辑
* include 逻辑

不应长期保留：

* 企业正文
* 文章正文
* SEO 文案
* FAQ 内容
* 服务详情
* 大段业务介绍

---

## 4. 工程目录规范

最终 Bukit 工程建议结构如下：

```text
project/
  demo/
    index.html
    insights.html
    article-detail.html
    companies.html
    china-companies.html
    malaysia-companies.html
    company-detail.html
    about.html
    contact.html
    join.html
    assets/
      css/
        style.css
      js/
        main.js
      images/

  demo.routes.yaml

  themes/
    <theme-name>/
      layouts/
        layouts/
          base.html
        pages/
          index.html
          insights.html
          article.html
          companies.html
          china-companies.html
          malaysia-companies.html
          company.html
          about.html
          contact.html
          join.html
        partials/
          header.html
          nav.html
          footer.html
        components/
          hero.html
          article-card.html
          company-card.html
          service-card.html
          faq.html
        bukit.templates.yaml
      assets/
        css/
        js/
        images/

  sites/
    <site-name>/
      site.yaml
      content/
        index.md
        pages/
        posts/
        companies/
        services/
      notion-seed/
        pages.json
        posts.json
        companies.json
        services.json
        sections.json
        faqs.json
        media.json
        components.json
        notion-database-map.yaml
      import-report.md
```

说明：

* `demo/` 是用户确认前的可视化阶段产物。
* `themes/` 是 Bukit 主题。
* `sites/` 是 Bukit 站点。
* `content/` 用于本地 Markdown review/build。
* `notion-seed/` 用于 Notion CMS 化。
* `import-report.md` 用于迁移审计。

---

## 5. 阶段一：需求采集规范

AI 在生成 Demo 前，应先提炼网站需求。

必须明确：

```text
网站名称
网站定位
目标用户
核心栏目
页面列表
视觉风格
语言
内容集合
是否需要 Notion CMS
是否需要多数据库 Notion
是否需要本地 preview
```

示例输出：

```text
站点名称：丝路商讯
定位：中国与马来西亚商务资讯与企业目录平台
核心入口：商务资讯、企业目录
页面：首页、资讯列表、资讯详情、企业列表、中国企业、马来西亚企业、企业详情、关于、联系、加入我们
内容集合：pages、posts、companies、services、faqs、sections
视觉风格：现代商务、深蓝金色、国际化
CMS：Notion 多数据库
构建模式：先 markdown preview，后 notion build
```

---

## 6. 阶段二：HTML Demo 生成规范

### 6.1 Demo 文件要求

AI 必须生成独立 HTML 文件：

```text
index.html
insights.html
article-detail.html
companies.html
china-companies.html
malaysia-companies.html
company-detail.html
about.html
contact.html
join.html
```

如果某页面不需要，可以省略，但必须在说明中明确。

---

### 6.2 HTML 基础结构

每个 HTML 文件必须包含：

```html
<!doctype html>
<html lang="zh">
<head>
  <meta charset="utf-8">
  <title>页面标题</title>
  <meta name="description" content="页面 SEO 描述">
  <link rel="stylesheet" href="assets/css/style.css">
</head>
<body data-page-type="Page">
  <header class="site-header"></header>
  <nav class="site-nav"></nav>
  <main></main>
  <footer class="site-footer"></footer>
  <script src="assets/js/main.js"></script>
</body>
</html>
```

---

### 6.3 页面类型规范

页面必须通过 `data-page-type` 或 route-map 标明类型。

允许类型：

```text
Home
Page
PostList
PostDetail
CompanyList
CompanyDetail
ServiceList
ServiceDetail
Contact
Join
```

---

### 6.4 标准 class 规范

文章卡片：

```html
<article class="article-card" data-collection="posts">
  <img data-field="cover" src="assets/images/news-1.jpg" alt="文章封面">
  <span data-field="category">商务资讯</span>
  <h3 data-field="title">文章标题</h3>
  <p data-field="summary">文章摘要</p>
  <a data-field="url" href="article-detail.html">阅读详情</a>
</article>
```

企业卡片：

```html
<article class="company-card" data-collection="companies">
  <img data-field="logo" src="assets/images/company-1.png" alt="企业 Logo">
  <h3 data-field="title">企业名称</h3>
  <p data-field="summary">企业简介</p>
  <span data-field="country">Malaysia</span>
  <span data-field="industry">Technology</span>
  <a data-field="url" href="company-detail.html">查看企业</a>
</article>
```

服务卡片：

```html
<article class="service-card" data-collection="services">
  <h3 data-field="title">服务名称</h3>
  <p data-field="summary">服务简介</p>
  <a data-field="url" href="service-detail.html">了解服务</a>
</article>
```

FAQ：

```html
<div class="faq-item" data-collection="faqs">
  <h3 data-field="question">常见问题</h3>
  <p data-field="answer">问题答案。</p>
</div>
```

---

## 7. 阶段三：route-map 规范

AI 必须生成 `demo.routes.yaml`。

示例：

```yaml
pages:
  - source: index.html
    route: /
    type: Home
    template: index

  - source: insights.html
    route: /insights/
    type: PostList
    template: insights

  - source: article-detail.html
    route: /insights/{slug}/
    type: PostDetail
    template: article

  - source: companies.html
    route: /companies/
    type: CompanyList
    template: companies

  - source: china-companies.html
    route: /china-companies/
    type: CompanyList
    template: china-companies

  - source: malaysia-companies.html
    route: /malaysia-companies/
    type: CompanyList
    template: malaysia-companies

  - source: company-detail.html
    route: /companies/{slug}/
    type: CompanyDetail
    template: company

  - source: about.html
    route: /about/
    type: Page
    template: about

  - source: contact.html
    route: /contact/
    type: Page
    template: contact

  - source: join.html
    route: /join/
    type: Page
    template: join
```

要求：

* 每个 HTML 文件必须出现在 route-map 中。
* route 必须以 `/` 开头。
* 动态详情页使用 `{slug}`。
* 列表页和详情页必须分开。
* template 名必须稳定，不允许随机变化。
* source 必须对应真实 HTML 文件。

---

## 8. 阶段四：用户确认规范

用户确认 Demo 时，应确认以下项目：

```text
视觉风格
页面完整性
导航结构
首页区块
列表卡片
详情页结构
移动端体验
核心 CTA
文案方向
图片风格
URL 结构
内容集合
```

确认后，Demo 进入工程化转换阶段。

在用户确认前，不应直接进入最终 Bukit 工程生成阶段。

---

## 9. 阶段五：Demo 转 Bukit 工程规范

### 9.1 拆分规则

| Demo 部分           | Bukit 目标                                        |
| ----------------- | ----------------------------------------------- |
| 公共 header         | `layouts/partials/header.html`                  |
| 公共 nav            | `layouts/partials/nav.html`                     |
| 公共 footer         | `layouts/partials/footer.html`                  |
| 首页                | `layouts/pages/index.html`                      |
| 列表页               | `layouts/pages/insights.html`, `companies.html` |
| 详情页               | `layouts/pages/article.html`, `company.html`    |
| 重复卡片              | `layouts/components/*.html`                     |
| CSS/JS/图片         | `themes/<theme>/assets/`                        |
| 页面正文              | `notion-seed/pages.json`                        |
| 文章数据              | `notion-seed/posts.json`                        |
| 企业数据              | `notion-seed/companies.json`                    |
| 服务数据              | `notion-seed/services.json`                     |
| FAQ/section/media | review-only seed                                |

---

### 9.2 模板变量规范

详情页应使用：

```html
<h1>{{ page.title }}</h1>
<p>{{ page.summary }}</p>
<div class="content">
  {{ page.content }}
</div>
```

列表页应使用：

```html
{{ for item in pages }}
  {{ include "components/article-card.html" }}
{{ end }}
```

组件应使用：

```html
<article class="company-card">
  <h3>{{ item.title }}</h3>
  <p>{{ item.summary }}</p>
  <span>{{ item.country }}</span>
  <span>{{ item.industry }}</span>
  <a href="{{ item.url }}">查看企业</a>
</article>
```

---

## 10. 阶段六：内容数据规范

### 10.1 pages.json

```json
[
  {
    "title": "关于我们",
    "slug": "about",
    "type": "Page",
    "template": "about",
    "summary": "平台介绍。",
    "content": "<p>页面正文。</p>",
    "seoTitle": "关于我们",
    "seoDescription": "了解平台定位、服务与愿景。",
    "published": true
  }
]
```

---

### 10.2 posts.json

```json
[
  {
    "title": "马来西亚数字经济发展趋势",
    "slug": "malaysia-digital-economy",
    "summary": "解析马来西亚数字经济政策与企业机会。",
    "content": "<p>文章正文。</p>",
    "tags": ["数字经济", "马来西亚"],
    "cover": "assets/images/news-1.jpg",
    "seoTitle": "马来西亚数字经济发展趋势",
    "seoDescription": "了解马来西亚数字经济发展机会。",
    "published": true
  }
]
```

---

### 10.3 companies.json

```json
[
  {
    "title": "ALi365 SDN BHD",
    "slug": "ali365",
    "summary": "专注企业数字化、AI、网站建设与跨境商务服务。",
    "country": "Malaysia",
    "industry": "Technology",
    "logo": "assets/images/ali365.png",
    "website": "https://ali365.com.my",
    "published": true
  }
]
```

---

### 10.4 services.json

```json
[
  {
    "title": "企业网站建设",
    "slug": "website-development",
    "summary": "为企业提供官网、内容站、电商站建设服务。",
    "content": "<p>服务详情。</p>",
    "published": true
  }
]
```

---

## 11. 阶段七：Notion database map 规范

`notion-database-map.yaml` 示例：

```yaml
databases:
  pages:
    title: Pages
    databaseId: ""
    seed: pages.json
    collection: page
    uniqueField: Slug

  posts:
    title: Posts
    databaseId: ""
    seed: posts.json
    collection: post
    uniqueField: Slug

  companies:
    title: Companies
    databaseId: ""
    seed: companies.json
    collection: company
    uniqueField: Slug

  services:
    title: Services
    databaseId: ""
    seed: services.json
    collection: service
    uniqueField: Slug
```

说明：

* 空 databaseId 表示可由 Bukit 自动创建。
* `uniqueField` 默认使用 `Slug`。
* 默认 Notion push 只处理 pages/posts/companies/services。
* sections/faqs/media/components 默认为 review-only。

---

## 12. 阶段八：site.yaml 规范

### 12.1 本地预览模式

```yaml
site:
  title: 丝路商讯
  baseUrl: https://example.com
  language: zh

content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page

build:
  output: dist
  clean: true

theme:
  name: silkroadbiz
```

适用于：

```text
Demo 确认后
本地预览
不依赖 Notion token
快速 build 验证
```

---

### 12.2 Notion-only 多源模式

```yaml
site:
  title: 丝路商讯
  baseUrl: https://example.com
  language: zh

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

    - type: notion
      name: companies
      mode: content
      collection: company
      notion:
        databaseId: ${NOTION_COMPANIES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

    - type: notion
      name: services
      mode: content
      collection: service
      notion:
        databaseId: ${NOTION_SERVICES_DATABASE_ID}
        tokenEnv: NOTION_TOKEN
        filterProperty: Published
        filterType: checkbox_true
        sortProperty: Title
        sortDirection: ascending

build:
  output: dist
  clean: true

theme:
  name: silkroadbiz
```

---

## 13. 阶段九：推荐命令规范

### 13.1 第一次导入：本地预览

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source markdown \
  --route-map demo.routes.yaml \
  --strict warn \
  --force \
  --verify
```

---

### 13.2 推送到 Notion

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-map sites/silkroadbiz/notion-seed/notion-database-map.yaml \
  --create-missing-databases \
  --parent-page-id <notion-parent-page-id> \
  --mode upsert \
  --update-content replace
```

---

### 13.3 Notion-only 正式模式

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --content-source notion \
  --build-source notion \
  --route-map demo.routes.yaml \
  --force
```

---

## 14. 质量门禁规范

每次工程化转换后，必须执行：

```bash
bukit doctor --config sites/<site-name>/site.yaml
bukit build --config sites/<site-name>/site.yaml
```

发布前建议执行：

```bash
dotnet test
bash scripts/test-all.sh
bash scripts/quality-gate.sh
```

---

## 15. import-report 审查规范

必须检查：

```text
Pages
Content Seeds
Seed Push Scope
Build/Data Source Relationship
Hardcoded Content Residue
Diagnostics
Link Validation
Visual Verification
Manual Review Required
```

如果 `Hardcoded Content Residue` 过高，应回到 Demo 或模板阶段清理业务文案残留。

如果 `Diagnostics` 出现危险协议、外部脚本、无效内部链接，应修复后重新 import。

---

## 16. ChatGPT / Agent 执行规范

AI 在执行本流程时，必须遵守：

1. 不直接跳过 Demo 确认阶段。
2. 生成 Demo 时必须同步生成 route-map。
3. 用户确认前，不生成最终 Bukit 工程。
4. 转 Bukit 工程时，必须拆分 partials/components/pages。
5. 业务内容必须抽取到 seed。
6. 模板字段必须与 seed 字段一致。
7. 不生成复杂运行时 JS 依赖。
8. 不生成外部不可控资源依赖。
9. 输出后必须给出 build 命令。
10. 出现 build 错误时，应根据错误修复模板或配置。

---

## 17. 推荐 ChatGPT Prompt

```markdown
你是 Bukit AI Demo-to-CMS 工程助手。

请根据用户需求，先生成可预览 HTML Demo，而不是直接生成最终 Bukit 工程。

Demo 必须满足：
- 独立 HTML 页面
- assets 本地资源
- demo.routes.yaml
- 语义化 header/nav/main/section/footer
- 标准 class：article-card/company-card/service-card/faq-item
- data-field 标注
- 列表页和详情页分离
- 不依赖复杂运行时 JS

用户确认 Demo 后，再将最终 Demo 转换为 Bukit 工程：
- themes/<theme>/layouts/layouts/base.html
- themes/<theme>/layouts/pages/*.html
- themes/<theme>/layouts/partials/*.html
- themes/<theme>/layouts/components/*.html
- themes/<theme>/layouts/bukit.templates.yaml
- sites/<site>/site.yaml
- sites/<site>/notion-seed/*.json
- sites/<site>/notion-seed/notion-database-map.yaml
- sites/<site>/import-report.md

默认采用：
- content-source notion
- build-source markdown
- strict warn

最终给出：
- import 命令
- build 命令
- notion push 命令
- 人工检查清单
```

---

## 18. 人工检查清单

### Demo 阶段

```text
[ ] 页面是否完整
[ ] 风格是否符合需求
[ ] 首页结构是否确认
[ ] 导航是否确认
[ ] 资讯列表是否确认
[ ] 企业列表是否确认
[ ] 详情页是否确认
[ ] 移动端是否确认
[ ] CTA 是否确认
[ ] 文案方向是否确认
```

### 工程化阶段

```text
[ ] route-map 是否完整
[ ] 每个 HTML 是否有对应 route
[ ] 模板是否拆分 partials/components
[ ] 业务内容是否进入 seed
[ ] site.yaml 是否正确
[ ] notion-database-map 是否生成
[ ] content/ 是否可用于本地 build
[ ] notion-seed/ 是否可用于 push
[ ] bukit build 是否通过
[ ] bukit doctor 是否通过
```

### CMS 化阶段

```text
[ ] Notion database 是否创建
[ ] schema validate 是否通过
[ ] push report 是否无 failed
[ ] 页面内容是否在 Notion 中可编辑
[ ] build-source notion 是否可构建
[ ] dist 是否生成
```

---

## 19. 最终定义

Bukit AI Demo-to-CMS Workflow 是一套将 AI 生成 Demo 转换为正式静态站点工程的流程。

它的核心价值是：

```text
ChatGPT 负责设计与结构化
Bukit 负责工程化与构建
Notion 负责内容中台
```

最终目标：

```text
用户需求
→ 可视化 Demo
→ 用户确认
→ Bukit 主题
→ 内容数据
→ Notion CMS
→ 静态构建
→ 发布
```
