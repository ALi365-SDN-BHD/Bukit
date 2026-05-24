# Bukit 内置插件升级优化方案

> 注：按用户要求，TaxonomyPlugin 暂不处理。

## 一、总览：Bukit 8 个内置插件现状（分析范围：7 个）

| 插件                    | 生命周期钩子       | 版本    | 主要功能                       | 严重程度  |
| --------------------- | ------------ | ----- | -------------------------- | ----- |
| ~~TaxonomyPlugin~~    | -            | -     | **暂不处理**                   | -     |
| **PaginationPlugin**  | derive-pages | 2.0.0 | 单 collection 列表分页          | 🔴 高  |
| **PagesIndexPlugin**  | derive-pages | 1.1.0 | 页面索引 + Notion 关系解析         | 🟢 低  |
| **ArchivePlugin**     | derive-pages | 2.0.0 | 年/月归档                      | 🟡 中等 |
| **SitemapPlugin**     | after-build  | 2.0.1 | sitemap.xml + 多语言 hreflang | 🟡 中等 |
| **RssPlugin**         | after-build  | 2.0.0 | RSS 2.0                    | 🔴 高  |
| **SearchIndexPlugin** | after-build  | 2.1.0 | search.json 搜索索引           | 🟡 中等 |
| **LlmsTxtPlugin**     | after-build  | 1.0.0 | llms.txt / llms-full.txt   | 🟢 低  |

***

## 二、优先级分级

### 🔴 P0 — 必须补齐（对标 Hugo 标准功能）

| 编号       | 插件/功能                 | 问题                                  | Hugo 对照                               | 升级方案                                          |
| -------- | --------------------- | ----------------------------------- | ------------------------------------- | --------------------------------------------- |
| **P0-1** | **RssPlugin**         | 仅支持 RSS 2.0，无 Atom / JSON Feed      | Hugo 原生支持 RSS + Atom + JSON Feed 三种格式 | 新增 Atom 和 JSON Feed 生成器，通过 `feedFormats` 配置控制 |
| **P0-2** | **PaginationPlugin**  | 仅单 collection，固定 URL 模式 `page/{n}/` | Hugo 支持任意列表分页，多实例，URL 可配              | 重构为多 collection 支持，允许 `urlPattern` 自定义        |
| **P0-3** | **SearchIndexPlugin** | 纯 JSON 无内置搜索 UI                     | Hugo 无内置搜索但社区方案成熟                     | 提供可选的前端搜索组件（纯 JS），开箱即用                        |

### 🟡 P1 — 强烈建议（对齐 Hugo 进阶功能）

| 编号       | 插件/功能             | 问题                                | Hugo 对照                  | 升级方案                                                      |
| -------- | ----------------- | --------------------------------- | ------------------------ | --------------------------------------------------------- |
| **P1-1** | **SitemapPlugin** | 不支持 `<priority>` / `<changefreq>` | Hugo 通过 front matter 支持  | 新增 front matter `sitemap.priority` / `sitemap.changefreq` |
| **P1-2** | **SitemapPlugin** | 不支持图片/视频 Sitemap                  | Hugo 也不支持（但 Google 推荐）   | **超越 Hugo**：新增 `<image:image>` / `<video:video>` 扩展       |
| **P1-3** | **ArchivePlugin** | 仅年/月两级，无日归档                       | Hugo 通过模板可灵活实现           | 新增 `depth` 配置：`yearly` / `monthly` / `daily`              |
| **P1-4** | **ArchivePlugin** | 模板硬编码 `pages/page.html`           | Hugo 模板可自定义              | 支持 `archive.template` 配置自定义模板                             |
| **P1-5** | **RssPlugin**     | 多 collection 合并到一个 feed           | Hugo 按 section 独立生成 feed | 新增 `collection.output.feedPath` 配置，独立 feed 路径             |

### 🟢 P2 — 锦上添花（超越 Hugo 或独特优势）

| 编号       | 功能                           | 问题        | 方案                                             |
| -------- | ---------------------------- | --------- | ---------------------------------------------- |
| **P2-1** | 新增 **RelatedContentPlugin**  | 无相关内容推荐   | Hugo 有原生 `.Related` API，基于关键词/标签/日期加权匹配        |
| **P2-2** | 新增 **MenuPlugin**            | 无菜单系统     | Hugo 有原生 Menu 系统（多菜单、嵌套、权重）                    |
| **P2-3** | 新增 **DataFilesPlugin**       | 无数据文件系统   | Hugo `data/` 目录 + `getJSON`/`getCSV` 远程数据      |
| **P2-4** | 新增 **AliasPlugin**           | 无 URL 重定向 | Hugo `aliases` front matter 自动生成 HTML redirect |
| **P2-5** | 新增 **ImageProcessingPlugin** | 无内置图片处理   | Hugo Resources 管道（裁剪/缩放/WebP）                  |
| **P2-6** | **SearchIndexPlugin**        | 无搜索权重/评分  | 新增 `searchWeight` front matter 和内容加权           |

***

## 三、逐插件详细方案

### 3.1 RssPlugin（🔴 P0）

**现状：**

* 仅生成 RSS 2.0 格式 `/rss.xml`

* `maxItems` 硬编码 20

* 多 collection 合并到单一 feed

* 不支持 `<enclosure>`（播客）

**升级方案：**

```yaml
# 新增配置项
site:
  feedFormats: ["rss", "atom", "json"]   # 要生成的格式，默认 ["rss"]
  feedLimit: 20                           # 可配置的最大条目数
  feedPath: "feed"                        # 基础路径前缀

collection:
  output:
    rss: true                             # 已有
    feedPath: "custom-feed"               # 新增：独立 feed 路径
    feedTitle: "My Blog"                  # 新增：自定义 feed 标题
    feedDescription: "..."                # 新增：自定义 feed 描述

# front matter:
feed:
  exclude: true                           # 新增：排除此页面
  enclosure:                              # 新增：播客附件
    url: "..."
    length: 12345
    type: "audio/mpeg"
```

**实现步骤：**

1. 新增 `AtomFeedGenerator` 和 `JsonFeedGenerator`
2. 重构 `RssPlugin` → `FeedPlugin`，根据 `feedFormats` 调度
3. 支持 per-collection 独立 feed 生成
4. 支持 `<enclosure>` 标签

***

### 3.2 PaginationPlugin（🔴 P0）

**现状：**

* 仅第一个 `pagination.enabled=true` 的 collection 生效

* URL 固定为 `{listRoute}page/{n}/`

* 无 pagination 多实例支持

**升级方案：**

```yaml
# 新增配置项
collection:
  pagination:
    enabled: true/false
    pageSize: 10
    urlPattern: "page/:num/"              # 新增：可配置 URL，:num 为占位符
    firstPageUsesListRoute: true          # 新增：第 1 页是否使用 listRoute

site:
  pagination:
    enabled: false                        # 新增：全局默认启用
    pageSize: 10
```

**实现步骤：**

1. 重构为遍历所有 collection，而非仅第一个
2. 实现 `urlPattern` 解析器（`:num` 占位符）
3. 每个 collection 独立生成分页页面
4. 路由冲突检测（不同 collection 不应产生相同 URL）

***

### 3.3 SearchIndexPlugin（🔴 P0）

**现状：**

* 生成 `search.json` 数组

* content 截断至 8000 字符

* 无内置搜索 UI

**升级方案：**

```yaml
# 新增配置项
site:
  search:
    ui: "default" / false                 # 新增：内置搜索 UI
    uiTheme: "light" / "dark" / "auto"    # 新增：UI 主题
    placeholderText: "搜索..."            # 新增：占位文本
    maxContentLength: 8000                # 已有

# front matter:
searchWeight: 5                           # 新增：搜索权重（默认 1）
searchExclude: false                      # 新增：排除搜索
```

**实现步骤：**

1. 新增 `searchWeight` 和 `searchExclude` 配置支持
2. 提供可选内置搜索 UI（纯 JS 实现，\~5KB，零依赖）
3. 搜索 UI 作为可选的 Scriban partial 注入
4. 支持搜索结果高亮和键盘导航

***

### 3.4 SitemapPlugin（🟡 P1）

**现状：**

* 生成标准 sitemap.xml，支持多语言 hreflang

* 缺少 `<priority>`, `<changefreq>`

* 缺少图片/视频 Sitemap

**升级方案：**

```yaml
# 新增配置项
site:
  sitemap:
    defaultPriority: 0.5                  # 新增
    defaultChangefreq: "weekly"           # 新增
    imageEnabled: false                   # 新增：是否启用 Image Sitemap 扩展
    videoEnabled: false                   # 新增：是否启用 Video Sitemap 扩展

# front matter:
sitemap:
  priority: 0.8                           # 新增
  changefreq: "daily"                     # 新增
  images:                                 # 新增
    - url: "..."
      caption: "..."
      title: "..."
  videos:                                 # 新增
    - url: "..."
      title: "..."
      thumbnail: "..."
```

**实现步骤：**

1. 新增 `sitemap.priority` / `sitemap.changefreq` front matter 支持
2. 添加全局默认值 `defaultPriority` / `defaultChangefreq`
3. 实现 Image Sitemap 扩展（`xmlns:image`）
4. 实现 Video Sitemap 扩展（`xmlns:video`）
5. 自动从 HTML 内容中提取 `<img>` 标签作为 image sitemap 条目

***

### 3.5 ArchivePlugin（🟡 P1）

**现状：**

* 仅年/月两级归档

* 模板硬编码 `pages/page.html`

* 仅支持单一 collection

**升级方案：**

```yaml
# 新增配置项
collection:
  output:
    archive:
      enabled: true / false
      depth: "monthly"                    # 新增: yearly / monthly / daily
      template: "pages/archive.html"      # 新增: 自定义模板
      routePrefix: "archive"              # 新增: URL 路径前缀
```

**实现步骤：**

1. 新增 `depth` 配置，支持 `daily` 深度
2. 新增 `template` 配置，允许自定义归档模板
3. 新增 `routePrefix` 配置，自定义归档 URL 前缀
4. 归档数据注入 `archive_data` 到模板：`years` → `months` → `days`

***

### 3.6 🟢 P2 新插件提案

#### 3.6.1 RelatedContentPlugin（相关内容推荐）

对标 Hugo 的 `.Related` API。

```yaml
site:
  related:
    enabled: true
    threshold: 80                         # 相关性阈值
    indices:                              # 匹配维度与权重
      - name: keywords
        weight: 100
      - name: tags
        weight: 80
      - name: categories
        weight: 60
      - name: date
        weight: 10
```

模板使用（Scriban）：

```scriban
{{ for related in page.related limit:5 }}
  <a href="{{ related.url }}">{{ related.title }}</a>
{{ end }}
```

**实现：** `IDerivePagesPlugin`，在 derive 阶段计算相关性矩阵，注入到每个 content item 的 fields。

***

#### 3.6.2 MenuPlugin（导航菜单系统）

对标 Hugo 的 Menu 系统。

```yaml
site:
  menus:
    main:
      - identifier: home
        name: 首页
        url: /
        weight: 1
      - identifier: blog
        name: 博客
        url: /blog/
        weight: 2
        children:
          - identifier: tech
            name: 技术
            url: /blog/tags/tech/
            weight: 1
    footer:
      - identifier: about
        name: 关于
        url: /about/
        weight: 1
```

**实现：** `IAfterBuildPlugin`，将菜单数据注入 `context.Data["menus"]`，模板通过 `site.menus.main` 访问。

***

#### 3.6.3 DataFilesPlugin（数据文件系统）

对标 Hugo 的 `data/` 目录。

```
data/
  authors.yaml
  navigation.json
  zh/
    strings.yaml
  en/
    strings.yaml
```

模板访问：

```scriban
{{ site.data.authors.john.name }}
{{ site.data.navigation.items }}
```

**实现：** 启动时加载 `data/` 目录下所有 YAML/JSON/TOML 文件，注入 `SiteData` 全局对象，支持多语言数据子目录。

***

#### 3.6.4 AliasPlugin（URL 重定向）

对标 Hugo 的 `aliases` front matter。

```yaml
# front matter:
aliases:
  - /old-blog-post/
  - /previous-url/
```

生成 HTML redirect 页面（`<meta http-equiv="refresh">` + `<link rel="canonical">`）。

***

#### 3.6.5 ImageProcessingPlugin（图片处理）

对标 Hugo Resources 管道。

```yaml
site:
  imageProcessing:
    enabled: true
    formats: ["webp", "avif"]
    quality: 80
    sizes: [400, 800, 1200]
```

模板使用：

```scriban
{{ img "hero.jpg" | resize:"800x" | format:"webp" }}
```

**注意：** 此插件依赖外部图片处理库（ImageSharp/SkiaSharp），需评估跨平台兼容性和 AOT 支持，可考虑作为独立 NuGet 包发布。

***

## 四、实施路线图

### Phase 1：P0 必须补齐（v2.8 目标）

| 任务                                       | 说明                                     | 工作量   |
| ---------------------------------------- | -------------------------------------- | ----- |
| RssPlugin → FeedPlugin（+Atom +JSON Feed） | 新增 AtomFeedGenerator、JsonFeedGenerator | 5-7 天 |
| PaginationPlugin 多 collection 重构         | 遍历所有 collection + urlPattern           | 3-4 天 |
| SearchIndexPlugin + 内置搜索 UI              | 纯 JS 搜索组件 + searchWeight               | 3-5 天 |

### Phase 2：P1 强烈建议（v2.9 目标）

| 任务                                             | 说明                      | 工作量   |
| ---------------------------------------------- | ----------------------- | ----- |
| SitemapPlugin + priority/changefreq + 图片/视频    | Image/Video Sitemap 扩展  | 4-5 天 |
| ArchivePlugin + depth + template + routePrefix | daily 归档 + 自定义模板        | 2-3 天 |
| FeedPlugin + per-collection 独立 feed            | 每 collection 独立 feed 路径 | 2-3 天 |

### Phase 3：P2 锦上添花（v3.0 目标）

| 任务                        | 说明                       | 工作量   |
| ------------------------- | ------------------------ | ----- |
| RelatedContentPlugin（新增）  | 基于关键词/标签加权的相关内容推荐        | 3-4 天 |
| MenuPlugin（新增）            | 多菜单嵌套系统                  | 2-3 天 |
| DataFilesPlugin（新增）       | data/ 目录 + 多语言数据         | 2-3 天 |
| AliasPlugin（新增）           | front matter aliases 重定向 | 1-2 天 |
| ImageProcessingPlugin（新增） | 图片处理管道（需评估依赖）            | 5-7 天 |
| SearchIndexPlugin + 权重评分  | searchWeight 加权          | 1-2 天 |

***

## 五、风险与注意事项

1. **向后兼容性**：所有升级必须保持现有 `site.yaml` 配置向后兼容，新配置项使用合理默认值。
2. **AOT 兼容**：Bukit 支持 AOT 编译，任何新引入的依赖（如图片处理库）需验证 AOT 兼容性。
3. **测试覆盖**：每个插件升级需补充对应的单元测试和集成测试。
4. **配置文件膨胀**：新增大量配置项可能导致 `site.yaml` 复杂度过高，建议每个插件的专属配置使用独立 section。
5. **ImageProcessingPlugin**：图片处理是重依赖功能，可考虑作为独立 NuGet 包发布，按需安装。

***

## 六、总结对比

| 维度      | 当前 Bukit     | Phase 1 后     | Phase 2 后  | Phase 3 后 | Hugo 对标       |
| ------- | ------------ | ------------- | ---------- | --------- | ------------- |
| Feed 格式 | 仅 RSS        | RSS+Atom+JSON | +独立 feed   | -         | RSS+Atom+JSON |
| 分页      | 单 collection | 多 collection  | +自定义 URL   | -         | 多实例分页         |
| 搜索      | JSON only    | +内置 UI        | -          | +权重评分     | 社区方案          |
| Sitemap | 基础           | -             | +SEO+图片+视频 | -         | 基础+分割         |
| 归档      | 年/月          | -             | +日/+自定义    | -         | 灵活            |
| 分类      | -            | -             | -          | -         | **暂不处理**      |
| 相关推荐    | 无            | -             | -          | ✅         | ✅             |
| 菜单系统    | 无            | -             | -          | ✅         | ✅             |
| 数据文件    | 无            | -             | -          | ✅         | ✅             |
| URL 重定向 | 无            | -             | -          | ✅         | ✅             |
| 图片处理    | 无            | -             | -          | 待评估       | ✅             |

