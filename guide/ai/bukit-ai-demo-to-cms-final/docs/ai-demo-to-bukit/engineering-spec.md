# Bukit AI Demo-to-CMS 工程化规范

## 1. 目标

本规范用于指导 AI 根据用户需求，先生成可视化 HTML Demo，待用户确认样式、页面结构、功能与文案方向后，再将最终 Demo 工程化转换为 Bukit 主题模板、内容数据、Notion seed、`site.yaml`、`notion-database-map.yaml` 等可构建文件。

标准流程：

```text
用户需求
→ AI 生成网站规划
→ AI 生成 HTML Demo
→ 用户确认视觉与功能
→ AI / Bukit 拆分为主题模板与数据
→ 生成 Notion seed 与 database map
→ Bukit build / doctor 验证
→ Notion push
→ build-source notion 正式 CMS 化
→ 静态发布
```

## 2. 适用范围

适用于：

- 企业官网
- 行业资讯站
- 企业目录站
- 产品展示站
- 招商落地页
- 本地服务站
- 多语言内容站
- SEO / GEO 内容站
- Notion 作为 CMS 的静态网站

不适用于：

- 高度交互型 SaaS Web App
- 依赖复杂前端状态管理的系统
- 大量用户登录、后台权限、在线交易类系统
- 主要依赖客户端 JavaScript 动态渲染的网站

## 3. 核心原则

### 3.1 先 Demo，后工程化

AI 不应在用户尚未确认视觉和功能时直接生成最终 Bukit 工程。

### 3.2 Demo 必须可迁移

Demo 必须满足：

- 页面文件独立
- HTML 结构语义化
- class 命名稳定
- 列表页与详情页分离
- 业务内容可抽取
- 图片、CSS、JS 本地化
- 必须生成 `demo.routes.yaml`
- 不依赖复杂运行时框架
- 不把业务文案写死在不可识别结构中

### 3.3 内容必须数据化

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

默认 Notion push 范围：

```text
pages / posts / companies / services
```

默认 review-only：

```text
sections / faqs / media / components
```

### 3.4 主题只负责结构与表现

主题中应保留：

- 页面布局
- 组件结构
- 样式 class
- 模板变量
- 循环逻辑
- include 逻辑

主题中不应长期保留：

- 企业正文
- 文章正文
- SEO 文案
- FAQ 内容
- 服务详情
- 大段业务介绍

## 4. 目录结构规范

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
      css/style.css
      js/main.js
      images/

  demo.routes.yaml

  themes/
    <theme-name>/
      layouts/
        layouts/base.html
        pages/
        partials/
        components/
        bukit.templates.yaml
      assets/

  sites/
    <site-name>/
      site.yaml
      content/
      notion-seed/
      import-report.md
```

## 5. 需求采集规范

生成 Demo 前，AI 应明确：

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

## 6. HTML Demo 规范

### 6.1 基础结构

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

### 6.2 页面类型

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

### 6.3 标准卡片 class

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

## 7. route-map 规范

AI 必须生成 `demo.routes.yaml`：

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

  - source: company-detail.html
    route: /companies/{slug}/
    type: CompanyDetail
    template: company
```

要求：

- 每个 HTML 文件必须出现在 route-map 中
- route 必须以 `/` 开头
- 动态详情页使用 `{slug}`
- 列表页和详情页必须分开
- template 名必须稳定
- source 必须对应真实 HTML 文件

## 8. 用户确认规范

用户确认 Demo 时，应确认：

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

用户确认后，Demo 才进入工程化转换阶段。

## 9. Demo 转 Bukit 规则

| Demo 部分 | Bukit 目标 |
|---|---|
| 公共 header | `layouts/partials/header.html` |
| 公共 nav | `layouts/partials/nav.html` |
| 公共 footer | `layouts/partials/footer.html` |
| 首页 | `layouts/pages/index.html` |
| 列表页 | `layouts/pages/*.html` |
| 详情页 | `layouts/pages/*.html` |
| 重复卡片 | `layouts/components/*.html` |
| CSS / JS / 图片 | `themes/<theme>/assets/` |
| 页面正文 | `notion-seed/pages.json` |
| 文章数据 | `notion-seed/posts.json` |
| 企业数据 | `notion-seed/companies.json` |
| 服务数据 | `notion-seed/services.json` |
| FAQ / section / media | review-only seed |

## 10. 模板变量规范

详情页：

```html
<h1>{{ page.title }}</h1>
<p>{{ page.summary }}</p>
<div class="content">
  {{ page.content }}
</div>
```

列表页：

```html
{{ for item in pages }}
  {{ include "components/article-card.html" }}
{{ end }}
```

组件：

```html
<article class="company-card">
  <h3>{{ item.title }}</h3>
  <p>{{ item.summary }}</p>
  <a href="{{ item.url }}">查看企业</a>
</article>
```

## 11. Notion database map 规范

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

## 12. 构建模式

### 12.1 本地预览模式

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

### 12.2 Notion-only 模式

```bash
bukit import html-demo ./demo   --theme <theme-name>   --content-source notion   --build-source notion   --route-map demo.routes.yaml   --force
```

## 13. Notion 推送

```bash
bukit notion push   --input sites/<site-name>/notion-seed   --database-map sites/<site-name>/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

## 14. 质量门禁

每次转换后必须执行：

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

## 15. import-report 审查

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
