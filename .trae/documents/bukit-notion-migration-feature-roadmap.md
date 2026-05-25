# Bukit Notion 内容迁移与多数据源能力功能改造清单

> 适用项目：Bukit 静态网站生成引擎  
> 目标方向：支持 Demo / Markdown 内容迁移到 Notion，并让 Bukit 可以直接从 Notion 构建网站  
> 使用场景：可交给 Codex、开发团队或 AI Agent 执行功能改造  
> 日期：2026-05-26

---

## 1. 改造背景

Bukit 当前的核心能力是静态网站生成。如果用户通过 `bukit create` 创建项目，默认内容通常来自本地 Markdown；如果用户将已有 Demo 迁移为 Bukit 主题，页面结构、文案、图片、列表数据往往混合在 HTML / CSS / JS / 模板中。

这会带来几个问题：

1. 内容和主题耦合严重，后期无法通过 CMS 管理。
2. Markdown 适合开发者，但不适合普通运营人员长期维护。
3. Demo 迁移后容易把业务文案硬编码到主题模板中。
4. 无法形成 “Notion 内容管理 → Bukit 构建 → 静态站部署” 的自动化闭环。
5. 后续 BukitJalil / AI Agent 很难基于结构化内容进行自动建站、自动发布和 SEO / GEO 优化。

因此，Bukit 需要新增一套正式的 **内容源抽象层 + Notion CMS 适配层 + 内容迁移能力**。

---

## 2. 总体目标

本次功能改造目标是让 Bukit 支持以下能力：

```text
1. Markdown → Notion
2. Demo HTML / 静态页面 → Bukit Theme + Notion
3. Notion → Bukit Build
4. Notion 数据结构初始化
5. Notion 数据校验
6. Markdown / Notion 内容源切换
7. 迁移记录与增量同步
8. 图片资源迁移
9. 主题模板内容硬编码检测
```

最终用户体验应为：

```bash
# 创建本地项目
bukit create silkroadbiz

# 初始化 Notion CMS 数据库结构
bukit notion init

# 将本地 Markdown 内容导入 Notion
bukit notion import markdown ./content

# 切换数据源为 Notion
bukit source use notion

# 从 Notion 构建静态网站
bukit build
```

如果用户是从 Demo 迁移：

```bash
bukit migrate demo ./demo \
  --theme silkroadbiz \
  --to notion \
  --extract-pages \
  --extract-sections \
  --extract-collections
```

---

## 3. 核心设计原则

### 3.1 内容与主题分离

Bukit 主题只负责：

```text
- 页面结构
- 模板布局
- 组件拆分
- 样式资源
- 路由模板
```

Notion / Markdown / JSON 等内容源负责：

```text
- 页面标题
- 页面正文
- 首页区块文案
- 文章内容
- 企业数据
- 图片地址
- SEO 字段
- 分类标签
- 多语言内容
```

### 3.2 渲染层不直接依赖 Markdown 或 Notion

所有内容源都必须先转换为统一模型：

```text
Markdown File  → BukitContentItem → Renderer
Notion Page    → BukitContentItem → Renderer
Demo Extractor → BukitContentItem → Notion Importer / Renderer
JSON File      → BukitContentItem → Renderer
```

### 3.3 Notion 是正式 CMS，Markdown 是开发与导入格式

推荐产品定位：

```text
开发阶段：Markdown
正式运营：Notion
自动化内容生产：Notion + AI Agent
```

---

## 4. 架构改造总览

建议新增如下模块：

```text
src/
├── Bukit.Core/
│   ├── Content/
│   │   ├── BukitContentItem.cs
│   │   ├── BukitContentCollection.cs
│   │   ├── IContentSource.cs
│   │   └── BukitContentSourceContext.cs
│   │
│   ├── Sources/
│   │   ├── Markdown/
│   │   │   ├── MarkdownContentSource.cs
│   │   │   ├── MarkdownFrontMatterParser.cs
│   │   │   └── MarkdownAssetScanner.cs
│   │   │
│   │   ├── Notion/
│   │   │   ├── NotionContentSource.cs
│   │   │   ├── NotionClient.cs
│   │   │   ├── NotionFieldMapper.cs
│   │   │   ├── NotionBlockConverter.cs
│   │   │   ├── NotionSchemaService.cs
│   │   │   └── NotionValidator.cs
│   │   │
│   │   └── Json/
│   │       └── JsonContentSource.cs
│   │
│   ├── Migrations/
│   │   ├── MigrationMapService.cs
│   │   ├── MarkdownToNotionMigration.cs
│   │   ├── DemoToNotionMigration.cs
│   │   └── MigrationReport.cs
│   │
│   ├── Assets/
│   │   └── AssetMigrationService.cs
│   │
│   ├── Validation/
│   │   ├── SlugValidator.cs
│   │   ├── ThemeContentLint.cs
│   │   └── ContentValidationReport.cs
│   │
│   └── Rendering/
│       └── RendererContextBuilder.cs
│
├── Bukit.Cli/
│   ├── Commands/
│   │   ├── NotionInitCommand.cs
│   │   ├── NotionImportMarkdownCommand.cs
│   │   ├── NotionValidateCommand.cs
│   │   ├── SourceUseCommand.cs
│   │   ├── MigrateDemoCommand.cs
│   │   └── ThemeLintCommand.cs
│
└── Bukit.Tests/
    ├── Content/
    ├── Sources/
    ├── Migrations/
    ├── Assets/
    └── Validation/
```

---

## 5. 功能改造清单

---

# Feature 1：统一内容模型 BukitContentItem

## 5.1 目标

让 Bukit 内部渲染系统不再直接绑定 Markdown 文件，也不直接绑定 Notion API，而是统一消费 `BukitContentItem`。

## 5.2 新增模型

```csharp
public sealed class BukitContentItem
{
    public string Id { get; set; } = "";
    public string SourceId { get; set; } = "";
    public string SourceType { get; set; } = ""; // markdown, notion, demo, json

    public string Type { get; set; } = ""; 
    // post, page, company, section, menu, category, tag, site

    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Summary { get; set; }

    public string? ContentMarkdown { get; set; }
    public string? ContentHtml { get; set; }

    public string? Template { get; set; }
    public string? Language { get; set; }

    public bool Published { get; set; }
    public DateTimeOffset? PublishAt { get; set; }

    public Dictionary<string, object?> Metadata { get; set; } = new();
}
```

## 5.3 内容集合模型

```csharp
public sealed class BukitContentCollection
{
    public IReadOnlyList<BukitContentItem> Items { get; init; } = [];

    public IEnumerable<BukitContentItem> Pages => Items.Where(x => x.Type == "page");
    public IEnumerable<BukitContentItem> Posts => Items.Where(x => x.Type == "post");
    public IEnumerable<BukitContentItem> Sections => Items.Where(x => x.Type == "section");
    public IEnumerable<BukitContentItem> Companies => Items.Where(x => x.Type == "company");
}
```

## 5.4 验收标准

- [ ] Markdown 内容可以转换为 `BukitContentItem`。
- [ ] Notion 内容可以转换为 `BukitContentItem`。
- [ ] 渲染层只依赖统一内容模型。
- [ ] 原有 Markdown 构建流程不被破坏。

---

# Feature 2：Content Source 抽象层

## 5.5 目标

支持 Markdown、Notion、JSON、未来 API 等多种数据源。

## 5.6 新增接口

```csharp
public interface IContentSource
{
    string Name { get; }

    Task<IReadOnlyList<BukitContentItem>> LoadAsync(
        BukitContentSourceContext context,
        CancellationToken cancellationToken = default);
}
```

## 5.7 新增上下文

```csharp
public sealed class BukitContentSourceContext
{
    public string ProjectRoot { get; set; } = "";
    public string Environment { get; set; } = "production";
    public string Language { get; set; } = "zh";
    public Dictionary<string, string> Settings { get; set; } = new();
}
```

## 5.8 Source Factory

```csharp
public sealed class ContentSourceFactory
{
    public IContentSource Create(BukitConfig config)
    {
        return config.Source.Type switch
        {
            "markdown" => new MarkdownContentSource(),
            "notion" => new NotionContentSource(),
            "json" => new JsonContentSource(),
            _ => throw new InvalidOperationException(
                $"Unsupported content source: {config.Source.Type}")
        };
    }
}
```

## 5.9 验收标准

- [ ] 支持 `source.type = markdown`。
- [ ] 支持 `source.type = notion`。
- [ ] 未知 source type 给出清晰错误。
- [ ] 新增内容源不需要修改渲染核心逻辑。

---

# Feature 3：Notion 配置支持

## 5.10 目标

让 Bukit 可以通过配置文件读取 Notion token、数据库 ID、字段映射。

## 5.11 配置示例

```yaml
source:
  type: notion

notion:
  token_env: NOTION_TOKEN

  databases:
    site: ${NOTION_SITE_DATABASE_ID}
    pages: ${NOTION_PAGES_DATABASE_ID}
    posts: ${NOTION_POSTS_DATABASE_ID}
    sections: ${NOTION_SECTIONS_DATABASE_ID}
    companies: ${NOTION_COMPANIES_DATABASE_ID}
    menus: ${NOTION_MENUS_DATABASE_ID}
    categories: ${NOTION_CATEGORIES_DATABASE_ID}
    tags: ${NOTION_TAGS_DATABASE_ID}

  fields:
    title: Title
    slug: Slug
    summary: Summary
    content: Content
    published: Published
    publish_at: PublishAt
    type: Type
    template: Template
    language: Language
    categories: Categories
    tags: Tags
    cover: Cover
    seo_title: SeoTitle
    seo_desc: SeoDesc
```

## 5.12 逻辑要求

- Notion Token 必须从环境变量读取。
- 不允许将 Token 写入项目配置文件。
- 如果数据库 ID 缺失，需要输出清晰错误。
- 字段名可通过配置覆盖，不能硬编码。

## 5.13 验收标准

- [ ] `bukit build` 可读取 Notion 配置。
- [ ] 缺少 `NOTION_TOKEN` 时清晰报错。
- [ ] 缺少数据库 ID 时清晰报错。
- [ ] 字段映射可自定义。

---

# Feature 4：Notion 数据库初始化命令

## 5.14 命令

```bash
bukit notion init
```

支持参数：

```bash
bukit notion init \
  --site silkroadbiz \
  --language zh \
  --template corporate
```

## 5.15 目标

自动创建或检查 Notion 数据库结构。

## 5.16 需要创建的数据库

```text
1. Site Settings
2. Pages
3. Posts
4. Sections
5. Companies
6. Menus
7. Categories
8. Tags
```

## 5.17 Site Settings 字段

| 字段 | 类型 | 必填 |
|---|---|---|
| Name | Title | 是 |
| SiteKey | Text | 是 |
| Language | Select | 是 |
| Logo | Files / URL | 否 |
| Slogan | Text | 否 |
| DefaultSeoTitle | Text | 否 |
| DefaultSeoDesc | Text | 否 |
| Published | Checkbox | 是 |

## 5.18 Pages 字段

| 字段 | 类型 | 必填 |
|---|---|---|
| Title | Title | 是 |
| Slug | Text | 是 |
| Type | Select | 是 |
| Template | Select | 否 |
| Summary | Text | 否 |
| Content | Blocks | 否 |
| SeoTitle | Text | 否 |
| SeoDesc | Text | 否 |
| Language | Select | 是 |
| Published | Checkbox | 是 |
| Sort | Number | 否 |

## 5.19 Posts 字段

| 字段 | 类型 | 必填 |
|---|---|---|
| Title | Title | 是 |
| Slug | Text | 是 |
| Summary | Text | 否 |
| Content | Blocks | 否 |
| Cover | Files / URL | 否 |
| Category | Select / Relation | 否 |
| Tags | Multi-select | 否 |
| PublishAt | Date | 否 |
| Author | Text | 否 |
| SeoTitle | Text | 否 |
| SeoDesc | Text | 否 |
| Language | Select | 是 |
| Published | Checkbox | 是 |

## 5.20 Sections 字段

| 字段 | 类型 | 必填 |
|---|---|---|
| Title | Title | 是 |
| SectionKey | Text | 是 |
| SectionType | Select | 是 |
| PageSlug | Text | 是 |
| Heading | Text | 否 |
| Subheading | Text | 否 |
| Body | Rich Text / Blocks | 否 |
| ButtonText | Text | 否 |
| ButtonUrl | Text | 否 |
| Image | Files / URL | 否 |
| DataSource | Select | 否 |
| Sort | Number | 是 |
| Published | Checkbox | 是 |

## 5.21 Companies 字段

| 字段 | 类型 | 必填 |
|---|---|---|
| Name | Title | 是 |
| Slug | Text | 是 |
| CompanyType | Select | 否 |
| Industry | Select | 否 |
| Country | Select | 否 |
| City | Text | 否 |
| Logo | Files / URL | 否 |
| Summary | Text | 否 |
| Description | Blocks | 否 |
| Website | URL | 否 |
| ContactEmail | Email | 否 |
| Tags | Multi-select | 否 |
| Featured | Checkbox | 否 |
| Published | Checkbox | 是 |

## 5.22 验收标准

- [ ] 可创建标准 Notion 数据库结构。
- [ ] 已存在数据库时不重复创建。
- [ ] 可以输出数据库 ID 并写入 `.env.example` 或提示用户配置。
- [ ] 字段缺失时可以自动补齐或提示。

---

# Feature 5：Markdown → Notion 导入命令

## 5.23 命令

```bash
bukit notion import markdown ./content
```

支持参数：

```bash
bukit notion import markdown ./content \
  --mode create-or-update \
  --dry-run \
  --assets public/uploads \
  --language zh
```

## 5.24 迁移流程

```text
扫描 content/**/*.md
    ↓
解析 Front Matter
    ↓
判断内容类型 post / page / section / company
    ↓
Markdown Body 转 Notion Blocks
    ↓
处理图片资源
    ↓
写入 Notion
    ↓
生成 migration map
    ↓
验证写入结果
```

## 5.25 Front Matter 映射规则

| Markdown Front Matter | Notion 字段 |
|---|---|
| title | Title |
| slug | Slug |
| summary | Summary |
| date | PublishAt |
| category | Category |
| tags | Tags |
| cover | Cover |
| published | Published |
| seo_title | SeoTitle |
| seo_desc | SeoDesc |
| body | Notion blocks |

## 5.26 内容类型判断规则

优先级：

```text
1. Front Matter 中存在 type 字段，优先使用 type。
2. 路径包含 content/posts，则识别为 post。
3. 路径包含 content/pages，则识别为 page。
4. 路径包含 content/companies，则识别为 company。
5. 路径包含 content/sections，则识别为 section。
6. 无法识别时默认为 page，并给出 warning。
```

## 5.27 示例 Markdown

```markdown
---
title: "马来西亚投资机会分析"
slug: "malaysia-investment-opportunities"
summary: "本文分析马来西亚投资市场机会。"
date: "2026-05-25"
category: "商务资讯"
tags:
  - 投资
  - 马来西亚
cover: "/images/investment.jpg"
published: true
seo_title: "马来西亚投资机会分析"
seo_desc: "了解马来西亚投资市场、企业出海和产业机会。"
---

## 市场背景

这里是正文内容。
```

## 5.28 验收标准

- [ ] 能扫描指定目录下所有 Markdown。
- [ ] 能解析 Front Matter。
- [ ] 能将正文转换为 Notion blocks。
- [ ] 能处理图片。
- [ ] 支持 `--dry-run`。
- [ ] 支持 `--mode create-or-update`。
- [ ] 能生成迁移记录。
- [ ] 重复执行不会重复创建内容。

---

# Feature 6：Markdown Body → Notion Blocks 转换器

## 5.29 目标

Markdown 正文不能整篇作为纯文本写入 Notion，而是要转换为 Notion blocks。

## 5.30 转换规则

| Markdown | Notion Block |
|---|---|
| `# Heading` | heading_1 |
| `## Heading` | heading_2 |
| `### Heading` | heading_3 |
| 普通段落 | paragraph |
| `- item` | bulleted_list_item |
| `1. item` | numbered_list_item |
| `> quote` | quote |
| 代码块 | code |
| `![alt](url)` | image |
| `[text](url)` | rich_text link |
| `---` | divider |
| 表格 | paragraph 或 synced custom block fallback |

## 5.31 处理逻辑

```text
1. 使用 Markdown AST 解析正文。
2. 遍历 AST 节点。
3. 按节点类型转换为 Notion block。
4. 图片节点生成 image block。
5. 链接节点转换为 rich_text link。
6. 长文本自动切分，避免超过 Notion rich_text 限制。
7. 写入后重新读取 blocks，验证数量和关键结构。
```

## 5.32 验收标准

- [ ] 标题、段落、列表、引用、代码块能正确转换。
- [ ] 图片能转换为 image block。
- [ ] 链接不会丢失。
- [ ] 长文本不会导致 Notion API 失败。
- [ ] 转换失败时有具体错误信息。

---

# Feature 7：图片资源迁移服务

## 5.33 新增组件

```text
AssetMigrationService.cs
```

## 5.34 处理流程

```text
Markdown 图片路径
    ↓
判断类型：
    - 本地相对路径
    - public 路径
    - 外部 URL
    - base64 图片
    ↓
处理：
    - 本地图片复制到 public/uploads/yyyy/mm/
    - 外部 URL 保留
    - base64 图片落盘为文件
    ↓
生成可访问 URL
    ↓
写入 Notion image block 或 Cover 字段
```

## 5.35 图片处理策略

| 图片来源 | 处理方式 |
|---|---|
| `/images/a.jpg` | 复制到 `public/uploads/` |
| `./assets/a.jpg` | 复制到 `public/uploads/` |
| `https://example.com/a.jpg` | 保留外部 URL |
| base64 | 转文件后保存 |
| Notion image | 构建时下载或引用 |

## 5.36 验收标准

- [ ] 本地图片能复制到 uploads 目录。
- [ ] 外部图片 URL 能保留。
- [ ] 缺失图片能输出 warning 或 error。
- [ ] strict 模式下图片缺失会失败。
- [ ] 迁移前后图片数量可校验。

---

# Feature 8：迁移记录 Migration Map

## 5.37 新增文件

```text
.bukit/migrations/notion-map.json
```

## 5.38 示例结构

```json
{
  "content/posts/malaysia-investment.md": {
    "source_hash": "abc123",
    "notion_page_id": "xxxx",
    "database": "posts",
    "slug": "malaysia-investment-opportunities",
    "status": "migrated",
    "migrated_at": "2026-05-26T23:00:00+08:00"
  }
}
```

## 5.39 作用

| 能力 | 说明 |
|---|---|
| 防重复导入 | 已导入文件不重复创建 |
| 支持更新 | 文件变化后更新对应 Notion 页面 |
| 支持回滚 | 可以追踪来源 |
| 支持 diff | 判断哪些内容发生变化 |
| 支持 dry-run | 预览迁移影响 |

## 5.40 验收标准

- [ ] 每次导入后更新 migration map。
- [ ] 文件未变化时跳过导入。
- [ ] 文件变化时可以 update Notion 页面。
- [ ] 删除源文件时可以报告 orphan Notion page。

---

# Feature 9：Notion → Bukit Build

## 5.41 命令

```bash
bukit build --source notion
```

或者读取配置：

```bash
bukit build
```

当配置为：

```yaml
source:
  type: notion
```

Bukit 自动从 Notion 读取内容。

## 5.42 构建流程

```text
读取 bukit.config.yml
    ↓
发现 source.type = notion
    ↓
读取 Notion databases
    ↓
过滤 Published = true
    ↓
转换为 BukitContentItem
    ↓
生成路由
    ↓
Scriban 模板渲染
    ↓
输出静态 HTML
```

## 5.43 路由规则

| Type | 路由 |
|---|---|
| page | `/{slug}/` |
| post | `/insights/{slug}/` |
| company | `/companies/{slug}/` |
| section | 不生成独立页面，只作为页面数据 |
| category | `/insights/category/{slug}/` |
| tag | `/insights/tag/{slug}/` |

## 5.44 验收标准

- [ ] Bukit 可以从 Notion 构建页面。
- [ ] `Published = false` 的内容不会构建。
- [ ] post / page / company 路由正确。
- [ ] section 只作为页面数据，不单独生成页面。
- [ ] Notion 内容可以被 Scriban 模板消费。

---

# Feature 10：Demo → Notion 迁移

## 5.45 命令

```bash
bukit migrate demo ./demo --to notion
```

支持参数：

```bash
bukit migrate demo ./demo \
  --theme silkroadbiz \
  --to notion \
  --extract-pages \
  --extract-sections \
  --extract-collections \
  --dry-run
```

## 5.46 迁移目标

将 Demo 拆成两部分：

```text
Demo 项目
├── 页面结构 / UI / CSS / 组件  → Bukit Theme
└── 文案 / 图片 / 文章 / 企业数据 → Notion Database
```

## 5.47 迁移流程

```text
读取 Demo HTML / React / Vue / 静态文件
    ↓
分析页面结构
    ↓
识别 layout / section / collection / asset
    ↓
生成 Bukit Theme 模板
    ↓
抽取文案、图片、列表内容
    ↓
写入 Notion
    ↓
生成 mapping 文件
    ↓
验证 theme 是否只保留结构，不保留业务内容
```

## 5.48 Demo 拆分规则

| Demo 内容 | 迁移目标 |
|---|---|
| CSS / Layout / HTML 结构 | Bukit Theme |
| Hero 文案 | Sections 数据库 |
| 首页模块 | Sections 数据库 |
| 文章列表 | Posts 数据库 |
| 企业列表 | Companies 数据库 |
| 关于我们正文 | Pages 数据库 |
| 菜单 | Menus 数据库 |
| 图片 | public/uploads 或外部 URL |
| SEO 信息 | Notion SEO 字段 |

## 5.49 静态 HTML MVP 解析规则

第一版只支持静态 HTML：

```text
1. 读取 .html 文件。
2. 提取 <title>。
3. 提取 meta description。
4. 提取 h1 作为页面标题候选。
5. 提取 section 标签作为 Sections。
6. 提取 img 标签作为 assets。
7. 提取 nav / header / footer 链接作为 Menus。
8. 根据文件路径生成 slug。
```

## 5.50 验收标准

- [ ] 可以迁移静态 HTML Demo。
- [ ] 首页 Hero 可以进入 Sections 数据库。
- [ ] 关于、联系等页面进入 Pages 数据库。
- [ ] 图片可以复制或记录。
- [ ] 迁移后的主题不硬编码业务文案。

---

# Feature 11：Section 内容模型

## 5.51 目标

支持首页、落地页、专题页的区块化内容。

## 5.52 Section 模型

```csharp
public sealed class BukitSection
{
    public string SectionKey { get; set; } = "";
    public string SectionType { get; set; } = "";
    public string PageSlug { get; set; } = "";
    public int Sort { get; set; }

    public string? Heading { get; set; }
    public string? Subheading { get; set; }
    public string? Body { get; set; }

    public string? ButtonText { get; set; }
    public string? ButtonUrl { get; set; }

    public string? Image { get; set; }

    public Dictionary<string, object?> Props { get; set; } = new();
}
```

## 5.53 Section 示例

```json
{
  "section_key": "home.hero",
  "section_type": "hero",
  "page_slug": "home",
  "heading": "连接中国与马来西亚商业机会",
  "subheading": "聚合商务资讯、企业名录与市场机会",
  "button_text": "查看企业",
  "button_url": "/companies/",
  "sort": 1,
  "published": true
}
```

## 5.54 验收标准

- [ ] 首页模块可以从 Sections 数据库读取。
- [ ] section 支持排序。
- [ ] section 支持不同类型。
- [ ] 模板可以根据 SectionType 渲染不同组件。

---

# Feature 12：主题内容硬编码检测

## 5.55 命令

```bash
bukit theme lint
```

## 5.56 检测目标

防止 Demo 迁移时把业务文案写死进主题模板。

## 5.57 检测规则

扫描主题模板中的：

```text
- 长中文段落
- 企业名称
- 电话号码
- 邮箱
- 文章正文
- 首页 Hero 文案
- 服务描述
- SEO 文案
- 明显营销文案
```

## 5.58 正确模板写法

```scriban
{{ hero = sections | where "section_key" "home.hero" | first }}

<section class="hero">
  <h1>{{ hero.heading }}</h1>
  <p>{{ hero.subheading }}</p>
  <a href="{{ hero.button_url }}">{{ hero.button_text }}</a>
</section>
```

## 5.59 错误模板写法

```html
<section class="hero">
  <h1>连接中国与马来西亚商业机会</h1>
  <p>聚合商务资讯、企业名录与市场机会</p>
</section>
```

## 5.60 验收标准

- [ ] 可以扫描主题模板。
- [ ] 可以识别明显硬编码业务文案。
- [ ] 可以输出文件路径和行号。
- [ ] strict 模式下可阻止构建。

---

# Feature 13：Notion 数据校验命令

## 5.61 命令

```bash
bukit notion validate
```

支持：

```bash
bukit notion validate --strict
```

## 5.62 校验内容

| 校验项 | 规则 |
|---|---|
| 必填字段 | Title、Slug、Published 必须存在 |
| Slug 唯一 | 同类型内容不得重复 |
| Published | 只有 Published = true 才参与构建 |
| 图片 | Cover / Image URL 可访问或格式合法 |
| 模板 | Template 必须存在 |
| 分类 | Category 必须存在 |
| 多语言 | 同 TranslationKey 的语言是否完整 |
| SEO | SeoTitle / SeoDesc 是否缺失 |
| 正文 | Posts / Pages 是否有内容 |
| 路由 | 是否存在冲突 |

## 5.63 输出示例

```text
Notion Validate Result

✔ Posts: 32 valid
✔ Pages: 6 valid
⚠ Sections: 2 missing image
✘ Companies: duplicate slug "ali365"

Failed:
- Companies / ALi365 SDN BHD: duplicate slug ali365
- Posts / 马来西亚投资机会: missing seo_desc
```

## 5.64 验收标准

- [ ] 可以校验全部数据库。
- [ ] 可以输出清晰报告。
- [ ] strict 模式下错误返回非 0 exit code。
- [ ] warning 不阻止普通构建。

---

# Feature 14：数据源切换命令

## 5.65 命令

```bash
bukit source use notion
```

```bash
bukit source use markdown
```

## 5.66 逻辑

修改 `bukit.config.yml`：

```yaml
source:
  type: notion
```

或：

```yaml
source:
  type: markdown
```

## 5.67 验收标准

- [ ] 可以切换到 notion。
- [ ] 可以切换回 markdown。
- [ ] 切换前校验必要配置。
- [ ] 切换后输出下一步操作提示。

---

# Feature 15：Dry-run 预览能力

## 5.68 要求

所有迁移命令必须支持：

```bash
--dry-run
```

## 5.69 示例

```bash
bukit notion import markdown ./content --dry-run
```

输出：

```text
Dry Run Result

Will create:
- Posts: 12
- Pages: 3
- Sections: 8

Will update:
- Posts: 2

Will skip:
- Unchanged: 5

Warnings:
- 2 posts missing seo_desc
- 1 image file not found
```

## 5.70 验收标准

- [ ] dry-run 不写入 Notion。
- [ ] dry-run 可以显示 create / update / skip 数量。
- [ ] dry-run 可以显示 warning。
- [ ] dry-run 结果可用于迁移前审查。

---

# Feature 16：错误处理与失败记录

## 5.71 失败记录文件

```text
.bukit/migrations/errors.json
```

## 5.72 错误结构

```json
{
  "source": "content/posts/a.md",
  "type": "post",
  "operation": "import_markdown_to_notion",
  "status": "failed",
  "reason": "Image file not found: ./images/a.jpg",
  "time": "2026-05-26T23:00:00+08:00"
}
```

## 5.73 错误处理规则

| 场景 | 处理 |
|---|---|
| Notion API 失败 | 记录失败，继续下一条 |
| 图片不存在 | warning 或 strict 模式失败 |
| Slug 重复 | 阻止导入 |
| 字段不存在 | 提示运行 `bukit notion init` |
| 正文为空 | warning 或跳过 |
| Published false | 不参与 build |
| 图片数量不一致 | 判定迁移失败 |

## 5.74 验收标准

- [ ] 所有失败都记录到 errors.json。
- [ ] 单条失败不影响其他内容迁移。
- [ ] strict 模式下关键错误会中断。
- [ ] 错误信息包含 source、operation、reason。

---

# Feature 17：严格模式

## 5.75 参数

```bash
--strict
```

## 5.76 严格模式失败条件

```text
- 缺少 slug
- 缺少 title
- slug 重复
- 图片不存在
- Notion 字段不存在
- Markdown body 为空
- SEO 字段为空
- Notion blocks 写入后数量不一致
- 模板不存在
- 路由冲突
```

## 5.77 验收标准

- [ ] strict 模式下错误返回非 0 exit code。
- [ ] 普通模式下部分问题只 warning。
- [ ] strict 模式可用于 CI / GitHub Actions。

---

## 6. 推荐开发优先级

---

# Phase 1：MVP，打通 Markdown → Notion → Build

## 6.1 必做功能

```text
1. BukitContentItem
2. IContentSource
3. MarkdownContentSource 重构
4. Notion 配置读取
5. NotionContentSource
6. NotionFieldMapper
7. MarkdownToNotionBlockConverter
8. AssetMigrationService 基础版
9. bukit notion import markdown
10. bukit build --source notion
11. bukit notion validate
12. migration map
```

## 6.2 Phase 1 验收目标

```text
用户可以把 bukit create 生成的 Markdown 内容导入 Notion，
然后 Bukit 可以直接从 Notion 构建网站。
```

---

# Phase 2：Demo → Theme + Notion

## 6.3 必做功能

```text
1. DemoContentExtractor
2. SectionExtractor
3. DemoToNotionMigration
4. ThemeContentLint
5. bukit migrate demo
6. Sections 数据库
7. Companies 数据库
8. Menus 数据库
```

## 6.4 Phase 2 验收目标

```text
用户可以把已有 Demo 项目迁移为 Bukit 主题，
同时把文案、文章、企业、首页区块写入 Notion。
```

---

# Phase 3：接入 BukitJalil / AI Agent

## 6.5 后续增强功能

```text
1. AI Content Agent
2. AI Template Agent
3. Notion 自动发布 Agent
4. 内容质量检查
5. 自动补 SEO
6. 多语言翻译流
7. 可视化迁移报告
8. GEO 内容结构优化
```

## 6.6 Phase 3 目标

```text
BukitJalil 可以通过自然语言控制 Bukit，
完成建站、迁移、内容导入、发布和内容持续更新。
```

---

## 7. 测试清单

## 7.1 单元测试

```text
tests/Bukit.Content.Tests/
├── BukitContentItemTests.cs
├── ContentSourceFactoryTests.cs
├── MarkdownFrontMatterParserTests.cs
├── MarkdownToNotionBlockTests.cs
├── NotionFieldMapperTests.cs
├── SlugValidationTests.cs
└── AssetMigrationServiceTests.cs
```

## 7.2 集成测试

```text
tests/Bukit.Integration.Tests/
├── MarkdownToNotionImportTests.cs
├── NotionToBuildTests.cs
├── DemoExtractorTests.cs
├── SourceSwitchTests.cs
└── ThemeLintTests.cs
```

## 7.3 必测场景

| 测试场景 | 目标 |
|---|---|
| Markdown front matter 解析 | 字段正确映射 |
| Markdown 图片转换 | image block 数量一致 |
| 重复 slug | 能检测并阻止 |
| Published false | 不参与构建 |
| source 切换 | markdown / notion 都能构建 |
| Demo section 抽取 | Hero、CTA、Features 正确入库 |
| Notion 字段缺失 | 给出清晰错误 |
| dry-run | 不写入数据 |
| strict 模式 | 错误返回非 0 exit code |
| migration map | 重复执行不重复创建 |

---

## 8. Codex 可执行任务拆分

---

### Task 1：实现统一内容模型

```text
在 Bukit.Core 中新增 Content 模块，实现 BukitContentItem、BukitContentCollection、IContentSource、BukitContentSourceContext。

要求：
1. 不破坏现有 Markdown 构建逻辑。
2. 现有 Markdown 内容读取最终也要转换为 BukitContentItem。
3. 渲染层后续只依赖 BukitContentItem，不直接依赖 Markdown 文件路径。
4. 添加基础单元测试。
```

---

### Task 2：实现 Notion 配置与字段映射

```text
新增 Notion 配置读取能力。

要求：
1. 从 bukit.config.yml 读取 source.type。
2. 支持 source.type = markdown / notion。
3. 支持 notion.databases 和 notion.fields 配置。
4. Notion Token 从环境变量读取，不允许写死。
5. 如果缺少必要配置，输出清晰错误。
6. 添加 NotionFieldMapper 单元测试。
```

---

### Task 3：实现 Markdown → Notion 导入

```text
新增命令：bukit notion import markdown ./content

要求：
1. 扫描 content/**/*.md。
2. 解析 Front Matter。
3. 映射到 Notion 字段。
4. Markdown body 转换为 Notion blocks。
5. 图片转换为 Notion image block。
6. 支持 --dry-run。
7. 支持 --mode create-or-update。
8. 生成 .bukit/migrations/notion-map.json。
9. 避免重复导入。
10. 写入后重新读取 Notion 页面验证。
```

---

### Task 4：实现 Notion → Bukit Build

```text
实现 NotionContentSource。

要求：
1. 从配置的 Notion databases 读取数据。
2. 只读取 Published = true 的内容。
3. 转换为 BukitContentItem。
4. 支持 page / post / section / company / menu 类型。
5. 根据 Type 和 Slug 生成路由。
6. 确保现有 Scriban 模板可以消费 Notion 内容。
7. 添加集成测试。
```

---

### Task 5：实现 Notion 数据校验

```text
新增命令：bukit notion validate

要求：
1. 校验必填字段。
2. 校验 slug 是否重复。
3. 校验 Published 字段。
4. 校验 template 是否存在。
5. 校验 SEO 字段。
6. 校验图片 URL。
7. 输出 human-readable report。
8. 支持 --strict，严格模式下发现错误直接退出非 0 状态码。
```

---

### Task 6：实现 Demo → Notion 迁移基础版

```text
新增命令：bukit migrate demo ./demo --to notion

要求：
1. 第一版只支持静态 HTML Demo。
2. 扫描 HTML 页面。
3. 抽取 title、meta description、h1、section、img、a。
4. 将页面级内容写入 Pages。
5. 将首页 section 写入 Sections。
6. 将图片复制到 public/uploads。
7. 生成迁移报告。
8. 不要把业务文案写死进 Bukit theme。
```

---

### Task 7：实现 Theme Lint

```text
新增命令：bukit theme lint

要求：
1. 扫描 themes/**/*.sbn、themes/**/*.html。
2. 检测长中文段落、邮箱、手机号、企业名称、SEO 文案等硬编码内容。
3. 输出文件路径、行号、疑似内容。
4. 支持 --strict。
5. strict 模式下发现疑似硬编码业务内容返回非 0 exit code。
```

---

## 9. 最小可行版本 MVP 范围

第一版不要一次性做太大。建议 MVP 只做：

```text
1. 统一内容模型
2. MarkdownContentSource 重构
3. Notion 配置读取
4. NotionContentSource 读取
5. Markdown → Notion 导入
6. Notion → Bukit Build
7. notion validate
8. migration map
```

暂缓：

```text
1. React / Vue Demo 深度解析
2. AI 自动拆分组件
3. Notion 双向同步
4. 可视化迁移界面
5. 多语言自动翻译
```

---

## 10. 风险点与注意事项

| 风险 | 说明 | 建议 |
|---|---|---|
| Notion API 限流 | 批量导入可能触发限制 | 增加重试、节流、分页 |
| Notion block 限制 | 长文本、复杂表格可能转换失败 | 长文本切分，表格 fallback |
| 图片不可访问 | 本地图片无法直接写入 Notion | 先复制到 public/uploads 或对象存储 |
| 字段名不一致 | 用户 Notion 数据库字段可能不同 | 提供 fields 映射配置 |
| Slug 冲突 | 多来源导入容易重复 | 导入前全局校验 |
| 主题硬编码 | Demo 迁移容易把内容写死 | 增加 theme lint |
| Markdown 与 Notion 差异 | Notion blocks 无法完整表达 Markdown | 保留原始 Markdown 备份 |

---

## 11. 最终产品定位建议

本次改造完成后，Bukit 不应只是普通 Markdown 静态站生成器，而应升级为：

> 面向 Notion CMS、AI Agent、GEO 内容生产和企业官网自动化的 .NET Native 静态网站生成引擎。

核心能力闭环：

```text
Demo / Markdown / AI Generated Content
        ↓
Bukit Migration Engine
        ↓
Notion CMS
        ↓
Bukit Content Source Layer
        ↓
Scriban Theme Renderer
        ↓
Static HTML
        ↓
GitHub Pages / Cloudflare Pages / Server
```

---

## 12. 最终结论

Bukit 的改造重点不是单独增加一个 Notion 导入脚本，而是建立正式的内容源架构：

```text
Content Source Abstraction
        ↓
Migration Engine
        ↓
Notion CMS Adapter
        ↓
Validation Layer
        ↓
Static Build Renderer
```

推荐实施顺序：

```text
第一步：Markdown → Notion → Build
第二步：Demo → Theme + Notion
第三步：BukitJalil / AI Agent 自动化建站
```

这样 Bukit 可以同时服务：

```text
- 开发者本地 Markdown 建站
- 企业 Notion CMS 建站
- Demo 快速迁移为主题
- AI Agent 自动生成和维护网站内容
- SEO / GEO 自动化内容生产
```
