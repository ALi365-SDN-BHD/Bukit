# 05 内容（Markdown）：Front Matter 字段、写法与示例

如果你希望内容直接跟随仓库版本管理、在本地编辑器里写作，Markdown 模式是最简单可靠的选择。

对照可运行示例：`examples/starter/content/`。

## 你将获得什么

- 一套推荐的 Front Matter 字段（page/post/i18n/tags/categories/SEO）
- 2 份可直接复制的“页面”和“文章”示例
- 常见问题：标题/slug/日期从哪来、为什么不出现在站点里

## 启用 Markdown 内容源

在 `site.yaml`：

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
```

引擎会递归读取 `content/` 下的所有 `*.md`。

## 限额与指定读取（大仓库/单篇调试）

当你的仓库 Markdown 数量很多，或你只想调试某几篇内容时，可以在配置里限制读取范围：

```yaml
content:
  provider: markdown
  markdown:
    dir: content
    defaultType: page
    maxItems: 5000
    includePaths:
      - hello-world.md
      - blog/2026-01-update.md
    includeGlobs:
      - blog/*.md
      - "**/pages/*.md"
```

说明：

- `maxItems`：最多读取多少篇（按路径排序后截断）。
- `includePaths`：只读取指定路径（相对 `content.markdown.dir`；可省略 `.md`）。
- `includeGlobs`：只读取匹配 glob 的文件（匹配相对路径，分隔符使用 `/`；`**` 表示跨目录）。

## Front Matter（YAML）基础写法

每个 Markdown 文件可选带一个 YAML Front Matter 段：

```yaml
---
type: page
title: 关于我们
slug: about
publishAt: 2026-01-01T00:00:00Z
language: zh-CN
tags: [bukit, starter]
categories: docs
summary: 一句话摘要（用于列表页或 meta）
seo_title: 自定义 SEO 标题（模板可用）
seo_desc: 自定义 SEO 描述（模板可用）
---
```

说明：Front Matter 的字段名大小写不敏感（例如 `Title` 与 `title` 等价）。如果同时写了大小写不同但名称相同的字段，后出现的值会覆盖先出现的值。

### 常用字段说明（用户视角）

| 字段 | 常见值 | 作用 |
|---|---|---|
| `collection` | 字符串 | 对应 site.collections 中的集合 key，决定路由与模板（推荐优先使用） |
| `type` | `page` / `post` | 兼容层：当未使用 collection 时决定默认路由与模板 |
| `title` | 文本 | 页面标题（缺省可从正文第一个 `#` 提取） |
| `slug` | `hello-world` | URL 核心片段（缺省为文件名） |
| `publishAt` | ISO 时间字符串 | 发布时间（缺省可能使用文件修改时间） |
| `language` | `zh-CN`/`en-US` | 多语言过滤与输出（启用 i18n 时建议每条都写） |
| `tags` | 数组或逗号分隔 | 用于 tags 派生页与文章组织 |
| `categories` | 数组或字符串 | 用于 categories 派生页与文章组织 |
| `summary` | 文本 | 列表页/卡片摘要（主题可用） |

你可以自定义更多字段（例如 `cover`, `reading_time`, `seo_*`），它们会进入 `page.fields.*` 供模板读取。

## 示例 1：页面（page）— 兼容层默认路由

文件：`content/about.md`

```markdown
---
type: page
title: 关于我们
slug: about
language: zh-CN
seo_title: 关于我们 - My Site
seo_desc: 这是一个用 Bukit 构建的示例站点
---

# 关于我们

这是一段示例内容。你可以把这里换成你的产品/团队介绍。
```

常见用途：

- 关于、联系、帮助中心、产品介绍、隐私政策

## 示例 2：文章（post）— 兼容层默认路由

文件：`content/2026-01-hello.md`

```markdown
---
type: post
title: 2026 年 1 月更新
slug: 2026-01-update
publishAt: 2026-01-15T10:00:00+08:00
language: zh-CN
tags: [release, roadmap]
categories: updates
summary: 本月主要更新：多语言、搜索与模块数据源
cover: /assets/covers/2026-01.png
reading_time: 6
---

# 2026 年 1 月更新

这里写文章正文……
```

常见用途：

- 博客、新闻、更新日志

## 多语言写法（Markdown）

当站点启用了多语言：

```yaml
site:
  languages: [zh-CN, en-US]
  defaultLanguage: zh-CN
```

建议每条内容都写 `language`，并保持 slug 在同语言内唯一。

示例（同一个主题的双语页面）：

`content/greeting-zh.md`

```markdown
---
type: page
title: 你好
slug: greeting
language: zh-CN
---

# 你好
```

`content/greeting-en.md`

```markdown
---
type: page
title: Hello
slug: greeting
language: en-US
---

# Hello
```

对照可运行示例：`examples/starter/content/greeting-zh.md` 与 `examples/starter/content/greeting-en.md`。

## 常见问题（FAQ）

### 1）我没写 title，会发生什么？

- 引擎会尝试从正文的第一个 `# ` 标题提取
- 如果正文也没有一级标题，可能会回退为 slug 或文件名

建议：用户可见页面尽量显式写 `title`，减少歧义。

### 2）我没写 slug，会发生什么？

- 通常会使用文件名（不带 `.md`）作为 slug

建议：slug 尽量稳定；如果你未来要改标题，建议不要改 slug（避免 URL 改动导致旧链接失效）。

### 3）为什么标签/分类页没生成？

- 需要主题支持 + 内置派生逻辑生效（详见：[10-内置功能与输出](./10-built-in-features.zh-CN.md)）
- 确认你的内容确实写了 `tags`/`categories`

### 4）我想让某篇内容“先不发布”

推荐两种方式：

- 不把文件加入仓库（本地写完再提交）
- 使用构建时的草稿机制（如果你的主题/约定使用某个字段标记草稿）：构建时可用 `--draft` 打开草稿渲染

如果你使用 Notion 模式，建议用 `Published` 字段过滤（见：[06-内容-Notion](./06-notion-content.zh-CN.md)）。
