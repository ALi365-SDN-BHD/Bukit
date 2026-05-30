# 审计报告修复方案

> 基于: `bukit-audit-report-2026-05-30-chatgpt-04.md`
> 共 5 个问题: P0×1, P1×2, P2×2

***

## 问题总览

| 编号   | 优先级 | 标题                                              | 当前状态                       | 涉及文件数                               |
| ---- | --- | ----------------------------------------------- | -------------------------- | ----------------------------------- |
| P0-1 | P0  | SafeUrl protocol-relative URL 放行 (`//evil.com`) | `SafeUrl.cs` 缺 `//` 前缀拦截   | 2 (src+test)                        |
| P1-1 | P1  | AudioBlockRenderer 未接入 SafeUrl                  | 8 个 renderer 中唯一缺失 SafeUrl | 2 (src+test)                        |
| P1-2 | P1  | BuildCommandTests 4 处 Task.WhenAny 假绿           | 4 测试不 await buildTask      | 1 (test)                            |
| P2-1 | P2  | externalPluginPolicy 非法值静默变 Warn                | ConfigLoader 无验证           | 3 (loader+validator+DiagnosticCode) |
| P2-2 | P2  | ConfigPathResolver 抛 InvalidOperationException  | 与代码库其他路径校验不一致              | 1 (src)                             |

***

## P0-1: SafeUrl protocol-relative URL 放行

### 问题分析

**当前代码** (`src/Bukit.Shared/SafeUrl.cs`):

```csharp
public static string? ForLink(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;
    var trimmed = url.Trim();
    if (trimmed.StartsWith('/')) return trimmed;    // ❌ //evil.com 从这里放行
    if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)) return null;
    return LinkSchemes.Contains(uri.Scheme) ? trimmed : null;
}
```

`ForLink`、`ForMedia`、`ForEmbed` 三个方法都有相同问题。

**根本原因**: `//evil.com` 以 `/` 开头，在 `StartsWith('/')` 检查时被当作"站点内部路径"直接放行。

**一致性观察**: `RouteSecurityValidator.ValidateInternalUrl` 已经明确拒绝 `//` 开头的 protocol-relative URL，使用 `DiagnosticCode.RouteInvalidInternalUrl`。

### 修复方案

#### Step 1: 修改 `SafeUrl.cs` 三个核心方法

在 `ForLink`、`ForMedia`、`ForEmbed` 中，将 `//` 检查放在 `/` 检查之前：

```csharp
if (trimmed.StartsWith("//", StringComparison.Ordinal))
    return null;

if (trimmed.StartsWith('/'))
    return trimmed;
```

同时更新 `IsExternal`，让 `//` 开头的也视为外部链接：

```csharp
public static bool IsExternal(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return false;
    var trimmed = url.Trim();
    if (trimmed.StartsWith("//", StringComparison.Ordinal)) return true;
    return !trimmed.StartsWith('/');
}
```

#### Step 2: 新增测试用例

在 `tests/Bukit.Shared.Tests/SafeUrlTests.cs` 新增：

```csharp
[Theory]
[InlineData("//evil.com")]
[InlineData("//evil.com/x.js")]
public void ForLink_ProtocolRelativeUrl_ReturnsNull(string url)
{
    Assert.Null(SafeUrl.ForLink(url));
}

[Theory]
[InlineData("//evil.com/a.png")]
[InlineData("//cdn.evil.com/audio.mp3")]
public void ForMedia_ProtocolRelativeUrl_ReturnsNull(string url)
{
    Assert.Null(SafeUrl.ForMedia(url));
}

[Theory]
[InlineData("//evil.com/embed")]
[InlineData("//attacker.com/widget")]
public void ForEmbed_ProtocolRelativeUrl_ReturnsNull(string url)
{
    Assert.Null(SafeUrl.ForEmbed(url));
}

[Fact]
public void IsExternal_ProtocolRelativeUrl_ReturnsTrue()
{
    Assert.True(SafeUrl.IsExternal("//evil.com"));
}
```

#### 涉及文件

* `src/Bukit.Shared/SafeUrl.cs` — 修改 ForLink/ForMedia/ForEmbed/IsExternal 四个方法

* `tests/Bukit.Shared.Tests/SafeUrlTests.cs` — 新增 protocol-relative 测试

***

## P1-1: AudioBlockRenderer 未接入 SafeUrl

### 问题分析

**当前代码** (`src/Bukit.Content/Notion/BlockRenderers/AudioBlockRenderer.cs`):

```csharp
var url = ExtractFileUrl(audio);
if (string.IsNullOrWhiteSpace(url))
{
    return Task.FromResult<string?>(null);
}

var encodedUrl = WebUtility.HtmlEncode(url);  // ❌ 没有 SafeUrl 校验

sb.Append($"<audio controls src=\"{encodedUrl}\"></audio>");
sb.Append($"<p><a href=\"{encodedUrl}\">Audio</a></p>");
```

**对比其他 7 个 renderer**: Video、File、Embed、Image、Pdf 等全部使用 SafeUrl，唯独 Audio 缺失。

### 修复方案

#### Step 1: 修改 AudioBlockRenderer

```csharp
var url = ExtractFileUrl(audio);
var safeUrl = SafeUrl.ForMedia(url);
if (string.IsNullOrWhiteSpace(safeUrl))
{
    return Task.FromResult<string?>(null);
}

var encodedUrl = WebUtility.HtmlEncode(safeUrl);
var captionText = audio.TryGetProperty("caption", out var cap) ? NotionRichTextRenderer.Render(cap) : null;

var sb = new StringBuilder();
sb.Append($"<audio controls src=\"{encodedUrl}\"></audio>");

var isExternal = SafeUrl.IsExternal(safeUrl);
var rel = isExternal ? " rel=\"noopener noreferrer\"" : "";
sb.Append($"<p><a href=\"{encodedUrl}\"{rel}>Audio</a></p>");

if (!string.IsNullOrWhiteSpace(captionText))
{
    sb.Append($"<p>{captionText}</p>");
}

return Task.FromResult<string?>(sb.ToString());
```

#### Step 2: 新增安全测试用例

在 `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs` 新增：

```csharp
[Theory]
[InlineData("javascript:alert(1)")]
[InlineData("data:text/html,<script>alert(1)</script>")]
[InlineData("//evil.com/audio.mp3")]
public async Task AudioBlockRenderer_DangerousUrl_ReturnsNull(string fileUrl)
{
    // 构造 Notion audio block JSON，external.url = fileUrl
    var json = $@"{{""audio"":{{""type"":""external"",""external"":{{""url"":""{fileUrl}""}}}}}}";
    using var doc = JsonDocument.Parse(json);
    var context = new NotionRenderContext(/* ... */);
    var renderer = new AudioBlockRenderer();
    var result = await renderer.RenderAsync(doc.RootElement, context, CancellationToken.None);
    Assert.Null(result);
}
```

#### 涉及文件

* `src/Bukit.Content/Notion/BlockRenderers/AudioBlockRenderer.cs` — 修改 RenderAsync 方法

* `tests/Bukit.Content.Tests/NotionBlockRendererEdgeCasesTests.cs` — 新增安全测试

***

## P1-2: BuildCommandTests 4 处 Task.WhenAny 假绿

### 问题分析

**BuildCommandTests.cs 中 4 个测试**没有 await buildTask：

| 测试方法                                                  | 行号   | 特殊风险                      |
| ----------------------------------------------------- | ---- | ------------------------- |
| `RunAsync_WithConfigOption_ResolvesAndStartsBuild`    | L41  | 普通缺失 await                |
| `RunAsync_WithSiteOption_ResolvesAndStartsBuild`      | L84  | Task.WhenAny 后删除临时目录 → 竞态 |
| `RunAsync_JobsFour_RunsSuccessfully`                  | L242 | 名为"成功"但未断言                |
| `RunAsync_CIFlagWithoutExternalPlugins_BuildSucceeds` | L434 | 名为"成功"但未断言                |

**当前模式**（错误）:

```csharp
await Task.WhenAny(BuildCommand.RunAsync(command), Task.Delay(Timeout.Infinite, cts.Token));
```

**正确模式**（已在 `RunAsync_CIEnvWithAllowExternalPlugins_BuildSucceeds` 中实现）:

```csharp
var buildTask = BuildCommand.RunAsync(command);
var completed = await Task.WhenAny(buildTask, Task.Delay(Timeout.Infinite, cts.Token));
if (completed == buildTask)
{
    try { await buildTask; }
    catch (Exception ex) { Assert.DoesNotContain("...", ex.Message); }
}
```

### 修复方案

#### Step 1: 逐个修改 4 个测试

对每个测试：

1. 保存 `buildTask` 变量
2. `Task.WhenAny` 后用 `Assert.Same(buildTask, completed)` 确认构建完成
3. `await buildTask` 获取结果或异常
4. 根据测试语义返回适当的断言

##### 1. RunAsync\_WithConfigOption\_ResolvesAndStartsBuild (L26-L42)

**当前**: 验证 --config 选项后 CWD 切换，Task.WhenAny 后直接删目录

**修改**:

* 保存 buildTask，assert 它完成（不超时）

* 如果 buildTask 抛异常，区分 `ConfigException`（配置问题，可接受）和其他异常

* 然后在 finally 块中清理临时目录

##### 2. RunAsync\_WithSiteOption\_ResolvesAndStartsBuild (L68-L86)

**当前**: 验证 --site 选项后路径解析，Task.WhenAny 后删目录（竞态风险）

**修改**:

* 保存 buildTask，assert 它完成

* 然后清理临时目录

##### 3. RunAsync\_JobsFour\_RunsSuccessfully (L228-L243)

**当前**: --jobs 4 选项，Task.WhenAny，不做任何断言

**修改**:

* 保存 buildTask，assert 完成

* 区分异常类型（`ConfigException` 可能因为测试环境缺少配置而出现）

##### 4. RunAsync\_CIFlagWithoutExternalPlugins\_BuildSucceeds (L418-L435)

**当前**: CI 环境标志不加载外部插件，Task.WhenAny

**修改**:

* 保存 buildTask，assert 完成

* 如果抛异常，确认不包含 "External plugins are disabled in CI"（这与 RunAsync\_CIEnvWithAllowExternalPlugins 的断言逻辑一致）

#### 涉及文件

* `tests/Bukit.Cli.Tests/BuildCommandTests.cs` — 修改 4 个测试方法

***

## P2-1: externalPluginPolicy 非法值静默变 Warn

### 问题分析

**当前代码** (`src/Bukit.Config/ConfigLoader.cs` L177-L192):

```csharp
return policy.Trim().ToLowerInvariant() switch
{
    "deny" => ExternalPluginPolicy.Deny,
    "warn" => ExternalPluginPolicy.Warn,
    "allow" => ExternalPluginPolicy.Allow,
    _ => ExternalPluginPolicy.Warn  // ❌ 拼写错误如 "alow" 被静默当作 Warn
};
```

**缺陷链**:

1. `ConfigLoader.ReadExternalPluginPolicy` 不报错
2. `ConfigValidator.Validate()` 不验证此字段
3. `ExternalPluginsValidator.ValidateExternalPlugins()` 也不验证此字段
4. 没有 `DiagnosticCode` 与此字段关联
5. 没有任何单元测试覆盖无效值场景

### 修复方案

#### Step 1: 修改 ConfigLoader.ReadExternalPluginPolicy

将 default 分支从静默返回 Warn 改为抛 `ConfigException`:

```csharp
private static ExternalPluginPolicy ReadExternalPluginPolicy(YamlMappingNode siteNode)
{
    var policy = ConfigYamlHelpers.GetOptionalString(siteNode, "externalPluginPolicy");
    if (string.IsNullOrWhiteSpace(policy))
    {
        return ExternalPluginPolicy.Warn;
    }

    return policy.Trim().ToLowerInvariant() switch
    {
        "deny" => ExternalPluginPolicy.Deny,
        "warn" => ExternalPluginPolicy.Warn,
        "allow" => ExternalPluginPolicy.Allow,
        _ => throw new ConfigException(
            $"site.externalPluginPolicy must be 'deny', 'warn', or 'allow'. Got: '{policy.Trim()}'.",
            DiagnosticCode.ConfigInvalidValue)
    };
}
```

#### Step 2: 新增 DiagnosticCode（可选，已有 `ConfigInvalidValue`）

检查现有 `DiagnosticCode.ConfigInvalidValue` (BKT-0002) 的描述："Config: a field has an invalid value"，语义完全匹配，**不需要新增专门的 DiagnosticCode**。

#### Step 3: 新增 ConfigValidator 校验（可选）

在 `I18nValidator.ValidateSite` 中加入显式校验：

```csharp
// 在 I18nValidator.cs 中加入
if (config.Site.ExternalPluginPolicy != ExternalPluginPolicy.Deny &&
    config.Site.ExternalPluginPolicy != ExternalPluginPolicy.Warn &&
    config.Site.ExternalPluginPolicy != ExternalPluginPolicy.Allow)
{
    Errors.Add(new ConfigError(
        "site.externalPluginPolicy",
        $"Must be 'deny', 'warn', or 'allow'.",
        DiagnosticCode.ConfigInvalidValue));
}
```

**注意**: 如果 ConfigLoader 已经抛异常，则永远不会走到 Validator。但如果 ConfigLoader 的读取通过 `Enum.TryParse` 等其他路径进来，Validator 可以充当第二层防护。评估后决定是否需要。

#### Step 4: 新增测试

在 `tests/Bukit.Engine.Tests/ConfigValidatorTests.cs` 新增：

```csharp
[Theory]
[InlineData("alow")]
[InlineData("denyy")]
[InlineData("denyallow")]
[InlineData("")]
public void ExternalPluginPolicy_InvalidValue_ThrowsConfigException(string invalidPolicy)
{
    var yaml = $@"
name: test
url: https://example.com
externalPluginPolicy: {invalidPolicy}
";
    var ex = Assert.Throws<ConfigException>(() => ConfigLoader.LoadFromString(yaml));
    Assert.Contains("externalPluginPolicy", ex.Message);
}
```

#### 涉及文件

* `src/Bukit.Config/ConfigLoader.cs` — 修改 ReadExternalPluginPolicy 的 default 分支

* `tests/Bukit.Engine.Tests/ConfigValidatorTests.cs` — 新增无效值测试

***

## P2-2: ConfigPathResolver 抛 InvalidOperationException

### 问题分析

**当前代码** (`src/Bukit.Cli/ConfigPathResolver.cs` L22-L29):

```csharp
if (!fullConfigPath.StartsWith(safeRoot, Bukit.Shared.PlatformPathHelper.PathComparison))
{
    throw new InvalidOperationException(  // ❌ 不是 ConfigException
        $"--site value '{site}' resolves to a path outside the sites directory.");
}
```

**问题**:

1. 抛出 `InvalidOperationException` 而非 `ConfigException`
2. 在 CLI 入口 `Program.cs` 中，`InvalidOperationException` 被兜底 `catch (Exception ex)` 捕获 → exit code 1
3. 而其他路径穿越场景（`BuildPathUtils`、`ThemePathResolver`）都使用 `ConfigException` + `DiagnosticCode.ConfigPathTraversal` → exit code 2
4. 没有 DiagnosticCode，用户无法通过错误码快速定位问题

### 修复方案

#### Step 1: 修改 ConfigPathResolver

```csharp
if (!fullConfigPath.StartsWith(safeRoot, Bukit.Shared.PlatformPathHelper.PathComparison))
{
    throw new ConfigException(
        $"--site value '{site}' resolves to a path outside the sites directory.",
        DiagnosticCode.ConfigPathTraversal);
}
```

确保 `using Bukit.Shared;` 已存在（`ConfigException` 和 `DiagnosticCode` 都在 `Bukit.Shared` 命名空间下）。

#### Step 2: 验证 CLI 入口行为

在 `Program.cs` 中确认 `ConfigException` 的 catch 块 (`exit code 2`) 会正确处理这个场景：

```csharp
catch (ConfigException ex)
{
    // exit code 2 - 已存在
}
```

#### Step 3: 检查是否有测试需要更新

搜索是否有测试捕获 `InvalidOperationException` 来验证此行为：

```csharp
// 如果有此类测试:
Assert.Throws<InvalidOperationException>(() => ConfigPathResolver.Resolve(...));
// 需改为:
Assert.Throws<ConfigException>(() => ConfigPathResolver.Resolve(...));
```

#### 涉及文件

* `src/Bukit.Cli/ConfigPathResolver.cs` — 异常类型从 `InvalidOperationException` → `ConfigException`，添加 `DiagnosticCode.ConfigPathTraversal`

* 任何引用此异常的测试文件（如果有）

***

## 执行顺序建议

| 顺序 | 问题                              | 理由                |
| -- | ------------------------------- | ----------------- |
| 1  | P0-1 SafeUrl `//` 修复            | P0 安全漏洞，最优先       |
| 2  | P1-1 AudioBlockRenderer SafeUrl | 依赖 SafeUrl 修复后的行为 |
| 3  | P1-2 Task.WhenAny 测试修复          | 独立修复              |
| 4  | P2-1 externalPluginPolicy       | 独立修复              |
| 5  | P2-2 ConfigPathResolver         | 独立修复              |

## 验证命令

每修复一个问题后运行:

```bash
# 单元测试
dotnet test tests/Bukit.Shared.Tests/       # P0-1
dotnet test tests/Bukit.Content.Tests/      # P1-1
dotnet test tests/Bukit.Cli.Tests/          # P1-2, P2-2
dotnet test tests/Bukit.Engine.Tests/       # P2-1

# 全量测试
dotnet test
```

