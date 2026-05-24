# Bukit Build Core Hardening TDD 修复计划

> **For agentic workers:** REQUIRED SUB-SKILL: 使用 test-driven-development 与 systematic-debugging。执行时必须逐个任务按 RED → 验证失败 → GREEN → 验证通过 → 重构 → 回归验证推进。不要在没有失败测试的情况下修改生产代码。不要提交 commit，除非用户明确要求。

**Goal:** 修复 `bukit-build-core-hardening-bugs-codex.md` 中 Build Core 的 P0/P1/P2 缺陷，并用可复现测试锁定静态路由、主题继承、增量构建、安全边界、插件与远程主题行为。

**Architecture:** 采用“小步 TDD + 根因优先”的修复方式。先为现有错误行为补失败测试，再在当前架构上引入最小可维护抽象：`RouteSecurityValidator`、静态 HTML route item、受控输出文件系统、安全 stale cleanup、Git runner 与 process plugin 输出限制。P0 先修正确性与安全边界，P1 修增量与插件稳定性，P2 做统一输出与 clean 保护。

**Tech Stack:** .NET 10、C#、xUnit、Bukit.Engine、Bukit.Routing、Bukit.Config、Bukit.Theme、Scriban renderer、YamlDotNet、Native AOT 兼容约束。

---

## 0. 只读分析结论

### 0.1 已确认根因

1. `StaticFileService` 当前用 `Path.GetDirectoryName(relativeOutputPath)` 推导 HTML URL，导致 `about.html` 被归到 `/`，`about/team.html` 被归到 `/about/`。
2. `StaticFileService` 当前对 `fileName[0]` 无空字符串保护，`static/.html` 会触发越界。
3. 静态 HTML 当前在 `RouteInventoryValidator.ValidateFinalRoutes` 之后渲染，未参与统一 route inventory 冲突检查。
4. `SiteEngine` 当前静态与 assets 复制顺序是 child 后 parent，父主题会覆盖子主题；并且父 assets/static 复制依赖 child 目录存在。
5. 增量 build 删除 manifest entry 但不删除旧输出文件，旧 HTML、assets、media、plugin output 会残留。
6. media cache 当前调用 `DirectoryCopy.SyncFiles`，只复制顶层文件。
7. `ThemeSourceManager` checkout 失败后只记录日志，仍返回 `ResolvedTheme`；`RunGit` 超时后没有可靠 kill process tree，且可能读取未退出进程的 `ExitCode`。
8. `RoutePathBuilder.NormalizeUrl`/`NormalizeOutputPath` 未提供统一安全校验层，协议 URL、协议相对 URL、unsafe slug 可进入 route/outputPath。
9. `RouteGenerator.TryApplyPartialRouteOverride` 当前只在 `url` 存在时生效，`outputPath` only override 被忽略。
10. `DirectoryHashCache.GetOrAdd(ctx.LayoutsDir)` 只覆盖 child layouts，未覆盖 parent/user layouts、theme config、components、renderer version。
11. process plugin 默认继承宿主环境变量，且 stdout/stderr 使用 `ReadToEndAsync` 无大小上限。
12. `build --clean` 直接删除 outputDir，没有 marker 保护。

### 0.2 关键文件地图

- 修改：`src/Bukit.Engine/StaticFileService.cs`
- 修改：`src/Bukit.Engine/SiteEngine.cs`
- 修改：`src/Bukit.Engine/RouteInventoryValidator.cs`
- 修改：`src/Bukit.Engine/DirectoryCopy.cs`
- 修改：`src/Bukit.Engine/FileWriter.cs`
- 修改：`src/Bukit.Engine/PageRenderDispatcher.cs`
- 修改：`src/Bukit.Engine/Incremental/BuildManifest.cs`
- 修改：`src/Bukit.Engine/Incremental/DirectoryHashCache.cs`
- 修改：`src/Bukit.Engine/ThemeSourceManager.cs`
- 修改：`src/Bukit.Engine/Plugins/Protocol/ProcessPluginInvoker.cs`
- 修改：`src/Bukit.Config/AppConfig.cs`
- 修改：`src/Bukit.Config/ConfigLoader.cs`
- 修改：`src/Bukit.Config/ConfigValidator.cs`
- 新增：`src/Bukit.Routing/RouteSecurityValidator.cs`
- 新增：`src/Bukit.Engine/Output/IOutputFileSystem.cs`
- 新增：`src/Bukit.Engine/Output/SafeOutputFileSystem.cs`
- 新增或扩展测试：`tests/Bukit.Engine.Tests/StaticFileServiceTests.cs`
- 修改测试：`tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`
- 新增或扩展测试：`tests/Bukit.Engine.Tests/RouteSecurityValidatorTests.cs`
- 新增或扩展测试：`tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`
- 新增或扩展测试：`tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
- 新增或扩展测试：`tests/Bukit.Engine.Tests/ThemeSourceManagerTests.cs`
- 新增或扩展测试：`tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs`
- 新增或扩展测试：`tests/Bukit.Config.Tests/ConfigLoaderTests.cs`
- 新增或扩展测试：`tests/Bukit.Config.Tests/ConfigValidatorTests.cs`

### 0.3 项目验证命令

单点 RED/GREEN 优先使用过滤命令：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~StaticFileServiceTests
```

阶段回归使用：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Theme.Tests/Bukit.Theme.Tests.csproj -c Release
```

最终必须运行：

```bash
dotnet build bukit.slnx -c Release -warnaserror
dotnet test bukit.slnx -c Release
dotnet format bukit.slnx --verify-no-changes
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
```

---

## 1. 执行约束

1. 每个 Bug ID 必须先写失败测试，并确认测试因目标缺陷失败。
2. 生产代码只做让当前失败测试通过的最小修改。
3. 一个测试失败原因不明确时，不继续实现，先修正测试或补充诊断。
4. 安全相关修复必须在 route validation 或 output filesystem 边界失败，不能依赖最后 `FileWriter` 才兜底。
5. 不新增无关功能；P2 的结构化能力只做支撑本轮缺陷的最小版本。
6. 不改变公开 CLI 参数，除非任务明确要求；新增配置必须保持默认兼容。
7. Native AOT 兼容：避免反射序列化新类型未注册、避免动态加载新外部程序集能力。

---

## 2. Phase 1：P0 静态 HTML、主题继承、增量删除、media 正确性

### Task 1：修复 BUG-001 / BUG-002 静态 HTML URL 与空文件名

**Files:**
- Modify: `src/Bukit.Engine/StaticFileService.cs`
- Test: `tests/Bukit.Engine.Tests/StaticFileServiceTests.cs`

- [ ] **Step 1 RED：新增静态 HTML URL 测试**

新增 `StaticFileServiceTests`，用一个 fake `ITemplateRenderer` 捕获 `PageModel.Page.Url`，覆盖：

```csharp
[Theory]
[InlineData("index.html", "/")]
[InlineData("about.html", "/about/")]
[InlineData("about/team.html", "/about/team/")]
[InlineData("docs/index.html", "/docs/")]
public void RenderStaticFiles_StaticHtml_GeneratesExpectedUrl(string relativePath, string expectedUrl)
```

运行：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~StaticFileServiceTests.RenderStaticFiles_StaticHtml_GeneratesExpectedUrl
```

预期：`about.html` 和 `about/team.html` 断言失败，证明 URL 根因存在。

- [ ] **Step 2 GREEN：提取 URL 构建方法**

在 `StaticFileService` 中新增 internal static 方法，便于测试与复用：

```csharp
internal static string BuildUrlFromStaticHtmlPath(string relativeOutputPath)
```

规则：
- `index.html` → `/`
- `docs/index.html` → `/docs/`
- `about.html` → `/about/`
- `about/team.html` → `/about/team/`
- normalize separator 为 `/`
- 输出交给 `RoutePathBuilder.NormalizeUrl` 与后续 `RouteSecurityValidator.ValidateInternalUrl`

- [ ] **Step 3 GREEN 验证**

运行 Task 1 的单测，必须 PASS。

- [ ] **Step 4 RED：新增 `.html` 不崩溃测试**

新增：

```csharp
[Fact]
public void RenderStaticFiles_EmptyHtmlFileName_SkipsFileAndDoesNotPolluteCurrentKeys()
```

断言：
- 不抛 `IndexOutOfRangeException`
- renderer 未收到 `.html`
- `currentKeys` 不包含 `.html`
- logger 或 warnings collector 包含 invalid static html warning

运行过滤命令，预期当前代码抛 `IndexOutOfRangeException`。

- [ ] **Step 5 GREEN：跳过非法 HTML 文件名**

给 `RenderStaticFiles` 增加可选 warning sink，或复用 `ILogger` 注入；若为最小改动，可新增参数默认 null：

```csharp
Action<string>? warn = null
```

空文件名时 warn 并 `continue`，不得生成 URL、title、outputPath、currentKeys。

- [ ] **Step 6 回归**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~StaticFileServiceTests
```

### Task 2：修复 BUG-003 静态 HTML 纳入 route inventory

**Files:**
- Modify: `src/Bukit.Engine/StaticFileService.cs`
- Modify: `src/Bukit.Engine/RouteInventoryValidator.cs`
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`
- Test: `tests/Bukit.Engine.Tests/RouteInventoryValidatorTests.cs`

- [ ] **Step 1 RED：静态页与内容页 URL 冲突测试**

新增集成测试：

```csharp
[Fact]
public async Task BuildAsync_StaticHtmlRouteConflictsWithContentRoute_FailsBeforeWrite()
```

最小站点：
- `content/about.md` frontmatter `route.url: /about/` 或 collection permalink `/{slug}/`
- `static/about.html`
- `theme.staticTemplate` 指向存在模板

运行：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~BuildAsync_StaticHtmlRouteConflictsWithContentRoute_FailsBeforeWrite
```

预期：当前代码不 fail，或在后续写入时互相覆盖，测试失败。

- [ ] **Step 2 RED：静态页 outputPath 冲突测试**

新增：

```csharp
[Fact]
public async Task BuildAsync_StaticHtmlOutputPathConflictsWithContentRoute_FailsWithBothSources()
```

断言异常消息包含 `static`、`content`、`/about/`、`about/index.html`。

- [ ] **Step 3 GREEN：创建静态 HTML route item 收集方法**

在 `StaticFileService` 中新增：

```csharp
internal static IReadOnlyList<RouteInfo> BuildStaticHtmlRoutes(string staticDir)
```

该方法只负责扫描与构造 route，不读取/渲染 HTML。非法 `.html` 跳过并可产出 warning。

- [ ] **Step 4 GREEN：扩展 RouteInventoryValidator**

新增 overload：

```csharp
public static void ValidateFinalRoutes(
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> derived,
    IReadOnlyList<RouteInfo>? specialRoutes,
    IReadOnlyList<RouteInfo>? staticHtmlRoutes)
```

`RouteInventoryEntry` 增加 `ForStaticHtmlRoute`，scope 使用 `static`。

- [ ] **Step 5 GREEN：调整 SiteEngine 阶段顺序**

在 `RouteInventoryValidator.ValidateFinalRoutes` 之前调用 `BuildStaticHtmlRoutes(ctx.StaticDir)`，将结果传入最终冲突检查。渲染阶段仍由 `RenderStaticFiles` 完成。

- [ ] **Step 6 回归**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~StaticFileServiceTests|FullyQualifiedName~RouteInventoryValidatorTests|FullyQualifiedName~BuildAsync_StaticHtml"
```

### Task 3：修复 BUG-004 / BUG-005 主题 assets/static 继承复制顺序

**Files:**
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：child assets 覆盖 parent assets**

新增：

```csharp
[Fact]
public async Task BuildAsync_ChildThemeAssetsOverrideParentThemeAssets()
```

站点结构：
- `themes/parent/assets/main.css` 内容 `parent`
- `themes/child/assets/main.css` 内容 `child`
- `theme.name: child`
- `theme.extends: parent`

断言 `dist/assets/main.css == child`。当前代码应失败为 `parent`。

- [ ] **Step 2 RED：child static 覆盖 parent static**

新增：

```csharp
[Fact]
public async Task BuildAsync_ChildThemeStaticOverrideParentThemeStatic()
```

断言 `dist/robots.txt == child`。当前代码应失败为 `parent`。

- [ ] **Step 3 RED：child 无 assets 时 parent assets 仍复制**

新增：

```csharp
[Fact]
public async Task BuildAsync_ParentThemeAssetsAreCopiedWhenChildHasNoAssets()
```

当前代码可能因 `if (Directory.Exists(ctx.AssetsDir))` 包住 parent assets 而失败。

- [ ] **Step 4 GREEN：统一复制顺序**

在 `SiteEngine.BuildVariantAsync` 中将静态和 assets 复制顺序改为：

```text
parent theme static/assets
child theme static/assets
project-level static/assets override
```

当前架构里 child/project 还未完全分离，最小改动为：
- parent static/assets 判断独立于 child 存在
- 先 sync parent，再 sync child
- 若项目 root-level override 已有解析路径，则最后 sync；没有则不新增行为

- [ ] **Step 5 回归**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~BuildAsync_ChildTheme|FullyQualifiedName~BuildAsync_ParentThemeAssets"
```

### Task 4：修复 BUG-006 删除内容页后旧 HTML 残留

**Files:**
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Modify: `src/Bukit.Engine/Incremental/BuildManifest.cs`
- Modify: `src/Bukit.Engine/FileWriter.cs`
- Later P2 merge: `src/Bukit.Engine/Output/SafeOutputFileSystem.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：增量删除页面测试**

新增：

```csharp
[Fact]
public async Task BuildAsync_IncrementalBuildDeletesRemovedPages()
```

流程：
1. 创建 `a.md`、`b.md`，build 非 clean 或第一次 clean。
2. 确认 `dist/blog/b/index.html` 存在。
3. 删除 `b.md`。
4. 使用 `Build.Clean = false`、incremental enabled 再 build。
5. 断言 `dist/blog/b/index.html` 不存在。

当前代码只移除 manifest entry，不删文件，测试失败。

- [ ] **Step 2 GREEN：用 manifest entry 的 OutputPath 安全删除**

新增内部方法：

```csharp
internal static void DeleteStaleManifestOutputs(string outputDir, BuildManifest manifest, ConcurrentDictionary<string, byte> currentKeys, ILogger logger)
```

逻辑：
- `removed = manifest.Entries.Where(k => !currentKeys.ContainsKey(k.Key))`
- 对每个 removed entry 使用 `entry.OutputPath`，若空则用 key
- 通过 `FileWriter` 新增的安全 full path helper 或 `SafeOutputFileSystem` 删除
- 删除后清理空目录，停止于 output root
- 最后从 manifest 移除 entry

- [ ] **Step 3 GREEN 验证**

运行目标测试，必须 PASS。

### Task 5：修复 BUG-007 media cache 嵌套复制与 stale cleanup

**Files:**
- Modify: `src/Bukit.Engine/DirectoryCopy.cs`
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：嵌套 media 复制测试**

新增：

```csharp
[Fact]
public void DirectoryCopy_SyncFilesRecursive_CopiesNestedFilesAndSkipsDotPrefixedFiles()
```

结构：
- `.cache/media/cover.png`
- `.cache/media/posts/2026/article-cover.png`
- `.cache/media/posts/.tmp`

断言输出保留嵌套结构且跳过 `.tmp`。

- [ ] **Step 2 GREEN：新增递归文件 sync**

在 `DirectoryCopy` 新增：

```csharp
public static void SyncFilesRecursive(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false)
```

保留相对路径，使用现有 `SyncFile` 的复制策略。

- [ ] **Step 3 RED：media stale cleanup 测试**

新增：

```csharp
[Fact]
public async Task BuildAsync_IncrementalBuildDeletesRemovedMediaFiles()
```

如果 BuildManifest 暂未记录 media outputs，此测试先失败。

- [ ] **Step 4 GREEN：记录 media outputs 或最小 scoped mirror**

优先采用 manifest 记录 media outputs：
- `BuildManifest` 增加 `Media` 字典，key 为 `assets/uploads/...`，value 为 hash 或 marker。
- sync 当前 media 时记录 current media keys。
- 对上次有、本次无的 media output 执行安全删除。

若为阶段内最小实现，可先限定只清理 `assets/uploads` 下由 media manifest 记录过的路径，不镜像删除用户手工文件。

- [ ] **Step 5 回归**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~DirectoryCopy|FullyQualifiedName~Media"
```

---

## 3. Phase 2：P0 安全与远程主题供应链

### Task 6：修复 BUG-010 / BUG-011 / BUG-022 路由和输出路径安全校验

**Files:**
- Create: `src/Bukit.Routing/RouteSecurityValidator.cs`
- Modify: `src/Bukit.Routing/RouteGenerator.cs`
- Modify: `src/Bukit.Routing/RoutePathBuilder.cs`
- Modify: `src/Bukit.Engine/RouteInventoryValidator.cs`
- Test: `tests/Bukit.Engine.Tests/RouteSecurityValidatorTests.cs`
- Test: `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`

- [ ] **Step 1 RED：危险 URL 拒绝测试**

新增：

```csharp
[Theory]
[InlineData("https://evil.com")]
[InlineData("//evil.com")]
[InlineData("javascript:alert(1)")]
[InlineData("data:text/html,test")]
[InlineData("vbscript:msgbox(1)")]
public void ValidateInternalUrl_ExternalOrDangerousUrl_Throws(string url)
```

当前无 validator，测试无法编译或失败。

- [ ] **Step 2 RED：unsafe slug/outputPath 拒绝测试**

新增：

```csharp
[Theory]
[InlineData("../evil")]
[InlineData("..\\evil")]
[InlineData("a/../../x")]
[InlineData("CON")]
[InlineData("aux")]
[InlineData("")]
public void ValidateOutputPath_UnsafeSegments_Throws(string value)
```

- [ ] **Step 3 GREEN：实现 RouteSecurityValidator**

公开 API：

```csharp
public static class RouteSecurityValidator
{
    public static void ValidateInternalUrl(string url, string? source = null);
    public static void ValidateOutputPath(string outputPath, string? source = null);
    public static void ValidateSlugSegment(string segment, string? source = null);
}
```

规则：
- URL 必须是空或单 `/` 开头，禁止 `//`。
- URL 禁止 URI scheme 和控制字符。
- 内部 URL normalize 后必须以 `/` 结尾。
- outputPath 禁止绝对路径、drive-qualified path、`..`、`.`、空 segment、控制字符、Windows reserved names。
- 错误消息包含 source/value。

- [ ] **Step 4 GREEN：接入 RouteGenerator 与 RouteInventoryValidator**

接入点：
- `BuildFromPattern` normalize URL 后 validate URL，再 build outputPath，再 validate outputPath。
- full override normalize 后 validate URL/outputPath。
- partial override normalize 后 validate。
- builtin fallback outputPath normalize 后 validate。
- `RouteInventoryValidator.ValidateEntries` 先 validate 再查重。

- [ ] **Step 5 RED/GREEN：编码后再次验证**

新增：

```csharp
[Theory]
[InlineData("..")]
[InlineData(".")]
[InlineData("CON")]
[InlineData("AUX")]
public void Generate_EncodedOutputPathIsValidatedAgain(string slug)
```

确保 `outputPathEncoding=slug|sanitize|urlencode|none` 后都验证最终 outputPath。

### Task 7：修复 BUG-012 partial route outputPath-only override

**Files:**
- Modify: `src/Bukit.Routing/RouteGenerator.cs`
- Test: `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`

- [ ] **Step 1 RED：outputPath-only override 测试**

新增：

```csharp
[Fact]
public void Generate_PartialRouteOverride_AppliesOutputPathOnly()
```

meta：

```csharp
["route"] = new Dictionary<string, object>
{
    ["outputPath"] = "custom/about/index.html"
}
```

断言 URL 仍是 base route，outputPath 变为 `custom/about/index.html`，template 仍是 base template。当前 `TryApplyPartialRouteOverride` 因 url 为空返回 false，测试失败。

- [ ] **Step 2 GREEN：组合式 partial override**

更新 `TryApplyPartialRouteOverride`：
- 没有任何 partial 字段 → false
- url 有值 → normalize URL；outputPath 无值则从 URL 推导
- outputPath 有值 → normalize/validate outputPath；url 无值则保留 base URL
- template 有值 → trim；否则保留 base template

- [ ] **Step 3 回归**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~RouteGeneratorTests
```

### Task 8：修复 BUG-008 / BUG-009 远程主题 checkout 与 git timeout

**Files:**
- Modify: `src/Bukit.Engine/ThemeSourceManager.cs`
- Test: `tests/Bukit.Engine.Tests/ThemeSourceManagerTests.cs`

- [ ] **Step 1 RED：不存在 tag 必须失败**

新增 fake git runner 注入测试：

```csharp
[Fact]
public void Resolve_WhenVersionTagDoesNotExist_ThrowsConfigException()
```

断言消息包含 source 与 versionTag。当前代码返回 `ResolvedTheme`，测试失败。

- [ ] **Step 2 RED：git timeout kill 测试**

新增：

```csharp
[Fact]
public async Task GitRunner_WhenTimedOut_KillsProcessTreeAndReturnsTimedOutResult()
```

使用抽象 process runner 或极短 timeout + 可控 fake process，不跑真实长耗时 git。

- [ ] **Step 3 GREEN：引入 GitRunner 抽象**

在 `ThemeSourceManager` 内部或独立 internal class 实现：

```csharp
internal sealed record GitResult(bool Success, string StdOut, string StdErr, int? ExitCode, bool TimedOut);
internal interface IGitRunner
{
    Task<GitResult> RunAsync(string args, string workingDirectory, TimeSpan timeout, CancellationToken cancellationToken);
}
```

生产实现使用 `ProcessStartInfo`，timeout 后 `Kill(entireProcessTree: true)`，返回 `TimedOut = true`，不读取未退出 `ExitCode`。

- [ ] **Step 4 GREEN：checkout 失败 hard fail**

逻辑：
- clone 失败 → throw `ConfigException`
- checkout 第一次失败 → fetch tags 一次
- retry checkout 失败 → throw `ConfigException`
- 不 fallback main/master/current checkout
- no versionTag 情况下的 `git pull` 失败至少 warning；P2 lock 任务再改变 build 默认更新策略

- [ ] **Step 5 回归**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter FullyQualifiedName~ThemeSourceManagerTests
```

---

## 4. Phase 3：P1 增量构建、assets、并发、插件安全

### Task 9：修复 BUG-013 composite template fingerprint

**Files:**
- Modify: `src/Bukit.Engine/Incremental/DirectoryHashCache.cs`
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：parent layout 变化触发重渲染**

新增：

```csharp
[Fact]
public async Task BuildAsync_IncrementalRerendersWhenParentLayoutChanges()
```

流程：第一次 build 后记录输出内容或 manifest templateHash；修改 parent layout；第二次 incremental build；断言输出变化且 render reason 包含 `template_changed` 或页面被重渲染。

- [ ] **Step 2 RED：user layout override 变化触发重渲染**

新增：

```csharp
[Fact]
public async Task BuildAsync_IncrementalRerendersWhenUserLayoutOverrideChanges()
```

- [ ] **Step 3 GREEN：复合 hash**

新增 helper：

```csharp
internal static string ComputeCompositeTemplateHash(
    string layoutsDir,
    string? parentLayoutsDir,
    string? userLayoutsDir,
    string? themeRoot,
    DirectoryHashCache cache)
```

hash 输入至少包含：child layouts、parent layouts、user layouts、`theme.yaml`、renderer version marker。不要引入非确定性时间。

### Task 10：修复 BUG-014 / BUG-015 assets/static stale cleanup 与 sha256 mode

**Files:**
- Modify: `src/Bukit.Engine/DirectoryCopy.cs`
- Modify: `src/Bukit.Engine/Incremental/BuildManifest.cs`
- Modify: `src/Bukit.Config/AppConfig.cs`
- Modify: `src/Bukit.Config/ConfigLoader.cs`
- Modify: `src/Bukit.Config/ConfigValidator.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
- Test: `tests/Bukit.Config.Tests/ConfigLoaderTests.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：删除 assets 后 dist 清理**

新增：

```csharp
[Fact]
public async Task BuildAsync_IncrementalBuildDeletesRemovedAssets()
```

- [ ] **Step 2 RED：size/time 相同但内容不同时 sha256 mode 复制**

新增：

```csharp
[Fact]
public void DirectoryCopy_Sha256ModeCopiesWhenContentChangedButSizeAndTimeSame()
```

- [ ] **Step 3 GREEN：配置与实现**

新增 build config：

```csharp
public string AssetHashMode { get; init; } = "size-time";
```

loader 读取 `build.assetHashMode`，validator 允许 `size-time|sha256`。

`DirectoryCopy` 增加：

```csharp
public static IReadOnlyList<string> Sync(string sourceDir, string destinationDir, DirectoryCopyOptions options)
```

options 包含 hash mode、relative prefix、ignore dot files；返回本次管理过的输出相对路径。

- [ ] **Step 4 GREEN：manifest 记录 assets/static**

`BuildManifest` 增加 `Assets` 字典，记录由 Bukit 管理的 assets/static 输出。删除上次存在但本次不存在的管理路径。

### Task 11：修复 BUG-016 多语言全局并发预算

**Files:**
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：并发预算测试**

新增：

```csharp
[Fact]
public async Task BuildAsync_MultiLanguageBuildRespectsGlobalConcurrencyBudget()
```

通过注入可观测 renderer/content provider 记录最大并发；`jobs=2`、languages=3 时总并发不得超过 2。

- [ ] **Step 2 GREEN：外层语言默认串行**

最小修复：多语言 outer loop 默认串行，每个语言内部用 `jobs`。如需保留语言并行，按预算：

```text
languageConcurrency = min(languages.Count, max(1, jobs / 2))
pageConcurrency = max(1, jobs / languageConcurrency)
```

优先使用串行，避免引入新 CLI 参数。

### Task 12：修复 BUG-017 / BUG-018 process plugin 环境变量与输出限制

**Files:**
- Modify: `src/Bukit.Config/AppConfig.cs`
- Modify: `src/Bukit.Config/ConfigLoader.cs`
- Modify: `src/Bukit.Config/ConfigValidator.cs`
- Modify: `src/Bukit.Engine/Plugins/Protocol/ProcessPluginInvoker.cs`
- Test: `tests/Bukit.Engine.Tests/ExternalProtocolPluginTests.cs`
- Test: `tests/Bukit.Config.Tests/ConfigLoaderTests.cs`

- [ ] **Step 1 RED：默认不继承宿主 secrets**

新增：

```csharp
[Fact]
public async Task ProcessPlugin_DoesNotReceiveHostSecretsByDefault()
```

设置进程环境 `OPENAI_API_KEY` 或 `GITHUB_TOKEN`，插件输出环境变量列表，断言不存在这些 key，存在 `BUKIT_PLUGIN_NAME` 等 allowlist 变量。

- [ ] **Step 2 GREEN：清空 Environment 并传入 allowlist**

`ProcessPluginInvoker`：
- `startInfo.Environment.Clear()`
- 写入 `BUKIT_PROJECT_ROOT`、`BUKIT_PLUGIN_NAME`、`BUKIT_PLUGIN_HOOK`、`BUKIT_OUTPUT_DIR`
- 可选 `ExternalPluginConfig.AllowEnvironment` 支持显式 allowlist，但默认空

- [ ] **Step 3 RED：stdout/stderr 超限测试**

新增：

```csharp
[Fact]
public async Task ProcessPlugin_FailsWhenStdoutExceedsLimit()

[Fact]
public async Task ProcessPlugin_FailsWhenStderrExceedsLimit()
```

- [ ] **Step 4 GREEN：限制输出读取**

新增 config：

```csharp
public int MaxStdoutBytes { get; init; } = 1048576;
public int MaxStderrBytes { get; init; } = 1048576;
public IReadOnlyList<string>? AllowEnvironment { get; init; }
```

读取 stdout/stderr 时边读边计数，超限 kill process tree，返回错误，不把完整超大输出放入结果。

### Task 13：修复 BUG-019 plugin outputs manifest cleanup

**Files:**
- Modify: `src/Bukit.Engine/Incremental/BuildManifest.cs`
- Modify: `src/Bukit.Engine/Plugins/PluginRunner.cs`
- Modify: `src/Bukit.Engine/Plugins/Protocol/ProtocolOutputWriter.cs`
- Test: `tests/Bukit.Engine.Tests/PluginRunnerTests.cs`

- [ ] **Step 1 RED：plugin stale output 删除测试**

新增：

```csharp
[Fact]
public async Task PluginOutputs_DeleteStaleFilesFromPreviousBuild()
```

第一次插件输出 `plugin/a.json`，第二次不输出，断言旧文件删除。

- [ ] **Step 2 GREEN：记录 plugin outputs**

`BuildManifest` 增加 `PluginOutputs` 字典，plugin output 写入时记录 plugin、hook、path、hash。build 结束前对比并安全删除 stale plugin outputs。

---

## 5. Phase 4：P2 统一输出系统、clean marker、远程主题锁定

### Task 14：修复 BUG-020 / BUG-021 安全输出文件系统与 clean marker

**Files:**
- Create: `src/Bukit.Engine/Output/IOutputFileSystem.cs`
- Create: `src/Bukit.Engine/Output/SafeOutputFileSystem.cs`
- Modify: `src/Bukit.Engine/FileWriter.cs`
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Modify: `src/Bukit.Engine/DirectoryCopy.cs`
- Test: `tests/Bukit.Engine.Tests/SafeOutputFileSystemTests.cs`
- Test: `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`

- [ ] **Step 1 RED：输出路径穿越拒绝测试**

新增：

```csharp
[Theory]
[InlineData("../evil.txt")]
[InlineData("/tmp/evil.txt")]
[InlineData("C:/evil.txt")]
public async Task SafeOutputFileSystem_RejectsUnsafeRelativePath(string relativePath)
```

- [ ] **Step 2 GREEN：实现 SafeOutputFileSystem**

API：

```csharp
public interface IOutputFileSystem
{
    Task WriteTextAsync(string relativePath, string content, CancellationToken ct);
    Task CopyFileAsync(string sourcePath, string relativePath, CancellationToken ct);
    Task DeleteFileAsync(string relativePath, CancellationToken ct);
    string GetSafeFullPath(string relativePath);
}
```

所有 full path 必须在 output root 下，且 relativePath 先经过 `RouteSecurityValidator.ValidateOutputPath`。

- [ ] **Step 3 RED：clean 无 marker 拒绝**

新增：

```csharp
[Fact]
public async Task BuildAsync_CleanRefusesDirectoryWithoutBukitMarker()
```

- [ ] **Step 4 GREEN：marker 保护**

规则：
- 输出成功创建 `dist/.bukit-output-marker`
- clean 前 outputDir 不存在或为空可以初始化
- outputDir 存在且有 marker 可以 clean
- outputDir 存在且无 marker 拒绝
- 永远拒绝 project root、home、filesystem root、`.git`

### Task 15：修复 BUG-023 远程主题可复现构建

**Files:**
- Modify: `src/Bukit.Engine/ThemeSourceManager.cs`
- Modify: `src/Bukit.Cli/Commands/ThemeCommand.cs`
- Test: `tests/Bukit.Engine.Tests/ThemeSourceManagerTests.cs`
- Test: `tests/Bukit.Cli.Tests/ThemeCommandTests.cs`

- [ ] **Step 1 RED：build 不隐式 pull 改变版本**

新增：

```csharp
[Fact]
public void Resolve_ExistingRemoteTheme_DoesNotPullDuringBuildByDefault()
```

fake git runner 记录调用，断言普通 build 不调用 `pull`。

- [ ] **Step 2 GREEN：最小 lock 行为**

最小策略：
- `Resolve` 已有 cache 时默认不 `git pull`
- 指定 `@ref` 时 checkout 到该 ref 并读取当前 commit
- 后续可由 `theme update` 或显式参数更新 cache
- 若已有 lock 文件，校验当前 commit 一致，不一致 fail

- [ ] **Step 3 RED/GREEN：lock 文件测试**

新增：

```csharp
[Fact]
public void Resolve_WhenLockCommitDiffers_Fails()
```

lock 格式可最小实现为 `.cache/themes/bukit-theme.lock` 或项目根 `bukit-theme.lock`，但不要破坏现有无 lock 项目。

---

## 6. 阶段验收矩阵

### 6.1 Phase 1 完成后

必须通过：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~StaticFileServiceTests|FullyQualifiedName~BuildAsync_StaticHtml|FullyQualifiedName~BuildAsync_ChildTheme|FullyQualifiedName~BuildAsync_ParentThemeAssets|FullyQualifiedName~IncrementalBuildDeletesRemovedPages|FullyQualifiedName~DirectoryCopy|FullyQualifiedName~Media"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
```

验收：
- `static/about.html` URL 为 `/about/`
- `static/about/team.html` URL 为 `/about/team/`
- `static/.html` warning + skip
- 静态 HTML 与内容 route 冲突 fail
- parent < child 复制顺序正确
- child 无 assets 时 parent assets 仍输出
- 删除内容页后旧 HTML 删除
- 嵌套 media 输出正确

### 6.2 Phase 2 完成后

必须通过：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~RouteSecurityValidatorTests|FullyQualifiedName~RouteGeneratorTests|FullyQualifiedName~ThemeSourceManagerTests"
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
```

验收：
- unsafe URL/slug/outputPath 在 route validation 阶段失败
- outputPath-only partial override 生效
- remote theme tag checkout 失败 hard fail
- git timeout kill process tree

### 6.3 Phase 3 完成后

必须通过：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~Incremental|FullyQualifiedName~ExternalProtocolPlugin|FullyQualifiedName~PluginOutputs|FullyQualifiedName~DirectoryCopy"
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
```

验收：
- parent/user layout 修改触发重渲染
- 删除 assets/static/media/plugin output 后 dist 不残留旧文件
- sha256 mode 识别内容变化
- 多语言并发不超过 jobs 预算
- process plugin 默认拿不到宿主 secrets
- stdout/stderr 超限 fail 且 kill

### 6.4 Phase 4 完成后

必须通过：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~SafeOutputFileSystem|FullyQualifiedName~Clean|FullyQualifiedName~ThemeSourceManager"
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
```

验收：
- 所有写入/复制/删除都可迁移到 safe output guard
- `--clean` 不会删除无 marker 目录
- 普通 build 不隐式 `git pull` 改变主题版本

### 6.5 最终验收

```bash
dotnet build bukit.slnx -c Release -warnaserror
dotnet test bukit.slnx -c Release
dotnet format bukit.slnx --verify-no-changes
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
```

最终报告必须列出：
- 已修复 Bug ID
- 每个 Bug 的主要修改文件
- 每个 Bug 的 RED/GREEN 测试命令与结果
- 是否影响公开 CLI 或配置格式
- 是否有 breaking change
- 未完成或需人工确认事项

---

## 7. 推荐执行顺序

1. Task 1：静态 HTML URL 与 `.html` 崩溃。
2. Task 6：先落地 `RouteSecurityValidator`，为后续所有 route/outputPath 提供统一安全边界。
3. Task 7：partial route override 修复。
4. Task 2：静态 HTML route inventory。
5. Task 3：主题继承复制顺序。
6. Task 4：stale page deletion。
7. Task 5：media recursive sync。
8. Task 8：remote theme checkout/git timeout。
9. Task 9：composite template hash。
10. Task 10：assets/static manifest 与 sha256 mode。
11. Task 12：process plugin 环境和输出限制。
12. Task 13：plugin outputs stale cleanup。
13. Task 11：多语言并发预算。
14. Task 14：safe output filesystem 与 clean marker。
15. Task 15：remote theme lock/reproducible build。

该顺序的原因：安全 validator 和静态 route 修复会影响后续多个任务；stale cleanup 依赖安全删除；process plugin 与 remote theme 可相对独立，适合在 P0 正确性稳定后推进。
