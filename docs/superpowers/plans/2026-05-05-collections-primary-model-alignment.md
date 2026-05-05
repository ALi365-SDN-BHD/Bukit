# Collections Primary Model Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 统一仓库对外心智到 `site.collections` 主模型，同时保留 `post/page` 兼容行为不变。

**Architecture:** 本次只收敛入口层和叙事层，不修改路由 fallback 逻辑。实现分三块：先调整 CLI 入口文案，再收敛 README 与 guide 文档，最后整理测试与示例叙事，使 `collections` 成为唯一推荐路径，而 `post/page` 明确降级为兼容层。

**Tech Stack:** C# / .NET 10 / xUnit / Markdown 文档

---

### Task 1: 收敛 CLI 入口文案

**Files:**
- Modify: `src/Bukit.Cli/Commands/DoctorCommand.cs`
- Modify: `src/Bukit.Cli/Commands/InitCommand.cs`
- Modify: `src/Bukit.Cli/Commands/HelpPrinter.cs`
- Test: `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`

- [ ] **Step 1: 先为 doctor 提示写失败测试**

```csharp
[Fact]
public async Task RunAsync_CollectionsMissingMessage_UsesCollectionsFirstWording()
{
    File.WriteAllText(_configPath, """
                                   site:
                                     name: test
                                     title: Test
                                   content:
                                     provider: markdown
                                     markdown:
                                       dir: content
                                   """);

    using var writer = new StringWriter(new StringBuilder());
    var originalOut = Console.Out;
    Console.SetOut(writer);
    try
    {
        var exitCode = await DoctorCommand.RunAsync(new ArgReader(new[] { "--config", _configPath }));

        var output = writer.ToString();
        Assert.Equal(1, exitCode);
        Assert.Contains("site.collections", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("主模型", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("兼容", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("article", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("post/page 是默认模型", output, StringComparison.OrdinalIgnoreCase);
    }
    finally
    {
        Console.SetOut(originalOut);
    }
}
```

- [ ] **Step 2: 运行目标测试，确认修改前失败**

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~RunAsync_CollectionsMissingMessage_UsesCollectionsFirstWording"`
Expected: FAIL，提示文案仍是旧 wording 或缺少 `article` / `兼容层`

- [ ] **Step 3: 修改 doctor 提示语，弱化 post/page 主模型表述**

```csharp
if (config.Site.Collections is null || config.Site.Collections.Count == 0)
{
    Console.WriteLine("✖ Migration required: site.collections is not configured");
    Console.WriteLine("  - collection 驱动路由已成为主模型，请在 site.collections 中声明每个内容集合的 permalink/template/listRoute");
    Console.WriteLine("  - post/page 默认规则仍作为兼容层保留，但不再是新项目的推荐主路径");
    Console.WriteLine("  - 示例：site.collections.article.permalink=/articles/{slug}/, template=pages/post.html, listRoute=/articles/");
    return 1;
}
```

- [ ] **Step 4: 检查 init/help 文案是否仍放大 post/page**

```bash
grep -RInE "post/page|默认模型|默认规则" src/Bukit.Cli/Commands/InitCommand.cs src/Bukit.Cli/Commands/HelpPrinter.cs
```

Expected: 若命中旧叙事，则进入下一步修改；若无命中，记录为无需改动

- [ ] **Step 5: 如存在旧叙事，则改为 collections-first 表达**

```csharp
// 仅示意：若 HelpPrinter 中存在相关帮助文案，改成如下口径
Console.WriteLine("Bukit 使用 site.collections 作为推荐内容组织模型。");
Console.WriteLine("post/page 默认规则仍保留用于兼容旧项目。");
```

- [ ] **Step 6: 运行 CLI 测试子集验证文案与现有行为**

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~DoctorCommandTests"`
Expected: PASS

- [ ] **Step 7: 提交 CLI 文案收敛**

```bash
git add src/Bukit.Cli/Commands/DoctorCommand.cs src/Bukit.Cli/Commands/InitCommand.cs src/Bukit.Cli/Commands/HelpPrinter.cs tests/Bukit.Cli.Tests/DoctorCommandTests.cs
git commit -m "docs(cli): align doctor messaging to collections-first model"
```

### Task 2: 收敛 README 与用户/开发文档

**Files:**
- Modify: `README.md`
- Modify: `README.zh-CN.md`
- Modify: `README.ms.md`
- Modify: `guide/user/README.md`
- Modify: `guide/user/README.zh-CN.md`
- Modify: `guide/user/README.ms.md`
- Modify: `guide/dev/README.md`
- Modify: `guide/dev/README.zh-CN.md`
- Modify: `guide/dev/README.ms.md`
- Modify: `guide/dev/routing.md`
- Modify: `guide/dev/architecture.md`
- Modify: `guide/dev/architecture-review.md`

- [ ] **Step 1: 全文定位旧叙事**

Run: `grep -RInE "post/page|默认模型|默认规则|default model|default rule" README*.md guide/user guide/dev`
Expected: 输出所有待改位置

- [ ] **Step 2: 修改中文 README 主叙事**

```md
- `site.collections` 是 Bukit 推荐的内容组织与路由主模型
- `post/page` 默认规则仍保留用于兼容旧项目
```

- [ ] **Step 3: 修改英文与马来文 README 的同义表述**

```md
- `site.collections` is the primary recommended model for content organization and routing
- The `post/page` defaults remain available as a compatibility fallback for existing projects
```

- [ ] **Step 4: 修改 user guide 入口与配置章节**

```md
## 推荐路径

新项目建议先定义 `site.collections`，再为每个 collection 指定：

- `permalink`
- `template`
- `listRoute`

`post/page` 默认规则仅用于兼容旧项目。
```

- [ ] **Step 5: 修改 dev guide，把默认规则降级为兼容规则**

```md
- 主路径：`site.collections`
- 兼容路径：`post/page` fallback
```

- [ ] **Step 6: 修改 routing/architecture 文档中的术语**

```md
系统优先按 `collections` 决定路由与模板；仅当未命中 collection 规则时，才回退到 `post/page` 默认兼容规则。
```

- [ ] **Step 7: 执行文档一致性检索**

Run: `grep -RInE "post/page 是默认模型|post/page.*主模型|default model.*post/page" README*.md guide/user guide/dev`
Expected: 无结果

- [ ] **Step 8: 提交文档收敛**

```bash
git add README.md README.zh-CN.md README.ms.md guide/user guide/dev
git commit -m "docs: promote collections as the primary content model"
```

### Task 3: 收敛测试与样例叙事

**Files:**
- Modify: `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`
- Modify: `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`
- Modify: `tests/Bukit.Engine.Tests/ConfigValidatorTests.cs`
- Modify: `examples/starter/site.yaml`
- Modify: `examples/starter/site.v2.yaml`
- Modify: `examples/starter/site.modules.yaml`

- [ ] **Step 1: 查找测试名和样例中的旧主模型表达**

Run: `grep -RInE "post/page|default rule|默认规则|默认模型" tests examples/starter`
Expected: 输出待改位置

- [ ] **Step 2: 重命名测试方法，使 collections-first 成为默认叙事**

```csharp
[Fact]
public void Generate_UsesCollectionRules_AsPrimaryRoutingModel()
{
    // 原本强调 post/page 的主路径测试，改成强调 collection 优先级
}

[Fact]
public void Generate_FallsBackToLegacyPostPageRules_WhenCollectionsMissing()
{
    // 兼容测试显式标记 legacy/fallback
}
```

- [ ] **Step 3: 如测试断言文案含旧叙事，同步改写**

```csharp
Assert.Contains("collections", message, StringComparison.OrdinalIgnoreCase);
Assert.Contains("compat", message, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 4: 检查示例配置注释与章节顺序**

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
```

Expected: 即使默认值仍为 `post/page`，注释或示例说明也要强调“这是默认提供的 collections 示例”，而不是“主模型定义”

- [ ] **Step 5: 补充必要的文案断言测试**

```csharp
[Fact]
public async Task RunAsync_CollectionsMissingMessage_MarksPostPageAsCompatibilityLayer()
{
    // 断言 doctor 提示中包含“兼容层”语义
}
```

- [ ] **Step 6: 运行相关测试子集**

Run: `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~RouteGenerator|FullyQualifiedName~ConfigValidator"`
Expected: PASS

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter "FullyQualifiedName~DoctorCommandTests"`
Expected: PASS

- [ ] **Step 7: 做最终差异检查**

Run: `git diff -- tests examples/starter`
Expected: 仅包含测试命名、测试提示断言与样例叙事收敛，不包含底层行为变更

- [ ] **Step 8: 提交测试与样例收敛**

```bash
git add tests examples/starter
git commit -m "test: align examples and tests with collections-first wording"
```

### Task 4: 最终验证与交付说明

**Files:**
- Modify: `README.zh-CN.md`

- [ ] **Step 1: 运行最终全文检索，确认唯一推荐路径**

Run: `grep -RInE "site.collections|post/page" README*.md guide/user guide/dev src/Bukit.Cli tests examples/starter`
Expected: 结果中 `site.collections` 出现在主叙事；`post/page` 仅出现在兼容语境、模板名或 legacy/fallback 测试中

- [ ] **Step 2: 运行工作区格式与空白检查**

Run: `git diff --check`
Expected: 无输出

- [ ] **Step 3: 运行最终测试矩阵**

Run: `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release`
Expected: PASS

Run: `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release`
Expected: PASS

- [ ] **Step 4: 写最终交付说明**

```md
- `collections` 现已成为所有用户入口中的唯一推荐主模型
- `post/page` 仍保留为兼容层，不影响旧项目
- 本次未修改路由 fallback 逻辑、模板文件名和已有项目配置
```

- [ ] **Step 5: 提交最终收尾**

```bash
git add README.zh-CN.md
git commit -m "chore: finalize collections-first alignment notes"
```
