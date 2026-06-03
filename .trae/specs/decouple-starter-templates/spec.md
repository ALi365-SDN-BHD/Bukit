# 解耦 Starter 共享模板 Spec

## Why

当前 starter 模板中 `page.html`（about/contact/join 共用）、`list.html`（资讯列表/企业列表共用）、`post.html`（资讯详情 + 侧边栏）三个模板被多个不同功能的内容类型共享。这导致：

1. **模板缺乏针对性**：about 页面和 contact 页面功能完全不同，却共用同一个模板，无法为特定页面类型做定制化设计
2. **误导用户**：新人看到 starter 后会认为"共享模板"是 Bukit 的固有模式，实际上这只是 starter 的配置选择
3. **URL 判断反模式**：用户可能在模板中通过判断 URL 来做条件渲染，这是脆弱的做法

## What Changes

- **分析与澄清**：明确共享模板属于 starter 主题而非 Bukit 引擎核心，引擎支持独立模板
- **文档增强**：在 `guide/user/08-themes-templates.md` 中补充"如何为不同内容类型使用独立模板"的最佳实践说明
- **Starter 重构**：将 starter 的共享模板拆分为独立模板，每种页面类型有专属模板

## Impact

- Affected specs: 无
- Affected code:
  - `examples/starter/site.yaml` — 拆分 collection 配置
  - `examples/starter/layouts/pages/` — 新增独立模板文件
  - `examples/starter/content/` — 更新 front matter
  - `guide/user/08-themes-templates.md` — 补充最佳实践文档

## Analysis: 共享模板的归属

### 共享模板属于 Starter 主题，不是 Bukit 引擎

| 模板 | 路径 | 归属 |
|------|------|------|
| `page.html` | `examples/starter/layouts/pages/page.html` | **Starter 主题** |
| `post.html` | `examples/starter/layouts/pages/post.html` | **Starter 主题** |
| `list.html` | `examples/starter/layouts/pages/list.html` | **Starter 主题** |

Bukit 引擎核心的职责仅限于：
- 根据 `site.yaml` 中 `collections.<key>.template` 配置决定每个内容项使用哪个模板文件（`RouteGenerator.cs`）
- 在 layouts 目录中查找并加载模板文件（`FileTemplateLoader.cs`）
- 将数据模型注入 Scriban 渲染（`ScribanTemplateRenderer.cs`）

引擎核心**不包含任何具体 HTML 模板**，也不强制任何"共享模板"模式。

### 为什么当前 starter 会共用模板

根本原因在于 `site.yaml` 的 collection 配置：

```yaml
# 当前 starter 配置
collections:
  page:
    permalink: /pages/{slug}/
    template: pages/page.html   # about/contact/join 都走同一个模板
  post:
    permalink: /blog/{slug}/
    template: pages/post.html   # 所有文章共用
```

所有 `collection: page` 的内容（about, contact, join）都走同一个 `template: pages/page.html`，自然看起来像"共用"。

### 引擎已支持独立模板，无需改代码

三种方式可实现独立模板：

1. **Collection 级别**：为每种内容类型创建独立 collection，各配专用 template
2. **单页 front matter 覆盖**：在 Markdown front matter 中设置 `route.template` 或 `template`
3. **FilteredList**：同一 collection 不同子集使用不同 `listTemplate`

## ADDED Requirements

### Requirement: Starter 模板独立化

Starter 示例站点的每个功能页面应使用独立模板，不再通过一个模板服务多种不同用途的页面。

#### Scenario: about 页面有专属模板
- **GIVEN** starter 站点包含 about, contact, join 三种页面
- **WHEN** 构建站点
- **THEN** about 使用 `pages/about.html`，contact 使用 `pages/contact.html`，join 使用 `pages/join.html`

#### Scenario: 不同内容类型的列表页有专属模板
- **GIVEN** starter 站点有文章列表和企业列表
- **WHEN** 构建站点
- **THEN** 文章列表使用专属 list template，企业列表使用专属 list template

### Requirement: 最佳实践文档补充

用户指南中应明确说明模板选择机制，引导用户为不同内容类型使用独立模板。

#### Scenario: 用户查阅模板文档
- **WHEN** 用户阅读 `guide/user/08-themes-templates.md`
- **THEN** 能看到"如何为不同内容类型配置独立模板"的说明和示例
