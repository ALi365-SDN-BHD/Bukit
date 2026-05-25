# Bukit Core Hardening 修复执行文档

> 目标：将 Bukit 从“可用的静态站点生成器”强化为“可预测、可验证、可恢复、可扩展”的坚实核心引擎。  
> 使用对象：Codex / 本地开发 Agent / 后续 PR 修复任务。  
> 修复原则：优先保证构建正确性、输出安全性、配置可诊断性，再继续扩展功能。

---

## 0. 背景与总体判断

Bukit 当前已经形成较完整的静态站点生成引擎架构：

```text
Config / Overrides
    ↓
BuildPlanner
    ↓
ContentPipeline
    ↓
ThemeBootstrapper
    ↓
RoutePipeline
    ↓
Taxonomy / Derived Pages / Plugins
    ↓
SeoPipeline
    ↓
RenderPipeline
    ↓
AssetPipeline
    ↓
PluginPipeline
    ↓
BuildReport
```

当前核心能力包括：

- Markdown / Notion / Composite 内容源
- Scriban 模板渲染
- 主题继承与组件系统
- collection / permalink / route override
- SEO / RSS / sitemap / GEO / llms.txt
- i18n 多语言输出
- incremental build
- built-in plugin 与 external protocol plugin
- .NET Native AOT 方向

但目前最需要强化的是：

```text
从“能生成网站”
升级为
“每一次构建都可解释、可预测、可验证、可恢复”
```

---

## 1. Codex 执行总原则

### 1.1 严格要求

Codex 修复时必须遵守：

1. 不要破坏现有公开 API，除非文档中明确要求。
2. 不要引入动态程序集加载、运行时反射扫描等不利于 Native AOT 的机制。
3. 不要绕过现有测试；修复必须新增 regression tests。
4. 所有输出文件写入、复制、删除都必须经过 safe output root 校验。
5. 所有配置解析失败必须可诊断，不能静默降级。
6. 增量构建必须以“正确性优先”，性能优化排在后面。
7. 不要一次性重构过大；按 P0 → P1 → P2 分批提交。
8. 每个修复点必须包含单元测试或集成测试。

### 1.2 建议执行命令

每轮修改后执行：

```bash
dotnet restore
dotnet build
dotnet test
```

如果仓库已有 AOT 发布测试或 CLI smoke test，继续执行：

```bash
dotnet publish src/Bukit.Cli/Bukit.Cli.csproj -c Release
```

如当前项目结构不同，请以实际 `.sln` / `.csproj` 为准，但不要跳过测试。

---

## 2. P0 修复任务：增量构建依赖指纹不完整

### 2.1 问题描述

当前页面增量构建跳过判断主要依赖：

```text
TemplateHash
MetadataHash
ContentHash
RouteHash
OutputExists
```

但实际渲染 HTML 还依赖：

- `site.title`
- `site.name`
- `site.description`
- `site.baseUrl`
- `site.url`
- `site.language`
- `site.analytics`
- `site.seo`
- `theme.params`
- `theme.shortcodes`
- `theme.components`
- `theme.componentValidation`
- `build.listPageContentMode`
- `collections`
- `taxonomy`
- plugin toggles
- external plugin 配置
- `site.data`
- `site.modules`
- i18n 当前语言
- SEO builder / html post processor 相关配置

如果这些配置变更，而内容、路由、模板文件不变，页面可能被错误跳过，导致输出 HTML 陈旧。

### 2.2 涉及文件

重点检查并修改：

```text
src/Bukit.Engine/SiteEngine.cs
src/Bukit.Engine/PageRenderDispatcher.cs
src/Bukit.Engine/RenderPipeline.cs
src/Bukit.Engine/Incremental/BuildManifest.cs
src/Bukit.Engine/Incremental/IncrementalBuildEngine.cs
src/Bukit.Engine/BuildResultFactory.cs
src/Bukit.Engine/BuildReporter.cs
```

实际文件名以仓库为准。

### 2.3 修复目标

新增 `RenderDependencyHash`，并加入 page / list page 的增量跳过判断。

建议依赖内容包括：

```text
site 渲染相关字段
theme 渲染相关字段
build 渲染相关字段
collections / taxonomy
SEO / Analytics 配置
插件启用状态
externalPlugins 配置摘要
当前语言 / baseUrl
siteModel.Modules 摘要
siteModel.Data 摘要
```

### 2.4 建议实现

新增类似：

```csharp
internal static class RenderDependencyHasher
{
    public static string Compute(
        AppConfig config,
        SiteModel siteModel,
        string baseUrl,
        string? defaultLanguage,
        IReadOnlyDictionary<string, IReadOnlyList<SeoAlternateModel>>? seoAlternates)
    {
        // 使用 deterministic JSON 序列化
        // 字典必须按 key 排序
        // 忽略 runtime-only / absolute path / timestamp
        // 只保留影响 HTML 输出的字段
    }
}
```

然后扩展 `RenderPipelineContext`：

```csharp
internal sealed record RenderPipelineContext(
    ...,
    string TemplateHash,
    string RenderDependencyHash,
    ...
);
```

扩展 `BuildManifestEntry`：

```csharp
public string? RenderDependencyHash { get; set; }
```

跳过判断中加入：

```csharp
existing.RenderDependencyHash == renderDependencyHash
```

写入 manifest 时保存：

```csharp
RenderDependencyHash = renderDependencyHash
```

### 2.5 兼容旧 manifest

旧 manifest 没有 `RenderDependencyHash` 时必须视为需要重新渲染：

```csharp
if (string.IsNullOrWhiteSpace(existing.RenderDependencyHash))
{
    canSkip = false;
}
```

不要因为旧缓存导致错误跳过。

### 2.6 测试用例

新增测试：

```text
Incremental_Rebuilds_When_SiteTitle_Changes
Incremental_Rebuilds_When_SiteDescription_Changes
Incremental_Rebuilds_When_BaseUrl_Changes
Incremental_Rebuilds_When_Analytics_Changes
Incremental_Rebuilds_When_SeoConfig_Changes
Incremental_Rebuilds_When_ThemeParams_Changes
Incremental_Rebuilds_When_Shortcodes_Change
Incremental_Rebuilds_When_Components_Change
Incremental_Rebuilds_When_Collections_Change
Incremental_Rebuilds_When_SiteData_Changes
Incremental_Rebuilds_When_PluginData_Changes
```

### 2.7 验收标准

- 修改 `site.title` 后，不修改内容文件，页面仍重新渲染。
- 修改 `theme.params` 后，不修改模板文件，页面仍重新渲染。
- 修改 SEO 默认图后，页面 SEO HTML 重新生成。
- 旧 manifest 第一次升级后不会错误跳过。
- 构建报告能显示 `render_dependency_changed` 或类似原因。

---

## 3. P0 修复任务：static HTML 覆盖 generated page

### 3.1 问题描述

当前只有在 `theme.staticTemplate` 存在时，static HTML 路由才参与冲突检查。  
如果没有配置 `staticTemplate`，`static/*.html` 会被原样复制到输出目录，可能覆盖由内容生成的页面。

风险示例：

```text
content/posts/a.md        → dist/blog/a/index.html
static/blog/a/index.html  → dist/blog/a/index.html
```

最终页面可能被 static 文件覆盖。

### 3.2 涉及文件

```text
src/Bukit.Engine/SiteEngine.cs
src/Bukit.Engine/StaticFileService.cs
src/Bukit.Engine/AssetPipeline.cs
src/Bukit.Routing/RouteInventoryValidator.cs
```

### 3.3 修复目标

无论是否配置 `staticTemplate`，都必须扫描 static HTML 输出目标，并与 generated routes / list routes / derived routes 进行冲突检查。

### 3.4 建议实现

在 `SiteEngine.BuildVariantAsync` 中：

```csharp
var staticHtmlRoutes = hasStaticDir
    ? StaticFileService.BuildStaticHtmlRoutes(
        ctx.StaticDir,
        staticTemplate ?? "__raw_static__",
        log.Warn)
    : Array.Empty<RouteInfo>();

RouteInventoryValidator.ValidateFinalRoutes(
    routed,
    pluginContext.DerivedRouted,
    listRoutes,
    staticHtmlRoutes);
```

如果当前 `BuildStaticHtmlRoutes` 要求 templateName 非空，则允许传入 placeholder。

### 3.5 AssetPipeline 二次防护

在复制 static 前，加入 output inventory 检查：

```text
如果 static 目标路径已经由 generated page/list/derived page 占用 → fail
```

推荐新增：

```csharp
OutputInventory
OutputInventoryBuilder
OutputConflictValidator
```

统一管理：

```text
generated page
derived page
list page
static html
static asset
theme asset
media
plugin output
```

### 3.6 测试用例

```text
StaticHtml_Conflicts_With_ContentPage_ShouldFail_WithoutStaticTemplate
StaticHtml_Conflicts_With_ContentPage_ShouldFail_WithStaticTemplate
StaticHtml_Conflicts_With_ListPage_ShouldFail
StaticHtml_Conflicts_With_DerivedPage_ShouldFail
StaticNonHtml_Conflicts_With_GeneratedPage_ShouldFail
```

### 3.7 验收标准

- static HTML 不再能静默覆盖 generated page。
- 无论是否设置 `staticTemplate`，冲突都会被发现。
- 错误信息必须包含两个冲突来源与输出路径。

---

## 4. P0 修复任务：统一 Safe Output FileSystem

### 4.1 问题描述

`FileWriter.GetSafeFullPath` 已有输出根目录逃逸检查，但不是所有输出都使用它。

当前存在直接复制逻辑：

```csharp
Path.Combine(outputDir, relativePath)
File.Copy(file, dest, overwrite: true)
```

相关场景包括：

- static 非 HTML 文件复制
- assets 复制
- media 复制
- theme tokens 输出
- plugin outputs 删除 / 追踪
- DirectoryCopy.Sync

### 4.2 涉及文件

```text
src/Bukit.Engine/FileWriter.cs
src/Bukit.Engine/StaticFileService.cs
src/Bukit.Engine/DirectoryCopy.cs
src/Bukit.Engine/AssetPipeline.cs
src/Bukit.Engine/Incremental/BuildManifestTracker.cs
src/Bukit.Engine/Output/SafeOutputFileSystem.cs
```

### 4.3 修复目标

建立唯一输出写入边界：

```csharp
public sealed class SafeOutputFileSystem
{
    public string GetSafeFullPath(string relativePath);
    public Task WriteTextAsync(string relativePath, string content, CancellationToken ct);
    public Task WriteBytesAsync(string relativePath, byte[] bytes, CancellationToken ct);
    public Task CopyFileAsync(string sourceFile, string relativeOutputPath, CancellationToken ct);
    public Task DeleteFileAsync(string relativePath, CancellationToken ct);
}
```

所有写入、复制、删除必须走它。

### 4.4 关键规则

`SafeOutputFileSystem` 必须：

1. 输出路径必须相对。
2. 禁止路径逃逸输出目录。
3. 禁止 `..` 段。
4. 禁止绝对路径。
5. 禁止 Windows drive-qualified path。
6. 禁止控制字符。
7. 可选：拒绝 Windows 保留设备名。
8. 可选：拒绝 symlink 输出目标逃逸。

### 4.5 测试用例

```text
SafeOutput_Rejects_PathTraversal
SafeOutput_Rejects_AbsolutePath
SafeOutput_Rejects_WindowsDrivePath
SafeOutput_Rejects_ControlCharacters
DirectoryCopy_UsesSafeOutputRoot
StaticCopy_UsesSafeOutputRoot
MediaCopy_UsesSafeOutputRoot
```

### 4.6 验收标准

- 全仓库搜索 `File.Copy(`，输出目录相关复制不能绕过 `SafeOutputFileSystem`。
- 全仓库搜索 `Path.Combine(outputDir`，必须确认每处都安全。
- 所有 output 写入均经过统一 safe-root 校验。

---

## 5. P0 修复任务：默认禁止发布敏感 dotfile

### 5.1 问题描述

`DirectoryCopyOptions.IgnoreDotPrefixedFiles` 当前默认是 `false`。  
static / assets 复制时默认可能发布：

```text
.env
.env.local
.git/config
.github/workflows/*.yml
.DS_Store
.npmrc
*.pem
*.key
*.pfx
```

### 5.2 涉及文件

```text
src/Bukit.Engine/DirectoryCopy.cs
src/Bukit.Engine/AssetPipeline.cs
src/Bukit.Config/ConfigLoader.cs
src/Bukit.Config/*.cs
```

### 5.3 修复目标

默认保护用户，不发布敏感文件。

建议新增配置：

```yaml
build:
  publishDotFiles: false
  staticAllowList:
    - ".well-known/**"
  staticDenyList:
    - ".env"
    - ".env.*"
    - ".git/**"
    - ".github/**"
    - "*.pem"
    - "*.key"
    - "*.pfx"
    - ".npmrc"
```

如果暂时不做 glob，可先实现固定 deny list + `.well-known` allowlist。

### 5.4 默认规则

默认拒绝：

```text
.env
.env.*
.git/
.github/
.svn/
.hg/
.DS_Store
Thumbs.db
*.pem
*.key
*.pfx
*.p12
.npmrc
.yarnrc
```

默认允许：

```text
.well-known/**
```

### 5.5 测试用例

```text
Static_DoesNotPublish_DotEnv_ByDefault
Static_DoesNotPublish_GitDirectory_ByDefault
Assets_DoesNotPublish_NpmRc_ByDefault
Static_Allows_WellKnown_ByDefault
Static_CanPublishDotFiles_WhenExplicitlyEnabled
```

### 5.6 验收标准

- 默认构建不会输出 `.env`。
- `.well-known/security.txt` 可以正常输出。
- 用户显式启用后才允许发布 dotfile。
- 构建日志应提示跳过敏感文件。

---

## 6. P1 修复任务：URL path segment 安全校验

### 6.1 问题描述

`RouteSecurityValidator.ValidateInternalUrl` 目前主要检查：

```text
不能为空
不能包含控制字符
不能是 protocol-relative URL
不能是外部 absolute URL
```

但没有明确拒绝：

```text
/../admin/
/a/../../b/
/%2e%2e/private/
/%2E%2E/private/
/a%2fb/
```

这些 URL 可能进入 canonical、sitemap、rss、hreflang、页面链接和 llms.txt。

### 6.2 涉及文件

```text
src/Bukit.Routing/RouteSecurityValidator.cs
src/Bukit.Routing/RoutePathBuilder.cs
src/Bukit.Routing/RouteGenerator.cs
```

### 6.3 修复目标

新增 URL segment 校验：

```text
拒绝 .
拒绝 ..
拒绝 URL decode 后为 . 或 ..
拒绝反斜杠
拒绝 encoded slash / backslash
拒绝控制字符
拒绝 protocol-relative
拒绝 absolute external URL
```

### 6.4 建议实现

在 `ValidateInternalUrl` 中增加：

```csharp
ValidateUrlPathSegments(value, source);
```

规则：

```csharp
var path = value.Split('?', '#')[0];

foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
{
    if (segment == "." || segment == "..") fail;
    var decoded = Uri.UnescapeDataString(segment);
    if (decoded == "." || decoded == "..") fail;
    if (decoded.Contains('/') || decoded.Contains('\\')) fail;
}
```

注意：`Uri.UnescapeDataString` 对异常编码可能抛异常，必须 fail。

### 6.5 测试用例

```text
RouteUrl_Rejects_DotDotSegment
RouteUrl_Rejects_EncodedDotDotSegment
RouteUrl_Rejects_Backslash
RouteUrl_Rejects_EncodedSlash
RouteUrl_Rejects_ProtocolRelative
RouteUrl_Rejects_ExternalAbsoluteUrl
RouteUrl_Allows_NormalChineseSlug
```

### 6.6 验收标准

- 危险 URL 在路由生成阶段失败。
- 错误信息包含 source 与原始 URL。
- 正常中文 slug、英文 slug、多层路径不受影响。

---

## 7. P1 修复任务：top-level outputPath 部分覆盖行为不一致

### 7.1 问题描述

当前 `RouteGenerator.TryApplyPartialRouteOverride` 只有在存在 nested `route` map 时，才允许 `outputPath` 部分覆盖：

```csharp
var useOutputPathOverride =
    !string.IsNullOrWhiteSpace(outputPathOverride)
    && HasNestedRouteMap(item.Meta);
```

这会导致：

```yaml
outputPath: custom/index.html
```

可能被读取但不生效。

### 7.2 涉及文件

```text
src/Bukit.Routing/RouteGenerator.cs
src/Bukit.Routing.Tests/*
```

### 7.3 修复方案二选一

#### 方案 A：统一支持 top-level outputPath

允许：

```yaml
outputPath: custom/index.html
```

也允许：

```yaml
route:
  outputPath: custom/index.html
```

#### 方案 B：废弃 top-level outputPath

如果发现 top-level `outputPath`，直接报错：

```text
Top-level outputPath is deprecated. Use route.outputPath instead.
```

### 7.4 推荐

推荐 **方案 B**。  
原因：路由字段聚合到 `route:` 下更清晰，降低 Notion / Markdown 字段污染。

### 7.5 测试用例

```text
Route_TopLevelOutputPath_ShouldFail_WithClearMessage
Route_NestedOutputPath_ShouldWork
Route_NestedUrl_ShouldRecomputeOutputPath
Route_NestedTemplate_ShouldOverrideTemplate
```

### 7.6 验收标准

- 不再出现“字段存在但被忽略”的行为。
- 错误信息明确指导用户迁移到 `route.outputPath`。

---

## 8. P1 修复任务：collections.yaml 解析失败不能静默忽略

### 8.1 问题描述

`ConfigLoader.TryReadCollectionsFile` 当前在 YAML 语法错误时可能直接返回 null。  
这会导致 collections 不生效，但构建仍继续。

### 8.2 涉及文件

```text
src/Bukit.Config/ConfigLoader.cs
src/Bukit.Config.Tests/*
```

### 8.3 修复目标

```text
collections.yaml 不存在 → 正常 fallback
collections.yaml 存在但为空 → warn 或 fail
collections.yaml 存在但 YAML 语法错误 → fail
collections.yaml 存在但结构错误 → fail
```

### 8.4 建议实现

```csharp
if (File.Exists(collectionsPath))
{
    try
    {
        yaml.Load(reader);
    }
    catch (YamlException ex)
    {
        throw new ConfigException($"Invalid YAML syntax in collections.yaml: {collectionsPath}", ex);
    }
}
```

### 8.5 测试用例

```text
CollectionsFile_Missing_ReturnsNull
CollectionsFile_InvalidYaml_ShouldThrowConfigException
CollectionsFile_InvalidShape_ShouldThrowConfigException
CollectionsFile_Valid_ShouldLoad
```

### 8.6 验收标准

- 拼错 `collections.yaml` 时构建失败。
- 错误信息包含文件路径。
- 不存在 `collections.yaml` 时不影响旧行为。

---

## 9. P1 修复任务：配置 bool/int 解析失败必须可诊断

### 9.1 问题描述

当前 `GetOptionalBool / GetOptionalInt / GetOptionalLong / GetOptionalDouble` 解析失败时返回 null，很多配置会继续使用默认值。

风险示例：

```yaml
build:
  clean: fasle
notion:
  pageSize: ten
```

用户以为配置生效，实际被默认值覆盖。

### 9.2 涉及文件

```text
src/Bukit.Config/ConfigLoader.cs
src/Bukit.Config/ConfigException.cs
src/Bukit.Config.Tests/*
```

### 9.3 修复目标

新增严格解析：

```csharp
GetOptionalBoolStrict(node, key, path)
GetOptionalIntStrict(node, key, path)
GetOptionalLongStrict(node, key, path)
GetOptionalDoubleStrict(node, key, path)
```

解析失败抛：

```text
Invalid config value: build.clean expected boolean, got "fasle"
```

### 9.4 兼容策略

可以新增配置：

```yaml
build:
  configFailMode: strict
```

但建议默认 strict。  
如果担心兼容，至少 CI 默认 strict。

### 9.5 测试用例

```text
Config_InvalidBool_ShouldThrow
Config_InvalidInt_ShouldThrow
Config_InvalidLong_ShouldThrow
Config_InvalidDouble_ShouldThrow
Config_ValidYesNoBool_ShouldParse
Config_ValidTrueFalseBool_ShouldParse
```

### 9.6 验收标准

- 拼写错误不会静默降级。
- 错误信息包含配置路径、期望类型、实际值。
- `yes/no/true/false` 可正常解析。

---

## 10. P1 修复任务：draft 字段统一 bool coercion

### 10.1 问题描述

当前草稿过滤只识别：

```text
true
"true"
"True"
```

不能识别：

```text
"TRUE"
"yes"
"1"
"on"
```

### 10.2 涉及文件

```text
src/Bukit.Engine/ContentPipeline.cs
src/Bukit.Shared/*
src/Bukit.Content/*
```

### 10.3 修复目标

新增统一 bool 转换工具：

```csharp
public static class ValueCoercion
{
    public static bool IsTruthy(object? value)
    {
        // true, "true", "yes", "1", "on"
    }

    public static bool IsFalsy(object? value)
    {
        // false, "false", "no", "0", "off"
    }

    public static bool? ToBooleanOrNull(object? value)
    {
        ...
    }
}
```

`ContentPipeline` 使用：

```csharp
items = items.Where(i =>
    !(i.Meta.TryGetValue("draft", out var d) && ValueCoercion.IsTruthy(d))
).ToList();
```

### 10.4 测试用例

```text
DraftFilter_Removes_BooleanTrue
DraftFilter_Removes_StringTrue_CaseInsensitive
DraftFilter_Removes_Yes
DraftFilter_Removes_One
DraftFilter_Removes_On
DraftFilter_Keeps_False
DraftFilter_Keeps_No
DraftFilter_Keeps_Zero
```

### 10.5 验收标准

- Markdown / Notion 的 draft 字段表现一致。
- bool 转换逻辑集中，不散落在各模块。

---

## 11. P1 修复任务：--jobs 并发参数没有贯穿所有渲染阶段

### 11.1 问题描述

页面渲染阶段尊重 `overrides.Jobs`，但特殊列表渲染和列表内容加载仍直接使用 `Environment.ProcessorCount`。

### 11.2 涉及文件

```text
src/Bukit.Engine/SiteEngine.cs
src/Bukit.Engine/RenderPipeline.cs
src/Bukit.Engine/PageRenderDispatcher.cs
```

### 11.3 修复目标

`MaxDegreeOfParallelism` 必须贯穿：

```text
RenderPagesAsync
RenderSpecialListsAsync
BuildPageInfosAsync
Notion content render
media download
image optimize
plugin derive/after-build 可选
```

先修 Render 相关：

```csharp
RenderSpecialListsAsync(..., int maxDegreeOfParallelism, ...)
BuildPageInfosAsync(..., int maxDegreeOfParallelism, ...)
```

### 11.4 测试用例

```text
RenderSpecialLists_Uses_ConfiguredJobs
BuildPageInfos_Uses_ConfiguredJobs
Jobs_ZeroOrNegative_FallsBackToProcessorCount
Jobs_One_RendersDeterministically
```

### 11.5 验收标准

- `bukit build --jobs 1` 时页面与列表渲染都单并发。
- 不再出现局部阶段绕过 jobs 限制。

---

## 12. P2 修复任务：site.data / plugin data 冲突策略

### 12.1 问题描述

当前 `MergeSiteData` 先合并 sourceData，再合并 pluginData。pluginData 会静默覆盖 sourceData。

### 12.2 涉及文件

```text
src/Bukit.Engine/SiteEngine.cs
src/Bukit.Config/*.cs
```

### 12.3 修复目标

新增配置：

```yaml
build:
  dataConflictPolicy: warn
```

可选值：

```text
fail
warn
plugin-wins
source-wins
```

默认建议：

```text
warn
```

CI 建议：

```text
fail
```

### 12.4 测试用例

```text
SiteDataConflict_Warn_ShouldLog
SiteDataConflict_Fail_ShouldThrow
SiteDataConflict_PluginWins_ShouldUsePluginValue
SiteDataConflict_SourceWins_ShouldKeepSourceValue
```

### 12.5 验收标准

- 数据覆盖不再静默发生。
- 构建日志能指出冲突 key。

---

## 13. P2 修复任务：YAML scalar 类型保持

### 13.1 问题描述

`ConfigLoader.ToObject` 当前把大多数 `YamlScalarNode` 转为 string。  
这可能导致：

```yaml
theme:
  params:
    enabled: false
```

在模板中变成字符串 `"false"`，进而产生 truthy 判断问题。

### 13.2 涉及文件

```text
src/Bukit.Config/ConfigLoader.cs
src/Bukit.Config.Tests/*
src/Bukit.Rendering.Tests/*
```

### 13.3 修复目标

实现 typed scalar：

```text
true / false / yes / no → bool
integer → long
float → double
null / ~ → null
其他 → string
```

注意保留显式字符串：

```yaml
enabled: !!str false
```

如果 YamlDotNet 可读取 tag，则尊重 `!!str`。

### 13.4 测试用例

```text
YamlObject_Bool_ShouldBecomeBool
YamlObject_Int_ShouldBecomeLongOrInt
YamlObject_Float_ShouldBecomeDouble
YamlObject_StringFalse_WithExplicitStringTag_ShouldRemainString
ThemeParams_BooleanFalse_ShouldBeFalsyInTemplate
```

### 13.5 验收标准

- `theme.params.enabled: false` 在模板中表现为 false。
- 不破坏已有字符串配置。

---

## 14. P2 修复任务：插件能力边界与输出声明

### 14.1 问题描述

external process plugin 虽然清空环境变量并限制 stdout/stderr，但仍具备较强本机进程能力。  
当前缺少能力声明与输出冲突声明。

### 14.2 涉及文件

```text
src/Bukit.Engine/Plugins/Protocol/*
src/Bukit.Config/ConfigLoader.cs
src/Bukit.Config/*.cs
src/Bukit.Engine/Incremental/BuildManifestTracker.cs
```

### 14.3 修复目标

配置增加：

```yaml
site:
  externalPlugins:
    my-plugin:
      runtime: process
      entry: plugins/my-plugin
      hooks:
        - after-build
      capabilities:
        readProject: true
        readContent: true
        writeOutput: true
        network: false
      outputs:
        - path: search.json
          conflictPolicy: fail
```

### 14.4 输出冲突策略

支持：

```text
fail
overwrite
skip
```

默认：

```text
fail
```

### 14.5 测试用例

```text
PluginOutput_Conflicts_WithGeneratedPage_ShouldFail
PluginOutput_Conflicts_WithStaticAsset_ShouldFail
PluginOutput_DeclaredOutput_ShouldTrackInManifest
PluginOutput_StaleOutput_ShouldBeDeleted
PluginEnvironment_OnlyAllowListedVariablesArePassed
```

### 14.6 验收标准

- 插件输出不会静默覆盖页面。
- plugin output 可追踪、可清理。
- 插件请求 JSON 不暴露不必要数据。

---

## 15. P2 修复任务：route explain 命令

### 15.1 目标

为调试 Notion / Markdown / demo 迁移提供可解释路由能力。

新增命令：

```bash
bukit route explain content/posts/demo.md
```

或：

```bash
bukit route explain --slug demo
```

输出：

```text
source: collection
collection: posts
pattern: /blog/{slug}/
slug: demo
url: /blog/demo/
outputPath: blog/demo/index.html
template: pages/post.html
conflicts: none
```

### 15.2 涉及文件

```text
src/Bukit.Cli/*
src/Bukit.Routing/RouteGenerator.cs
src/Bukit.Engine/RoutePipeline.cs
```

### 15.3 建议实现

`RouteGenerator.GenerateWithSource` 已经返回 `RouteSource`，可以扩展一个 explanation model：

```csharp
public sealed record RouteExplanation(
    RouteInfo Route,
    RouteSource Source,
    string? Collection,
    string? Pattern,
    string? Template,
    IReadOnlyList<string> Warnings);
```

### 15.4 测试用例

```text
RouteExplain_PostCollection_ShouldShowCollection
RouteExplain_Permalink_ShouldShowPattern
RouteExplain_FullOverride_ShouldShowOverride
RouteExplain_Conflict_ShouldShowConflict
```

### 15.5 验收标准

- CLI 可输出 route 来源。
- 输出足够帮助定位 Notion slug / collection / template 问题。

---

## 16. 推荐新增测试矩阵

### 16.1 增量构建

| 测试 | 期望 |
|---|---|
| 修改 `site.title` | 页面重新渲染 |
| 修改 `site.description` | 页面重新渲染 |
| 修改 `baseUrl` | 页面重新渲染 |
| 修改 GA ID | 页面重新渲染 |
| 修改 `theme.params` | 页面重新渲染 |
| 修改 shortcode 映射 | 页面重新渲染 |
| 修改 component 映射 | 页面重新渲染 |
| 修改 SEO default image | 页面重新渲染 |
| 修改 collection list route | 列表页重新渲染 |
| 修改 plugin data | 依赖页面重新渲染 |

### 16.2 路由安全

| 输入 | 期望 |
|---|---|
| `/../admin/` | fail |
| `/%2e%2e/admin/` | fail |
| `//evil.com/x` | fail |
| `https://evil.com/x` | fail |
| `CON/index.html` | fail |
| `中文-slug` | pass |
| duplicate route differing only by case | warn/fail，按配置 |

### 16.3 static 覆盖

| 场景 | 期望 |
|---|---|
| generated `/blog/a/` + `static/blog/a/index.html` | fail |
| `static/.env` | 不发布 |
| `static/.well-known/security.txt` | 发布 |
| plugin 输出覆盖页面 | fail |
| assets 覆盖 generated page | fail |

### 16.4 配置

| 配置 | 期望 |
|---|---|
| `clean: fasle` | fail |
| `pageSize: ten` | fail |
| `pageSize: 0` | fail 或按规则报错 |
| `pageSize: -1` | fail |
| malformed `collections.yaml` | fail |
| unknown `outputPathEncoding` | fail |
| unknown permalink token `{abc}` | fail |

---

## 17. 建议提交拆分

### PR 1：Incremental correctness

包含：

- `RenderDependencyHash`
- manifest schema 兼容
- render reason 增加
- 增量构建测试

### PR 2：Output safety

包含：

- SafeOutputFileSystem 统一写入
- static/generated conflict check
- dotfile denylist
- static/assets/media copy 测试

### PR 3：Config strictness

包含：

- collections.yaml 解析错误 fail
- bool/int/long/double strict parsing
- draft bool coercion
- typed YAML scalar

### PR 4：Concurrency consistency

包含：

- `--jobs` 贯穿 RenderSpecialLists / BuildPageInfos
- 并发测试

### PR 5：Plugin hardening

包含：

- plugin capabilities
- plugin outputs declaration
- plugin output conflict policy
- plugin output inventory

### PR 6：Developer diagnostics

包含：

- route explain
- build report 增强
- output inventory report
- stale deletion report

---

## 18. Codex 执行 Prompt

可以将下面 prompt 直接交给 Codex：

```text
你正在修复 ALi365-SDN-BHD/Bukit 仓库。目标是强化 Bukit 核心构建引擎的正确性、安全性和可诊断性。请按本 markdown 文档的 P0 → P1 → P2 顺序执行，不要一次性重构全部代码。

优先完成 PR 1：Incremental correctness。

具体要求：
1. 新增 RenderDependencyHash。
2. RenderDependencyHash 必须包含影响最终 HTML 的配置、siteModel、SEO、theme、collection、taxonomy、plugin toggles、external plugin 配置摘要。
3. RenderDependencyHash 必须加入页面与列表页的 incremental skip 判断。
4. 旧 manifest 没有 RenderDependencyHash 时必须触发重新渲染。
5. 新增 regression tests：
   - 修改 site.title 后页面重新渲染
   - 修改 theme.params 后页面重新渲染
   - 修改 analytics / SEO 配置后页面重新渲染
   - 旧 manifest 不会错误 skip
6. 不要破坏 Native AOT。
7. 不要引入动态 assembly loading。
8. 所有新增 hash 序列化必须 deterministic，字典按 key 排序。
9. 修改完成后执行 dotnet build 和 dotnet test，并修复失败测试。
10. 输出最终修改摘要、测试结果、仍需后续处理的问题。

完成 PR 1 后，再继续 PR 2：Output safety。
```

---

## 19. 完成定义

Bukit Core Hardening 完成后，应满足：

```text
1. 修改任何影响 HTML 的配置，不会被增量构建错误跳过。
2. static/assets/plugin 不会静默覆盖 generated page。
3. 所有输出写入、复制、删除都经过 safe output root 校验。
4. 默认不会发布敏感 dotfile。
5. URL / outputPath 安全校验一致。
6. 配置错误不会静默吞掉。
7. --jobs 对主要并发阶段有效。
8. 插件输出可声明、可追踪、可冲突检测。
9. route explain 可以解释页面最终路由来源。
10. 每个历史 bug 都有 regression test。
```

---

## 20. 最终路线图

```text
Sprint 1：增量构建正确性 + static 覆盖防护
Sprint 2：路径安全 + 输出系统统一
Sprint 3：配置严格模式 + route explain
Sprint 4：插件能力边界 + output inventory
Sprint 5：golden tests / regression tests / build report 增强
```

完成这些后，Bukit 才适合继续承载：

- BukitJalil 上层 AI 建站控制台
- Notion 全量内容迁移
- 企业官网批量生成
- SEO/GEO 自动化
- AI Agent 自动生成主题与内容
- 多语言内容站与企业知识站生成
