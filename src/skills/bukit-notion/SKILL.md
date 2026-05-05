---
name: bukit-notion
description: Use when configuring Notion as a content source for Bukit, troubleshooting Notion content fetch failures or incomplete data, understanding property mapping rules, or dealing with image localization issues
---

# Bukit Notion 内容源

## Overview

Bukit 通过 Notion API 将数据库页面转换为 `ContentItem`，支持 26 种块类型的 HTML 渲染和 18 种属性类型的字段映射。配置集中在 site.yaml 的 `content.notion` 节点。

## 前置准备

### 1. 创建 Notion Integration

1. 访问 [Notion Integrations](https://www.notion.so/my-integrations)
2. 创建 Internal Integration，获取 API Key（即 `NOTION_TOKEN`）
3. 设置环境变量：`NOTION_TOKEN=secret_xxxx`

### 2. 获取数据库 ID

从 Notion 数据库页面的 URL 中提取。例如：
- `https://www.notion.so/workspace/abc123?v=...` → 数据库 ID 为 `abc123`

### 3. 授权 Integration 访问数据库

在 Notion 数据库中点击右上角 `...` → Connections → 添加刚创建的 Integration。

### 验证

```bash
bukit doctor
```
会检查 Notion API 连通性和数据库可达性。

## Notion 属性映射规则

| Notion 类型 | 映射类型 | 值来源 | 模板访问 |
|------------|---------|--------|---------|
| `title` | `text` | 纯文本提取 | `page.fields.Name.value` |
| `rich_text` | `text` | 纯文本提取 | `page.fields.Description.value` |
| `url` | `text` | URL 字符串 | `page.fields.Link.value` |
| `email` | `text` | 邮箱字符串 | `page.fields.Email.value` |
| `phone_number` | `text` | 电话号码 | — |
| `number` | `number` | 数值 | `page.fields.Price.value` |
| `checkbox` | `bool` | true/false | `page.fields.Published.value` |
| `date` | `date` | DateTimeOffset | `page.fields.Date.value` |
| `created_time` | `date` 或 `text` | 创建时间 | — |
| `last_edited_time` | `date` 或 `text` | 最后编辑时间 | — |
| `created_by` | `text` | 用户名或 ID | — |
| `last_edited_by` | `text` | 用户名或 ID | — |
| `select` | `text` | 选中项名称 | `page.fields.Category.value` |
| `status` | `text` | 状态名称 | `page.fields.Status.value` |
| `multi_select` | `list` | 选中项名称数组 | `for tag in page.fields.Tags.value` |
| `people` | `list` | 用户名数组 | — |
| `files` | `list` | 文件 URL 数组 | — |
| `relation` | `list` | 关联页面 ID 数组 | — |
| `formula` | 动态 | 根据公式结果类型 | — |
| `rollup` | 动态 | 根据汇总结果类型 | — |

**字段过滤**：`fieldPolicy.mode: whitelist` 时，`fieldPolicy.allowed` 列表外的属性不会出现在 `page.fields` 中。

## 块渲染支持

26 种 Notion 块类型自动渲染为 HTML：

| 块类型 | 渲染为 | CSS 类 |
|--------|--------|--------|
| 段落 (paragraph) | `<p>` | — |
| 标题 1/2/3 | `<h1>`/`<h2>`/`<h3>` | — |
| 引用 (quote) | `<blockquote>` | — |
| 代码 (code) | `<pre><code>` + 语言标注 | — |
| 分割线 (divider) | `<hr>` | — |
| 图片 (image) | `<figure><img><figcaption>` | — |
| 标注 (callout) | `<div class="callout">` + 图标 | `.callout`, `.callout-icon`, `.callout-content` |
| 待办事项 (to_do) | `<div class="to-do"><input type="checkbox">` | `.to-do` |
| 折叠块 (toggle) | `<details><summary>` | — |
| 书签 (bookmark) | `<a class="bookmark">` | `a.bookmark` |
| 嵌入 (embed) | `<div class="video-embed"><iframe>` | `.video-embed` |
| 公式 (equation) | `<div class="math-block">` | `.math-block`, `.math-inline` |
| 表格 (table) | `<table>` | — |
| 列布局 (column_list) | `<div class="notion-columns">` | `.notion-columns`, `.notion-column` |
| 音频 (audio) | `<audio>` | — |
| 视频 (video) | `<video>` | — |
| 文件 (file) | 下载链接 | `.notion-file` |
| PDF | PDF 查看器 | `.notion-pdf` |
| 链接预览 (link_preview) | 链接卡片 | — |
| 页面链接 (link_to_page) | 内部链接 | `.notion-child-page` |
| 同步块 (synced_block) | 内联渲染 | — |
| 目录 (table_of_contents) | 占位符 | — |
| 子实体 (child_entity) | 子页面引用 | `.notion-child-database` |

默认主题 CSS（`bukit init` 生成）已包含所有这些 CSS 类的样式。

## 关联关系处理

`relation` 类型字段会被解析为关联页面 ID 数组。Bukit 会自动解析关联关系，在 `page.fields.RelatedField.value` 中提供引用信息。

## 图片本地化

Bukit 自动将 Notion 中的远程图片下载到本地并重写 HTML 中的 URL：

```yaml
content:
  media:
    downloadToLocal: true        # 启用下载
    downloadDir: assets/uploads  # 下载目录
    urlBase: /assets/uploads     # HTML 中替换后的前缀
    fieldKeys: [cover, image, thumbnail, og_image, icon]  # 要处理的字段
    maxConcurrency: 4            # 下载并发度
    maxFileSizeBytes: 52428800   # 最大文件 50MB
    blockPrivateNetworks: true   # 阻止内网地址
```

## 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 内容拉取为空 | 数据库未授权给 Integration | 在 Notion 数据库 Connections 中添加 Integration |
| `NOTION_TOKEN is required` | 环境变量未设置 | `export NOTION_TOKEN=secret_xxx` 或 Windows `$env:NOTION_TOKEN = "secret_xxx"` |
| 401 Unauthorized | Token 无效或过期 | 重新生成 Integration Token |
| 404 Not Found | databaseId 错误 | 从 Notion URL 重新提取数据库 ID |
| 某些属性不显示 | `fieldPolicy.mode: whitelist` 限制 | 将需要的字段名加入 `fieldPolicy.allowed` 列表 |
| 图片未下载 | `downloadToLocal: false` | 设为 true；检查 `fieldKeys` 是否包含对应字段名 |
| 图片 404 | 下载失败或路径错误 | 检查 `downloadDir` 和 `urlBase` 配置 |
| API 速率限制 (429) | 请求过频繁 | 降低 `renderConcurrency` 和 `maxRps` |
| 构建缓慢 | Notion API 调用过多 | 启用 `cacheMode: readwrite` 缓存 |
| 关联页面内容缺失 | 关联解析未完成 | 确保关联的数据库也给 Integration 授权 |
| filterProperty 过滤无效 | 属性名不匹配或类型不对 | 确认 `filterProperty` 是 checkbox 类型，且 `filterType: checkbox_true` |
