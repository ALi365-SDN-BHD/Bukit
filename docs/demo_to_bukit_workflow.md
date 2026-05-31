# Demo-to-Bukit Workflow

> 将 ChatGPT 生成的 HTML Demo 转换为可维护、可复用、可批量构建的 Bukit 静态网站工程流程。

---

## 1. 文档目标

本文档定义 **Demo-to-Bukit Workflow** 的标准流程，用于指导开发者、AI Agent 或 BukitJalil 控制台将一个由 ChatGPT / AI 生成的 HTML 静态 Demo，逐步迁移为符合 Bukit 架构的正式网站工程。

该流程的核心目标不是让 AI 直接生成最终站点，而是建立一条稳定、可审计、可重复执行的工程化路径：

```text
AI HTML Demo
    ↓
结构审查
    ↓
Bukit Theme 拆分
    ↓
内容数据化
    ↓
Notion / JSON 数据源接入
    ↓
Bukit Build
    ↓
Preview Deploy
    ↓
Production Release
```

---

## 2. 核心原则

### 2.1 HTML Demo 是设计稿，不是最终工程

ChatGPT 生成的 HTML Demo 主要用于快速验证：

- 页面视觉方向
- 信息架构
- 首页模块顺序
- 内容表达方式
- 响应式布局
- 用户体验路径

HTML Demo 不应该直接作为正式网站长期维护，否则会产生以下问题：

- 内容硬编码严重
- 页面无法批量复用
- 多语言扩展困难
- SEO 元数据不可统一管理
- 主题组件无法沉淀
- 后续无法接入 Notion CMS
- 难以支撑大量网站创建

### 2.2 Bukit 负责确定性构建

Bukit 的职责是将结构化内容、模板、主题和配置稳定编译为静态 HTML 输出。

因此，最终工程应该遵循：

```text
结构由模板控制
内容由数据源控制
样式由主题控制
构建由 Bukit 控制
部署由流水线控制
```

### 2.3 内容必须数据化

HTML Demo 中的业务文案、页面正文、文章、企业资料、FAQ、SEO 信息等，不应长期保留在模板中。

正式迁移时应抽离到：

- Notion 数据库
- JSON 文件
- YAML 配置
- 未来的 Bukit CMS 数据源

在当前 Bukit 项目规划中，推荐优先使用 **Notion** 作为内容中台。

---

## 3. 适用场景

Demo-to-Bukit Workflow 适用于以下类型的网站：

| 类型 | 适用度 | 说明 |
|---|---:|---|
| 企业官网 | 高 | 首页、关于、服务、联系等结构稳定 |
| 服务落地页 | 高 | 适合批量生成 SEO 页面 |
| 旅游 DMC 网站 | 高 | 目的地、线路、服务、表单等模块清晰 |
| 商务资讯站 | 高 | 文章列表、文章详情、分类页 |
| 企业目录站 | 高 | 企业列表、企业详情、分类筛选 |
| 留学 / 签证 / ESD 服务站 | 高 | FAQ、流程、服务详情可组件化 |
| 产品展示站 | 中高 | 需要产品数据结构 |
| 电商交易站 | 低 | 需要订单、支付、库存等动态能力 |
| 会员系统 / SaaS 后台 | 低 | 不适合纯静态站点作为核心系统 |

---

## 4. 总体流程

### 阶段一：生成 HTML Demo

由 ChatGPT 或其他 AI 工具根据业务需求生成一套完整可预览的静态 HTML Demo。

建议输出结构：

```text
demo/
├── index.html
├── about.html
├── services.html
├── service-detail.html
├── insights.html
├── article-detail.html
├── companies.html
├── company-detail.html
├── contact.html
└── assets/
    ├── css/
    │   └── style.css
    ├── js/
    │   └── app.js
    └── images/
        └── placeholder.jpg
```

生成 Demo 时应明确要求：

- 使用纯 HTML + CSS + 少量原生 JS
- 不使用 React / Vue / Next.js
- 不依赖复杂构建工具
- 所有图片使用占位路径
- 页面结构清晰，便于后续组件拆分
- 不将多个页面混合在一个 HTML 文件中
- 页面模块命名清晰，例如 hero、services、faq、cta

---

### 阶段二：Demo 审查

在迁移到 Bukit 之前，需要对 Demo 进行审查。

#### 2.1 页面完整性审查

检查是否包含目标网站需要的全部页面：

```text
首页
关于我们
服务列表
服务详情
文章列表
文章详情
企业列表
企业详情
联系我们
申请 / 入驻页面
```

#### 2.2 结构一致性审查

检查不同页面是否存在统一结构：

- Header 是否统一
- Footer 是否统一
- 页面标题区是否统一
- Card 组件是否统一
- CTA 区块是否统一
- 列表页与详情页是否结构清晰

#### 2.3 内容审查

检查 Demo 文案是否满足：

- 业务定位准确
- 无明显 AI 空话
- 无虚假承诺
- 无重复段落
- 多语言表达自然
- SEO 标题和描述合理

#### 2.4 响应式审查

至少检查：

```text
Desktop ≥ 1200px
Tablet 768px - 1199px
Mobile ≤ 767px
```

---

### 阶段三：拆分为 Bukit Theme

Demo 审查通过后，将 HTML Demo 拆分为 Bukit 主题。

推荐主题目录结构：

```text
themes/
└── silkroadbiz/
    ├── theme.yaml
    ├── layouts/
    │   └── base.html
    ├── pages/
    │   ├── index.html
    │   ├── page.html
    │   ├── insights.html
    │   ├── article.html
    │   ├── companies.html
    │   └── company.html
    ├── components/
    │   ├── header.html
    │   ├── footer.html
    │   ├── hero.html
    │   ├── service-card.html
    │   ├── article-card.html
    │   ├── company-card.html
    │   ├── faq.html
    │   ├── cta.html
    │   └── pagination.html
    └── assets/
        ├── css/
        │   └── style.css
        ├── js/
        │   └── app.js
        └── images/
```

---

## 5. HTML 到 Bukit 的映射规则

| HTML Demo 内容 | Bukit 工程位置 | 说明 |
|---|---|---|
| `<head>` 公共部分 | `layouts/base.html` | 包含 SEO、meta、CSS 引用 |
| Header 导航 | `components/header.html` | 所有页面复用 |
| Footer | `components/footer.html` | 所有页面复用 |
| 首页 | `pages/index.html` | 首页专用模板 |
| 普通页面 | `pages/page.html` | 关于、联系等页面 |
| 文章列表 | `pages/insights.html` | 数据驱动列表 |
| 文章详情 | `pages/article.html` | 渲染单篇内容 |
| 企业列表 | `pages/companies.html` | 数据驱动企业目录 |
| 企业详情 | `pages/company.html` | 渲染单个企业资料 |
| Hero 区块 | `components/hero.html` | 可配置标题、副标题、按钮 |
| FAQ 区块 | `components/faq.html` | 从数据源循环渲染 |
| CTA 区块 | `components/cta.html` | 多页面复用 |
| CSS | `assets/css/style.css` | 保持独立资源 |
| JS | `assets/js/app.js` | 只保留必要交互 |
| 页面文案 | Notion / JSON | 不长期写死在模板中 |

---

## 6. 模板变量替换规则

### 6.1 静态标题替换

原始 HTML：

```html
<h1>丝路商讯</h1>
<p>连接中国与马来西亚商业机会</p>
```

Bukit 模板：

```html
<h1>{{ site.title }}</h1>
<p>{{ site.description }}</p>
```

---

### 6.2 页面内容替换

原始 HTML：

```html
<h2>关于我们</h2>
<p>我们专注于中马企业服务与商务资讯。</p>
```

Bukit 模板：

```html
<h2>{{ page.title }}</h2>
<div class="content">
  {{ page.content }}
</div>
```

---

### 6.3 列表数据循环

原始 HTML：

```html
<div class="article-card">
  <h3>马来西亚投资指南</h3>
  <p>了解马来西亚投资环境。</p>
</div>
```

Bukit 模板：

```html
{{ for post in posts }}
<article class="article-card">
  <h3><a href="{{ post.url }}">{{ post.title }}</a></h3>
  <p>{{ post.summary }}</p>
</article>
{{ end }}
```

---

### 6.4 企业目录循环

```html
{{ for company in companies }}
<article class="company-card">
  <h3><a href="{{ company.url }}">{{ company.title }}</a></h3>
  <p>{{ company.summary }}</p>
  <span>{{ company.country }}</span>
</article>
{{ end }}
```

---

## 7. Notion 数据模型建议

### 7.1 Pages 数据库

用于管理普通页面。

| 字段 | 类型 | 说明 |
|---|---|---|
| title | Title | 页面标题 |
| slug | Text | 页面路径 |
| type | Select | Page / AppPage |
| summary | Text | 页面摘要 |
| content | Rich Text | 页面正文 |
| language | Select | zh / en / ms |
| published | Checkbox | 是否发布 |
| publish_date | Date | 发布时间 |
| seo_title | Text | SEO 标题 |
| seo_description | Text | SEO 描述 |
| cover | Files / URL | 页面封面 |

### 7.2 Posts 数据库

用于管理文章内容。

| 字段 | 类型 | 说明 |
|---|---|---|
| title | Title | 文章标题 |
| slug | Text | 文章路径 |
| summary | Text | 摘要 |
| content | Rich Text | 正文 |
| tags | Multi-select | 标签 |
| category | Select | 分类 |
| language | Select | 语言 |
| published | Checkbox | 是否发布 |
| publish_date | Date | 发布时间 |
| seo_title | Text | SEO 标题 |
| seo_description | Text | SEO 描述 |
| cover | Files / URL | 封面图 |

### 7.3 Companies 数据库

用于企业目录。

| 字段 | 类型 | 说明 |
|---|---|---|
| title | Title | 企业名称 |
| slug | Text | 企业详情路径 |
| summary | Text | 企业简介 |
| content | Rich Text | 详细介绍 |
| country | Select | China / Malaysia / Other |
| industry | Select | 行业 |
| website | URL | 官网 |
| logo | Files / URL | Logo |
| published | Checkbox | 是否发布 |
| seo_title | Text | SEO 标题 |
| seo_description | Text | SEO 描述 |

---

## 8. site.yaml 示例

```yaml
site:
  title: "丝路商讯"
  description: "连接中国与马来西亚商业机会的商务资讯与企业目录平台"
  base_url: "https://example.com"
  language: "zh"
  theme: "silkroadbiz"

source:
  type: "notion"
  databases:
    pages: "${NOTION_PAGES_DATABASE_ID}"
    posts: "${NOTION_POSTS_DATABASE_ID}"
    companies: "${NOTION_COMPANIES_DATABASE_ID}"

build:
  output: "dist"
  clean: true

seo:
  sitemap: true
  robots: true
  canonical: true
```

---

## 9. 推荐 CLI 流程

### 9.1 创建项目

```bash
bukit create site silkroadbiz --theme silkroadbiz --source notion
```

### 9.2 从 Demo 初始化主题草稿

未来建议增加命令：

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

该命令可用于：

- 复制 assets
- 识别公共 layout
- 生成初始 page templates
- 生成 components 草稿
- 输出内容抽取报告

### 9.3 构建网站

```bash
bukit build --config sites/silkroadbiz/site.yaml --output dist --clean
```

### 9.4 本地预览

```bash
bukit serve --root dist --port 5080
```

---

## 10. BukitJalil 自动化扩展

Demo-to-Bukit Workflow 后续可以作为 BukitJalil 的核心能力。

### 10.1 用户输入

```text
创建一个面向中国企业的马来西亚商务资讯和企业目录网站。
要求：现代、专业、中英文、包含资讯和企业列表。
```

### 10.2 AI 生成 HTML Demo

BukitJalil 调用 ChatGPT 生成：

- 页面结构
- 视觉 Demo
- 初始文案
- SEO 信息

### 10.3 AI 拆分 Bukit Theme

系统继续调用 AI Agent：

- Layout Agent：提取 header、footer、base layout
- Component Agent：识别 hero、card、faq、cta 等组件
- Content Agent：抽取页面文案
- Notion Agent：写入 Notion
- Build Agent：触发 Bukit 构建
- Deploy Agent：部署预览站点

### 10.4 输出结果

```text
项目已创建
主题已生成
内容已写入 Notion
Bukit 构建成功
预览站点已部署
```

---

## 11. 质量门禁

迁移完成后，必须执行以下检查。

### 11.1 结构检查

- 不允许所有页面内容都塞进一个模板
- 不允许列表页硬编码文章卡片
- 不允许详情页使用固定假数据
- Header/Footer 必须组件化
- 重复区块必须抽成组件

### 11.2 内容检查

- 页面正文必须来自数据源
- 文章必须来自 posts 集合
- 企业信息必须来自 companies 集合
- SEO title / description 必须可配置
- 多语言内容不能混写在同一字段中

### 11.3 安全检查

- 外部链接必须安全处理
- 用户输入内容必须经过 HTML 安全策略
- 图片 URL 必须校验
- 不允许模板直接输出未处理的危险脚本
- 构建输出不得包含 `.env`、密钥、私有配置

### 11.4 构建检查

- `bukit doctor` 通过
- `bukit build` 通过
- 输出目录结构正确
- sitemap.xml 正确生成
- robots.txt 正确生成
- 所有内部链接可访问
- 404 页面可用

### 11.5 视觉检查

- 首页与 Demo 基本一致
- 移动端布局正常
- 导航正常展开 / 收起
- CTA 按钮链接正确
- 列表页分页正常
- 图片不变形

---

## 12. 验收标准

一个 Demo 成功迁移为 Bukit 工程，应满足以下标准：

| 项目 | 标准 |
|---|---|
| 页面结构 | 与原 HTML Demo 保持一致 |
| 视觉效果 | 与 Demo 高度接近 |
| 模板结构 | layout / pages / components 清晰拆分 |
| 内容来源 | 页面、文章、企业信息来自数据源 |
| 多语言 | 支持独立语言内容 |
| SEO | 每个页面有独立 SEO 配置 |
| 构建 | Bukit build 成功 |
| 预览 | 本地和线上预览正常 |
| 可维护性 | 修改 Notion 内容后可重新构建 |
| 可复用性 | 主题可用于创建第二个同类网站 |

---

## 13. 推荐开发任务清单

### P0：基础流程

- [ ] 定义 Demo-to-Bukit 迁移规范
- [ ] 整理标准主题目录结构
- [ ] 编写 HTML Demo 生成 Prompt
- [ ] 编写 Bukit 主题迁移 Prompt
- [ ] 建立 Notion 字段规范
- [ ] 完成一个示例 Demo 迁移

### P1：工程化支持

- [ ] 增加 `bukit create site` 能力
- [ ] 增加 `bukit import html-demo` 草稿命令
- [ ] 增加主题组件校验
- [ ] 增加内容字段完整性检查
- [ ] 增加构建输出安全检查
- [ ] 增加本地预览脚本

### P2：自动化支持

- [ ] 接入 GitHub Actions 构建
- [ ] 接入 Cloudflare Pages 预览部署
- [ ] 构建状态写回 Notion
- [ ] 失败原因写入 `msg` 字段
- [ ] 成功后触发发布链接 / Webhook

### P3：BukitJalil 集成

- [ ] UI 中增加“从 HTML Demo 创建 Bukit 主题”入口
- [ ] 增加 AI Demo Generator
- [ ] 增加 AI Theme Splitter
- [ ] 增加 AI Content Extractor
- [ ] 增加 Notion Writer
- [ ] 增加 Preview Deploy Agent

---

## 14. ChatGPT 生成 HTML Demo Prompt

```text
你是资深网站 UI 设计师和前端工程师。

请为【业务名称】生成一个完整 HTML 静态网站 Demo。

要求：
1. 使用纯 HTML + CSS + 少量原生 JS。
2. 不使用 React、Vue、Next.js。
3. 不使用复杂构建工具。
4. 页面结构必须清晰，后续需要迁移到 Bukit 静态站点生成器。
5. 所有页面模块必须适合拆分为组件。
6. CSS 写在独立 style.css 中。
7. JS 写在独立 app.js 中。
8. 图片使用占位路径，例如 /assets/images/hero.jpg。
9. 页面包括：首页、关于我们、服务列表、服务详情、文章列表、文章详情、联系我们。
10. 输出完整文件结构和每个文件内容。

网站业务：
【填写业务说明】

设计风格：
专业、现代、移动端友好、适合企业官网。
```

---

## 15. ChatGPT 迁移 Bukit Prompt

```text
你是 Bukit 静态网站生成器的主题迁移工程师。

我会提供一套 HTML Demo。
请将它迁移为 Bukit 主题结构。

要求：
1. 保持原 Demo 的页面结构、视觉布局和文案不变。
2. 不要把所有内容硬编码在一个模板里。
3. 必须拆分为 layout、page templates、components、assets、content schema。
4. 重复出现的区块必须抽成 components。
5. 页面正文内容应抽离为 Notion 数据字段。
6. 文章、企业、服务等列表必须改为数据驱动。
7. 输出建议的目录结构。
8. 输出每个模板文件的迁移说明。
9. 输出 Notion 数据库字段设计。
10. 输出 site.yaml 示例。
11. 输出迁移检查清单。

HTML Demo：
【粘贴 HTML 文件内容或文件结构】
```

---

## 16. 标准交付物

一次完整 Demo-to-Bukit 迁移应交付：

```text
1. 原始 HTML Demo
2. Bukit Theme 目录
3. site.yaml
4. Notion 数据库字段设计
5. 内容迁移记录
6. 构建日志
7. 预览地址
8. 问题清单
9. 验收报告
```

---

## 17. 最终定位

Demo-to-Bukit Workflow 的定位是：

> 用 ChatGPT 快速生成网站视觉原型，再通过 Bukit 将原型工程化、数据化、主题化和可批量构建化。

它解决的问题不是“生成一个网页”，而是建立一套从 AI 原型到正式静态网站工程的标准流水线。

最终目标：

```text
一句话生成 Demo
    ↓
一键迁移 Bukit Theme
    ↓
一键写入 Notion
    ↓
一键构建预览
    ↓
确认后发布正式站点
```

这将成为 Bukit 面向 AI 建站、批量企业官网、内容站和目录站生成的核心工作流之一。


---

# 18. 统一 Demo 导入方案：`bukit import html-demo`

## 18.1 功能定位

`bukit import html-demo` 是 Demo-to-Bukit Workflow 的工程化入口命令，用于将一套已有 HTML Demo 自动转换为 Bukit 可识别、可维护、可继续人工修正的站点工程草稿。

推荐命令形式：

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

该命令的目标不是一次性做到 100% 完美自动迁移，而是完成以下核心工作：

```text
HTML Demo 输入
    ↓
页面扫描
    ↓
结构识别
    ↓
Layout / Component 拆分
    ↓
内容抽取与数据化
    ↓
主题工程生成
    ↓
Notion Seed / JSON Seed 生成
    ↓
site.yaml 生成
    ↓
导入报告生成
    ↓
可执行 Bukit Build 的工程草稿
```

最终产物应该是一个可以继续执行、审查和修复的 Bukit 项目，而不是一个黑盒自动生成结果。

---

## 18.2 命令设计

### 基础命令

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

含义：

- 读取 `./demo` 目录中的 HTML Demo
- 创建或更新 `themes/silkroadbiz`
- 提取 CSS / JS / 图片资源
- 生成 Bukit 主题模板
- 抽取业务内容到 seed 数据
- 生成导入报告

---

### 推荐完整命令

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --site-path sites/silkroadbiz \
  --content-source notion \
  --report \
  --dry-run
```

---

### 参数说明

| 参数 | 说明 | 默认值 |
|---|---|---|
| `input` | HTML Demo 目录 | 必填 |
| `--theme` | 目标主题名称 | 必填 |
| `--site-path` | 目标站点目录 | `sites/{theme}` |
| `--content-source` | seed 输出类型：`notion` / `json` / `yaml`，默认仍生成本地 markdown 草稿用于 build | `notion` |
| `--extract-content` | 是否抽取业务内容 | `true` |
| `--generate-seed` | 是否生成 seed 数据 | `true` |
| `--preserve-html` | 是否保留原始 HTML 快照 | `true` |
| `--overwrite` | 是否覆盖已有主题文件 | `false` |
| `--force` | 忽略部分警告继续导入 | `false` |
| `--dry-run` | 只分析不写入文件 | `false` |
| `--report` | 生成导入报告 | `true` |
| `--language` | 默认语言 | `zh` |
| `--base-url` | 默认站点 URL | 空 |
| `--strict` | 严格模式，遇到高风险问题直接失败 | `false` |

---

## 18.3 输出目录结构

执行导入后，建议生成以下结构：

```text
sites/
└── silkroadbiz/
    ├── site.yaml
    ├── content/
    │   ├── index.md
    │   ├── pages/
    │   ├── posts/
    │   ├── companies/
    │   └── services/
    ├── notion-seed/
    │   ├── pages.json
    │   ├── sections.json
    │   ├── posts.json
    │   ├── companies.json
    │   ├── services.json
    │   ├── faqs.json
    │   └── media.json
    ├── import-report.md
    └── original-demo/
        ├── index.html
        ├── insights.html
        └── assets/

themes/
└── silkroadbiz/
    ├── layouts/
    │   ├── layouts/
    │   │   └── base.html
    │   ├── pages/
    │   │   ├── index.html
    │   │   ├── page.html
    │   │   ├── insights.html
    │   │   ├── article.html
    │   │   ├── companies.html
    │   │   └── company.html
    │   ├── partials/
    │   │   ├── header.html
    │   │   ├── nav.html
    │   │   └── footer.html
    │   └── components/
    │       ├── hero.html
    │       ├── service-card.html
    │       ├── article-card.html
    │       ├── company-card.html
    │       ├── faq.html
    │       ├── cta.html
    │       └── pagination.html
    └── assets/
        ├── css/
        ├── js/
        └── images/
```

---

## 18.4 导入流水线

### Phase 0：输入验证

验证 HTML Demo 是否符合最低要求：

- 输入目录存在
- 至少包含一个 `index.html`
- HTML 文件可解析
- 资源路径可识别
- 不存在明显危险文件
- 文件大小在合理范围内

建议拒绝导入：

```text
.env
*.pem
*.key
node_modules/
.git/
.vscode/
dist/
build/
```

---

### Phase 1：页面发现

扫描 `./demo` 下所有 HTML 文件。

示例：

```text
index.html              → /
about.html              → /about/
contact.html            → /contact/
insights.html           → /insights/
article-detail.html     → /insights/{slug}/
companies.html          → /companies/
company-detail.html     → /companies/{slug}/
```

页面识别规则：

| 文件名 / 特征 | 页面类型 |
|---|---|
| `index.html` | Home |
| `about.html` | Page |
| `contact.html` | Contact Page |
| `insights.html` / `blog.html` / `news.html` | Post List |
| `article.html` / `article-detail.html` / `post.html` | Post Detail |
| `companies.html` | Company List |
| `company.html` / `company-detail.html` | Company Detail |
| `services.html` | Service List |
| `service-detail.html` | Service Detail |

输出：

```json
{
  "pages": [
    {
      "source": "index.html",
      "route": "/",
      "type": "Home",
      "template": "index"
    }
  ]
}
```

---

### Phase 2：公共结构识别

识别所有页面中的公共结构：

- `<head>`
- Header
- Navigation
- Footer
- 全局脚本
- 全局样式

提取为：

```text
layouts/base.html
components/header.html
components/footer.html
```

识别策略：

1. 多个页面中重复率高的顶部区块 → Header
2. 多个页面中重复率高的底部区块 → Footer
3. `<title>` / meta / CSS 引用 → base layout
4. 页面主体差异部分 → page template

---

### Phase 3：组件识别

识别 Demo 中可复用组件。

常见规则：

| HTML 特征 | 组件类型 |
|---|---|
| `.hero` / `<section id="hero">` | hero |
| `.card` 重复出现 | card |
| `.article-card` | article-card |
| `.company-card` | company-card |
| `.service-card` | service-card |
| `.faq` / `.faq-item` | faq |
| `.cta` | cta |
| `.pagination` | pagination |
| `.breadcrumb` | breadcrumb |
| `.stats` | stats |

生成组件：

```text
components/hero.html
components/article-card.html
components/company-card.html
components/faq.html
components/cta.html
```

原则：

- 重复结构必须组件化
- 列表项必须变成循环渲染
- 详情页模板不得硬编码某一条 Demo 数据
- 组件文件只保留结构，不保留具体业务文案

---

### Phase 4：内容抽取与数据化

这是命令的核心阶段。

HTML Demo 中的业务内容需要按类别抽离。

```text
页面级内容      → pages.json
首页区块内容    → sections.json
文章卡片 / 详情 → posts.json
企业卡片 / 详情 → companies.json
服务卡片 / 详情 → services.json
FAQ 问答        → faqs.json
图片资源        → media.json
SEO 信息        → 对应内容记录的 seo 字段
```

---

### 4.1 页面内容抽取

来源：

```html
<h1>关于丝路商讯</h1>
<p>我们连接中国与马来西亚商业机会。</p>
```

输出到 `pages.json`：

```json
[
  {
    "title": "关于丝路商讯",
    "slug": "about",
    "type": "Page",
    "template": "page",
    "summary": "我们连接中国与马来西亚商业机会。",
    "content": "我们连接中国与马来西亚商业机会。",
    "language": "zh",
    "published": true,
    "seo_title": "关于丝路商讯",
    "seo_description": "了解丝路商讯的定位、服务和愿景。"
  }
]
```

---

### 4.2 Section 内容抽取

来源：

```html
<section class="hero">
  <h1>连接中国与马来西亚商业机会</h1>
  <p>聚合商务资讯、企业目录与市场机会。</p>
  <a href="/companies/">浏览企业目录</a>
</section>
```

输出到 `sections.json`：

```json
[
  {
    "page_slug": "/",
    "section_type": "hero",
    "heading": "连接中国与马来西亚商业机会",
    "subheading": "聚合商务资讯、企业目录与市场机会。",
    "button_text": "浏览企业目录",
    "button_url": "/companies/",
    "sort_order": 10,
    "language": "zh",
    "published": true
  }
]
```

模板中替换为：

```html
{{ include "components/section-renderer.html" section }}
```

---

### 4.3 Posts 内容抽取

来源：

```html
<article class="article-card">
  <h3>马来西亚投资指南</h3>
  <p>了解马来西亚投资环境与企业设立流程。</p>
</article>
```

输出到 `posts.json`：

```json
[
  {
    "title": "马来西亚投资指南",
    "slug": "malaysia-investment-guide",
    "summary": "了解马来西亚投资环境与企业设立流程。",
    "content": "",
    "category": "商务资讯",
    "tags": ["马来西亚", "投资", "企业服务"],
    "language": "zh",
    "published": true,
    "seo_title": "马来西亚投资指南",
    "seo_description": "了解马来西亚投资环境与企业设立流程。"
  }
]
```

---

### 4.4 Companies 内容抽取

来源：

```html
<div class="company-card">
  <h3>ABC Trading Sdn Bhd</h3>
  <p>马来西亚本地进出口贸易企业。</p>
</div>
```

输出到 `companies.json`：

```json
[
  {
    "title": "ABC Trading Sdn Bhd",
    "slug": "abc-trading-sdn-bhd",
    "summary": "马来西亚本地进出口贸易企业。",
    "content": "",
    "country": "Malaysia",
    "industry": "Trading",
    "language": "zh",
    "published": true,
    "seo_title": "ABC Trading Sdn Bhd",
    "seo_description": "马来西亚本地进出口贸易企业。"
  }
]
```

---

### 4.5 FAQ 内容抽取

来源：

```html
<div class="faq-item">
  <h3>丝路商讯适合哪些企业？</h3>
  <p>适合希望拓展中马业务的企业。</p>
</div>
```

输出到 `faqs.json`：

```json
[
  {
    "question": "丝路商讯适合哪些企业？",
    "answer": "适合希望拓展中马业务的企业。",
    "page_slug": "/",
    "category": "general",
    "sort_order": 10,
    "language": "zh",
    "published": true
  }
]
```

---

## 18.5 内容分类决策规则

导入器需要有清晰的分类策略。

### 页面级内容

归入 `Pages`：

```text
页面 H1
页面摘要
关于我们正文
联系我们正文
隐私政策
条款页面
入驻说明页面
```

### 区块级内容

归入 `Sections`：

```text
Hero
CTA
首页优势模块
统计数字模块
合作伙伴模块
推荐文章模块
推荐企业模块
页面顶部 Banner
```

### 集合型内容

归入集合表：

```text
文章列表       → Posts
企业卡片       → Companies
服务卡片       → Services
FAQ 项         → FAQs
案例卡片       → Cases
团队成员       → TeamMembers
合作伙伴 Logo  → Partners
```

### 站点级配置

归入 `site.yaml`：

```text
站点名称
默认语言
base_url
主题名称
导航菜单
默认 SEO
联系邮箱
电话
社交媒体链接
```

---

## 18.6 主题生成规则

### Layout 生成

`layouts/base.html` 应包含：

```html
<!doctype html>
<html lang="{{ site.language }}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{ page.seo_title | default: site.title }}</title>
  <meta name="description" content="{{ page.seo_description | default: site.description }}">
  <link rel="stylesheet" href="/assets/css/style.css">
</head>
<body>
  {{ include "components/header.html" }}
  <main>
    {{ content }}
  </main>
  {{ include "components/footer.html" }}
  <script src="/assets/js/app.js"></script>
</body>
</html>
```

---

### 首页模板生成

`pages/index.html` 应采用 Sections 驱动：

```html
{{ for section in page.sections }}
  {{ include "components/section-renderer.html" section }}
{{ end }}
```

---

### 列表页模板生成

文章列表：

```html
<section class="page-header">
  <h1>{{ page.title }}</h1>
  <p>{{ page.summary }}</p>
</section>

<section class="article-grid">
  {{ for post in posts }}
    {{ include "components/article-card.html" post }}
  {{ end }}
</section>

{{ include "components/pagination.html" }}
```

---

### 详情页模板生成

文章详情：

```html
<article class="article-detail">
  <header>
    <h1>{{ post.title }}</h1>
    <p>{{ post.summary }}</p>
  </header>

  <div class="content">
    {{ post.content }}
  </div>
</article>
```

---

## 18.7 `site.yaml` 生成规则

导入器应根据 Demo 自动生成基础 `site.yaml`：

```yaml
site:
  name: "silkroadbiz"
  title: "silkroadbiz"
  description: "Generated from HTML Demo"
  baseUrl: "/"
  language: "zh"
  seo:
    renderMode: inject
  collections:
    page:
      permalink: "/{slug}/"
      template: "pages/page.html"
    post:
      permalink: "/insights/{slug}/"
      template: "pages/article.html"
      listRoute: "/insights/"
      listTemplate: "pages/insights.html"
    company:
      permalink: "/companies/{slug}/"
      template: "pages/company.html"
      listRoute: "/companies/"
      listTemplate: "pages/companies.html"
    service:
      permalink: "/services/{slug}/"
      template: "pages/service.html"
      listRoute: "/services/"
      listTemplate: "pages/services.html"

content:
  provider: markdown
  markdown:
    dir: "sites/silkroadbiz/content"
    defaultType: page

build:
  output: "dist"
  clean: true

theme:
  name: "silkroadbiz"
```

如果 `--content-source json` 或 `--content-source yaml`，该选项只影响 seed 审核文件的输出目录/格式意图；当前默认可构建工程仍由本地 markdown content 驱动。

`--content-source notion` 生成 `sites/silkroadbiz/notion-seed/*.json`；`json` 生成 `sites/silkroadbiz/data/*.json`；`yaml` 生成 `sites/silkroadbiz/data/*.yaml`，供后续人工或外部命令导入。

JSON/YAML seed 如果需要回灌为本地 markdown content，可使用：

```bash
bukit import seed sites/silkroadbiz/data --output sites/silkroadbiz/content --force
```

该命令只处理 `pages`、`posts`、`companies`、`services` 四类内容记录，并写入 `content/pages/`、`content/posts/`、`content/companies/`、`content/services/`。FAQ、media、components 仍作为审核数据保留，不会自动变成可路由页面。

---

## 18.8 Notion 写入边界

`bukit import html-demo` 默认不应该直接写入 Notion。

推荐设计：

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

只生成：

```text
notion-seed/*.json
site.yaml
theme files
report
```

然后由独立命令写入 Notion：

```bash
bukit notion push \
  --input sites/silkroadbiz/notion-seed \
  --database-id <notion-database-id> \
  --dry-run
```

当前实现首先生成本地推送计划 `notion-push-plan.json`。非 `--dry-run` 会校验 `NOTION_TOKEN`（或 `--token-env` 指定的环境变量），但实际数据库字段映射仍应在人工审核推送计划后按目标 Notion schema 明确配置。

原因：

- 导入阶段需要人工检查
- Notion 写入是外部副作用
- 避免错误 Demo 直接污染正式内容库
- 方便 dry-run 和版本控制

如果未来支持一体化，可增加显式参数：

```bash
bukit import html-demo ./demo \
  --theme silkroadbiz \
  --push-notion \
  --notion-site silkroadbiz
```

但正式写入必须要求显式确认。

---

## 18.9 导入报告

每次导入必须生成：

```text
sites/{site}/import-report.md
```

报告内容：

```markdown
# HTML Demo Import Report

## Summary

- Input: ./demo
- Theme: silkroadbiz
- Content Source: notion
- Pages Found: 8
- Components Generated: 12
- Posts Extracted: 6
- Companies Extracted: 10
- FAQs Extracted: 8
- Warnings: 3
- Errors: 0

## Pages

| Source | Route | Type | Template | Status |
|---|---|---|---|---|
| index.html | / | Home | index | generated |
| insights.html | /insights/ | PostList | insights | generated |

## Components

| Component | Source | Status |
|---|---|---|
| header.html | repeated top section | generated |
| footer.html | repeated bottom section | generated |
| hero.html | .hero | generated |

## Content Seeds

| Seed File | Count |
|---|---:|
| pages.json | 5 |
| sections.json | 12 |
| posts.json | 6 |
| companies.json | 10 |
| faqs.json | 8 |

## Hardcoded Residuals

- components/footer.html contains phone number: +60 12-000 0000
- pages/index.html contains business phrase: 连接中国与马来西亚商业机会

## Manual Review Required

- Confirm article slugs
- Confirm company country classification
- Confirm SEO description quality
```

---

## 18.10 错误与警告策略

### 必须失败的错误

```text
输入目录不存在
index.html 不存在
HTML 完全不可解析
目标主题已存在且未传 --overwrite
检测到危险文件
输出目录不可写
```

### 可以警告但继续的问题

```text
部分图片缺失
部分页面缺少 title
部分链接无法识别
SEO description 缺失
无法判断某些卡片类型
存在疑似硬编码残留
```

### 严格模式

如果使用：

```bash
bukit import html-demo ./demo --theme silkroadbiz --strict
```

以下问题也应视为失败：

```text
缺少 SEO description
存在无法分类内容
存在硬编码业务文案残留
存在空 slug
存在重复 slug
存在无效内部链接
```

---

## 18.11 安全策略

导入器必须执行安全检查。

### 文件安全

拒绝复制：

```text
.env
.env.*
*.pem
*.key
*.pfx
id_rsa
.git/
node_modules/
```

### HTML 安全

需要识别并报告：

```text
inline script
外部未知 script
iframe
form action 指向未知外部地址
javascript: URL
data: URL
onload / onclick 等 inline event handler
```

不建议默认删除所有内容，但必须在报告中标出，并在 `--strict` 下失败。

### URL 安全

对以下内容做校验：

```text
href
src
form action
canonical
og:image
```

禁止危险协议：

```text
javascript:
vbscript:
file:
```

---

## 18.12 资源处理策略

### CSS / JS

默认复制到：

```text
themes/{theme}/assets/css/
themes/{theme}/assets/js/
```

并更新模板引用路径。

### Images

默认复制到：

```text
themes/{theme}/assets/images/
```

同时生成 `media.json`：

```json
[
  {
    "source": "assets/images/hero.jpg",
    "target": "/assets/images/hero.jpg",
    "used_by": ["index.html"],
    "alt": "",
    "status": "copied"
  }
]
```

### 外部资源

外部 CDN 默认不下载，只记录：

```json
{
  "url": "https://cdn.example.com/lib.js",
  "type": "script",
  "status": "external",
  "review_required": true
}
```

---

## 18.13 生成模式

建议支持三种生成模式。

### 1. Draft 模式

默认模式。

```bash
bukit import html-demo ./demo --theme silkroadbiz
```

特点：

- 尽量生成可运行工程
- 遇到不确定内容写入报告
- 适合人工继续修正

---

### 2. Strict 模式

```bash
bukit import html-demo ./demo --theme silkroadbiz --strict
```

特点：

- 更强校验
- 不允许明显硬编码残留
- 不允许重复 slug
- 不允许无效链接
- 适合 CI/CD

---

### 3. Analyze-only 模式

```bash
bukit import html-demo ./demo --theme silkroadbiz --dry-run
```

特点：

- 不写入文件
- 只输出分析结果
- 适合先判断 Demo 是否值得迁移

---

## 18.14 内部模块设计

建议在 Bukit 中拆分如下模块：

```text
Bukit.Importing/
├── HtmlDemoImporter.cs
├── HtmlDemoScanner.cs
├── HtmlDocumentParser.cs
├── PageClassifier.cs
├── LayoutExtractor.cs
├── ComponentExtractor.cs
├── ContentExtractor.cs
├── SeoExtractor.cs
├── AssetImporter.cs
├── ThemeGenerator.cs
├── SeedGenerator.cs
├── SiteConfigGenerator.cs
├── ImportReportWriter.cs
├── ImportSafetyScanner.cs
└── ImportDiagnostics.cs
```

### 核心对象

```csharp
public sealed record HtmlDemoImportOptions
{
    public required string InputPath { get; init; }
    public required string ThemeName { get; init; }
    public string? SitePath { get; init; }
    public string ContentSource { get; init; } = "notion";
    public bool ExtractContent { get; init; } = true;
    public bool GenerateSeed { get; init; } = true;
    public bool DryRun { get; init; }
    public bool Strict { get; init; }
    public bool Overwrite { get; init; }
    public string Language { get; init; } = "zh";
}
```

```csharp
public sealed record HtmlDemoImportResult
{
    public required string ThemePath { get; init; }
    public required string SitePath { get; init; }
    public int PagesFound { get; init; }
    public int ComponentsGenerated { get; init; }
    public int RecordsExtracted { get; init; }
    public IReadOnlyList<ImportDiagnostic> Diagnostics { get; init; } = [];
}
```

---

## 18.15 验收标准

`bukit import html-demo` 完成后，应满足：

```text
1. 生成 themes/{theme}
2. 生成 sites/{site}/site.yaml
3. 生成 notion-seed/*.json
4. 生成 import-report.md
5. Header / Footer 已组件化
6. 列表页已改为数据循环
7. 详情页已改为数据模板
8. 首页核心区块已进入 sections.json
9. 文章 / 企业 / FAQ 已进入集合 seed
10. 模板中不应残留大量业务硬编码
11. 可以执行 bukit build
12. 构建结果与原 Demo 视觉上基本一致
```

---

## 18.16 推荐执行流程

完整流程建议如下：

```bash
# 1. 先分析 Demo
bukit import html-demo ./demo --theme silkroadbiz --dry-run

# 2. 生成 Bukit 工程草稿
bukit import html-demo ./demo --theme silkroadbiz --site-path sites/silkroadbiz

# 3. 人工检查导入报告
cat sites/silkroadbiz/import-report.md

# 4. 检查 seed 数据
ls sites/silkroadbiz/notion-seed

# 5. 写入 Notion
bukit notion push --input sites/silkroadbiz/notion-seed

# 6. 构建
bukit build --config sites/silkroadbiz/site.yaml

# 7. 本地预览
bukit preview --dir dist
```

---

## 18.17 与 BukitJalil 的关系

`bukit import html-demo` 是 CLI 层能力。

BukitJalil 可以在 UI 中封装它：

```text
上传 / 选择 HTML Demo
    ↓
AI 分析 Demo
    ↓
执行 bukit import html-demo
    ↓
展示导入报告
    ↓
人工确认内容分类
    ↓
写入 Notion
    ↓
触发 Bukit Build
    ↓
部署 Preview
```

BukitJalil 不应该替代 CLI，而是提供更友好的操作界面和 AI 辅助修正能力。

---

## 18.18 最终定义

统一 Demo 导入方案可以定义为：

```text
bukit import html-demo ./demo --theme silkroadbiz
```

该命令完成：

```text
导入 HTML Demo
结构识别
Layout 拆分
组件拆分
内容数据化
Seed 数据生成
主题工程生成
站点配置生成
安全扫描
导入报告生成
```

该命令不默认完成：

```text
直接写入 Notion
直接部署生产环境
直接覆盖已有线上站点
直接删除原始内容
```

推荐边界：

> Import 负责生成可审查的 Bukit 工程草稿；Notion Push 负责内容入库；Build 负责构建；Deploy 负责发布。

这可以确保 Demo-to-Bukit 既具备自动化效率，又保留工程可控性、安全性和可审计性。
