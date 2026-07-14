# Bukit Analytics 内置插件实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development`（推荐）或 `superpowers:executing-plans` 按任务逐项实施。本计划全部执行步骤使用复选框跟踪。

**文档版本：** v1.0  
**制定日期：** 2026-07-12  
**适用项目：** Bukit 静态网站生成引擎  
**基线提交：** `02d0232bcfeac55dcb13931f753354c4c15db628`  
**目标版本建议：** 先在后续次版本完成功能迁移与兼容，再在下一主版本评估删除旧字段  

**目标：** 将 Bukit 现有、与 SEO 管线耦合的单一 Google Analytics 注入能力，重构为 Core 内部拥有、Provider 化、Native AOT 友好、可验证且不扩大外部插件协议的 Analytics 内置插件体系。

**架构：** Analytics 由 `Bukit.Engine` 内部的 `AnalyticsPlugin` 负责，通过内部 HTML Transform Pipeline 在页面写盘前注入。Provider 只产生结构化的 `HeadEnd` 与 `BodyStart` 片段；外部进程插件、`Bukit.PluginHost`、`Bukit.Plugin.Abstractions` 均不获得页面渲染、`BuildContext` 或任意 HTML 注入能力。

**技术栈：** .NET / C#、Native AOT、YamlDotNet 配置模型、Scriban 渲染、xUnit、Bukit 定向门禁脚本。

---

## 1. 背景与现状判断

Bukit 当前已经具备最小 Analytics 能力，并非全新功能：

- `site.analytics.enabled`
- `site.analytics.googleAnalyticsId`
- `site.analytics.disableInPreview`
- `SiteModel.Analytics`
- `SeoHtmlRenderer` 中的 GA4 `gtag.js` 注入

当前实现存在以下结构性问题：

1. **Analytics 与 SEO 注入耦合。**
   - Analytics 由 `SeoPipeline` 把 `AnalyticsModel` 传给 `SeoHtmlRenderer`。
   - 当 SEO 被禁用，或 SEO 不执行 HTML 注入时，Analytics 也无法可靠注入。

2. **仅支持单一 Google Analytics ID。**
   - 无 Provider 列表。
   - 无 Google Tag Manager 的 `<body>` 起始位置注入能力。
   - 无 Plausible、Umami 等可扩展模型。

3. **现有 `disableInPreview` 缺少明确执行模式。**
   - `ConfigOverrides` 目前没有 Production/Development 构建意图。
   - `bukit dev` 当前先生成带 GA 的 HTML，再由 `DevRequestHandler` 在响应时调用 Preview 过滤器剥离 GA。
   - `bukit preview` 当前使用两条 GA 专用正则过滤响应，只覆盖 GA4，不覆盖未来 Provider。
   - 目标状态是：dev 构建本身按 Development 模式关闭 production-only Analytics；dev/preview 响应过滤作为兼容与残留清理层，且不得改写磁盘产物。

4. **当前插件钩子不适合 Analytics。**
   - Core 插件主要使用 `derive-pages` 与 `after-build`。
   - Analytics 不应在 `after-build` 阶段扫描并重写所有 HTML 文件。
   - 不应为了 Analytics 向外部插件增加逐页 HTML Transform Hook。

5. **配置契约尚未完成严格校验。**
   - 现有 `googleAnalyticsId` 需要格式校验。
   - 新 Provider 需要类型、必填字段、URL、安全和重复配置校验。

因此，本计划采用：

> **Core 内部 AnalyticsPlugin + 内部 HTML Transform Pipeline + Typed Providers + 旧配置兼容层。**

---

## 2. 核心决策

### 2.1 采用的方案

```text
site.yaml
   ↓
Bukit.Config 严格解析与验证
   ↓
AnalyticsConfigNormalizer
   ↓
ResolvedAnalyticsConfig
   ↓
AnalyticsProviderRegistry
   ↓
AnalyticsPlugin（Core internal）
   ↓
HtmlTransformPipeline
   ├── SeoHtmlTransform
   └── AnalyticsPlugin
   ↓
PageRenderDispatcher 写入最终 HTML
```

### 2.2 “内置插件”的准确含义

本计划中的 `AnalyticsPlugin`：

- 位于 `src/Bukit-Core/Bukit.Engine/`；
- 类型使用 `internal`；
- 与 Bukit Core 同版本编译和发布；
- 只由 Core 内部注册；
- 不属于外部插件 SDK；
- 不通过 `Bukit.PluginHost` 执行；
- 不向第三方公开页面渲染或 HTML 修改接口。

### 2.3 明确拒绝的方案

#### 方案 A：外部进程插件逐页修改 HTML

拒绝原因：

- 需要暴露输出目录或全部 HTML 内容；
- 容易破坏确定性构建和增量构建；
- 增加目录遍历、竞态写入和供应链风险；
- 与“外部插件严禁调用 Core、只能遵循协议”原则冲突。

#### 方案 B：在 `after-build` 中扫描并重写所有 HTML

拒绝原因：

- 构建后再改文件，Manifest 与真实产物可能不一致；
- 每次构建需要额外全目录扫描；
- 增量构建难以正确判断 Analytics 配置变化；
- 失败时可能留下部分写入产物。

#### 方案 C：只通过 Theme partial 实现

拒绝原因：

- 每个主题重复实现；
- 无法统一配置验证、环境控制和注入去重；
- GTM 需要同时处理 `<head>` 与 `<body>`；
- 无法作为 Bukit 标准能力跨主题使用。

---

## 3. 范围

### 3.1 本期必须交付

1. 将 Analytics 注入从 SEO 渲染器中解耦。
2. 建立 Core internal Analytics Plugin。
3. 建立 Provider 化配置和运行模型。
4. 支持：
   - Google Analytics 4；
   - Google Tag Manager；
   - Plausible；
   - Umami。
5. 支持注入位置：
   - `HeadEnd`；
   - `BodyStart`。
6. `bukit dev` 默认不注入 production-only Analytics。
7. 普通 `bukit build` 和 CI production build 正常注入。
8. 保留旧 `googleAnalyticsId` 配置兼容。
9. 配置变化能够使增量构建重新渲染相关 HTML。
10. 输出可审计的 Analytics 构建摘要。
11. 增加配置、单元、集成、架构和 Native AOT 相关验证。

### 3.2 本期明确不做

- Google Analytics Data API 查询；
- OAuth 登录与授权；
- Analytics Dashboard；
- 统计数据同步到 Notion；
- Cookie Consent Management Platform；
- 任意 JavaScript 文本注入；
- 外部插件渲染 Hook；
- 外部 Provider 动态加载；
- 插件市场；
- 运行时 DLL 插件；
- 自动修改第三方主题文件；
- 对既有 `bukit preview` 产物动态移除脚本。

---

## 4. 全局约束

1. **不得扩展外部插件协议。**
   - 不修改外部进程插件 JSON 协议以传递页面 HTML。
   - 不增加 external `render-page`、`transform-html` 或等价 Hook。

2. **不得向外部插件暴露 Core 渲染对象。**
   - 不将 `BuildContext`、`PageModel`、`SiteModel`、Renderer 或输出目录写权限加入未来 SDK。

3. **不得把 Analytics Provider 放入 `Bukit.Plugin.Abstractions`。**
   - Provider 接口必须为 `internal`，并位于 `Bukit.Engine`。

4. **不得允许 arbitrary JavaScript。**
   - 不增加 `customScript`、`headHtml`、`bodyHtml` 等字段。
   - 用户只配置受控 ID、域名与 HTTPS script URL。

5. **必须保持 Native AOT 兼容。**
   - 不使用运行时程序集扫描。
   - 不使用反射发现 Provider。
   - Provider 使用显式静态注册。

6. **必须保持确定性输出。**
   - 同一配置和输入产生同一 HTML。
   - Provider 顺序由配置顺序确定。
   - 生成片段不得包含当前时间或随机值。

7. **必须支持幂等注入。**
   - 重复处理同一 HTML 不得产生重复脚本。
   - Core 管理的 Analytics 标记必须可识别和替换。

8. **必须保持 SEO 与 Analytics 独立。**
   - `site.seo.enabled: false` 时 Analytics 仍可按配置工作。
   - `site.seo.renderMode: off|theme` 不得阻断 Analytics。

9. **必须遵守仓库门禁规则。**
   - 每个代码子任务使用该任务中列出的精确路径执行 `scripts/checks/post-change-targeted.sh`。
   - 未经用户明确要求，不运行 full/release gate。
   - 插件、配置契约和安全边界改动完成后必须执行一次只读综合审计。

10. **不得修改备份目录。**
    - 不修改 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`。

---

## 5. 目标配置契约

### 5.1 推荐新配置

```yaml
site:
  analytics:
    enabled: true
    productionOnly: true
    providers:
      - type: google-analytics
        measurementId: G-XXXXXXXXXX

      - type: google-tag-manager
        containerId: GTM-XXXXXXX

      - type: plausible
        domain: example.com
        scriptUrl: https://plausible.io/js/script.js

      - type: umami
        websiteId: 00000000-0000-0000-0000-000000000000
        scriptUrl: https://analytics.example.com/script.js
```

### 5.2 旧配置兼容

继续接受：

```yaml
site:
  analytics:
    enabled: true
    googleAnalyticsId: G-XXXXXXXXXX
    disableInPreview: true
```

兼容规则：

1. `googleAnalyticsId` 转换为一个 `google-analytics` Provider。
2. 只配置旧字段时，不改变原有 GA4 HTML 语义。
3. 同时配置 `googleAnalyticsId` 和等价 GA Provider 时，配置检查失败，禁止重复注入。
4. `productionOnly` 使用 nullable 配置：未设置时沿用 `disableInPreview`；设置后作为新行为开关。
5. `disableInPreview` 保持默认值 `true`，确保旧站点的 dev/preview 行为不倒退。
6. 使用 `googleAnalyticsId` 时输出一次明确、非阻塞的弃用警告。
7. 本期不删除旧字段；删除时间必须另行制定 breaking-change 计划。

### 5.3 Provider 字段矩阵

| Provider | 必填字段 | 可选字段 | 注入位置 |
|---|---|---|---|
| `google-analytics` | `measurementId` | 无 | `HeadEnd` |
| `google-tag-manager` | `containerId` | 无 | `HeadEnd` + `BodyStart` |
| `plausible` | `domain` | `scriptUrl` | `HeadEnd` |
| `umami` | `websiteId`, `scriptUrl` | 无 | `HeadEnd` |

### 5.4 安全校验

- `measurementId`：必须匹配 `^G-[A-Z0-9]+$`。
- `containerId`：必须匹配 `^GTM-[A-Z0-9]+$`。
- `domain`：
  - 不得包含 scheme；
  - 不得包含路径、查询、片段或用户信息；
  - 允许合法域名和 IDN 转换后的 ASCII 形式。
- `websiteId`：必须为合法 UUID。
- `scriptUrl`：
  - 必须为绝对 HTTPS URL；
  - 禁止用户名和密码；
  - 禁止片段；
  - 禁止 `javascript:`、`data:`、`file:`、`http:`；
  - 路径必须指向脚本资源；
  - 不进行服务器端抓取，因此不把它当作构建期 HTTP 请求入口。
- Provider `type` 为固定枚举，不接受未知类型。
- 相同 Provider 唯一标识不得重复。
- 所有 ID 在写入 HTML 前必须执行 HTML attribute 编码和 JavaScript string 编码。

---

## 6. 目标文件结构

### 6.1 新增文件

```text
src/Bukit-Core/Bukit.Config/
  AnalyticsConfigNormalizer.cs
  AnalyticsConfigValidator.cs

src/Bukit-Core/Bukit.Engine/Analytics/
  AnalyticsPlugin.cs
  AnalyticsProviderRegistry.cs
  AnalyticsInjectionPolicy.cs
  AnalyticsHtmlRenderer.cs
  AnalyticsBuildSummary.cs
  AnalyticsBuildSummaryCollector.cs
  IAnalyticsProvider.cs
  AnalyticsHtmlFragments.cs
  AnalyticsRenderContext.cs
  AnalyticsValueEncoder.cs
  GoogleAnalyticsProvider.cs
  GoogleTagManagerProvider.cs
  PlausibleProvider.cs
  UmamiProvider.cs

src/Bukit-Core/Bukit.Engine/Html/
  IHtmlTransform.cs
  HtmlTransformContext.cs
  HtmlTransformPipeline.cs

src/Bukit-Core/Bukit.Config/
  BuildExecutionMode.cs

src/Bukit-Core/Bukit.Cli/Commands/
  PreviewAnalyticsFilter.cs

tests/Bukit.Config.Tests/Analytics/
  AnalyticsConfigValidatorTests.cs
  AnalyticsConfigNormalizerTests.cs

tests/Bukit.Engine.Tests/Analytics/
  AnalyticsHtmlRendererTests.cs
  AnalyticsPipelineTests.cs
  GoogleAnalyticsProviderTests.cs
  GoogleTagManagerProviderTests.cs
  PlausibleProviderTests.cs
  UmamiProviderTests.cs
  AnalyticsIntegrationTests.cs
  AnalyticsIncrementalBuildTests.cs
  AnalyticsBuildSummaryTests.cs
  AnalyticsProviderRegistryTests.cs
  TestAnalytics.cs
  AnalyticsBuildFixture.cs

guide/user/
  14-analytics.md

examples/analytics/
  site.yaml
```

### 6.2 修改文件

```text
src/Bukit-Core/Bukit.Config/AppConfig.cs
src/Bukit-Core/Bukit.Config/ConfigOverrides.cs
src/Bukit-Core/Bukit.Config/ConfigValidator.cs
src/Bukit-Core/Bukit.Rendering/Models.cs
src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs
src/Bukit-Core/Bukit.Engine/SeoPipeline.cs
src/Bukit-Core/Bukit.Engine/SeoHtmlRenderer.cs
src/Bukit-Core/Bukit.Engine/HtmlHeadScanner.cs
src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs
src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHasher.cs
src/Bukit-Core/Bukit.Engine/BuildReporter.cs
src/Bukit-Core/Bukit.Engine/BuildVariantResult.cs
src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs
src/Bukit-Core/Bukit.Cli/Commands/PreviewCommand.cs
src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs
tests/Bukit.Engine.Tests/SeoHtmlRendererTests.cs
tests/Bukit.Engine.Tests/SeoPipelineTests.cs
tests/Bukit.Architecture.Tests/AnalyticsBoundaryTests.cs
guide/user/04-site-yaml-config.md
guide/user/README.md
README.md
README.zh-CN.md
README.ms.md
```

> 以上现有路径已按基线提交核对。若实施分支已移动，Task 0 必须重新检索并在首个代码提交前更新本计划中的路径映射。

---

## 7. 关键内部接口设计

### 7.1 配置模型

修改 `src/Bukit-Core/Bukit.Config/AppConfig.cs`：

```csharp
public sealed record AnalyticsConfig
{
    public bool Enabled { get; init; } = true;
    // null 表示沿用旧 disableInPreview 行为。
    public bool? ProductionOnly { get; init; }

    // 兼容字段：本期保留，不作为新文档首选配置。
    public string? GoogleAnalyticsId { get; init; }
    public bool DisableInPreview { get; init; } = true;

    public IReadOnlyList<AnalyticsProviderConfig>? Providers { get; init; }
}

public sealed record AnalyticsProviderConfig
{
    public required string Type { get; init; }
    public string? MeasurementId { get; init; }
    public string? ContainerId { get; init; }
    public string? Domain { get; init; }
    public string? WebsiteId { get; init; }
    public string? ScriptUrl { get; init; }
}
```

### 7.2 规范化结果

新增 `AnalyticsConfigNormalizer.cs`：

```csharp
public sealed record ResolvedAnalyticsConfig
{
    public bool Enabled { get; init; }
    public bool ProductionOnly { get; init; }
    public bool UsesLegacyFields { get; init; }
    public IReadOnlyList<ResolvedAnalyticsProvider> Providers { get; init; }
        = Array.Empty<ResolvedAnalyticsProvider>();
}

public sealed record ResolvedAnalyticsProvider
{
    public required string Type { get; init; }
    public required string Key { get; init; }
    public IReadOnlyDictionary<string, string> Options { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}
```

### 7.3 内部 Provider 接口

新增 `src/Bukit-Core/Bukit.Engine/Analytics/IAnalyticsProvider.cs`：

```csharp
namespace Bukit.Engine.Analytics;

internal interface IAnalyticsProvider
{
    string Type { get; }

    AnalyticsHtmlFragments Render(
        ResolvedAnalyticsProvider provider,
        AnalyticsRenderContext context);
}
```

### 7.4 HTML 片段模型

```csharp
namespace Bukit.Engine.Analytics;

internal sealed record AnalyticsHtmlFragments
{
    internal static readonly AnalyticsHtmlFragments Empty = new()
    {
        ProviderKey = "empty"
    };

    public required string ProviderKey { get; init; }
    public string? HeadEnd { get; init; }
    public string? BodyStart { get; init; }
}
```

### 7.5 HTML Transform Pipeline

```csharp
namespace Bukit.Engine.Html;

internal interface IHtmlTransform
{
    string Name { get; }
    string Transform(HtmlTransformContext context, string html);
}

internal sealed record HtmlTransformContext(
    string RouteUrl,
    string OutputPath,
    bool IsListPage,
    BuildExecutionMode ExecutionMode,
    PageInfo? Page,
    ILogger Logger);

internal sealed class HtmlTransformPipeline
{
    private readonly IReadOnlyList<IHtmlTransform> _transforms;

    internal HtmlTransformPipeline(IEnumerable<IHtmlTransform> transforms)
    {
        _transforms = transforms.ToArray();
    }

    internal string Transform(HtmlTransformContext context, string html)
    {
        foreach (var transform in _transforms)
        {
            html = transform.Transform(context, html);
        }

        return html;
    }
}
```

该接口必须保持 `internal`，不得移动到任何 Abstractions 或 PluginHost 项目。

### 7.6 显式 Provider 注册

```csharp
namespace Bukit.Engine.Analytics;

internal sealed class AnalyticsProviderRegistry
{
    private readonly IReadOnlyDictionary<string, IAnalyticsProvider> _providers;

    internal AnalyticsProviderRegistry(IEnumerable<IAnalyticsProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.Type,
            StringComparer.OrdinalIgnoreCase);
    }

    internal static AnalyticsProviderRegistry CreateDefault()
        => new(
        [
            new GoogleAnalyticsProvider(),
            new GoogleTagManagerProvider(),
            new PlausibleProvider(),
            new UmamiProvider()
        ]);

    internal IAnalyticsProvider GetRequired(string type)
        => _providers.TryGetValue(type, out var provider)
            ? provider
            : throw new ConfigException(
                $"Unsupported analytics provider type: {type}",
                DiagnosticCode.ConfigInvalidValue);
}
```

该设计允许测试注入 Fake Provider，但生产注册仍为显式静态列表。禁止使用程序集扫描、`Activator.CreateInstance`、反射发现 Provider或运行时 DLL 加载。

---

# 8. 实施任务

## Task 0：锁定代码基线与实际文件映射

**目标：** 在修改前确认当前分支仍与计划基线一致，并定位 dev 构建入口、架构边界测试和 Analytics 全部引用。

**修改文件：** 无。

- [ ] **Step 1：确认 Git 状态和提交**

```bash
git status --short
git rev-parse HEAD
```

预期：

- 工作区无与本任务无关的未提交改动；
- 输出提交 SHA，并记录到实施日志或 PR 描述。

- [ ] **Step 2：检索现有 Analytics 实现**

```bash
rg -n \
  "GoogleAnalyticsId|DisableInPreview|AnalyticsModel|InjectIntoHead|googletagmanager" \
  src/Bukit-Core tests guide README*.md
```

预期至少命中：

```text
src/Bukit-Core/Bukit.Config/AppConfig.cs
src/Bukit-Core/Bukit.Rendering/Models.cs
src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs
src/Bukit-Core/Bukit.Engine/SeoPipeline.cs
src/Bukit-Core/Bukit.Engine/SeoHtmlRenderer.cs
guide/user/04-site-yaml-config.md
```

- [ ] **Step 3：定位 dev 构建入口**

```bash
rg -n \
  "ConfigOverrides|BuildAsync\(|dev|LiveReload" \
  src/Bukit-Core/Bukit.Cli src/Bukit-Core/Bukit.Cli.Shared
```

预期：找到 `bukit dev` 调用 Core Build 的唯一或主要入口。将实际文件路径记录为后续 Task 5 的修改目标。

- [ ] **Step 4：定位插件架构边界测试**

```bash
rg -n \
  "PluginHost|Plugin.Abstractions|Engine.Abstractions|Architecture|dependency" \
  tests/Bukit.Architecture.Tests
```

预期：找到现有项目引用边界测试文件。将实际路径记录为 Task 8 修改目标。

- [ ] **Step 5：建立基线测试证据**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~SeoHtmlRendererTests|FullyQualifiedName~SeoPipelineTests"
```

预期：PASS。若失败，停止本计划实施，先记录基线失败，不得把既有失败混入 Analytics 变更。

---

## Task 1：扩展并严格验证 Analytics 配置契约

**目标：** 增加 typed Provider 配置、productionOnly、旧字段兼容和严格校验。

**文件：**

- Modify: `src/Bukit-Core/Bukit.Config/AppConfig.cs`
- Modify: `src/Bukit-Core/Bukit.Config/ConfigValidator.cs`
- Create: `src/Bukit-Core/Bukit.Config/AnalyticsConfigValidator.cs`
- Create: `src/Bukit-Core/Bukit.Config/AnalyticsConfigNormalizer.cs`
- Test: `tests/Bukit.Config.Tests/Analytics/AnalyticsConfigValidatorTests.cs`
- Test: `tests/Bukit.Config.Tests/Analytics/AnalyticsConfigNormalizerTests.cs`

**产出接口：**

```csharp
AnalyticsConfigValidator.Validate(AnalyticsConfig config)
ResolvedAnalyticsConfig AnalyticsConfigNormalizer.Normalize(AnalyticsConfig config)
```

- [ ] **Step 1：先编写配置失败测试**

至少覆盖：

```csharp
[Theory]
[InlineData("google-analytics", "BAD", null)]
[InlineData("google-tag-manager", null, "G-ABC")]
public void Validate_RejectsMalformedGoogleIdentifiers(
    string type,
    string? measurementId,
    string? containerId)
{
    var config = new AnalyticsConfig
    {
        Providers =
        [
            new AnalyticsProviderConfig
            {
                Type = type,
                MeasurementId = measurementId,
                ContainerId = containerId
            }
        ]
    };

    Assert.Throws<ConfigException>(() =>
        AnalyticsConfigValidator.Validate(config));
}

[Fact]
public void Validate_RejectsDuplicateLegacyAndProviderGa()
{
    var config = new AnalyticsConfig
    {
        GoogleAnalyticsId = "G-ABC123",
        Providers =
        [
            new AnalyticsProviderConfig
            {
                Type = "google-analytics",
                MeasurementId = "G-ABC123"
            }
        ]
    };

    Assert.Throws<ConfigException>(() =>
        AnalyticsConfigValidator.Validate(config));
}

[Fact]
public void Validate_RejectsHttpScriptUrl()
{
    var config = new AnalyticsConfig
    {
        Providers =
        [
            new AnalyticsProviderConfig
            {
                Type = "umami",
                WebsiteId = "00000000-0000-0000-0000-000000000000",
                ScriptUrl = "http://analytics.example.com/script.js"
            }
        ]
    };

    Assert.Throws<ConfigException>(() =>
        AnalyticsConfigValidator.Validate(config));
}
```

- [ ] **Step 2：运行测试并确认失败**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsConfig"
```

预期：FAIL，原因是新类型或验证器尚不存在。

- [ ] **Step 3：实现配置 records 与验证器**

验证器必须：

- 先处理全局字段冲突；
- 再逐 Provider 验证；
- 使用固定 `switch`；
- 拒绝未知 Provider；
- 拒绝 Provider 不适用字段；
- 拒绝重复唯一 Key；
- 错误信息包含完整配置路径，例如：

```text
site.analytics.providers[1].containerId must match ^GTM-[A-Z0-9]+$.
```

- [ ] **Step 4：把验证器接入 ConfigValidator**

在 `ConfigValidator.Validate` 中加入：

```csharp
AnalyticsConfigValidator.Validate(config.Site.Analytics);
```

位置应在站点基础配置验证后、构建执行前。

- [ ] **Step 5：实现旧字段规范化测试**

```csharp
[Fact]
public void Normalize_MapsLegacyGoogleAnalyticsIdToProvider()
{
    var resolved = AnalyticsConfigNormalizer.Normalize(new AnalyticsConfig
    {
        Enabled = true,
        GoogleAnalyticsId = "G-ABC123",
        DisableInPreview = true
    });

    Assert.True(resolved.Enabled);
    Assert.True(resolved.ProductionOnly);
    Assert.True(resolved.UsesLegacyFields);

    var provider = Assert.Single(resolved.Providers);
    Assert.Equal("google-analytics", provider.Type);
    Assert.Equal("G-ABC123", provider.Options["measurementId"]);
}
```

- [ ] **Step 6：实现规范化器**

规范化器要求：

- 不修改原始 `AnalyticsConfig`；
- 输出不可变 Provider 列表；
- 保留配置顺序；
- 旧 GA Provider 放在列表首位；
- Provider Key 必须稳定，例如：

```text
google-analytics:G-ABC123
google-tag-manager:GTM-ABC123
plausible:example.com
umami:00000000-0000-0000-0000-000000000000
```

- [ ] **Step 7：运行定向测试**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsConfig"
```

预期：PASS。

- [ ] **Step 8：运行仓库定向门禁**

```bash
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Config/AppConfig.cs \
  src/Bukit-Core/Bukit.Config/ConfigValidator.cs \
  src/Bukit-Core/Bukit.Config/AnalyticsConfigValidator.cs \
  src/Bukit-Core/Bukit.Config/AnalyticsConfigNormalizer.cs \
  tests/Bukit.Config.Tests/Analytics
```

预期：PASS。

- [ ] **Step 9：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Config \
  tests/Bukit.Config.Tests/Analytics
git commit -m "feat(config): add typed analytics providers"
```

---

## Task 2：建立 Core internal Analytics Provider 基础设施

**目标：** 创建不依赖反射、不进入外部插件协议的 Provider 契约与注册表。

**文件：**

- Create: `src/Bukit-Core/Bukit.Engine/Analytics/IAnalyticsProvider.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsHtmlFragments.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsRenderContext.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsProviderRegistry.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/AnalyticsProviderRegistryTests.cs`

- [ ] **Step 1：编写注册表测试**

```csharp
[Fact]
public void GetRequired_ReturnsExplicitlyRegisteredProvider()
{
    var registry = new AnalyticsProviderRegistry(
    [
        new FakeAnalyticsProvider("fake")
    ]);

    var provider = registry.GetRequired("FAKE");

    Assert.Equal("fake", provider.Type);
}

[Fact]
public void GetRequired_RejectsUnknownProvider()
{
    var registry = new AnalyticsProviderRegistry(Array.Empty<IAnalyticsProvider>());

    Assert.Throws<ConfigException>(() =>
        registry.GetRequired("custom-script"));
}
```

- [ ] **Step 2：运行测试确认失败**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsProviderRegistryTests"
```

预期：FAIL。

- [ ] **Step 3：实现内部 Provider 基础类型**

`AnalyticsRenderContext` 至少包含：

```csharp
internal sealed record AnalyticsRenderContext(
    string RouteUrl,
    string OutputPath,
    bool IsListPage,
    BuildExecutionMode ExecutionMode);
```

所有类型必须为 `internal`。

- [ ] **Step 4：实现显式注册表**

要求：

- 构造函数接收显式 Provider 实例；
- `CreateDefault()` 使用固定 Provider 列表；
- 类型 Key 大小写不敏感；
- 重复 type 在构造时失败；
- 无运行时扫描；
- 无 DI 容器动态发现；
- 未知类型抛 `ConfigException`。

- [ ] **Step 5：运行定向测试与门禁**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsProviderRegistryTests"

bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Analytics \
  tests/Bukit.Engine.Tests/Analytics/AnalyticsProviderRegistryTests.cs
```

预期：PASS。

- [ ] **Step 6：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Engine/Analytics \
  tests/Bukit.Engine.Tests/Analytics/AnalyticsProviderRegistryTests.cs
git commit -m "feat(engine): add internal analytics provider registry"
```

---

## Task 3：实现 GA4 与 GTM Provider

**目标：** 首先迁移现有 GA4 行为，并增加 GTM 的 head/body 双位置注入片段。

**文件：**

- Create: `src/Bukit-Core/Bukit.Engine/Analytics/GoogleAnalyticsProvider.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/GoogleTagManagerProvider.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/GoogleAnalyticsProviderTests.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/GoogleTagManagerProviderTests.cs`

- [ ] **Step 1：编写 GA4 输出测试**

```csharp
[Fact]
public void Render_ProducesManagedGa4HeadFragment()
{
    var provider = new GoogleAnalyticsProvider();
    var config = TestAnalytics.Provider(
        "google-analytics",
        ("measurementId", "G-ABC123"));

    var fragments = provider.Render(
        config,
        TestAnalytics.RenderContext());

    Assert.Contains(
        "https://www.googletagmanager.com/gtag/js?id=G-ABC123",
        fragments.HeadEnd,
        StringComparison.Ordinal);
    Assert.Contains("gtag('config', 'G-ABC123')", fragments.HeadEnd);
    Assert.Null(fragments.BodyStart);
}
```

- [ ] **Step 2：编写 GTM 输出测试**

```csharp
[Fact]
public void Render_ProducesHeadScriptAndBodyNoscript()
{
    var provider = new GoogleTagManagerProvider();
    var config = TestAnalytics.Provider(
        "google-tag-manager",
        ("containerId", "GTM-ABC123"));

    var fragments = provider.Render(
        config,
        TestAnalytics.RenderContext());

    Assert.Contains("GTM-ABC123", fragments.HeadEnd);
    Assert.Contains("googletagmanager.com/ns.html?id=GTM-ABC123", fragments.BodyStart);
    Assert.Contains("<noscript>", fragments.BodyStart);
}
```

- [ ] **Step 3：运行测试确认失败**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~GoogleAnalyticsProviderTests|FullyQualifiedName~GoogleTagManagerProviderTests"
```

预期：FAIL。

- [ ] **Step 4：实现 GA4 Provider**

要求：

- 保持当前 `gtag.js` 和 `gtag('config', ...)` 语义；
- 返回稳定 `ProviderKey`，例如 `google-analytics:G-ABC123`；
- 管理标记由 `AnalyticsHtmlRenderer` 统一生成；
- ID 必须分别经过 attribute 与 JavaScript string 编码；
- 不在 Provider 内读取文件、环境变量或网络。

- [ ] **Step 5：实现 GTM Provider**

要求：

- Head fragment 包含标准 GTM loader；
- BodyStart fragment 包含 `<noscript><iframe ...></iframe></noscript>`；
- iframe 必须含 `height="0" width="0" style="display:none;visibility:hidden"`；
- 两个位置均返回同一 `ProviderKey`，由 Renderer 包裹对应管理标记；
- 不使用任意用户脚本文本。

- [ ] **Step 6：把 GA4 与 GTM 加入 `AnalyticsProviderRegistry.CreateDefault()`**

使用显式 `new GoogleAnalyticsProvider()` 与 `new GoogleTagManagerProvider()`，不得使用扫描。

- [ ] **Step 7：运行定向测试与门禁**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~GoogleAnalyticsProviderTests|FullyQualifiedName~GoogleTagManagerProviderTests|FullyQualifiedName~AnalyticsProviderRegistryTests"

bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Analytics/GoogleAnalyticsProvider.cs \
  src/Bukit-Core/Bukit.Engine/Analytics/GoogleTagManagerProvider.cs \
  tests/Bukit.Engine.Tests/Analytics/GoogleAnalyticsProviderTests.cs \
  tests/Bukit.Engine.Tests/Analytics/GoogleTagManagerProviderTests.cs
```

预期：PASS。

- [ ] **Step 8：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsProviderRegistry.cs \
  src/Bukit-Core/Bukit.Engine/Analytics/GoogleAnalyticsProvider.cs \
  src/Bukit-Core/Bukit.Engine/Analytics/GoogleTagManagerProvider.cs \
  tests/Bukit.Engine.Tests/Analytics/GoogleAnalyticsProviderTests.cs \
  tests/Bukit.Engine.Tests/Analytics/GoogleTagManagerProviderTests.cs
git commit -m "feat(engine): add ga4 and gtm analytics providers"
```

---

## Task 4：实现 Plausible 与 Umami Provider

**目标：** 在相同 Provider 契约上增加两个无 Core 特例的扩展实现。

**文件：**

- Create: `src/Bukit-Core/Bukit.Engine/Analytics/PlausibleProvider.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/UmamiProvider.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/PlausibleProviderTests.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/UmamiProviderTests.cs`

- [ ] **Step 1：编写 Plausible 测试**

```csharp
[Fact]
public void Render_UsesDomainAndConfiguredHttpsScriptUrl()
{
    var provider = new PlausibleProvider();
    var config = TestAnalytics.Provider(
        "plausible",
        ("domain", "example.com"),
        ("scriptUrl", "https://plausible.io/js/script.js"));

    var fragments = provider.Render(config, TestAnalytics.RenderContext());

    Assert.Contains("data-domain=\"example.com\"", fragments.HeadEnd);
    Assert.Contains("src=\"https://plausible.io/js/script.js\"", fragments.HeadEnd);
    Assert.Null(fragments.BodyStart);
}
```

- [ ] **Step 2：编写 Umami 测试**

```csharp
[Fact]
public void Render_UsesWebsiteIdAndScriptUrl()
{
    var provider = new UmamiProvider();
    var config = TestAnalytics.Provider(
        "umami",
        ("websiteId", "00000000-0000-0000-0000-000000000000"),
        ("scriptUrl", "https://analytics.example.com/script.js"));

    var fragments = provider.Render(config, TestAnalytics.RenderContext());

    Assert.Contains(
        "data-website-id=\"00000000-0000-0000-0000-000000000000\"",
        fragments.HeadEnd);
    Assert.Contains(
        "src=\"https://analytics.example.com/script.js\"",
        fragments.HeadEnd);
}
```

- [ ] **Step 3：运行测试确认失败**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~PlausibleProviderTests|FullyQualifiedName~UmamiProviderTests"
```

预期：FAIL。

- [ ] **Step 4：实现两个 Provider**

要求：

- 只产生 `HeadEnd`；
- 返回稳定 `ProviderKey`，管理标记由 Renderer 生成；
- 不访问网络；
- 不允许内联用户脚本；
- 所有属性值 HTML 编码；
- Provider 不重复执行配置校验，只依赖已规范化的有效输入。

- [ ] **Step 5：把 Plausible 与 Umami 加入 `AnalyticsProviderRegistry.CreateDefault()`**

保持 Provider 顺序为：GA4、GTM、Plausible、Umami。实际输出顺序仍由用户配置顺序决定。

- [ ] **Step 6：运行测试与门禁**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~PlausibleProviderTests|FullyQualifiedName~UmamiProviderTests|FullyQualifiedName~AnalyticsProviderRegistryTests"

bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Analytics/PlausibleProvider.cs \
  src/Bukit-Core/Bukit.Engine/Analytics/UmamiProvider.cs \
  tests/Bukit.Engine.Tests/Analytics/PlausibleProviderTests.cs \
  tests/Bukit.Engine.Tests/Analytics/UmamiProviderTests.cs
```

预期：PASS。

- [ ] **Step 7：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsProviderRegistry.cs \
  src/Bukit-Core/Bukit.Engine/Analytics/PlausibleProvider.cs \
  src/Bukit-Core/Bukit.Engine/Analytics/UmamiProvider.cs \
  tests/Bukit.Engine.Tests/Analytics/PlausibleProviderTests.cs \
  tests/Bukit.Engine.Tests/Analytics/UmamiProviderTests.cs
git commit -m "feat(engine): add plausible and umami providers"
```

---

## Task 5：建立 HTML Transform Pipeline 并从 SEO 解耦 Analytics

**目标：** 在页面写盘前组合 SEO 和 Analytics Transform，使 Analytics 不再依赖 SEO 开关。

**文件：**

- Create: `src/Bukit-Core/Bukit.Engine/Html/IHtmlTransform.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Html/HtmlTransformContext.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Html/HtmlTransformPipeline.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsHtmlRenderer.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsInjectionPolicy.cs`
- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsPlugin.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoPipeline.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/SeoHtmlRenderer.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/HtmlHeadScanner.cs`
- Modify: `src/Bukit-Core/Bukit.Rendering/Models.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/AnalyticsHtmlRendererTests.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/AnalyticsPipelineTests.cs`
- Modify test: `tests/Bukit.Engine.Tests/SeoHtmlRendererTests.cs`
- Modify test: `tests/Bukit.Engine.Tests/SeoPipelineTests.cs`

### 5.1 注入算法

`AnalyticsHtmlRenderer` 必须：

1. 扫描并移除所有 `bukit:analytics:<provider>` 管理块；
2. 在一个兼容周期内移除旧版 Core 生成、但没有管理标记的 GA4 external/inline 脚本；
3. 由 Renderer 根据 `ProviderKey` 生成 start/end 管理标记，Provider 不直接生成标记；
4. 将 `HeadEnd` 插入 `</head>` 前；
5. 将 `BodyStart` 插入 `<body ...>` 开始标签后；
6. 不重建缺失的 HTML 结构；
7. 若需要的 slot 不存在，记录 skip reason；
8. 保留非 Bukit 管理的其他脚本；
9. 第二次执行结果与第一次一致。

- [ ] **Step 1：编写 HTML 注入与幂等测试**

```csharp
[Fact]
public void Inject_AddsHeadAndBodyFragmentsExactlyOnce()
{
    const string html = "<html><head></head><body class=\"page\"><main>ok</main></body></html>";
    var fragments = new AnalyticsHtmlFragments
    {
        HeadEnd = "<!-- bukit:analytics:test:start --><script src=\"/a.js\"></script><!-- bukit:analytics:test:end -->",
        BodyStart = "<!-- bukit:analytics:test-body:start --><noscript>x</noscript><!-- bukit:analytics:test-body:end -->"
    };

    var once = AnalyticsHtmlRenderer.Inject(html, [fragments]);
    var twice = AnalyticsHtmlRenderer.Inject(once.Html, [fragments]);

    Assert.Equal(once.Html, twice.Html);
    Assert.Equal(1, CountOccurrences(twice.Html, "src=\"/a.js\""));
    Assert.Equal(1, CountOccurrences(twice.Html, "<noscript>x</noscript>"));
}
```

- [ ] **Step 2：编写缺失 slot 测试**

```csharp
[Fact]
public void Inject_WhenBodyIsMissing_InjectsHeadAndReportsBodySkip()
{
    const string html = "<html><head></head><main>ok</main></html>";
    var fragments = new AnalyticsHtmlFragments
    {
        HeadEnd = "<script src=\"/a.js\"></script>",
        BodyStart = "<noscript>x</noscript>"
    };

    var result = AnalyticsHtmlRenderer.Inject(html, [fragments]);

    Assert.Contains("src=\"/a.js\"", result.Html);
    Assert.DoesNotContain("<noscript>x</noscript>", result.Html);
    Assert.Contains("body_missing", result.SkipReasons);
}
```

- [ ] **Step 3：编写 SEO 关闭时 Analytics 仍注入的集成测试**

```csharp
[Fact]
public async Task Build_WhenSeoDisabled_StillInjectsAnalytics()
{
    var fixture = await AnalyticsBuildFixture.CreateAsync(new SiteConfig
    {
        Name = "test",
        Title = "Test",
        Seo = new SeoConfig { Enabled = false },
        Analytics = TestAnalytics.GoogleAnalytics("G-ABC123")
    });

    var result = await fixture.BuildAsync(BuildExecutionMode.Production);
    var html = await File.ReadAllTextAsync(result.HomeHtmlPath);

    Assert.Contains("gtag/js?id=G-ABC123", html);
    Assert.DoesNotContain("property=\"og:title\"", html);
}
```

- [ ] **Step 4：运行测试确认失败**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsHtmlRendererTests|FullyQualifiedName~AnalyticsPipelineTests"
```

预期：FAIL。

- [ ] **Step 5：实现内部 HTML Transform Pipeline**

约束：

- `IHtmlTransform`、Context、Pipeline 全部 `internal`；
- Pipeline 顺序固定：SEO 后处理完成后执行 Analytics；
- Analytics 不依赖 `SeoModel`；
- Transform 只处理当前页面字符串，不访问输出目录。

- [ ] **Step 6：扩展 `HtmlHeadScanner` 的安全扫描能力**

增加 `TryFindBodyStart` 或等价内部方法，必须跳过 comments、script/style/title/textarea raw text，并正确处理带引号的 `>`。不得用单条正则查找 `<body>`。

- [ ] **Step 7：实现 AnalyticsPlugin**

核心逻辑应等价于：

```csharp
internal sealed class AnalyticsPlugin : IHtmlTransform
{
    private readonly ResolvedAnalyticsConfig _config;
    private readonly AnalyticsBuildSummaryCollector _summary;

    public string Name => "analytics";

    internal AnalyticsPlugin(
        ResolvedAnalyticsConfig config,
        AnalyticsBuildSummaryCollector summary)
    {
        _config = config;
        _summary = summary;
    }

    public string Transform(HtmlTransformContext context, string html)
    {
        var decision = AnalyticsInjectionPolicy.Evaluate(_config, context);
        if (!decision.ShouldInject)
        {
            _summary.RecordSkip(decision.Reason);
            return AnalyticsHtmlRenderer.RemoveManagedBlocks(html);
        }

        var registry = AnalyticsProviderRegistry.CreateDefault();
        var fragments = _config.Providers
            .Select(provider => registry
                .GetRequired(provider.Type)
                .Render(provider, context.ToAnalyticsContext()))
            .ToArray();

        var result = AnalyticsHtmlRenderer.Inject(html, fragments);
        _summary.RecordResult(result);
        return result.Html;
    }
}
```

- [ ] **Step 8：从 SeoHtmlRenderer 移除 Analytics 责任**

必须完成：

- `SeoHtmlRenderer.InjectIntoHead` 不再接收 `AnalyticsModel`；
- `SeoHtmlRenderer.RenderHead` 不再渲染 GA；
- `IsManagedTag` 不再负责识别 GA；
- `SeoPipeline.Execute` 不再接收 Analytics 参数；
- SEO 测试只验证 SEO。

- [ ] **Step 9：在 VariantBuildPipeline 组合 transforms**

预期结构：

```csharp
var analyticsConfig = AnalyticsConfigNormalizer.Normalize(config.Site.Analytics);
var analyticsSummary = new AnalyticsBuildSummaryCollector();
var analyticsPlugin = new AnalyticsPlugin(analyticsConfig, analyticsSummary);

var htmlTransforms = new HtmlTransformPipeline(
[
    analyticsPlugin
]);
```

SEO 仍可继续使用现有 postprocessor，但最终传给 `RenderPipelineContext` 的 page/list processor 必须组合为：

```csharp
html =>
{
    var seoProcessed = seoProcessor is null
        ? html
        : seoProcessor(..., html);

    return htmlTransforms.Transform(context, seoProcessed);
}
```

- [ ] **Step 10：覆盖 Page、List 和 Static HTML 路径**

当前 `PageRenderDispatcher` 的三种 RenderEntry：

- `Page`
- `List`
- `Static`

都必须通过同一 Analytics Transform。不得只覆盖内容详情页和列表页。

- [ ] **Step 11：运行 SEO 回归与 Analytics 测试**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~SeoHtmlRendererTests|FullyQualifiedName~SeoPipelineTests|FullyQualifiedName~Analytics"
```

预期：PASS。

- [ ] **Step 12：运行定向门禁**

```bash
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Html \
  src/Bukit-Core/Bukit.Engine/Analytics \
  src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs \
  src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs \
  src/Bukit-Core/Bukit.Engine/SeoPipeline.cs \
  src/Bukit-Core/Bukit.Engine/SeoHtmlRenderer.cs \
  src/Bukit-Core/Bukit.Engine/HtmlHeadScanner.cs \
  src/Bukit-Core/Bukit.Rendering/Models.cs \
  tests/Bukit.Engine.Tests/Analytics \
  tests/Bukit.Engine.Tests/SeoHtmlRendererTests.cs \
  tests/Bukit.Engine.Tests/SeoPipelineTests.cs
```

预期：PASS。

- [ ] **Step 13：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Engine \
  src/Bukit-Core/Bukit.Rendering/Models.cs \
  tests/Bukit.Engine.Tests/Analytics \
  tests/Bukit.Engine.Tests/SeoHtmlRendererTests.cs \
  tests/Bukit.Engine.Tests/SeoPipelineTests.cs
git commit -m "refactor(engine): decouple analytics from seo injection"
```

---

## Task 6：增加 Production/Development 构建语义

**目标：** 让 `productionOnly` 有明确执行依据，并保证 `bukit dev` 不污染正式统计数据。

**文件：**

- Create: `src/Bukit-Core/Bukit.Config/BuildExecutionMode.cs`
- Modify: `src/Bukit-Core/Bukit.Config/ConfigOverrides.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/PreviewCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Commands/PreviewAnalyticsFilter.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/AnalyticsPipelineTests.cs`
- Modify test: `tests/Bukit.Cli.Tests/DevCommandTests.cs`
- Modify test: `tests/Bukit.Cli.Tests/PreviewCommandTests.cs`

### 6.1 模式定义

```csharp
namespace Bukit.Config;

public enum BuildExecutionMode
{
    Production = 0,
    Development = 1
}
```

在 `ConfigOverrides` 中增加：

```csharp
public BuildExecutionMode ExecutionMode { get; init; }
    = BuildExecutionMode.Production;
```

### 6.2 行为矩阵

| 场景 | ExecutionMode | `productionOnly: true` | `productionOnly: false` |
|---|---|---:|---:|
| `bukit build` | Production | 注入 | 注入 |
| CI build | Production | 注入 | 注入 |
| `bukit dev` | Development | 不注入 | 注入 |
| `bukit preview` | 不构建 | 响应中移除 Bukit 管理块，磁盘不变 | 原样服务既有产物 |

- [ ] **Step 1：编写 InjectionPolicy 测试**

```csharp
[Theory]
[InlineData(BuildExecutionMode.Production, true, true)]
[InlineData(BuildExecutionMode.Development, true, false)]
[InlineData(BuildExecutionMode.Development, false, true)]
public void Evaluate_RespectsProductionOnly(
    BuildExecutionMode mode,
    bool productionOnly,
    bool expected)
{
    var config = TestAnalytics.Resolved(productionOnly: productionOnly);
    var context = TestAnalytics.TransformContext(mode);

    var decision = AnalyticsInjectionPolicy.Evaluate(config, context);

    Assert.Equal(expected, decision.ShouldInject);
}
```

- [ ] **Step 2：运行测试确认失败**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsPipelineTests"
```

预期：FAIL。

- [ ] **Step 3：实现 BuildExecutionMode 和 Policy**

禁用时必须移除由 Bukit 管理的 Analytics 块，防止增量处理沿用旧片段。

- [ ] **Step 4：让 `bukit dev` 显式设置 Development**

在 `src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs` 的 `CreateBuildOverrides` 中设置：

```csharp
return new ConfigOverrides
{
    Clean = clean,
    Output = outputOverride,
    Incremental = true,
    CacheDir = cacheDir,
    ExecutionMode = BuildExecutionMode.Development
};
```

不得通过环境变量、命令名字符串或调用栈猜测当前模式。

- [ ] **Step 5：确认普通 build 默认 Production**

不增加额外 CLI 参数。普通 `bukit build` 依赖 `ConfigOverrides` 默认值。

- [ ] **Step 6：通用化 dev/preview 响应过滤**

创建 `PreviewAnalyticsFilter`：

- 优先移除 `<!-- bukit:analytics:*:start --> ... <!-- bukit:analytics:*:end -->` 管理块；
- 在一个兼容周期内继续移除旧版无标记 GA4 脚本；
- 只修改响应字符串，不修改磁盘文件；
- 对已无 Analytics 的 HTML 保持幂等。

修改 `PreviewCommand.ApplyPreviewAnalyticsPolicy` 与 `DevRequestHandler` 调用该过滤器。

- [ ] **Step 7：编写 CLI/集成测试**

至少验证：

- `DevCommand.CreateBuildOverrides` 返回 Development；
- 普通 build 默认 Production；
- preview 在 productionOnly=true 时移除 GA/GTM/Plausible/Umami 管理块；
- preview 不修改磁盘文件；
- productionOnly=false 时响应不变。

- [ ] **Step 8：运行测试与门禁**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Analytics"

dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~DevCommandTests|FullyQualifiedName~PreviewCommandTests|FullyQualifiedName~Analytics"

bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Config/BuildExecutionMode.cs \
  src/Bukit-Core/Bukit.Config/ConfigOverrides.cs \
  src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/PreviewCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs \
  src/Bukit-Core/Bukit.Cli/Commands/PreviewAnalyticsFilter.cs \
  tests/Bukit.Engine.Tests/Analytics \
  tests/Bukit.Cli.Tests/DevCommandTests.cs \
  tests/Bukit.Cli.Tests/PreviewCommandTests.cs
```

预期：PASS。

- [ ] **Step 9：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Config/BuildExecutionMode.cs \
  src/Bukit-Core/Bukit.Config/ConfigOverrides.cs \
  src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/PreviewCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs \
  src/Bukit-Core/Bukit.Cli/Commands/PreviewAnalyticsFilter.cs \
  tests/Bukit.Engine.Tests/Analytics \
  tests/Bukit.Cli.Tests/DevCommandTests.cs \
  tests/Bukit.Cli.Tests/PreviewCommandTests.cs
git commit -m "feat(cli): apply analytics policy to dev and preview"
```

---

## Task 7：接入增量构建依赖与 Analytics 构建摘要

**目标：** Analytics 配置变化触发页面重渲染，并在构建报告中提供不泄漏敏感信息的摘要。

**文件：**

- Create: `src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsBuildSummary.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHasher.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/BuildVariantResult.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/BuildReporter.cs`
- Modify: `src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/AnalyticsIncrementalBuildTests.cs`
- Test: `tests/Bukit.Engine.Tests/Analytics/AnalyticsBuildSummaryTests.cs`

### 7.1 摘要字段

```csharp
internal sealed record AnalyticsBuildSummary
{
    public bool Enabled { get; init; }
    public IReadOnlyList<string> ProviderTypes { get; init; }
        = Array.Empty<string>();
    public int InjectedPages { get; init; }
    public IReadOnlyDictionary<string, int> SkippedByReason { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);
}
```

报告中只允许输出 Provider type，不输出完整 ID、域名中的敏感路径或完整 script URL。

### 7.2 Skip reasons 固定值

```text
analytics_disabled
no_providers
development_mode
head_missing
body_missing
```

- [ ] **Step 1：编写增量构建测试**

```csharp
[Fact]
public async Task ChangingAnalyticsId_ForcesHtmlRerender()
{
    await using var fixture = await AnalyticsBuildFixture.CreateAsync();

    var first = await fixture.BuildAsync(
        measurementId: "G-FIRST1",
        incremental: true);

    var second = await fixture.BuildAsync(
        measurementId: "G-SECOND2",
        incremental: true);

    Assert.Contains("G-SECOND2", second.HomeHtml);
    Assert.DoesNotContain("G-FIRST1", second.HomeHtml);
    Assert.Contains(
        "render_dependency_changed",
        second.BuildResult.RenderReasons.Keys);
}
```

- [ ] **Step 2：运行测试确认失败**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~AnalyticsIncrementalBuildTests|FullyQualifiedName~AnalyticsBuildSummaryTests"
```

预期：FAIL。

- [ ] **Step 3：把 resolved Analytics 加入 render dependency hash**

将 `RenderDependencyHasher.Compute` 扩展为接收规范化 Analytics 与 `BuildExecutionMode`，或把等价稳定值写入专用 helper。Hash 输入必须包括：

- `enabled`；
- `productionOnly`；
- 当前 `BuildExecutionMode`；
- 当前模式下的有效注入决策；
- Provider 顺序；
- Provider type；
- Provider 的规范化 options。

不得包含：

- 构建时间；
- 随机值；
- Logger 状态；
- 运行时对象 hash code。

- [ ] **Step 4：实现线程安全摘要 collector**

由于页面并行渲染，collector 必须使用：

- `Interlocked`；
- `ConcurrentDictionary<string, int>`；
- 只读 Snapshot。

- [ ] **Step 5：接入 BuildVariantResult 与 BuildReporter**

`BuildReporter` 当前手写 `Utf8JsonWriter`，因此直接增加稳定字段，不引入反射序列化或新的 source-generation 依赖。建议 JSON 报告结构：

```json
{
  "analytics": {
    "enabled": true,
    "providers": ["google-analytics", "google-tag-manager"],
    "injectedPages": 128,
    "skippedByReason": {
      "body_missing": 1
    }
  }
}
```

- [ ] **Step 6：运行测试与门禁**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Analytics"

bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsBuildSummary.cs \
  src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHasher.cs \
  src/Bukit-Core/Bukit.Engine/BuildVariantResult.cs \
  src/Bukit-Core/Bukit.Engine/BuildReporter.cs \
  src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs \
  tests/Bukit.Engine.Tests/Analytics
```

预期：PASS。

- [ ] **Step 7：提交**

```bash
git add \
  src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsBuildSummary.cs \
  src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHasher.cs \
  src/Bukit-Core/Bukit.Engine/BuildVariantResult.cs \
  src/Bukit-Core/Bukit.Engine/BuildReporter.cs \
  src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs \
  tests/Bukit.Engine.Tests/Analytics
git commit -m "feat(engine): report analytics injection results"
```

---

## Task 8：增加外部插件边界架构测试

**目标：** 用自动化测试锁定 Analytics 只能存在于 Core internal，禁止未来误暴露给外部插件。

**文件：**

- Create: `tests/Bukit.Architecture.Tests/AnalyticsBoundaryTests.cs`

- [ ] **Step 1：编写边界测试**

至少验证：

1. `Bukit.Plugin.Abstractions` 不引用 `Bukit.Engine`。
2. `Bukit.PluginHost` 不引用 `Bukit.Engine.Analytics`。
3. `IAnalyticsProvider` 不是 public。
4. `IHtmlTransform` 不是 public。
5. External plugin protocol 中没有 HTML body、rendered HTML、output directory write capability。
6. Analytics Provider 只位于 `Bukit.Engine` 程序集。

示例：

```csharp
[Fact]
public void AnalyticsContracts_AreNotPublicPluginApi()
{
    var engineAssembly = typeof(SiteEngine).Assembly;

    var analyticsTypes = engineAssembly.GetTypes()
        .Where(type => type.Namespace?.StartsWith(
            "Bukit.Engine.Analytics",
            StringComparison.Ordinal) is true)
        .ToArray();

    Assert.NotEmpty(analyticsTypes);
    Assert.All(analyticsTypes, type => Assert.False(type.IsPublic));
}
```

若 Native AOT 架构测试规则禁止 `Assembly.GetTypes()`，则使用已有的项目引用/源码边界测试模式实现同等约束，不把反射引入生产代码。

- [ ] **Step 2：运行测试确认失败或缺少覆盖**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Analytics|FullyQualifiedName~Plugin"
```

- [ ] **Step 3：修正任何错误可见性或项目引用**

禁止通过放宽测试来通过。若类型意外为 public，应改为 internal。

- [ ] **Step 4：运行门禁**

```bash
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Engine/Analytics \
  src/Bukit-Core/Bukit.Engine/Html \
  src/Bukit-Core/Bukit.Plugin.Abstractions \
  src/Bukit-Core/Bukit.PluginHost \
  tests/Bukit.Architecture.Tests
```

预期：PASS。

- [ ] **Step 5：提交**

```bash
git add tests/Bukit.Architecture.Tests
git commit -m "test(architecture): lock analytics plugin boundary"
```

---

## Task 9：更新配置 Schema、用户文档与示例

**目标：** 让 `bukit config schema`、配置参考和用户指南与代码一致。

**文件：**

- Modify: `guide/user/04-site-yaml-config.md`
- Create: `guide/user/14-analytics.md`
- Modify: `guide/user/README.md`
- Create: `examples/analytics/site.yaml`
- Modify: `README.md`
- Modify: `README.zh-CN.md`
- Modify: `README.ms.md`
- Test: 使用现有 config schema 和 docs gate。

- [ ] **Step 1：更新字段参考**

新增字段：

```text
site.analytics.productionOnly
site.analytics.providers[]
site.analytics.providers[].type
site.analytics.providers[].measurementId
site.analytics.providers[].containerId
site.analytics.providers[].domain
site.analytics.providers[].websiteId
site.analytics.providers[].scriptUrl
```

旧字段标注：

```text
site.analytics.googleAnalyticsId   deprecated, compatibility only
site.analytics.disableInPreview    deprecated, compatibility only
```

- [ ] **Step 2：编写 Analytics 用户指南**

`guide/user/14-analytics.md` 必须包括：

1. 适用场景；
2. Provider 配置；
3. dev/build/preview 行为；
4. GTM head/body 行为；
5. 严格校验错误示例；
6. 旧配置迁移；
7. 安全限制；
8. 不支持 arbitrary JavaScript；
9. 与主题 partial 的边界；
10. 与外部插件的边界；
11. 验证命令。

验证命令：

```bash
bukit config check
bukit doctor
bukit build --clean
```

- [ ] **Step 3：增加完整示例**

`examples/analytics/site.yaml` 必须包含：

- 最小有效站点配置；
- GA4；
- GTM；
- 注释形式的 Plausible 与 Umami 示例；
- 不包含真实生产 ID 或密钥。

- [ ] **Step 4：生成并检查 Schema**

```bash
dotnet run --project src/Bukit-Core/Bukit.Cli -c Release -- \
  config schema > /tmp/bukit-site.schema.json

rg -n \
  'productionOnly|providers|measurementId|containerId|websiteId|scriptUrl' \
  /tmp/bukit-site.schema.json
```

预期：所有新字段存在，未知字段仍被拒绝。

- [ ] **Step 5：运行文档定向门禁**

```bash
bash scripts/checks/post-change-targeted.sh -- \
  guide/user/04-site-yaml-config.md \
  guide/user/14-analytics.md \
  guide/user/README.md \
  examples/analytics/site.yaml \
  README.md \
  README.zh-CN.md \
  README.ms.md
```

预期：PASS。

- [ ] **Step 6：提交**

```bash
git add \
  guide/user/04-site-yaml-config.md \
  guide/user/14-analytics.md \
  guide/user/README.md \
  examples/analytics/site.yaml \
  README.md \
  README.zh-CN.md \
  README.ms.md
git commit -m "docs: add analytics provider configuration guide"
```

---

## Task 10：完整集成验证、Native AOT 验证与只读综合审计

**目标：** 在不运行用户未授权的 full/release gate 前提下，完成 Analytics 父任务的最终定向验收。

**文件：** 不新增功能代码；只允许修复本计划范围内发现的问题。

### 10.1 集成测试矩阵

| 编号 | 场景 | 预期 |
|---|---|---|
| A-01 | Analytics disabled | 无任何 Bukit Analytics 管理块 |
| A-02 | 无 Provider | 不注入，报告 `no_providers` |
| A-03 | 旧 GA 配置 | 生成与旧语义等价的 GA4 脚本 |
| A-04 | 新 GA Provider | Head 中恰好一份 GA4 |
| A-05 | GTM | Head loader + BodyStart noscript 各一份 |
| A-06 | Plausible | Head 中正确 domain/script |
| A-07 | Umami | Head 中正确 websiteId/script |
| A-08 | SEO disabled | Analytics 正常注入 |
| A-09 | SEO renderMode off | Analytics 正常注入 |
| A-10 | dev + productionOnly | 无注入 |
| A-11 | dev + productionOnly=false | 正常注入 |
| A-12 | production build | 正常注入 |
| A-13 | HTML 无 head | Head Provider 跳过并报告 |
| A-14 | HTML 无 body | GTM noscript 跳过并报告 |
| A-15 | 重复处理 HTML | 无重复脚本 |
| A-16 | Provider 配置变化 | 增量构建重新渲染 |
| A-17 | 多语言构建 | 每个语言变体正确注入 |
| A-18 | Static HTML route | 正确注入 |
| A-19 | 非法 ID | `config check` 失败 |
| A-20 | HTTP scriptUrl | `config check` 失败 |
| A-21 | 外部插件项目 | 无新增 Engine Analytics 依赖 |
| A-22 | Native AOT publish | 成功，无反射发现警告 |

- [ ] **Step 1：运行 Config 定向测试**

```bash
dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Analytics"
```

预期：PASS。

- [ ] **Step 2：运行 Engine 定向测试**

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Analytics|FullyQualifiedName~SeoHtmlRendererTests|FullyQualifiedName~SeoPipelineTests"
```

预期：PASS。

- [ ] **Step 3：运行 CLI 定向测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Dev|FullyQualifiedName~Config|FullyQualifiedName~Analytics"
```

预期：PASS。

- [ ] **Step 4：运行架构边界测试**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release \
  --filter "FullyQualifiedName~Analytics|FullyQualifiedName~Plugin"
```

预期：PASS。

- [ ] **Step 5：运行最终定向门禁**

```bash
bash scripts/checks/post-change-targeted.sh -- \
  src/Bukit-Core/Bukit.Config \
  src/Bukit-Core/Bukit.Engine/Analytics \
  src/Bukit-Core/Bukit.Engine/Html \
  src/Bukit-Core/Bukit.Engine/VariantBuildPipeline.cs \
  src/Bukit-Core/Bukit.Engine/PageRenderDispatcher.cs \
  src/Bukit-Core/Bukit.Engine/SeoPipeline.cs \
  src/Bukit-Core/Bukit.Engine/SeoHtmlRenderer.cs \
  src/Bukit-Core/Bukit.Engine/HtmlHeadScanner.cs \
  src/Bukit-Core/Bukit.Rendering/Models.cs \
  tests/Bukit.Config.Tests/Analytics \
  tests/Bukit.Engine.Tests/Analytics \
  tests/Bukit.Architecture.Tests \
  guide/user/04-site-yaml-config.md \
  guide/user/14-analytics.md \
  examples/analytics/site.yaml
```

预期：PASS。

- [ ] **Step 6：执行 Native AOT 定向发布验证**

先根据当前平台选择仓库已支持的 RID。Linux x64 验证命令：

```bash
dotnet publish src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  -c Release \
  -r linux-x64 \
  -p:PublishAot=true \
  --self-contained true \
  -o /tmp/bukit-analytics-aot
```

预期：

- 发布成功；
- 无 Analytics 相关 trimming/reflection 警告；
- 产物可执行 `version` 和 `config check`。

- [ ] **Step 7：执行只读综合审计**

审计范围：

1. 所有 Task 的 diff 是否严格在范围内；
2. 是否存在 external Plugin API 扩大；
3. 是否存在任意脚本注入；
4. Provider 校验是否完整；
5. GA/GTM 编码是否存在 XSS 风险；
6. 并行构建 summary 是否线程安全；
7. 增量 hash 是否覆盖所有有效配置；
8. SEO disabled/off/theme 模式是否仍支持 Analytics；
9. Static/List/Page 是否全部覆盖；
10. 文档与 schema 是否一致；
11. 是否修改了 backup/reference 目录；
12. 是否有与 Analytics 无关的重构。

审计只能：

- 阅读 diff；
- 收集证据；
- 推荐定向检查；

不得：

- 修改文件；
- 创建新提交；
- 扩大任务范围。

- [ ] **Step 8：处理审计问题并复跑受影响门禁**

只复跑受影响的定向测试和定向门禁，不直接运行整个 release gate。

- [ ] **Step 9：最终提交**

若审计修复产生改动：

```bash
git add \
  src/Bukit-Core/Bukit.Config \
  src/Bukit-Core/Bukit.Engine/Analytics \
  src/Bukit-Core/Bukit.Engine/Html \
  src/Bukit-Core/Bukit.Engine/Incremental/RenderDependencyHasher.cs \
  src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/PreviewCommand.cs \
  src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs \
  src/Bukit-Core/Bukit.Cli/Commands/PreviewAnalyticsFilter.cs \
  tests/Bukit.Config.Tests/Analytics \
  tests/Bukit.Engine.Tests/Analytics \
  tests/Bukit.Architecture.Tests/AnalyticsBoundaryTests.cs
git commit -m "fix(engine): close analytics audit findings"
```

若无改动，不创建空提交。

---

# 9. 验收标准

## 9.1 功能验收

- [ ] 支持 GA4。
- [ ] 支持 GTM。
- [ ] 支持 Plausible。
- [ ] 支持 Umami。
- [ ] GTM 能正确注入 Head 和 BodyStart。
- [ ] 同一 Provider 不重复注入。
- [ ] 多 Provider 按配置顺序输出。
- [ ] SEO disabled 时 Analytics 仍工作。
- [ ] SEO renderMode 非 inject 时 Analytics 仍工作。
- [ ] dev 默认不注入 production-only Analytics。
- [ ] production build 正常注入。
- [ ] 多语言、列表页、内容页和 static route 全部覆盖。

## 9.2 配置验收

- [ ] 未知 Provider 被拒绝。
- [ ] Provider 缺少必填字段被拒绝。
- [ ] 非法 GA/GTM ID 被拒绝。
- [ ] 非 HTTPS script URL 被拒绝。
- [ ] 重复 Provider 被拒绝。
- [ ] 旧配置仍可使用。
- [ ] 旧字段与新字段冲突时明确失败。
- [ ] `bukit config schema` 包含所有新字段。
- [ ] 未知配置字段仍被拒绝。

## 9.3 安全验收

- [ ] 无 arbitrary JavaScript 字段。
- [ ] ID 同时进行 HTML attribute 与 JS string 编码。
- [ ] scriptUrl 仅允许绝对 HTTPS。
- [ ] Provider 不访问网络或文件系统。
- [ ] 外部插件无法获取页面 HTML。
- [ ] 外部插件无法调用 Analytics Core 类型。
- [ ] `IAnalyticsProvider` 和 HTML Transform 均为 internal。
- [ ] 不增加运行时 DLL 加载或程序集扫描。

## 9.4 稳定性验收

- [ ] 重复注入幂等。
- [ ] 缺失 head/body 不产生损坏 HTML。
- [ ] 并行构建没有计数竞态。
- [ ] Analytics 配置变化触发增量重渲染。
- [ ] 配置不变时增量构建可正常跳过。
- [ ] 失败不留下部分 after-build 重写产物。
- [ ] Native AOT 发布成功。

## 9.5 文档验收

- [ ] 用户指南与实际 schema 一致。
- [ ] README 不宣称外部插件可注入 HTML。
- [ ] dev/build/preview 语义写清楚。
- [ ] 旧字段弃用策略写清楚。
- [ ] 示例不含真实 ID 或密钥。

---

# 10. 风险与控制措施

| 风险 | 影响 | 控制措施 |
|---|---|---|
| Analytics 仍被 SEO 开关阻断 | 功能错误 | 独立 Transform + SEO disabled 集成测试 |
| GTM body 注入破坏 HTML | 页面异常 | 使用现有 scanner 思路，覆盖 attributes/comments/scripts |
| Provider 重复注入 | 重复统计 | 管理标记 + 规范化唯一 Key + 幂等测试 |
| 旧配置行为改变 | 兼容性回归 | 旧字段规范化 + golden HTML 测试 |
| dev 污染统计数据 | 数据质量下降 | 显式 BuildExecutionMode |
| 任意 script URL 带来供应链风险 | 前端安全风险 | 仅 HTTPS、无任意代码、文档警告、自托管由用户负责 |
| 外部插件协议被扩大 | Core 边界破坏 | 架构测试锁定，无 render hook |
| Incremental hash 未覆盖 Provider | 旧脚本残留 | resolved config 纳入依赖 hash |
| 并行 summary 竞态 | 报告不准确 | Interlocked + ConcurrentDictionary |
| Provider 反射发现破坏 AOT | 发布失败 | 静态注册表 + AOT publish 验证 |

---

# 11. 回滚策略

1. 每个任务独立提交，避免一次性大提交。
2. Task 1 先增加兼容配置，不立即删除旧行为。
3. Task 5 完成并通过回归后，才移除 `SeoHtmlRenderer` 中旧 GA 注入。
4. 若新管线出现阻塞问题：
   - 回滚 Task 5 及之后提交；
   - 保留 Task 1 的配置类型时必须确保旧运行路径不会读取新 Provider；
   - 不通过把 HTML Transform 暴露给外部插件来临时绕过问题。
5. 不采用双写两套 Analytics 脚本的临时方案。
6. 不保留长期隐藏 feature flag；功能稳定后应删除临时迁移开关。

---

# 12. 发布建议

建议拆为两个可独立验收的版本波次：

## 波次 A：基础设施与 Google Provider

包含：

- 配置 Provider 化；
- 旧字段兼容；
- HTML Transform Pipeline；
- SEO 解耦；
- GA4；
- GTM；
- dev/production 模式；
- 增量与报告；
- 架构边界测试。

该波次可先进入稳定版本。

## 波次 B：Plausible 与 Umami

包含：

- Plausible；
- Umami；
- 自托管 HTTPS script URL 文档；
- Provider 扩展回归。

若希望一次发布，也必须保留 Task 4 的独立提交和独立验收门禁。

---

# 13. 最终完成定义（Definition of Done）

只有同时满足以下条件，才可宣布任务完成：

1. 所有任务范围内代码已提交；
2. 所有定向测试通过；
3. 所有定向门禁通过；
4. Native AOT 定向发布验证通过；
5. 只读综合审计无未解决问题；
6. 外部插件协议未变化；
7. `Bukit.PluginHost` 与 `Bukit.Plugin.Abstractions` 未获得 Analytics/HTML Transform 能力；
8. SEO disabled/off/theme 模式下 Analytics 行为符合矩阵；
9. 旧 GA 配置兼容；
10. 新 Provider 配置严格验证；
11. 文档、Schema、示例和实现保持一致；
12. 未修改备份目录；
13. 未混入与 Analytics 无关的重构；
14. 最终报告列出：
    - 变更文件；
    - 支持 Provider；
    - 兼容策略；
    - 测试命令与结果；
    - AOT 结果；
    - 审计结论；
    - 未进入本期的后续事项。

---

# 14. 后续可选增强

以下事项不阻塞本计划完成，可在后续独立立项：

1. 基于 route pattern 的 Analytics 排除规则；
2. 单页面 `analytics_inject: false`；
3. Consent Mode v2；
4. CSP nonce/hash 集成；
5. Microsoft Clarity Provider；
6. Matomo Provider；
7. Analytics 验证命令；
8. 部署后脚本可达性检查；
9. Analytics API 报表外部进程插件；
10. Notion/数据库统计同步外部插件。

其中第 7–10 项若实现，应作为独立命令或外部进程插件，不得反向扩大 Core 页面渲染接口。
