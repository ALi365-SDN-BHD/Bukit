# Bukit Clone 实现计划

## CLI 命令 + AI Skill 混合架构

---

## 一、目标与范围

### 目标

在 Bukit 中新增 `bukit clone` 命令和一个 `bukit-clone` AI Skill，实现从目标网站设计令牌 → Bukit 主题的自动生成。

### 三阶段流水线

```
Phase 1: Reconnaissance (AI Agent 负责)    → tokens.json + layout.json
Phase 2: Theme Generation (CLI 负责)       → themes/<name>/ (17 个文件)
Phase 3: Verify (CLI + AI 共同)            → bukit doctor → bukit build
```

### 本次实现范围

- ✅ CLI 命令 `bukit clone --tokens <file> --theme <name>`（从预提取令牌生成主题）
- ✅ 核心生成器 `CloneThemeGenerator`（根据设计令牌生成 17 个主题文件）
- ✅ AI Skill 文件 `bukit-clone/SKILL.md`（指导 AI Agent 提取设计令牌 + 调用 CLI）
- ✅ 数据模型：`CloneTokens`、`CloneLayoutInfo`、`SectionInfo`
- ✅ 单元测试：验证生成器产出的 CSS 变量和模板结构
- ❌ 不实现浏览器端自动令牌提取（留给 AI Agent Skill 处理）
- ❌ 不修改 Bukit.Engine（零引擎侵入）

---

## 二、当前状态分析

### 现有架构

```
src/Bukit.Cli/
  Commands/
    ThemeCommand.cs          ← theme create/list/use
    StarterThemeScaffold.cs  ← 内置 17 个模板常量 + WriteTo()
  Program.cs                 ← 命令路由 (switch command)
  Cli/BukitCliSpecs.cs      ← CLI 规格注册表

src/skills/
  using-bukit/               ← 技能总入口（含 9 个 Skill 概览表）
  bukit-theme/               ← 主题开发 Skill
  bukit-templating/          ← 模板开发 Skill
  共 10 个 Skill 目录

theme/
  <name>/
    assets/style.css         ← CSS（含 `:root` 变量 + 完整语义类）
    layouts/
      layouts/base.html      ← HTML 骨架 ({{ content }})
      pages/                 ← page.html, post.html, index.html, list.html
      partials/              ← header, footer, list-card, pagination-nav
      bukit.templates.yaml   ← 模板能力声明
```

### 克隆能力在现有架构中的定位

```
bukit clone           ← 新增，对标 bukit theme create 的模式
  └── CLI 层：CloneThemeGenerator（从令牌生成 17 个文件）
  └── Skill 层：bukit-clone（指导 AI Agent 提取设计令牌）

不修改：
  - Bukit.Engine（引擎不感知克隆）
  - Bukit.Config（令牌不是 site.yaml 配置）
```

### 命名约定

- 命令类：`CloneCommand`（对标 `ThemeCommand`）
- 生成器类：`CloneThemeGenerator`（对标 `StarterThemeScaffold`）
- 数据模型：`CloneTokens`、`CloneLayoutInfo`、`SectionInfo`（record 类型）
- Skill 目录：`src/skills/bukit-clone/`

---

## 三、新增文件清单

| # | 文件路径 | 类型 | 说明 |
|---|---------|------|------|
| 1 | `src/Bukit.Cli/Commands/CloneCommand.cs` | 新建 | CLI 命令入口（解析参数、加载令牌、调用生成器、可选切换主题） |
| 2 | `src/Bukit.Cli/Commands/CloneThemeGenerator.cs` | 新建 | 核心主题生成器（方法对标 `StarterThemeScaffold.WriteTo`） |
| 3 | `src/Bukit.Cli/Commands/CloneModels.cs` | 新建 | 数据模型记录（`CloneTokens`、`CloneLayoutInfo`、`SectionInfo`） |
| 4 | `src/skills/bukit-clone/SKILL.md` | 新建 | AI Agent Skill 文件（~5KB，指导令牌提取 + CLI 调用） |
| 5 | `tests/Bukit.Cli.Tests/CloneCommandTests.cs` | 新建 | CLI 命令和生成器单元测试 |

---

## 四、修改文件清单

| # | 文件路径 | 变更内容 |
|---|---------|---------|
| 6 | `src/Bukit.Cli/Program.cs` | 在 `command switch` 中添加 `"clone" => await CloneCommand.RunAsync(reader)` |
| 7 | `src/Bukit.Cli/Cli/BukitCliSpecs.cs` | 在 `CreateRegistry()` 中添加 `clone` 命令的 `CliCommandSpec`（含 `--tokens`, `--theme`, `--layout`, `--brand`, `--use`, `--force`） |
| 8 | `src/skills/using-bukit/SKILL.md` | 在 Skill 概览表中新增第 10 行 `bukit-clone`，新增路由规则，新增 `bukit clone` 到 Quick Reference |

---

## 五、详细设计

### 5.1 数据模型（CloneModels.cs）

```csharp
namespace Bukit.Cli.Commands;

/// <summary> 从目标网站提取的设计令牌 </summary>
public sealed record CloneTokens
{
    // --- 颜色系统 (从 getComputedStyle() 提取) ---
    public string? Bg { get; init; }             // body/根元素背景色
    public string? Surface { get; init; }         // 卡片/区块背景色
    public string? SurfaceMuted { get; init; }    // Surface 的 95% 亮度版本 (自动计算)
    public string? Text { get; init; }            // 主文字色
    public string? Muted { get; init; }           // 次要文字色 (meta/subtitle)
    public string? Border { get; init; }          // 边框色
    public string? Primary { get; init; }         // 主按钮色 (a/button 颜色)
    public string? PrimaryStrong { get; init; }   // 主按钮悬停色 (自动计算)
    public string? Accent { get; init; }          // 强调色 (badge/tag/eyebrow)

    // --- 尺寸系统 ---
    public string? Radius { get; init; }          // 圆角 (card/button)
    public string? ContentMax { get; init; }      // 文章内容区最大宽度
    public string? WideMax { get; init; }         // 全宽容器最大宽度
    public string? Shadow { get; init; }          // 卡片阴影

    // --- 字体系统 ---
    public string? FontFamily { get; init; }      // 主体字体栈
    public string? HeadingFontFamily { get; init; } // 标题字体 (可选，默认同 body)
    public string? CodeFontFamily { get; init; }  // 等宽字体栈
    public string? GoogleFontsUrl { get; init; }  // Google Fonts 引用 URL (含所有 weight)
}

/// <summary> 页面布局信息 (AI Agent 分析 DOM 产出) </summary>
public sealed record CloneLayoutInfo
{
    public string? SiteTitle { get; init; }       // 导航栏/页脚站点名
    public string? HeroHeading { get; init; }     // Hero 区主标题
    public string? HeroSubtext { get; init; }     // Hero 区副文案
    public bool HasFeaturesSection { get; init; } // 是否有特性/卡片列表区
    public bool HasCTASection { get; init; }      // 是否有 CTA 行动号召区
    public List<SectionInfo> ExtraSections { get; init; } = [];  // 额外的自定义区块
}

/// <summary> 页面上的独立区块信息 </summary>
public sealed record SectionInfo
{
    public string Semantic { get; init; } = "content";  // "hero"|"features"|"testimonials"|"cta"|"content"
    public string? Heading { get; init; }
    public string? ContentHtml { get; init; }
    public List<string> ImageUrls { get; init; } = [];
}
```

### 5.2 CloneThemeGenerator 设计

对标 `StarterThemeScaffold.WriteTo(rootDir, themeName, primaryColor, accentColor)`，新的入口签名：

```csharp
namespace Bukit.Cli.Commands;

public static class CloneThemeGenerator
{
    /// <summary>
    /// 从设计令牌 + 布局信息生成完整 Bukit 主题。
    /// </summary>
    public static void WriteTo(
        string rootDir,
        string themeName,
        CloneTokens tokens,
        CloneLayoutInfo layout,
        string? brand = null)
    {
        // 1. 生成 CSS（从 tokens 填充变量值）
        var css = GenerateStyleCss(tokens);
        WriteFile(rootDir, $"themes/{themeName}/assets/style.css", css);

        // 2. 生成 base.html（含 Google Fonts 引用）
        WriteFile(rootDir, $"themes/{themeName}/layouts/layouts/base.html",
            GenerateBaseLayout(tokens));

        // 3. 生成 partials
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/header.html",
            GenerateHeader(brand ?? layout.SiteTitle));
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/footer.html",
            FooterPartial);
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/list-card.html",
            ListCardPartial);
        WriteFile(rootDir, $"themes/{themeName}/layouts/partials/pagination-nav.html",
            PaginationNavPartial);

        // 4. 生成页面模板
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/index.html",
            GenerateIndex(layout, brand));
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/page.html",
            PageTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/post.html",
            PostTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/list.html",
            ListTemplate);

        // 5. 可选模板（分页/分类/搜索）
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/pagination.html",
            StarterThemeScaffold.PaginationTemplate);  // 复用标准模板
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-index.html",
            StarterThemeScaffold.TaxonomyIndexTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/taxonomy-term.html",
            StarterThemeScaffold.TaxonomyTermTemplate);
        WriteFile(rootDir, $"themes/{themeName}/layouts/pages/search.html",
            StarterThemeScaffold.SearchTemplate);

        // 6. 模板能力声明
        WriteFile(rootDir, $"themes/{themeName}/layouts/bukit.templates.yaml",
            StarterThemeScaffold.TemplateCapabilities);
    }
}
```

**关键方法**：

| 方法 | 说明 |
|------|------|
| `GenerateStyleCss(CloneTokens)` | 从 tokens 填充 `:root` CSS 变量，其余语义类名保持不变。颜色字段使用 `??` 回退到 starter 默认值 |
| `GenerateBaseLayout(CloneTokens)` | 当 `GoogleFontsUrl` 非空时，在 `<head>` 中插入 Google Fonts `<link>` 标签；其余结构与 starter 一致 |
| `GenerateIndex(CloneLayoutInfo, string?)` | 根据 `layout` 信息有条件地生成 Hero、Features 区块；始终包含 Latest content 列表 |
| `GenerateHeader(string?)` | 导航栏使用 brand 名，nav-links 为默认 Home/Blog/Pages 三项 |

### 5.3 CloneCommand 设计

```csharp
namespace Bukit.Cli.Commands;

public static class CloneCommand
{
    public static async Task<int> RunAsync(ArgReader reader)
    {
        // 解析参数
        var tokensPath = reader.GetOption("--tokens");
        var layoutPath = reader.GetOption("--layout");
        var themeName = reader.GetOption("--theme") ?? "cloned";
        var brand = reader.GetOption("--brand");
        var use = reader.HasFlag("--use");
        var force = reader.HasFlag("--force");

        // 验证
        if (string.IsNullOrWhiteSpace(tokensPath))
        {
            Console.Error.WriteLine("Missing required option: --tokens <file>");
            return 2;
        }

        if (!CloneModels.IsSafeThemeName(themeName))
        {
            Console.Error.WriteLine("Invalid theme name.");
            return 2;
        }

        var resolved = ConfigPathResolver.Resolve(reader);
        var rootDir = resolved.RootDir;

        // 检查覆盖
        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (Directory.Exists(themeDir))
        {
            if (!force)
            {
                Console.Error.WriteLine($"Theme already exists: {themeName}. Use --force to overwrite.");
                return 2;
            }
            Directory.Delete(themeDir, recursive: true);
        }

        // 加载令牌
        var tokensJson = await File.ReadAllTextAsync(tokensPath);
        var tokens = CloneTokens.FromJson(tokensJson);

        // 加载布局（可选）
        CloneLayoutInfo layout;
        if (layoutPath is not null)
        {
            var layoutJson = await File.ReadAllTextAsync(layoutPath);
            layout = CloneLayoutInfo.FromJson(layoutJson);
        }
        else
        {
            layout = CloneLayoutInfo.Default;
        }

        // 生成主题
        CloneThemeGenerator.WriteTo(rootDir, themeName, tokens, layout, brand);
        Console.WriteLine($"Theme cloned: {themeName}");

        // 可选：切换主题
        if (use)
        {
            return await ThemeCommand.SetThemeAsync(themeName, reader,
                brand: brand, primaryColor: tokens.Primary, accentColor: tokens.Accent);
        }

        return 0;
    }
}
```

### 5.4 AI Skill 设计（bukit-clone/SKILL.md）

~5KB Markdown 文件，结构如下：

```markdown
---
name: bukit-clone
description: |-
  Clone any website's visual design into a Bukit theme. 
  Use when the user wants to clone/copy a website's appearance,
  replicate a design, or create a theme from an existing site.
argument-hint: "<url> [--theme <name>]"
user-invocable: true
---

# Bukit Clone Website → Theme

## Overview
Clone any website's visual design as a Bukit theme.
Two-phase workflow: (1) extract design tokens via browser, (2) generate theme via CLI.

## Phase 1: Extraction (Agent does this)
1. Open the target URL with browser MCP
2. Take full-page screenshots (1440px + 390px)
3. Run the JS extraction script to get tokens
4. Analyze page layout (nav/hero/features/footer)
5. Save tokens.json and layout.json

## Phase 2: Generation (CLI does this)
bukit clone --tokens tokens.json --layout layout.json --theme <name> --use

## Phase 3: Verify
bukit doctor && bukit build

## Design Token Extraction Script
[内嵌 JS 脚本]
```

### 5.5 BukitCliSpecs 新增规格

```csharp
var clone = new CliCommandSpec(
    Name: "clone",
    Description: "从设计令牌生成 Bukit 主题",
    Options: new[]
    {
        new CliOptionSpec("--tokens", "设计令牌 JSON 文件", CliOptionType.String,
            ValueName: "file", Required: true),
        new CliOptionSpec("--theme", "目标主题名", CliOptionType.String,
            ValueName: "name"),
        new CliOptionSpec("--layout", "页面布局 JSON 文件", CliOptionType.String,
            ValueName: "file"),
        new CliOptionSpec("--brand", "品牌名 (用于导航栏和页脚)"),
        new CliOptionSpec("--use", "创建后切换到该主题", CliOptionType.Flag),
        new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag),
        new CliOptionSpec("--config", "配置文件路径"),
        new CliOptionSpec("--site", "多站点名")
    });
```

### 5.6 Program.cs 修改

在 `command switch` 中添加一条：

```csharp
"clone" => await CloneCommand.RunAsync(reader),
```

### 5.7 using-bukit/SKILL.md 修改

1. Skill 概览表中新增第 10 行：
```
| 10 | bukit-clone | Website cloning to Bukit theme | When user wants to clone a website's design |
```

2. 路由规则中新增：
```
### User says "using bukit, clone this website"
1. Load using-bukit → Identify as clone task
2. Load bukit-clone → Extraction + CLI generation workflow
3. Load bukit-cli-reference → Verify commands
4. May need bukit-theme → Theme structure context
```

3. Quick Reference 中新增：
```
bukit clone --tokens <file> --theme <name>  # Generate theme from design tokens
```

---

## 六、默认值回退策略

当 `tokens.json` 中某些字段未提供时，回退到 `StarterThemeScaffold` 的默认值：

| tokens 字段 | 回退值 (starter 默认) |
|-----------|-------------------|
| `Bg` | `#fbfaf8` |
| `Surface` | `#ffffff` |
| `SurfaceMuted` | `#f3f1ed` |
| `Text` | `#202124` |
| `Muted` | `#66615b` |
| `Border` | `#ded9d0` |
| `Primary` | `#0b5fff` |
| `PrimaryStrong` | `#0846b8` |
| `Accent` | `#0f7b6c` |
| `Radius` | `8px` |
| `ContentMax` | `760px` |
| `WideMax` | `1080px` |
| `Shadow` | `0 16px 40px rgba(32, 33, 36, 0.08)` |
| `FontFamily` | `system-ui, ...` (系统字体栈) |
| `HeadingFontFamily` | 同 `FontFamily` |
| `CodeFontFamily` | `SFMono-Regular, Consolas, monospace` |

---

## 七、测试策略

### CloneCommandTests.cs

| 测试用例 | 输入 | 期望 |
|---------|------|------|
| `Clone_ThemesWithMinimalTokens_GeneratesAllFiles` | 最小 tokens（仅 primary=#ff0000） | 17 个文件全部生成，CSS 含 `--primary: #ff0000;` |
| `Clone_WithFullTokens_AllCssVariablesSet` | 完整 tokens（全部字段） | CSS `:root` 包含全部变量，值与输入一致 |
| `Clone_WithGoogleFonts_BaseHasLinkTags` | tokens.GoogleFontsUrl 有值 | base.html 包含 Google Fonts `<link>` 标签 |
| `Clone_WithLayoutInfo_IndexHasHeroBlock` | layout.HeroHeading 有值 | index.html 包含 Hero section |
| `Clone_ThemeAlreadyExists_ErrorsWithoutForce` | 重复主题名，无 --force | 返回 2，不覆盖 |
| `Clone_ThemeAlreadyExists_OverwritesWithForce` | 重复主题名，有 --force | 返回 0，覆盖 |
| `Clone_MissingTokensArg_Errors` | 无 --tokens | 返回 2 |
| `Clone_InvalidJson_Errors` | tokens.json 格式错误 | 抛出异常（JSON 解析失败） |

---

## 八、实现步骤

### Step 1：创建数据模型
- 文件：`src/Bukit.Cli/Commands/CloneModels.cs`
- 内容：`CloneTokens`、`CloneLayoutInfo`、`SectionInfo` record 类型
- 包含 `FromJson(string)` 静态工厂方法（使用 `System.Text.Json`）
- 包含 `IsSafeThemeName(string)` 工具方法

### Step 2：创建主题生成器
- 文件：`src/Bukit.Cli/Commands/CloneThemeGenerator.cs`
- 内容：
  - `WriteTo(rootDir, themeName, tokens, layout, brand)` 主入口
  - `GenerateStyleCss(CloneTokens)` — 填充 CSS 变量
  - `GenerateBaseLayout(CloneTokens)` — 含字体引用
  - `GenerateIndex(CloneLayoutInfo, string?)` — 首页模板
  - `GenerateHeader(string?)` — 导航栏 partial
  - 标准 partial 常量（footer/list-card/pagination-nav — 直接复用 `StarterThemeScaffold` 内部常量）
  - 标准页面模板常量（page/post/list — 直接复用）

### Step 3：创建 CLI 命令
- 文件：`src/Bukit.Cli/Commands/CloneCommand.cs`
- 内容：`RunAsync(ArgReader)` 入口 + 参数解析 + 调用生成器

### Step 4：注册 CLI 命令
- 修改 `src/Bukit.Cli/Program.cs`：添加 `"clone"` 路由
- 修改 `src/Bukit.Cli/Cli/BukitCliSpecs.cs`：添加 `clone` 命令规格

### Step 5：创建 AI Skill
- 文件：`src/skills/bukit-clone/SKILL.md`
- 内容：Skill YAML frontmatter + Phase 1/2/3 指导 + 内嵌 JS 提取脚本

### Step 6：更新 using-bukit Skill
- 修改 `src/skills/using-bukit/SKILL.md`：
  - Skill 概览表新增 bukit-clone
  - 路由规则新增 clone 场景
  - Quick Reference 新增 clone 命令

### Step 7：编写测试
- 文件：`tests/Bukit.Cli.Tests/CloneCommandTests.cs`
- 内容：8 个测试用例（见上表）

### Step 8：验证
```bash
# 1. 编译
dotnet build

# 2. 运行测试
dotnet test tests/Bukit.Cli.Tests/

# 3. 手动验证：创建示例 tokens.json 并运行 clone
echo '{"colors":{"primary":"#3b82f6","accent":"#10b981"}}' > /tmp/test-tokens.json
cd examples/starter
dotnet run -- clone --tokens /tmp/test-tokens.json --theme test-clone
# 检查 themes/test-clone/ 目录
# 检查 CSS 文件中的变量值
```

---

## 九、不做的部分

| 内容 | 原因 |
|------|------|
| 浏览器端令牌提取 | 留给 AI Agent Skill（通过 Chrome MCP）处理 |
| Bukit.Engine 修改 | 克隆是开发期操作，不需要引擎感知 |
| 协议插件实现 | 生命周期冲突，前文已论证不适合 |
| 多 URL 并行克隆 | 后续版本迭代，Phase 1 仅单 URL |
| tokens.json Schema 生成 | 手动维护，后续可用 JSON Schema 自动化 |
| 布局自动推断（CLI 端） | 语义识别由 AI Agent 处理，CLI 不做 DOM 分析 |

---

## 十、风险与缓解

| 风险 | 缓解 |
|------|------|
| `CloneTokens.FromJson` 解析失败 | 使用 `System.Text.Json` + `JsonSerializerOptions.PropertyNameCaseInsensitive`；无效 token 字段用存根值并输出 warning |
| `StarterThemeScaffold` 内部常量不可访问 | 将需要共享的模板常量改为 `internal`（如果当前是 `private`）；或直接在 `CloneThemeGenerator` 中复制这些常量 |
| `ConfigPathResolver.Resolve(reader)` 无 `--config` 时工作目录不存在 | 当 `rootDir` 下无 `themes/` 目录时自动创建 |
| 生成的主题与 starter 差异不大 | 这是正确行为——只有 tokens 不同的 CSS 变量值变化，模板结构一致 |
| JSON tokens 与 YAML-centric Bukit 风格不一致 | 接受此差异：AI Agent 在浏览器中执行 JS 提取后写 JSON 是最自然的路径 |
