# Weaken post/page Mental Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在保持 `site.collections` 为主模型的前提下，逐步弱化 post/page 作为默认心智模型——先收敛用户文档叙事（L1），再修复 InitCommand Notion 模式缺失 collections 的问题（L2），不修改路由 fallback 逻辑。

**Architecture:** 所有变更分为两块：用户文档收敛（10 个 .md 文件）和 InitCommand Notion 模式修复（1 个 .cs + 1 个 test）。文档收敛的核心策略是：不删除 post/page 的实用信息，而是在每次提及 post/page 作为默认规则时，补上 "推荐优先使用 site.collections" 的引导，并将 post/page 类型体系明确标记为兼容层。InitCommand 修复则是在 Notion 模式的 site.yaml 中生成与 Markdown 模式同构的 collections 节点。

**Tech Stack:** C# / .NET 10 / xUnit / Markdown 文档

---

### Task 1: 收敛 02-核心概念.md — 路由部分改为 collections-first

**Files:**
- Modify: `guide/user/02-核心概念.md`

- [ ] **Step 1: 修改构建流程图与三句话概要**

原文 (L7-L26):
```text
site.yaml
  │
  ├─ content（Markdown / Notion / sources）
  │     └─ 读取内容 → 统一为 ContentItem
  │
  ├─ routing（按 type/slug/language/... 决定 URL）
  │
  ├─ rendering（按模板把内容渲染成 HTML）
  │
  └─ plugins（可选：生成 sitemap/rss/search、派生页等）
        ↓
      dist/（静态文件输出目录）
```
替换为:
```text
site.yaml
  │
  ├─ content（Markdown / Notion / sources）
  │     └─ 读取内容 → 统一为 ContentItem
  │
  ├─ routing（优先按 site.collections 决定 URL；兼容层回退 type/permalinks）
  │
  ├─ rendering（按模板把内容渲染成 HTML）
  │
  └─ plugins（可选：生成 sitemap/rss/search、派生页等）
        ↓
      dist/（静态文件输出目录）
```

三句话概要 (L22-L26) 替换为:
```
你要记住的只有三句话：

1. **内容来自哪里**（content.provider / sources）
2. **每条内容输出到哪里**（推荐通过 site.collections 配置；兼容通过 slug/type + 路由覆盖字段）
3. **用什么模板渲染**（collection 指定 template；也可被 template 覆盖）
```

- [ ] **Step 2: 修改 Meta 键说明**

原文 (L44-L50):
```markdown
常见 Meta 键（你在 Markdown Front Matter 或 Notion 字段里提供）：

- `type`：通常是 `page` 或 `post`（决定默认路由与模板）
- `slug`：URL 的核心部分（一般推荐稳定不变）
- `language`：内容语言归属（多语言时用于过滤与关联）
- `tags` / `categories`：标签/分类（用于派生列表页）
- `route` / `url` / `outputPath` / `template`：用于"覆盖默认路由/模板"的高级用法（谨慎使用）
```
替换为:
```markdown
常见 Meta 键（你在 Markdown Front Matter 或 Notion 字段里提供）：

- `collection`：内容所属集合（推荐），对应 site.collections 中的 key，决定路由与模板
- `type`：`page` 或 `post`（兼容层，当未配置 collection 时用于决定默认路由与模板）
- `slug`：URL 的核心部分（一般推荐稳定不变）
- `language`：内容语言归属（多语言时用于过滤与关联）
- `tags` / `categories`：标签/分类（用于派生列表页）
- `route` / `url` / `outputPath` / `template`：用于"覆盖默认路由/模板"的高级用法（谨慎使用）
```

- [ ] **Step 3: 修改路由章节**

原文 (L74-L85):
```markdown
## 路由：一条内容会变成哪个 URL？

最常见的规则是"按类型输出到固定前缀下"：

- `type: page`：常见输出到 `/pages/<slug>/`（具体由主题与路由规则决定）
- `type: post`：常见输出到 `/blog/<slug>/`

你可以通过以下方式控制结果：

- 改 `slug`：改变路径的一段（推荐的常用方式）
- 改 `type`：改变"它属于页面还是文章"（谨慎，涉及模板与列表）
- 用 `route/url/outputPath` 覆盖：更强，但更容易配错（详见：[03-项目目录与约定](./03-项目目录与约定.md) 与 [14-故障排查](./14-故障排查.md)）
```
替换为:
```markdown
## 路由：一条内容会变成哪个 URL？

推荐方式：通过 `site.collections` 为每个集合定义 permalink、template 和 listRoute（详见：[04-配置-site-yaml](./04-配置-site-yaml.md)）。

兼容层说明（当未配置 site.collections 或内容项缺少 collection 时生效）：

- `type: page`：默认输出到 `/pages/<slug>/`
- `type: post`：默认输出到 `/blog/<slug>/`

你可以通过以下方式控制结果：

- 在 site.collections 中声明集合规则（推荐）
- 在内容的 meta 中指定 `collection` 对应集合 key（推荐）
- 改 `slug`：改变路径的一段
- 改 `type`：改变兼容层行为的类型归属
- 用 `route/url/outputPath` 覆盖：更强，但更容易配错（详见：[03-项目目录与约定](./03-项目目录与约定.md) 与 [14-故障排查](./14-故障排查.md)）
```

- [ ] **Step 4: 提交**

```bash
git add guide/user/02-核心概念.md
git commit -m "docs: weaken post/page narrative in core concepts, promote collections-first"
```

### Task 2: 收敛 03-项目目录与约定.md — 目录结构与类型体系

**Files:**
- Modify: `guide/user/03-项目目录与约定.md`

- [ ] **Step 1: 修改推荐目录结构与注释**

原文 (L15):
```
  content/            # Markdown 内容（page/post）
```
替换为:
```
  content/            # Markdown 内容
```

- [ ] **Step 2: 修改 type 章节**

原文 (L132-L137):
```markdown
### type（page / post）

- `page`：页面（关于、帮助、产品介绍等）
- `post`：文章（博客、新闻、更新日志等）

主题一般会按 type 区分模板和列表页；不建议随意增加太多自定义 type，除非你的主题已支持对应模板。
```
替换为:
```markdown
### type（page / post）— 兼容层

> 推荐优先使用 `site.collections` 定义内容集合与路由规则（见 [04-配置-site-yaml](./04-配置-site-yaml.md)）。

当未配置 collections 时，引擎使用 type 字段作为兼容回退：

- `page`：页面（关于、帮助、产品介绍等）
- `post`：文章（博客、新闻、更新日志等）

主题一般会按 type 或 collection 区分模板和列表页；不建议随意增加太多自定义 type，除非你的主题已支持对应模板。
```

- [ ] **Step 3: 提交**

```bash
git add guide/user/03-项目目录与约定.md
git commit -m "docs: mark post/page as compatibility layer in project directory conventions"
```

### Task 3: 收敛 01-快速开始.md — 加入 collections 引导

**Files:**
- Modify: `guide/user/01-快速开始.md`

- [ ] **Step 1: 在最小 site.yaml 后加入 collections 引导段落**

在原文 L95（`logging:` 块结束后）追加段落:

```markdown
> **推荐：使用 site.collections 定义路由与模板** 以上配置依赖 post/page 兼容层路由（page → `/pages/`，post → `/blog/`）。新项目建议显式声明 collections（见 [04-配置-site-yaml](./04-配置-site-yaml.md)），示例：
>
> ```yaml
> site:
>   collections:
>     page:
>       permalink: /pages/{slug}/
>       template: pages/page.html
>       listRoute: /pages/
>     post:
>       permalink: /blog/{slug}/
>       template: pages/post.html
>       listRoute: /blog/
> ```

- [ ] **Step 2: 修改示例 Front Matter 的注释**

原文 (L101-L113) — 在 Front Matter 示例的 `type: page` 上方添加注释:

```markdown
```markdown
---
# 推荐：如已配置 site.collections，可额外设置 collection 字段以精确匹配集合规则
type: page
title: Hello World
...
```
```

改为:

```markdown
```markdown
---
type: page
# 提示：如已在 site.yaml 配置 site.collections，可添加 collection 字段以精确匹配
title: Hello World
slug: hello-world
tags: [demo, first]
summary: 这是我的第一篇页面
---
```

- [ ] **Step 3: 提交**

```bash
git add guide/user/01-快速开始.md
git commit -m "docs: add collections guidance to quickstart, retain post/page compat info"
```

### Task 4: 收敛 05-内容-Markdown.md — 字段说明与示例

**Files:**
- Modify: `guide/user/05-内容-Markdown.md`

- [ ] **Step 1: 修改常用字段说明表**

原文 (L75-L78):
```markdown
| 字段 | 常见值 | 作用 |
|---|---|---|
| `type` | `page` / `post` | 决定默认路由与模板 |
```
替换为:
```markdown
| 字段 | 常见值 | 作用 |
|---|---|---|
| `collection` | 字符串 | 对应 site.collections 中的集合 key，决定路由与模板（推荐优先使用） |
| `type` | `page` / `post` | 兼容层：当未使用 collection 时决定默认路由与模板 |
```

- [ ] **Step 2: 修改"示例 1：页面（page）"标题与引导**

原文 (L88):
```markdown
## 示例 1：页面（page）
```
替换为:
```markdown
## 示例 1：页面（page）— 兼容层默认路由
```

并在 Front Matter 示例代码块后（L105 之后）、常见用途列表之前插入:
```markdown
> 推荐：新项目建议在 site.yaml 中声明 `site.collections.page`，并通过 `collection: page` 字段匹配，而非依赖 type 兼容路由。
```

- [ ] **Step 3: 修改"示例 2：文章（post）"标题与引导**

原文 (L111):
```markdown
## 示例 2：文章（post）
```
替换为:
```markdown
## 示例 2：文章（post）— 兼容层默认路由
```

并在 Front Matter 示例代码块后（L133 之后）、常见用途列表之前插入:
```markdown
> 推荐：新项目建议在 site.yaml 中声明 `site.collections.post`，并通过 `collection: post` 字段匹配。
```

- [ ] **Step 4: 提交**

```bash
git add guide/user/05-内容-Markdown.md
git commit -m "docs: add collections-first guidance to Markdown content page"
```

### Task 5: 收敛 06-内容-Notion.md — Type 字段与模拟数据

**Files:**
- Modify: `guide/user/06-内容-Notion.md`

- [ ] **Step 1: 修改引擎决策字段表**

原文 (L84):
```
| `Type` | select 或 multi_select | `page`/`post`（缺省常为 post） |
```
替换为:
```
| `Type` | select 或 multi_select | `page`/`post`（兼容层用途；推荐额外建 `Collection` 字段对应 site.collections key） |
```

- [ ] **Step 2: 修改模拟数据表的 Type 列说明段落**

在原文 L115（模拟数据表最后一个完整行结束后）、L117（`Published` 说明）之前插入:

```markdown
> **推荐：使用 site.collections 替代 type 默认路由。** 如果你在 Notion 数据库中新增一个 `Collection` 字段（select 类型，值如 `blog`、`docs`），并在 site.yaml 的 site.collections 中声明对应的集合规则，引擎将优先使用 collection 驱动路由，而不是 type 兼容回退。
```

- [ ] **Step 3: 提交**

```bash
git add guide/user/06-内容-Notion.md
git commit -m "docs: recommend collection field over type in Notion content guide"
```

### Task 6: 收敛 07-内容-多源-sources.md — 示例中加入 collections

**Files:**
- Modify: `guide/user/07-内容-多源-sources.md`

- [ ] **Step 1: 在 sources 基本结构示例后加入 collections 指引**

在原文 L25（第一个 YAML 示例块结束）后、L27（字段说明表）前插入:

```markdown
> **推荐：搭配 site.collections 使用。** 当使用 sources 模式时，建议同时在 site.yaml 顶层声明 `site.collections`，让每个 source 的内容通过 collection key 精确匹配路由规则（而不是依赖 type 兼容层）。
```

- [ ] **Step 2: 修改三个组合示例的引导说明**

在"组合示例 1"的 YAML 块结束后（L66 后）、配套说明之前插入:

```markdown
> 如果希望精确控制路由，可在 site.yaml 中添加：
> ```yaml
> site:
>   collections:
>     page:
>       permalink: /pages/{slug}/
>       template: pages/page.html
>       listRoute: /pages/
> ```

在"组合示例 2"的 YAML 块结束后（L119 后）插入:

```markdown
> 建议在 site.yaml 中声明 site.collections（例如 `blog`、`docs`），并在每个 Notion 数据库的 Collection 字段中填写对应的 key。这样引擎可以精确匹配路由规则，而不依赖 type 兼容回退。
```

在"组合示例 3"的 YAML 块结束后（L160 后）、排查建议之前插入:

```markdown
> 推荐为每个 content source 在 site.collections 中声明对应集合规则，使 Markdown 页面和 Notion 博客都能通过 collection 驱动路由。
```

- [ ] **Step 3: 提交**

```bash
git add guide/user/07-内容-多源-sources.md
git commit -m "docs: promote collections usage in sources guide examples"
```

### Task 7: 收敛 15-场景化示例.md — Recipe 配置加入 collections

**Files:**
- Modify: `guide/user/15-场景化示例（Recipes）.md`

- [ ] **Step 1: Recipe 1（最小博客）— 在配置后加 collections 推荐**

在原文 L35（第一个 Recipe 的 YAML 配置块结束）后、L37（模拟数据标题）前插入:

```markdown
> **推荐：显式声明 site.collections。** 以上配置依赖 post/page 兼容路由。更规范的做法是添加：
> ```yaml
> site:
>   collections:
>     post:
>       permalink: /blog/{slug}/
>       template: pages/post.html
>       listRoute: /blog/
> ```

- [ ] **Step 2: Recipe 2（多语言站点）— 在配置后加 collections 推荐**

在原文 L96（第二个 Recipe 的 YAML 配置块结束）后、L98（模拟数据标题）前插入:

```markdown
> **推荐：为多语言站点显式声明 collections。** 新增 `site.collections.page` 和 `site.collections.post` 可以让路由更可控，尤其是在多语言 + collection 组合时。
```

- [ ] **Step 3: Recipe 4（Notion 当 CMS）— 在配置后加 collections 推荐**

在原文 L221（第四个 Recipe 的 YAML 配置块结束）后、L223（运行要点标题）前插入:

```markdown
> **推荐：声明 site.collections 并配合 Notion Collection 字段。** 建议在 site.yaml 中添加 `site.collections` 节点，同时在 Notion 数据库中创建 `Collection` 字段（select 类型），使引擎优先使用 collection 驱动路由。
```

- [ ] **Step 4: 提交**

```bash
git add guide/user/15-场景化示例（Recipes）.md
git commit -m "docs: add collections-first recommendations to all recipes"
```

### Task 8: 收敛 guide/dev/content.md — Meta 说明

**Files:**
- Modify: `guide/dev/content.md`

- [ ] **Step 1: 修改 Meta 键说明**

原文 (L24):
```markdown
- `type`：`post` / `page`（影响默认路由与模板）
```
替换为:
```markdown
- `collection`：对应 site.collections 中的 key（推荐；优先级高于 type）
- `type`：`post` / `page`（兼容层；当未配置 collection 或 collection 未命中时用于默认路由与模板）
```

- [ ] **Step 2: 提交**

```bash
git add guide/dev/content.md
git commit -m "docs: add collection field to dev content model docs"
```

### Task 9: 收敛 guide/dev/init-create.md — hello-world.md 说明

**Files:**
- Modify: `guide/dev/init-create.md`

- [ ] **Step 1: 修改生成结构说明**

原文 (L67):
```markdown
- `hello-world.md` 默认作为 `type: page` 的内容页渲染（路由规则见 [routing](./routing.md)）
```
替换为:
```markdown
- `hello-world.md` 默认作为 `type: page` 的内容页渲染。新项目建议在 site.yaml 中配置 `site.collections`（生成器默认已包含），使路由由 collection 规则驱动（路由规则见 [routing](./routing.md)）
```

- [ ] **Step 2: 提交**

```bash
git add guide/dev/init-create.md
git commit -m "docs: mention collections in init-create dev docs"
```

### Task 10: 修复 InitCommand.cs — Notion 模式生成 collections

**Files:**
- Modify: `src/Bukit.Cli/Commands/InitCommand.cs`
- Test: `tests/Bukit.Cli.Tests/InitCommandTests.cs`（如不存在则看 DoctorCommandTests.cs 的 `InitGeneratedMarkdownSite_ContainsCollections_AndPassesDoctor` 测试模式）

- [ ] **Step 1: 先查现有 InitCommand 测试**

Run: `grep -RIn "InitCommand" tests/`
Expected: 找到已有测试文件或在 DoctorCommandTests 中的 `InitGeneratedMarkdownSite_ContainsCollections_AndPassesDoctor`——它将作为 Notion 测试的模板。

- [ ] **Step 2: 写 Notion 模式 collections 测试（在 DoctorCommandTests.cs 中）**

```csharp
[Fact]
public async Task InitGeneratedNotionSite_ContainsCollections_AndPassesDoctor()
{
    var initRootDir = Path.Combine(Path.GetTempPath(), "bukit-init-notion-doctor-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(initRootDir);

    try
    {
        var siteDir = Path.Combine(initRootDir, "site");
        var initExitCode = await InitCommand.RunAsync(new ArgReader(new[] { "init", siteDir, "--provider", "notion" }));
        Assert.Equal(0, initExitCode);

        var generatedConfigPath = Path.Combine(siteDir, "site.yaml");
        var yaml = await File.ReadAllTextAsync(generatedConfigPath);
        Assert.Contains("collections:", yaml, StringComparison.Ordinal);

        using var writer = new StringWriter(new StringBuilder());
        var originalOut = Console.Out;
        Console.SetOut(writer);
        try
        {
            var doctorExitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", generatedConfigPath }));

            Assert.Equal(0, doctorExitCode);
            Assert.Contains("Doctor passed", writer.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
    finally
    {
        if (Directory.Exists(initRootDir))
        {
            Directory.Delete(initRootDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: 运行测试，确认修改前失败**

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~InitGeneratedNotionSite_ContainsCollections_AndPassesDoctor"`
Expected: FAIL — Notion 生成配置中不包含 `collections:`

- [ ] **Step 4: 修改 InitCommand.cs — Notion 模式 site.yaml 模板**

在 [InitCommand.cs:L64-L91](file:///e:/Github/Bukit/src/Bukit.Cli/Commands/InitCommand.cs#L64-L91)，将 Notion 分支的 site.yaml 模板字符串替换为:

```csharp
return """
site:
  name: my-site
  title: My Site
  baseUrl: /
  language: zh-CN
  timezone: Asia/Shanghai
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/

content:
  provider: notion
  notion:
    databaseId: xxxxx

build:
  output: dist
  clean: true

theme:
  name: starter
  layouts: layouts
  assets: assets
  static: static

logging:
  level: info
""";
```

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~InitGeneratedNotionSite_ContainsCollections_AndPassesDoctor"`
Expected: PASS

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~DoctorCommandTests"`
Expected: PASS（全部 7 个测试通过，不破坏已有测试）

- [ ] **Step 6: 提交**

```bash
git add src/Bukit.Cli/Commands/InitCommand.cs tests/Bukit.Cli.Tests/DoctorCommandTests.cs
git commit -m "feat: generate site.collections in Notion init mode, aligning with markdown mode"
```

### Task 11: 最终验证与全文一致性检索

- [ ] **Step 1: 最终文档一致性检索 — 确认无遗漏的旧叙事**

Run: `grep -RInE "决定默认路由与模板" guide/user guide/dev`
Expected: 仅 `guide/user/05-内容-Markdown.md` 的 type 行出现（已标记"兼容层"），其余位置不应有此表述。

Run: `grep -RInE "通常是.*page.*或.*post" guide/user guide/dev`
Expected: 无结果（核心概念文档已改为包含 `collection` 引导的新表述）

- [ ] **Step 2: 确认 collections 在所有入口点可见**

Run: `grep -RInE "site\.collections|collections 主|collections-first" guide/user guide/dev README*.md`
Expected: README（3 种语言）已包含；routing.md 已包含；多个 guide/user 文件经本次修改已包含

- [ ] **Step 3: 运行完整 CLI 测试矩阵**

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release`
Expected: PASS（含新增 Notion 测试）

- [ ] **Step 4: 运行完整 Engine 测试矩阵**

Run: `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release`
Expected: PASS

- [ ] **Step 5: 提交最终收尾**

```bash
git add .
git commit -m "chore: finalize post/page mental model weakening - all user docs and init command"
```
