# Bukit AI Demo-to-CMS Workflow

## 简介

本目录定义一套面向 ChatGPT、Codex、Cursor、Trae 与其他 AI Agent 的工程化网站生产流程。

核心目标不是生成一次性 HTML 页面，而是将用户需求转化为：

```text
用户需求
→ AI 生成可视化 HTML Demo
→ 用户确认样式、页面与功能
→ AI / Bukit 将 Demo 转换为 Bukit 主题与内容数据
→ Notion CMS 化
→ Bukit 构建与发布
```

该流程适用于企业官网、行业资讯站、企业目录站、产品展示站、招商站、本地服务站、SEO/GEO 内容站等以内容为核心的网站。

## 文件说明

| 文件 | 作用 |
|---|---|
| `README.md` | 流程入口、适用范围与快速开始 |
| `engineering-spec.md` | Demo、主题、数据、配置与构建的工程化规范 |
| `prompt-template.md` | 可直接交给 ChatGPT / Codex 使用的标准提示词 |
| `checklist.md` | Demo、工程化、Notion CMS 与发布阶段的检查清单 |

## 核心原则

1. **先 Demo，后工程化**  
   用户先确认视觉效果、页面结构和功能，再生成最终 Bukit 工程。

2. **Demo 必须可迁移**  
   Demo 不是一次性 HTML，必须具备语义化结构、标准 class、route-map 和可抽取内容。

3. **内容必须数据化**  
   页面正文、文章、企业资料、服务、SEO 等应进入内容文件或 Notion，而不是长期保留在模板中。

4. **主题只负责结构与表现**  
   主题中保留布局、组件、样式和模板变量，不保留大段业务文案。

5. **Bukit 负责质量门禁**  
   通过 `import-report.md`、`bukit doctor`、`bukit build`、Notion schema validate 等机制验证工程质量。

## 推荐流程

### 1. 生成 Demo

AI 根据用户需求生成：

```text
demo/
  index.html
  insights.html
  article-detail.html
  companies.html
  company-detail.html
  about.html
  contact.html
  assets/
demo.routes.yaml
```

### 2. 用户确认

用户确认：

- 视觉风格
- 首页布局
- 导航结构
- 列表页与详情页
- 移动端体验
- CTA 与文案方向
- URL 结构

### 3. 转换为 Bukit 工程

```bash
bukit import html-demo ./demo   --theme silkroadbiz   --content-source notion   --build-source markdown   --route-map demo.routes.yaml   --strict warn   --force   --verify
```

### 4. 推送到 Notion

```bash
bukit notion push   --input sites/silkroadbiz/notion-seed   --database-map sites/silkroadbiz/notion-seed/notion-database-map.yaml   --create-missing-databases   --parent-page-id <notion-parent-page-id>   --mode upsert   --update-content replace
```

### 5. 切换为 Notion-only 构建

```bash
bukit import html-demo ./demo   --theme silkroadbiz   --content-source notion   --build-source notion   --route-map demo.routes.yaml   --force
```

## 默认内容范围

默认 Notion push 集合：

```text
pages
posts
companies
services
```

默认 review-only seed：

```text
sections
faqs
media
components
```

如需将 review-only seed 完整 CMS 化，应为其设计独立 Notion schema。

## 下一步

- 阅读 [`engineering-spec.md`](./engineering-spec.md)
- 使用 [`prompt-template.md`](./prompt-template.md)
- 在每个阶段执行 [`checklist.md`](./checklist.md)
