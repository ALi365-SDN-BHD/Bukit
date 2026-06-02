# SilkRoad Biz Demo → Bukit Theme 迁移计划

## 目标

将 `/Users/ali/Documents/trae_projects/silkroad_biz/demo/` 静态 HTML demo 迁移为 Bukit 主题 `silkroad-biz`，保留原有的视觉设计和内容结构。

***

## 源项目分析

### 结构概览

* **26 个文件**：8 种页面类型、1 个 CSS 文件、1 个 JS 文件

* **纯静态 HTML**：无构建工具、无框架、无数据层

* **设计系统**：深绿(#0f3d2e) + 金色(#c9a227) + 奶油(#f7f2e8) 配色

* **字体**：Noto Sans SC（正文）+ Noto Serif SC（标题）

* **布局**：CSS Grid 多列布局、sticky header、侧边栏

### 页面类型映射

| 原始页面                         | Bukit 模板                    | 集合类型           |
| ---------------------------- | --------------------------- | -------------- |
| `index.html` (首页)            | `pages/index.html`          | 首页             |
| `insights.html` (商务资讯)       | `pages/insights.html` (列表)  | posts (文章)     |
| `article-detail.html` (文章详情) | `pages/article.html` (详情)   | posts (文章)     |
| `companies.html` (企业资源库)     | `pages/companies.html` (列表) | companies (企业) |
| `company-detail.html` (企业详情) | `pages/company.html` (详情)   | companies (企业) |
| `join.html` (申请入驻)           | `pages/page.html`           | page (单页)      |
| `about.html` (关于商讯)          | `pages/page.html`           | page (单页)      |
| `contact.html` (联系我们)        | `pages/page.html`           | page (单页)      |

***

## 实施步骤

### 步骤 1：执行 `bukit import html-demo` 命令

使用 Bukit CLI 自动扫描 demo 目录并生成主题骨架。

```bash
cd /Users/ali/mydev/Git/Github/Bukit

dotnet src/Bukit.Cli/bin/Release/net10.0/bukit.dll import html-demo \
  /Users/ali/Documents/trae_projects/silkroad_biz/demo \
  --theme silkroad-biz \
  --force \
  --verify
```

**预期产出**：

* `themes/silkroad-biz/` — 主题目录（layouts、partials、assets）

* `sites/silkroad-biz/site.yaml` — 站点配置

* `sites/silkroad-biz/content/` — Markdown 内容草稿

* `sites/silkroad-biz/import-report.md` — 导入报告

* `sites/silkroad-biz/original-demo/` — 原始 demo 备份

### 步骤 2：审查导入报告

读取 `sites/silkroad-biz/import-report.md`，检查：

* 扫描到的页面数量和类型

* 生成的模板列表

* 提取的设计 token

* 诊断信息和硬编码内容残留

### 步骤 3：修正站点配置 `sites/silkroad-biz/site.yaml`

根据原始 demo 的信息修改 site.yaml：

```yaml
site:
  name: silkroad-biz
  title: 丝路商讯 SilkRoute Business Insight
  description: 马来西亚-中国商业情报与企业资源平台
  baseUrl: /
  language: zh-CN
  seo:
    renderMode: inject
  url: https://example.com  # 上线前替换
    
  collections:
    page:
      permalink: '/{slug}/'
      template: 'pages/page.html'
    post:
      permalink: '/insights/{slug}/'
      template: 'pages/article.html'
      listRoute: '/insights/'
      sortBy: publish_date
      sortDirection: desc
    company:
      permalink: '/companies/{slug}/'
      template: 'pages/company.html'
      listRoute: '/companies/'
      sortBy: title
      sortDirection: asc

content:
  provider: markdown
  contentDir: sites/silkroad-biz/content

theme:
  name: silkroad-biz
  params:
    brand: 丝路商讯
    footer_text: 丝路商讯 SilkRoute Business Insight
    primary_color: '#0f3d2e'
    accent_color: '#c9a227'
    bg_color: '#fbf8f1'
    font_family: '"Noto Sans SC", "Microsoft YaHei", Arial, sans-serif'
    font_family_heading: '"Noto Serif SC", serif'
    nav_items:
      - { label: '首页', url: '/' }
      - { label: '商务资讯', url: '/insights/' }
      - { label: '企业资源库', url: '/companies/' }
      - { label: '申请入驻', url: '/join/' }
      - { label: '关于商讯', url: '/about/' }
      - { label: '联系我们', url: '/contact/' }
```

### 步骤 4：迁移设计系统到 CSS

将原始 demo 的 `assets/css/style.css` 设计 token 适配到 Bukit 主题的 `themes/silkroad-biz/assets/style.css`（如 import 命令已生成则在其基础上修改）：

**核心设计 token（从原始 demo 提取）**：

```css
:root {
  --green: #0f3d2e;       /* 品牌主色 */
  --dark: #08291f;         /* 深色/页脚 */
  --gold: #c9a227;         /* 强调色 */
  --cream: #f7f2e8;        /* 暖色背景 */
  --light: #fbf8f1;        /* 页面背景 */
  --text: #1f2933;         /* 正文 */
  --muted: #667085;        /* 次要文字 */
  --border: #e8dfcd;       /* 边框色 */
  --shadow: 0 18px 50px rgba(15,61,46,.1);
  --radius: 22px;          /* 大圆角 */
  --max: 1180px;           /* 最大宽度 */
}
```

**迁移的样式组件**：

* Header（sticky + backdrop blur）

* 导航菜单（桌面 + 移动端汉堡菜单）

* Hero 区域（渐变叠加背景）

* 卡片网格（`.entry-grid`、`.grid`）

* 侧边栏布局（`.article-layout`、`.company-layout`）

* 文章/企业详情页

* 筛选标签（filter pills）

* 分页导航

* 表单样式（入驻申请）

* 关于页（双栏 + 步骤卡片）

* 联系页（信息卡片）

* 页脚（4 列网格）

* 响应式断点（980px）

* 按钮样式（`.btn-gold`、`.btn-primary`）

### 步骤 5：精细化模板

根据原始 demo 的页面结构，调整生成的 Scriban 模板：

#### `layouts/base.html`

* `<html lang="zh-CN">`

* 引入 Noto Sans SC / Noto Serif SC 字体（Google Fonts CDN）

* 链接 `{{ site.base_url }}/assets/style.css`

* 链接 `{{ site.base_url }}/assets/main.js`

* `{{ include "partials/header.html" }}`

* `<main>{{ content }}</main>`

* `{{ include "partials/footer.html" }}`

#### `partials/header.html`

* 站点标题 "丝路商讯"

* 导航菜单（从 site.params.nav\_items 循环生成）

* "申请入驻" CTA 按钮（金色渐变）

* 语言切换器（占位）

* 移动端汉堡菜单按钮

#### `partials/footer.html`

* 4 列网格布局

* 平台介绍、快速链接、联系方式、关注我们

* "Powered by bukit" 归属链接

#### `pages/index.html`（首页）

* Hero 区域：大标题 + 副标题 + 三个 CTA 按钮

* 入口卡片区（商务资讯 + 企业资源库）

* 精选文章网格（从 posts 集合取前 3 篇）

* 精选企业卡片区（从 companies 集合取前 4 条）

* CTA 横幅

#### `pages/article.html`（文章详情）

* 封面图（如 `page.fields.cover.value` 存在）

* 文章标题 + 元信息

* `.article-layout` 双栏布局（正文 + 侧边栏）

* 侧边栏：相关文章 + 相关链接

* 提示/声明区块

#### `pages/insights.html`（文章列表）

* 页面标题

* 筛选标签栏（从 `page.fields.tags.value` 或分类生成）

* 文章卡片网格（3 列）

* 分页导航

#### `pages/company.html`（企业详情）

* 企业头信息（logo 首字母 + 分类标签 + 名称 + 简介）

* 封面图

* 内容区：简介、核心能力（列表）、合作方向

* `.company-layout` 侧边栏（企业信息 + 商务链接）

* 免责声明

#### `pages/companies.html`（企业列表）

* 筛选标签栏

* 分组标题（中国企业 / 马来西亚企业）

* 企业卡片网格

* 分页导航

#### `pages/page.html`（通用页面：关于、联系、入驻）

* 标题 + 内容渲染

* 保持原始 HTML 结构

### 步骤 6：迁移 JavaScript 行为

将原始 `assets/js/main.js` 的移动端菜单切换逻辑适配到 Bukit 主题的 `themes/silkroad-biz/assets/main.js`：

```javascript
(function(){
  var toggle = document.querySelector('.mobile-toggle');
  var nav = document.querySelector('.nav-menu');
  if (!toggle || !nav) return;
  toggle.addEventListener('click', function(){
    var isOpen = nav.style.display === 'flex';
    nav.style.display = isOpen ? 'none' : 'flex';
  });
})();
```

### 步骤 7：运行 doctor 和 build 验证

```bash
# 1. 诊断配置和模板
dotnet src/Bukit.Cli/bin/Release/net10.0/bukit.dll doctor \
  --config sites/silkroad-biz/site.yaml

# 2. 构建站点
dotnet src/Bukit.Cli/bin/Release/net10.0/bukit.dll build \
  --config sites/silkroad-biz/site.yaml

# 3. 预览
dotnet src/Bukit.Cli/bin/Release/net10.0/bukit.dll preview \
  --config sites/silkroad-biz/site.yaml
```

### 步骤 8：视觉对比和调优

1. 在浏览器中打开 `http://localhost:4173` 预览
2. 与原始 demo 进行视觉对比（桌面端 + 移动端 980px 断点下）
3. 调整 CSS 细节直到视觉一致
4. 确保所有导航链接正常工作

***

## 关键风险和注意事项

1. **搜索功能和表单提交**：原始 demo 的搜索和表单仅为静态占位，迁移后同样保持静态展示
2. **语言切换器**：原始 demo 含非功能性的语言切换器，迁移后保持占位
3. **分页**：原始 demo 有手动复制的 page-1/page-2 页面，Bukit 会自动处理分页
4. **内容数据**：原始 demo 有 3 篇实际文章和 9 个企业条目，import 命令将尝试提取为 Markdown 内容文件
5. **Noto 字体**：需要从 Google Fonts CDN 加载，或本地化到 static 目录

***

## 最终产出

```
themes/silkroad-biz/
  theme.yaml                    # 主题元数据
  layouts/
    layouts/base.html           # 基础布局
    pages/
      index.html                # 首页
      page.html                 # 通用页面
      article.html              # 文章详情
      insights.html             # 文章列表
      company.html              # 企业详情
      companies.html            # 企业列表
    partials/
      header.html               # 导航头
      footer.html               # 页脚
    components/
      article-card.html         # 文章卡片
      company-card.html         # 企业卡片
      cta.html                  # CTA 横幅
    bukit.templates.yaml        # 模板能力声明
  assets/
    style.css                   # 完整样式表
    main.js                     # 移动端菜单 JS
  static/
    (如有需要本地化的资源)

sites/silkroad-biz/
  site.yaml                     # 站点配置
  content/
    posts/                      # 文章 Markdown
    companies/                  # 企业 Markdown
    pages/                      # 单页 Markdown
  import-report.md              # 导入报告
  original-demo/                # 原始 demo 备份
```

