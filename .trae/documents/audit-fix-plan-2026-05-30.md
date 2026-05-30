# 审计报告修复方案

基于 `/Users/ali/mydev/Git/Github/Bukit/.trae/documents/bukit-audit-report-2026-05-30-chatgpt-03.md` 的深度分析，
以下为分阶段修复方案。

***

## 代码库现状分析

### 关键发现

1. **SafeUrl 工具不存在** — 目前全量 block renderer 只用 `WebUtility.HtmlEncode()` 做 HTML attribute escaping，没有任何 URL scheme allowlist 机制。`UrlRedactor`（仅用于日志脱敏）是最近的工具。
2. **ConfigException / ContentException / RenderException 均存在** — 继承自 `BukitException`（带 `DiagnosticCode? Code`），但 `RouteSecurityValidator.Fail()` 仍抛 `InvalidOperationException`，不走类型化异常体系。
3. **DiagnosticCode 枚举已有 36 个码值** — 覆盖 10 个子系统（0x00\~0x09），路由安全缺少对应码值。
4. **CLI 错误处理不分类** — `Program.cs` 中 `catch (Exception)` 统一返回 1，未区分 Config/Content/Render/Plugin 异常。
5. **版本漂移确认** — `Directory.Build.props` 版本 `1.0.7`，`Bukit.Cli.csproj` fallback `1.0.6`。
6. **jobs 无上限** — `VariantBuildPipeline` 和 `PageRenderDispatcher` 无 `Math.Clamp` 保护。
7. **FollowSymlinks 默认禁用** — 安全，但若开启则无 realpath 检查和警告。
8. **CLI stderr 版本横幅** — `Program.cs:L19`，除 `version` 命令外每次命令都打印到 stderr。
9. **NotionColorToCss** — 未知颜色 fallback 为原始 `notionColor` 字符串（L220），存在 CSS injection 风险。
10. **list item color class 编码不一致** — `NotionBlocksRenderer.RenderListItemAsync` 直接用 `$"class=\"notion-{color}\""` 无 HtmlEncode，而 `GetBlockColorClass` 有。

### 测试现状

* **RouteSecurityValidatorTests** — 用 `Assert.Throws<InvalidOperationException>`（需要改为 `ConfigException`）。

* **NotionRichTextRendererExtendedTests** — 226 行，覆盖 equation/mention/link/annotation/color。无需改写，只需补充恶意 URL 测试。

* **Block renderer 测试** — 尚不存在（需要使用 SearchCodebase 查证后决定是否新建测试文件）。

* **CLI 测试** — 需查证现有 CLI 测试文件。

***

## 阶段一：P0 — 安全修复（正式版前必须完成）

### 任务 1：SafeUrl 工具 + 全局应用

**范围**：

* 新建 `Bukit.Shared/SafeUrl.cs`

* `NotionRichTextRenderer.cs` — 链接和 mention href

* 8 个 Block Renderer：Image / Video / Embed / Bookmark / File / Pdf / LinkPreview / NotionBlockHelpers

* 新建 `tests/Bukit.Shared.Tests/SafeUrlTests.cs`（如 Shared 目录无测试项目则放入 Content 测试项目）

* `tests/Bukit.Content.Tests/NotionRichTextRendererExtendedTests.cs` — 补充恶意 URL 测试

**设计**：

```csharp
namespace Bukit.Shared;

internal static class SafeUrl
{
    private static readonly HashSet<string> LinkSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel"
    };

    private static readonly HashSet<string> MediaSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    public static string? ForLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith('/')) return trimmed; // 内部路径放行
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return LinkSchemes.Contains(uri.Scheme) ? trimmed : null;
    }

    public static string? ForMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.StartsWith('/')) return trimmed; // 内部路径放行
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        return MediaSchemes.Contains(uri.Scheme) ? trimmed : null;
    }

    public static string? ForEmbed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
        // embed 只允许 https
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase)) return null;
        return trimmed;
    }

    public static bool IsExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        return !trimmed.StartsWith('/');
    }
}
```

**应用策略**：

| 渲染器                                 | 场景                     | 方法                   | 额外处理                              |
| ----------------------------------- | ---------------------- | -------------------- | --------------------------------- |
| `NotionRichTextRenderer`            | `<a href>` (text link) | `SafeUrl.ForLink()`  | 外部链接加 `rel="noopener noreferrer"` |
| `NotionRichTextRenderer`            | `<a href>` (mention)   | `SafeUrl.ForLink()`  | 外部链接加 `rel="noopener noreferrer"` |
| `ImageBlockRenderer`                | `<img src>`            | `SafeUrl.ForMedia()` | —                                 |
| `VideoBlockRenderer`                | `<video src>`          | `SafeUrl.ForMedia()` | —                                 |
| `VideoBlockRenderer`                | fallback `<a>`         | `SafeUrl.ForLink()`  | rel                               |
| `EmbedBlockRenderer`                | `<iframe src>`         | `SafeUrl.ForEmbed()` | https only                        |
| `BookmarkBlockRenderer`             | `<a href>`             | `SafeUrl.ForLink()`  | rel                               |
| `FileBlockRenderer`                 | `<a href>`             | `SafeUrl.ForLink()`  | rel                               |
| `PdfBlockRenderer`                  | `<iframe src>`         | `SafeUrl.ForMedia()` | —                                 |
| `LinkPreviewBlockRenderer`          | `<a href>`             | `SafeUrl.ForLink()`  | rel                               |
| `NotionBlockHelpers.ExtractFileUrl` | raw URL                | 调用方各自做校验             | `ExtractFileUrl` 不做校验，由调用方校验      |

**测试矩阵**（SafeUrlTests）：

| URL                           | 方法                        | 期望            |
| ----------------------------- | ------------------------- | ------------- |
| `https://example.com`         | ForLink                   | 通过            |
| `/assets/a.png`               | ForLink/ForMedia          | 通过            |
| `javascript:alert(1)`         | ForLink/ForMedia          | null          |
| `data:text/html,...`          | ForLink/ForMedia/ForEmbed | null          |
| `file:///etc/passwd`          | ForLink/ForMedia/ForEmbed | null          |
| `vbscript:msgbox(1)`          | ForLink/ForMedia          | null          |
| `http://img.com/1.png`        | ForMedia                  | 通过            |
| `https://youtube.com/embed/x` | ForEmbed                  | 通过            |
| `http://youtube.com/embed/x`  | ForEmbed                  | null（非 https） |
| `mailto:user@example.com`     | ForLink                   | 通过            |
| `tel:+123456`                 | ForLink                   | 通过            |

***

### 任务 2：外部插件安全策略

**范围**：

* `AppConfig.cs` — 新增 `ExternalPluginPolicy` 枚举和 `ExternalPluginPolicy` 配置项

* `ExternalPluginsValidator.cs` — 增加 sha256 校验

* `ExternalPluginManifestRecord` — 增加 sha256 字段

* `DoctorCommand.cs`（或 doctor 管线）— 增加外部插件风险报告

* `PluginRegistry.cs` — 构建时检查 policy 决定是否加载外部插件

* `ConfigOverrides.cs` — `AllowExternalPlugins` 与 policy 联动

**设计**：

```csharp
// AppConfig.cs
public enum ExternalPluginPolicy
{
    Deny,   // 禁止加载外部插件
    Warn,   // 加载但输出醒目警告（本地默认）
    Allow   // 无警告加载
}

// AppConfig.cs SiteConfig 新增字段
public ExternalPluginPolicy ExternalPluginPolicy { get; init; } = ExternalPluginPolicy.Warn;
```

**插件配置增加 sha256**：

```yaml
externalPlugins:
  my-plugin:
    runtime: process
    entry: tools/my-plugin
    sha256: "abc123..."
```

sha256 校验逻辑：

* 若提供 sha256，启动前校验文件哈希，不匹配则拒绝加载（抛 `ConfigException`）

* 若未提供 sha256，根据 `externalPluginPolicy` 决定：

  * `Deny` → 拒绝加载

  * `Warn` → 加载但输出安全警告

  * `Allow` → 正常加载

**CLI 联动**：

* `--allow-external-plugins` 现有选项保留

* CI 模式（`--ci`）下自动设置 `ExternalPluginPolicy = Deny`

**Doctor 检查**：

* 若 `site.externalPlugins` 不为空且 `externalPluginPolicy != Deny`，输出警告：

  > External plugins execute local processes and should only be enabled for trusted projects.
  > Set site.externalPluginPolicy: deny to disable external plugins.

**CI 默认 Deny 的实现**：

* `Program.cs` 或 `BuildCommand` 中，当检测到 CI 环境变量（如 `CI=true`）时，自动将 `ExternalPluginPolicy` 降级为 `Deny`

***

## 阶段二：P1 — 必须修复（正式版前）

### 任务 3：RouteSecurityValidator 使用 ConfigException

**范围**：

* `RouteSecurityValidator.cs` — `Fail()` 方法改为抛 `ConfigException`

* `DiagnosticCode.cs` — 新增路由安全相关诊断码

* `tests/Bukit.Engine.Tests/RouteSecurityValidatorTests.cs` — 改为 `Assert.Throws<ConfigException>`

**新增 DiagnosticCode**：

```csharp
// Route 子系统（0x02）
RouteInvalidInternalUrl    = 0x0205,
RouteUnsafeOutputPath      = 0x0206,
RouteReservedWindowsPath   = 0x0207,
RouteEncodedSlashInPath    = 0x0208,
```

**RouteSecurityValidator.Fail 改动**：

```csharp
private static void Fail(string reason, string? value, string? source, DiagnosticCode code)
{
    var sourceText = string.IsNullOrWhiteSpace(source) ? "route" : source;
    throw new ConfigException(
        $"Invalid {sourceText}: {reason}. Value: '{value ?? string.Empty}'",
        code);
}
```

其中各 Fail 调用点传入合适的 `DiagnosticCode`：

| 校验位置                                                    | DiagnosticCode             |
| ------------------------------------------------------- | -------------------------- |
| `ValidateInternalUrl` — URL 为空 / 控制字符 / 协议相对 / 外部绝对 URL | `RouteInvalidInternalUrl`  |
| `ValidateUrlPathSegments` — `.` / `..` / 百分号编码遍历        | `RouteInvalidInternalUrl`  |
| `ValidateUrlPathSegments` — 编码斜杠                        | `RouteEncodedSlashInPath`  |
| `ValidateOutputPath` — 空路径 / 控制字符 / 绝对路径 / 驱动器限定 / 遍历   | `RouteUnsafeOutputPath`    |
| `ValidateOutputPath` — 保留 Windows 名                     | `RouteReservedWindowsPath` |

***

### 任务 4：版本一致性

**范围**：

* `Bukit.Cli.csproj` — 移除 stale fallback `1.0.6`

* 新建 `tests/Bukit.Cli.Tests/VersionConsistencyTests.cs`（或追加到现有 CLI 测试）

**改动**：

`Bukit.Cli.csproj` L19-21 当前：

```xml
<BuildInfoVersionBase Condition="'$(VersionPrefix)' != ''">$(VersionPrefix)</BuildInfoVersionBase>
<BuildInfoVersionBase Condition="'$(Version)' != ''">$(Version)</BuildInfoVersionBase>
<BuildInfoVersionBase Condition="'$(BuildInfoVersionBase)' == ''">1.0.6</BuildInfoVersionBase>
```

改为：

```xml
<BuildInfoVersionBase Condition="'$(VersionPrefix)' != ''">$(VersionPrefix)</BuildInfoVersionBase>
<BuildInfoVersionBase Condition="'$(Version)' != ''">$(Version)</BuildInfoVersionBase>
<BuildInfoVersionBase Condition="'$(BuildInfoVersionBase)' == ''">1.0.7</BuildInfoVersionBase>
```

**测试**：验证 `bukit version` 输出包含 `1.0.7`。

***

### 任务 5：渲染并发上限

**范围**：

* `VariantBuildPipeline.cs` — `BuildSeoStageAsync` 中 `maxDegreeOfParallelism`

* `PageRenderDispatcher.cs` — `DispatchAsync` / `RenderPagesAsync` / `RenderSpecialListsAsync`

**设计**：

```csharp
private static int ClampMaxParallelism(int? requestedJobs)
{
    var cpu = Environment.ProcessorCount;
    var max = requestedJobs ?? cpu;
    max = Math.Clamp(max, 1, Math.Max(1, cpu * 2));
    // 环境变量解除上限
    if (Environment.GetEnvironmentVariable("BUKIT_MAX_JOBS") is { Length: > 0 } env &&
        int.TryParse(env, out var envMax) && envMax > 0)
    {
        max = Math.Clamp(envMax, 1, cpu * 4); // 硬上限为 4x CPU
    }
    return max;
}
```

在所有位置替换：

```csharp
var maxDegreeOfParallelism = overrides.Jobs ?? Environment.ProcessorCount;
// 改为：
var maxDegreeOfParallelism = ClampMaxParallelism(overrides.Jobs);
```

`PageRenderDispatcher` 同理。

***

### 任务 6：Symlink Follow 安全强化

**范围**：

* `DirectoryCopy.cs` — 当 `FollowSymlinks=true` 时增加 realpath 检查

* `BuildConfig` / `ConfigLoader` — 开启时输出警告

**设计**：

`DirectoryCopy.Sync()` 中 `FollowSymlinks == true` 的分支增加 realpath 检查：

```csharp
if (options.FollowSymlinks && IsSymlink(sourcePath))
{
    var realPath = Path.GetFullPath(sourcePath).ResolveLinkTarget(true)?.FullName 
                   ?? new FileInfo(sourcePath).LinkTarget;
    // 检查 realpath 是否在 sourceRoot 内
    // 检查 realpath 是否指向项目外的敏感路径
}
```

**警告机制**：

* `ConfigLoader` 加载时，若 `FollowSymlinks = true`，通过 logger 输出警告。

* Doctor 检查中增加 symlink 安全检查项。

* CI 模式下默认禁止 `FollowSymlinks`（除非显式 `--allow-symlinks`）。

***

## 阶段三：P2 — 质量增强（建议修复）

### 任务 7：CLI 错误分层

**范围**：

* `Program.cs` — 增加分类 catch 块

**设计**：

```csharp
catch (CommandArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (ConfigException ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.Code.HasValue)
        Console.Error.WriteLine($"  DiagnosticCode: {ex.Code.Value}");
    return 2;
}
catch (ContentException ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.Code.HasValue)
        Console.Error.WriteLine($"  DiagnosticCode: {ex.Code.Value}");
    return 2;
}
catch (RenderException ex)
{
    Console.Error.WriteLine(ex.Message);
    if (ex.Code.HasValue)
        Console.Error.WriteLine($"  DiagnosticCode: {ex.Code.Value}");
    return 3;
}
catch (Exception ex)
{
    // ...
    return 1;
}
```

注意：当前无 `PluginException` 类型（仅有 `PluginExecutionFailed` DiagnosticCode）。可暂不新增专用异常类型，因为 Plugin 异常可能以 `ConfigException` 或普通 `Exception` 形式抛出。

***

### 任务 8：CLI stderr 版本横幅调整

**范围**：

* `Program.cs` — 默认不输出版本信息到 stderr

**设计**：

```csharp
// 原来：除 version 命令外都输出到 stderr
// 改为：只在 --verbose 时输出到 stderr，或者完全移除
```

方案 A（推荐）：完全移除 stderr 版本输出，让 `version` 命令独立处理。
方案 B：改为 `--verbose` 才输出到 stderr。

建议采用方案 A — 因为自动化脚本不应受版本横幅干扰，且 `bukit version` 已提供独立的版本查询入口。

***

### 任务 9：NotionColorToCss 安全回退

**范围**：

* `NotionRichTextRenderer.cs` — `NotionColorToCss` 方法

**改动**（L220）：

```csharp
// 原来
return string.Equals(result, "inherit", StringComparison.Ordinal) ? notionColor : result;
// 改为
return string.Equals(result, "inherit", StringComparison.Ordinal) ? "inherit" : result;
```

未知颜色直接返回 `"inherit"`，不把原始值注入 inline style。

***

### 任务 10：List Item Color Class 编码统一

**范围**：

* `NotionBlocksRenderer.cs` — `RenderListItemAsync` 方法

**改动**（L137-144）：

```csharp
// 原来
colorClass = $" class=\"notion-{color}\"";

// 改为：复用 GetBlockColorClass helper
colorClass = NotionBlockHelpers.GetBlockColorClass(container);
```

注意：`GetBlockColorClass` 接受 `JsonElement typeContainer`，而当前 `RenderListItemAsync` 中颜色读取自外层 `container` 的 `"color"` 属性 — 需要确认传入参数是否一致。如果数据结构不同，则至少手动加上 `WebUtility.HtmlEncode(color)`。

***

### 任务 11：outputPathEncoding 默认值建议

**范围**：

* `DoctorCommand` — 增加 warning

* 不改变当前默认值（避免破坏性变更）

**设计**：

在 doctor 检查中增加：

> outputPathEncoding is set to "none". This may create platform-incompatible filenames.
> Consider setting site.outputPathEncoding to "sanitize".

***

## 实施顺序与依赖关系

```
Phase 0: 基础设施（可并行）
├── 任务 1 SafeUrl 工具（纯新增，无依赖）
│   └── 任务 1.5 应用到所有 block renderer（依赖 SafeUrl）
│
Phase 1: P0 修复（优先）
├── 任务 2 外部插件安全策略（独立）
│
Phase 2: P1 修复（可并行）
├── 任务 3 RouteSecurityValidator → ConfigException（独立）
├── 任务 4 版本一致性（独立）
├── 任务 5 渲染并发上限（独立）
├── 任务 6 Symlink 强化（独立）
│
Phase 3: P2 增强
├── 任务 7 CLI 错误分层（独立）
├── 任务 8 CLI 版本横幅（独立）
├── 任务 9 NotionColorToCss（独立）
├── 任务 10 List Item Color 编码（独立）
├── 任务 11 outputPathEncoding doctor 警告（独立）
└── 整体验证（dotnet build + dotnet test）
```

* P0 两个任务必须在正式版前完成。

* P1 五个任务强烈建议在正式版前完成。

* P2 六个任务可在正式版后迭代修复。

## 验证方案

每个阶段完成后执行：

```bash
dotnet build src/Bukit.Cli/Bukit.Cli.csproj -c Release
dotnet test tests/Bukit.Shared.Tests/
dotnet test tests/Bukit.Content.Tests/
dotnet test tests/Bukit.Engine.Tests/
dotnet test tests/Bukit.Routing.Tests/
dotnet test tests/Bukit.Cli.Tests/
```

关键验证点：

1. **SafeUrl** — 所有恶意 URL 被拒绝，合法 URL 正常通过
2. **External Plugin** — CI 下拒绝，本地 warn，allow 正常
3. **RouteSecurityValidator** — 抛出 `ConfigException` 而非 `InvalidOperationException`
4. **Version** — `bukit version` 输出 `1.0.7`
5. **Jobs** — 传入 `--jobs 999` 时被 clamp
6. **CLI 退出码** — Config 错误返回 2，其他错误返回 1
7. **所有现有测试** — 不引入回归（需要更新 `RouteSecurityValidatorTests` 中的异常类型断言）

