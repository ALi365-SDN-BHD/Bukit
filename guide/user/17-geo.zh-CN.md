# 17 生成式引擎优化（GEO）：llms.txt、AI爬虫与结构化数据

GEO 让你的 Bukit 站点能被 AI 驱动的搜索引擎（ChatGPT Search、Perplexity、Google AI Overviews、Bing Copilot）发现和阅读，超越传统 SEO。

可运行示例：`examples/starter/site.i18n.seo.yaml`

## 你将获得

- 为 AI 引擎自动生成 `llms.txt` 和 `llms-full.txt`
- 自动 AI 爬虫 `robots.txt` 规则（识别 12 种机器人）
- 从内容 Front Matter 生成 FAQPage、HowTo、Article 结构化数据
- GEO 审计及数值评分（0–100）
- 构建时诊断警告（缺失或格式错误的 GEO 数据）

## 第一步：启用 GEO

GEO 配置位于 `site.seo.geo` 下。所有字段都有合理默认值，最简配置：

```yaml
site:
  seo:
    enabled: true
    geo:
      enabled: true
```

仅此即可生成 `llms.txt` 并允许 AI 爬虫访问。无需额外配置即可实现基本 GEO。

## 第二步：配置 AI 爬虫访问权限

控制哪些 AI 机器人可以抓取你的站点：

```yaml
site:
  seo:
    geo:
      aiBotMode: selective       # allow | block | selective
      aiBotAllowList:
        - GPTBot
        - PerplexityBot
        - Google-Extended
      aiBotBlockList:
        - CCBot
```

**识别的 AI 机器人**：GPTBot、ChatGPT-User、Google-Extended、Claude-Web、ClaudeBot、Anthropic-AI、PerplexityBot、Cohere-AI、CCBot、Diffbot、FacebookBot、OAI-SearchBot。

| 模式 | 行为 |
|------|------|
| `allow` | 允许所有 AI 机器人（默认） |
| `block` | 阻止所有 AI 机器人 |
| `selective` | 白名单通过，其他阻止 |

## 第三步：在内容中添加 GEO 结构化数据

在 Front Matter 中添加 `geo:` 字段以生成丰富的 Schema.org JSON-LD：

### FAQ 页面

```yaml
---
title: 常见问题
collection: page
geo:
  schema_type: FAQPage
  faq:
    - question: Bukit 支持哪些内容来源？
      answer: Notion、Markdown 和本地文件。
    - question: 如何部署？
      answer: 通过 bukit deploy 部署到 GitHub Pages。
---
```

### HowTo 指南

```yaml
---
title: 如何用 Bukit 搭建博客
collection: post
geo:
  schema_type: HowTo
  about: 静态站点生成
  steps:
    - name: 下载 Bukit
      text: 从 GitHub Releases 下载对应平台的二进制文件。
      image: /assets/images/download.png
    - name: 初始化站点
      text: 运行 bukit init my-blog。
    - name: 创建内容
      text: 在 content/ 目录中添加 markdown 文件。
---
```

### 带作者信息的文章

```yaml
---
title: 静态站点的未来
collection: post
geo:
  schema_type: Article
  about: Web 开发
  date_reviewed: "2026-05-19"
  author:
    name: 张三
    url: https://example.com/about
    same_as:
      - https://github.com/zhangsan
      - https://twitter.com/zhangsan
---
```

## 第四步：生成 llms-full.txt（可选）

默认只生成包含标题和摘要的 `llms.txt`。要包含完整页面内容：

```yaml
site:
  seo:
    geo:
      llmsFullTxt: true
```

## 第五步：自定义 llms.txt

控制文章数量和添加外部链接：

```yaml
site:
  seo:
    geo:
      llmsTxtMaxArticles: 30
      llmsTxtOptionalLinks:
        - title: GitHub 仓库
          url: https://github.com/user/repo
          description: 源代码和问题跟踪
```

## 第六步：运行 GEO 审计

```bash
bukit build
bukit geo audit --dir dist
```

### GEO 评分解读

| 指标 | 满分 |
|------|------|
| llms.txt 已生成 | 25 |
| llms-full.txt 已生成 | 15 |
| 至少有 1 条 GEO 增强路由 | 10 |
| Article schema 覆盖率 | 15 |
| FAQPage 或 HowTo 已使用 | 15 |
| Person 作者 schema 已使用 | 10 |
| SpeakableSpecification 已使用 | 5 |
| 多条路由有 GEO 覆盖 | 5 |

## 常见问题

| 问题 | 原因 | 修复 |
|------|------|------|
| llms.txt 未生成 | `geo.enabled: false` 或 `geo.llmsTxt: false` | 启用 GEO + llmsTxt |
| FAQPage schema 不出现 | `geo.faq` 数组为空 | 添加至少一条 FAQ |
| HowTo schema 不出现 | `geo.steps` 数组为空 | 添加至少一个步骤 |
| GEO Score 为 0 | 无 llms.txt，无 GEO front matter | 启用 llmsTxt，添加 `geo:` 字段 |

## 下一步

- [12 CLI 参考](./12-cli-reference.md)
- [11 I18n & SEO](./11-i18n-seo.md)
- [开发者: GEO 架构](../dev/geo.md)
