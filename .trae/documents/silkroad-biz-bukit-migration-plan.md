# 丝路商讯 Bukit 主题迁移计划

## 摘要

将 `/Users/ali/Documents/trae_projects/silkroad_biz/demo/` 下的纯静态 HTML/CSS/JS 演示站（24 个 HTML 页面，1 个 CSS，1 个 JS）完整迁移为 Bukit 主题 + Markdown 内容源。目标：100% 复制原站结构、配色、样式、动作、文案、数据。

## 当前状态分析

### 源站（demo/）

* **技术栈**：纯静态 HTML5 + CSS3（504 行）+ 原生 JS（1 行压缩代码）

* **框架**：无框架，零依赖

* **页面总数**：24 个 HTML

* **设计令牌**：`:root` CSS 变量定义在 `style.css`

  * 主色 `--green: #0f3d2e`（深墨绿）

  * 深色 `--dark: #08291f`

  * 金色 `--gold: #c9a227`

  * 米色 `--cream: #f7f2e8`

  * 浅色 `--light: #fbf8f1`

  * 文字 `--text: #1f2933` / `--muted: #667085`

  * 边框 `--border: #e8dfcd`

  * 最大宽 `--max: 1180px`，圆角 `--radius: 22px`

* **字体**：Noto Sans SC（正文）、Noto Serif SC（标题）、Microsoft YaHei（回退）

* **JS 功能**：仅移动端汉堡菜单切换（`main.js`）

* **共享组件**：site-header（sticky 导航）、footer（4 列）、CTA 区块

* **响应式断点**：`980px`

#### 页面分类

| 类型    | 页面                              | 说明                                 |
| ----- | ------------------------------- | ---------------------------------- |
| 首页    | `index.html`                    | Hero + 双入口卡片 + 最新资讯 + 企业双板块 + CTA  |
| 内容列表  | `insights.html`                 | 资讯列表（分类筛选 + 分页），共 48 条（当前 6 条真实内容） |
| 资讯详情  | `article-detail.html` + 3 篇具体文章 | 文章内容 + 侧边栏（相关资讯/入口）                |
| 企业总览  | `companies.html`                | 中国企业 + 马来西亚企业双板块，18 家企业            |
| 企业子列表 | `china-companies.html` + 分页     | 已进驻中国企业独立列表                        |
| 企业子列表 | `malaysia-companies.html` + 分页  | 马来西亚企业独立列表                         |
| 企业详情  | `company-detail.html` + 6 家企业   | 企业信息（logo/简介/能力/合作方向）+ 侧边栏         |
| 表单页   | `join.html`                     | 申请入驻说明 + 表单（9 个字段）                 |
| 信息页   | `about.html`                    | 平台介绍 + 4 步运作路径 + 免责声明              |
| 信息页   | `contact.html`                  | 联系信息（WhatsApp/Email/地址）+ 可联系事项     |

### Bukit 主题系统

* 主题位于 `themes/<name>/`

* 必须文件：`layouts/layouts/base.html` + 4 个页面模板（page/post/index/list）

* 推荐：`partials/header.html`、`partials/footer.html`、`partials/list-card.html`

* CSS 在 `assets/style.css`

* SEO 用 inject 模式（starter 默认）

* Footer 必须含 "Powered by bukit" 归因链接

## 拟变更

### 目录结构

```
/Users/ali/Documents/trae_projects/silkroad_biz/
├── site.yaml                          # Bukit 站点配置
├── themes/
│   └── silkroad/
│       ├── theme.yaml                 # 主题自描述
│       ├── layouts/
│       │   ├── layouts/
│       │   │   └── base.html          # HTML 骨架（含设计令牌注入）
│       │   ├── pages/
│       │   │   ├── index.html         # 首页（全自定义 Hero + 入口卡片 + 资讯 + 企业 + CTA）
│       │   │   ├── page.html          # 通用页面（about/contact/join 共用）
│       │   │   ├── post.html          # 资讯详情（文章 + 侧边栏）
│       │   │   ├── list.html          # 通用列表页（资讯列表/企业列表共用）
│       │   │   └── company.html       # 企业详情（企业信息 + 侧边栏）
│       │   ├── partials/
│       │   │   ├── header.html        # 导航栏（brand + 8 个链接 + 语言 + CTA 按钮 + 汉堡）
│       │   │   ├── footer.html        # 4 列页脚 + Powered by bukit
│       │   │   ├── cta-section.html   # 全宽 CTA 区块（可复用）
│       │   │   ├── list-card.html     # 通用列表卡片
│       │   │   ├── company-card.html  # 企业卡片（含 logo/标签）
│       │   │   └── pagination-nav.html # 分页导航
│       │   └── bukit.templates.yaml   # 模板能力清单
│       ├── assets/
│       │   ├── style.css              # 完整 CSS（从 demo 迁移，按 Bukit 设计令牌规范重构）
│       │   └── main.js                # 移动端汉堡菜单 JS
│       └── static/
│           └── favicon.ico
├── content/
│   ├── pages/
│   │   ├── about.md                   # 关于商讯
│   │   ├── contact.md                 # 联系我们
│   │   └── join.md                    # 申请入驻
│   ├── insights/
│   │   ├── china-market-entry.md      # 中国企业进入马来西亚第一步
│   │   ├── local-business-china-clients.md  # 本地企业连接中国客户
│   │   └── company-directory-value.md       # 企业入驻平台价值
│   └── companies/
│       ├── china-tech-bridge.md
│       ├── guangzhou-smart-manufacturing.md
│       ├── china-ecommerce-connect.md
│       ├── nanyang-tech.md
│       ├── melaka-trade.md
│       └── kl-mice.md
```

### 1. `site.yaml` — 站点配置

```yaml
site:
  name: silkroad-biz
  title: 丝路商讯
  description: 马中商务资讯与企业资源平台
  language: zh-CN
  baseUrl: /
  metadata:
    description: 丝路旗下的马中商务资讯与企业资源展示平台

  collections:
    pages:
      template: pages/page.html
      permalink: /{slug}/
    insights:
      template: pages/post.html
      permalink: /insights/{slug}/
      listRoute: /insights/
      pagination:
        enabled: true
        pageSize: 9
    china-companies:
      template: pages/company.html
      permalink: /companies/{slug}/
      listRoute: /china-companies/
      pagination:
        enabled: true
        pageSize: 9
    malaysia-companies:
      template: pages/company.html
      permalink: /companies/{slug}/
      listRoute: /malaysia-companies/
      pagination:
        enabled: true
        pageSize: 9

  menus:
    main:
      - label: 首页
        url: /
      - label: 商务资讯
        url: /insights/
      - label: 企业资源库
        url: /companies/
      - label: 已进驻中国企业
        url: /china-companies/
      - label: 马来西亚企业
        url: /malaysia-companies/
      - label: 申请入驻
        url: /join/
      - label: 关于商讯
        url: /about/
      - label: 联系我们
        url: /contact/

  search:
    enabled: false

  feed:
    enabled: false

  seo:
    renderMode: inject

build:
  output: dist

theme:
  name: silkroad
  params:
    brand: 丝路商讯
    brand_sub: Business Insight
    footer_text: © 2026 SilkRoute Business Insight.
    footer_tagline: 丝路旗下业务｜商务资讯与企业资源平台
    whatsapp: "+60 12-345 6789"
    email: insight@silkroute.com
    location: Kuala Lumpur, Malaysia
    cta_heading: 希望展示企业或发布商务资讯？
    cta_text: 提交企业资料、合作方向或商务内容线索，我们将评估是否适合展示在丝路商讯平台。
    cta_button: 申请企业入驻 →
    cta_url: /join/
    nav:
      - { label: 首页, url: /, key: home }
      - { label: 商务资讯, url: /insights/, key: insights }
      - { label: 企业资源库, url: /companies/, key: companies }
      - { label: 已进驻中国企业, url: /china-companies/, key: china-companies }
      - { label: 马来西亚企业, url: /malaysia-companies/, key: malaysia-companies }
      - { label: 申请入驻, url: /join/, key: join }
      - { label: 关于商讯, url: /about/, key: about }
      - { label: 联系我们, url: /contact/, key: contact }

content:
  provider: markdown
  sources:
    - content/
```

### 2. 主题模板（Scriban）

#### `layouts/layouts/base.html`

完整的 HTML 骨架，包含：

* 设计令牌注入（通过 `theme.params` 生成 CSS 变量）

* `<head>` 中加载 `style.css`、`main.js`

* 共享的 header → main → cta → footer 布局链

* inject 模式 SEO（不手动引入 seo/analytics partials）

#### `layouts/pages/index.html` — 首页

完全自定义，不使用通用 list 模板：

* Hero 区域（渐变背景 + 大标题 + 三个 CTA 按钮）

* 双入口卡片（商务资讯 + 企业列表）

* 最新商务资讯（通过 `site.modules` 或直接遍历 insights collection）

* 企业双板块（中国企业 + 马来西亚企业）

* CTA 区块（通过 include partial）

#### `layouts/pages/page.html` — 通用页面

用于 about、contact、join：

* 使用 `page-hero` 样式作为页面标题区

* 通过 front matter 的 `page_type` 字段渲染不同内容布局

#### `layouts/pages/post.html` — 资讯详情

* 继承 base

* 文章主内容区（分类 + 标题 + 摘要 + 封面图 + 内容）

* 侧边栏（相关资讯 + 相关入口）

* CTA 区块

#### `layouts/pages/list.html` — 通用列表

* 继承 base

* page-hero 标题区

* 筛选 pills（通过 collection 分类字段动态生成）

* 卡片网格（3 列）

* 分页导航

#### `layouts/pages/company.html` — 企业详情

* 继承 base

* 企业 logo + 分类/城市 + 名称 + 简介

* 封面图 + 企业简介/服务能力/合作方向

* 侧边栏（企业类型/状态/对接入口）

* 免责声明 notice

* CTA 区块

#### `layouts/partials/header.html`

导航栏，包含：

* Brand 区域（图标 `讯` + 标题 + 副标题）

* 8 个导航链接，active 状态高亮（金色）

* 语言切换器（`中文 ▾`）

* "申请入驻" 金色 CTA 按钮

* 移动端汉堡菜单按钮

#### `layouts/partials/footer.html`

4 列布局：

* 品牌介绍

* 快速链接

* 企业分类

* 联系我们（WhatsApp/Email/地址）

* 底部版权 + "Powered by bukit" 归因

#### `layouts/partials/cta-section.html`

全宽深绿背景 CTA：

* 标题 + 描述 + 金色按钮

* 可提取通过 `site.params` 自定义

#### `layouts/partials/list-card.html` / `company-card.html`

卡片组件：

* 资讯卡片：图片 + 分类标签 + 标题 + 摘要 + 阅读链接

* 企业卡片：logo 方块 + 分类 + 名称 + 描述 + 标签 + 详情链接

#### `layouts/partials/pagination-nav.html`

分页导航：上一页 | 页码 | 下一页

### 3. CSS (`assets/style.css`)

完整迁移 demo 的 504 行 CSS，改造要点：

* 保留所有 CSS 变量定义（`:root` 选择器不变）

* 响应式断点保持 `980px`

* 所有样式类名保持不变（`.site-header`, `.hero`, `.card`, `.cta` 等）

* 添加 Bukit 特定样式：

  * hamburger 菜单的 JS 与 CSS 配合

  * pagination 组件样式保留

### 4. JS (`assets/main.js`)

迁移原 `main.js` 的汉堡菜单逻辑，稍作格式化：

```javascript
(function(){
  const b = document.querySelector('.mobile-toggle');
  const n = document.querySelector('.nav-menu');
  if (!b || !n) return;
  b.onclick = () => {
    const o = n.style.display === 'flex';
    n.style.display = o ? 'none' : 'flex';
    n.style.position = 'absolute';
    n.style.left = '0';
    n.style.right = '0';
    n.style.top = '78px';
    n.style.flexDirection = 'column';
    n.style.background = '#fff';
    n.style.padding = '18px 24px';
    n.style.borderBottom = '1px solid #e8dfcd';
  };
})();
```

### 5. Markdown 内容文件

每个 `.md` 文件含 YAML front matter，例如：

```markdown
---
title: 中国企业进入马来西亚市场的第一步
date: 2026-05-19
category: Market Entry
category_cn: 市场进入
summary: 先判断行业机会、目标客户、渠道结构和本地合作方式，再制定可执行的进入路径。
cover: https://images.unsplash.com/photo-1521791136064-7986c2920216?auto=format&fit=crop&w=1200&q=80
---

## 一、为什么这个主题重要

马中商务合作不是简单的信息交换...
```

企业 Markdown 的 front matter：

```markdown
---
title: China Tech Bridge
name_cn: China Tech Bridge
type: china
category: 科技与软件
city: Kuala Lumpur
logo: CTS
summary: 已在马来西亚市场开展软件、AI 工具和企业数字化解决方案业务的中国企业。
cover: https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=1200&q=80
tags: [AI 应用, 企业软件]
status: 展示企业
cooperation: 开放对接
---
```

### 6. `themes/silkroad/theme.yaml`

```yaml
name: silkroad
version: 1.0.0
description: 丝路商讯 — 马中商务资讯与企业资源平台主题
author: SilkRoute
license: MIT
tags: [business, chinese, malaysia, directory, news]
```

## 假设与决策

1. **公司列表使用两个独立 Collection**：`china-companies` 和 `malaysia-companies`，而非单一 `companies` collection + filteredLists。这样可以保持 URL 结构（`/china-companies/`、`/malaysia-companies/`）与原 demo 一致。
2. **首页不遍历 collection 列表**：首页直接硬编码展示内容区块（hero、入口卡片、最新资讯先手动选 3 篇），因为首页布局较复杂，后续可改为动态渲染。
3. **表单页不集成后端**：`join.html` 的表单保持静态展示（仅 UI），不添加 form action 或提交逻辑（原 demo 也是 `<button type="button">`）。
4. **"企业资源库总览页"（`/companies/`）**：作为一个特殊页面，重定向或展示双板块入口。在 site.yaml 中不为其创建 collection；作为一个自定义静态页面处理。
5. **图片保持 Unsplash CDN 外链**：不下载到本地，减少迁移复杂度。
6. **不使用外部 CSS/JS 框架**：保持与原 demo 一致，纯 Bukit CSS。
7. **SEO 使用 inject 模式**：不手动添加 seo/analytics partials。

## 验证步骤

1. 在目标目录运行 `bukit doctor` 验证模板完整性
2. 运行 `bukit build` 确保构建成功
3. 运行 `bukit dev` 启动 HMR 开发服务器
4. 逐页对比视觉：首页、资讯列表、资讯详情、企业列表、企业详情、关于、联系、入驻
5. 验证响应式：桌面端（>980px）和移动端（<980px）
6. 验证导航 active 状态和"Powered by bukit"归因
7. 验证 CSS 变量在 :root 中正确生效
8. 对比所有文字内容与原 demo 一致

## 实施步骤（9 步）

1. **创建项目结构与 site.yaml**
2. **创建主题骨架（theme.yaml + base.html + 空页面模板）**
3. **迁移 CSS（assets/style.css）+ JS（assets/main.js）**
4. **创建 partials（header.html / footer.html / cta-section.html / list-card.html / company-card.html / pagination-nav.html）**
5. **创建 pages/index.html（首页）**
6. **创建 pages/page.html / post.html / list.html / company.html**
7. **创建 Markdown 内容文件（3 篇资讯 + 6 家企业 + 3 个页面）**
8. **运行 bukit doctor + bukit build 验证**
9. **预览并修复问题**

