# Bukit Analytics 内置插件详细开发计划

> 本文档是 Analytics 内置插件的唯一实施基线。实现必须严格遵守本文档定义的插件边界、配置硬切换、安全约束和验证顺序。

**文档版本：** v2.0

**修订日期：** 2026-07-15

**适用项目：** Bukit 静态网站生成引擎

**基线提交：** `b4498a27604a9e08972661d08360868322268f05`

**交付性质：** 原子 breaking change，不提供旧配置或旧渲染模型兼容

---

## 一、结论与架构决策

### 1.1 现状问题

当前 Analytics 方案不能直接实施，原因如下：

- 旧方案中的 `AnalyticsPlugin : IHtmlTransform` 只是一个 HTML Transform，没有注册到 `BuiltInPluginSource`，不属于现有 Bukit 内置插件生命周期。
- 旧方案保留了 `googleAnalyticsId`、`disableInPreview`、旧字段规范化和弃用警告，与本次彻底移除、禁止兼容的要求冲突。
- 旧 Analytics 通过 `AnalyticsModel → site.analytics → Theme/SEO` 传递，没有实现 Core 内置插件所有权。
- Analytics 部分依赖 `SeoPipeline`，SEO 关闭或使用 Theme 模式时可能阻断 Analytics。
- 当前 Render Pipeline 没有对 Static Render Entry 执行统一 HTML 后处理；仅修改 content/list delegate 无法覆盖全部 HTML。
- 向冻结的 `build-report.v1` 增加顶层字段会破坏现有严格 Schema。
- `guide/user/14-analytics.md` 与现有 `14-troubleshooting.md` 编号冲突，新用户文档必须使用 `guide/user/19-analytics.md`。

### 1.2 最终架构

Analytics 必须是正式注册的 Core 内置插件：

```text
BuiltInPluginSource
  └── AnalyticsPlugin
        ├── IBukitPlugin
        ├── IOrderedPlugin
        ├── IHookFilterPlugin
        └── IHtmlTransformPlugin（Bukit.Engine internal）
                  │
                  ▼
PluginRegistry → PluginRunner.CollectHtmlTransforms()
                  │
                  ▼
HtmlTransformPipeline
  1. SeoHtmlTransform（Core Transform）
  2. AnalyticsHtmlTransform（内置插件贡献）
                  │
                  ▼
Content / List / Static HTML → 写入磁盘
```

架构边界锁定如下：

- `AnalyticsPlugin` 位于 `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/`，注册名为 `analytics`，版本为 `1.0.0`，顺序为 `1000`。
- `IHtmlTransformPlugin`、`IHtmlTransform`、Transform Context 和 Analytics Provider 接口全部保持 `Bukit.Engine` 内部类型。
- 不修改 `Bukit.Engine.Abstractions` 的公开插件生命周期。
- 不向 `Bukit.PluginHost`、`Bukit.Plugin.Abstractions` 或 `bukit-plugin-v1` 暴露页面 HTML、Provider 或 Transform 能力。
- 不使用程序集扫描、反射发现、运行时 DLL 加载或外部进程逐页处理 HTML。
- `AnalyticsPlugin` 只负责注册元数据和创建每个 Build Variant 独立的 Transform，不保存跨构建可变状态。

### 1.3 启用规则

采用现有内置插件语义下的双层开关：

```text
插件生命周期开关：site.plugins.analytics.enabled
功能输出开关：    site.analytics.enabled
```

有效注入条件固定为：

```text
pluginEnabled
&& analytics.enabled
&& providers.Count > 0
&& (executionMode == Production || productionOnly == false)
```

默认值：

- `site.plugins.analytics.enabled` 未配置时为 `true`。
- `site.analytics.enabled` 为 `true`。
- `site.analytics.productionOnly` 为 `true`。
- `site.analytics.providers` 为空数组，因此默认不输出脚本。
- 任一开关为 `false` 都不注入。
- 通用插件开关为 `false` 时，不创建 Analytics Transform，也不产生 `html-transform` 插件执行记录。

---

## 二、配置、接口与输出契约

### 2.1 唯一支持的配置契约

```yaml
site:
  plugins:
    analytics:
      enabled: true

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

公开配置类型调整为：

```csharp
public sealed record AnalyticsConfig
{
    public bool Enabled { get; init; } = true;
    public bool ProductionOnly { get; init; } = true;
    public IReadOnlyList<AnalyticsProviderConfig> Providers { get; init; }
        = Array.Empty<AnalyticsProviderConfig>();
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

### 2.2 旧配置必须彻底移除

必须删除：

- `AnalyticsConfig.GoogleAnalyticsId`
- `AnalyticsConfig.DisableInPreview`
- YAML Loader 对 `googleAnalyticsId`、`disableInPreview` 的读取
- JSON Schema 中的两个旧字段
- 旧字段规范化、映射、弃用警告和冲突判断
- `DevCommand.ResolveDisableAnalytics`
- Preview 中旧 GA4 脚本识别逻辑
- 旧字段对应的文档、示例和正向测试

严格字段验证只允许：

```text
site.analytics:
  enabled
  productionOnly
  providers
```

旧字段出现在 YAML 时，必须由现有严格字段验证器直接报告未知字段：

- 不得静默忽略。
- 不得映射到 Provider。
- 不得降级为弃用警告。
- 不得根据旧字段推断新字段。
- 不得通过环境变量或内部 fallback 恢复旧行为。

生产代码中不得再出现旧属性或旧字段读取逻辑。旧字段字符串只允许存在于：

- 验证其必须失败的负向契约测试；
- 明确说明已删除的 breaking-change 文档。

### 2.3 Provider 验证矩阵

| Provider | 必填字段 | 允许的可选字段 | 注入点 |
|---|---|---|---|
| `google-analytics` | `measurementId` | 无 | HeadEnd |
| `google-tag-manager` | `containerId` | 无 | HeadEnd、BodyStart |
| `plausible` | `domain` | `scriptUrl` | HeadEnd |
| `umami` | `websiteId`、`scriptUrl` | 无 | HeadEnd |

验证规则：

- `type` 只接受表中的小写 kebab-case 精确值。
- `measurementId` 匹配 `^G-[A-Z0-9]+$`。
- `containerId` 匹配 `^GTM-[A-Z0-9]+$`。
- `domain` 经 IDN ASCII 规范化后必须为合法 DNS 主机名；禁止 scheme、端口、路径、查询、片段、用户信息和 IP 地址。
- `websiteId` 必须为 UUID。
- `scriptUrl` 必须为绝对 HTTPS URL；禁止凭据、片段、非默认脚本路径和非 `.js` 路径。
- Plausible 未提供 `scriptUrl` 时使用 `https://plausible.io/js/script.js`。
- Provider 对象不得携带不属于该类型的字段。
- Provider 唯一键分别由 type 加 measurementId/containerId/domain/websiteId 构成；相同唯一键重复时配置失败。
- 多 Provider 按 YAML 顺序输出。
- 所有属性值使用 HTML attribute 编码；GA 固定 JavaScript 模板中的值使用专用 JavaScript string 编码。
- Provider 不读取文件、不访问网络、不接受任意 JavaScript 字符串。

### 2.4 删除 Theme/Scriban Analytics API

完全移除：

- `Bukit.Rendering.AnalyticsModel`
- `SiteModel.Analytics`
- Scriban Binder 中的 `site.analytics`
- `ScribanModelKnownFields.AnalyticsFields`
- Starter Theme 的 `AnalyticsPartial.html`
- SEO Theme 模式中由主题自行输出 Analytics 的说明和测试

这是明确的公开渲染模型破坏性变更：

- 不提供旧属性别名。
- 不保留空 Analytics Model。
- 不提供 Theme fallback。
- Theme 和 Scriban 模板不能读取 Analytics 配置。
- Analytics 只能由 Core 内置插件输出。

### 2.5 内部插件和 Transform 接口

增加 Engine 内部生命周期：

```csharp
internal interface IHtmlTransformPlugin
{
    IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context);
}

internal sealed record HtmlTransformPluginContext(
    BuildContext BuildContext,
    BuildExecutionMode ExecutionMode);

internal interface IHtmlTransform
{
    string Name { get; }
    string Transform(HtmlTransformContext context, string html);
}
```

`AnalyticsPlugin` 的契约为：

```csharp
internal sealed class AnalyticsPlugin :
    IBukitPlugin,
    IOrderedPlugin,
    IHookFilterPlugin,
    IHtmlTransformPlugin
{
    public string Name => "analytics";
    public string Version => "1.0.0";
    public int Order => 1000;
}
```

实现要求：

- `BuiltInPluginSource.GetPlugins()` 只注册一个 `AnalyticsPlugin`。
- Hook 名固定为 `html-transform`，由 Engine 内部常量维护，不扩展公开 `PluginCapability`。
- `PluginRunner` 使用现有插件排序、注册缓存和 `site.plugins.<name>.enabled` 规则收集 Transform。
- 每个 Build Variant 创建独立 `AnalyticsHtmlTransform` 和线程安全统计状态；缓存的插件对象不得保存跨构建可变状态。
- Transform 异常遵循 `site.pluginFailMode`：
  - `strict`：记录失败并重新抛出；
  - `warn`：该次调用返回进入插件前的 HTML，聚合一次警告，后续 Transform 继续。
- 每个 Variant 最终只生成一条 `PluginExecutionInfo("analytics", "html-transform", ...)`。
- 执行时间为各页面调用耗时总和；任一页面失败则执行结果为失败。

### 2.6 HTML 管线与幂等协议

现有 SEO 后处理 delegate 收敛为统一 `HtmlTransformPipeline`：

- SEO Model 构建仍由 `SeoPipeline` 负责。
- SEO HTML 注入改为 `SeoHtmlTransform`。
- Core Transform 始终先于插件 Transform。
- Analytics 是否运行不依赖 `site.seo.enabled` 或 `site.seo.renderMode`。
- `PageRenderDispatcher` 必须在写文件前，对 Content、List、Static 三类 Render Entry 统一调用管线。

Analytics 管理块格式固定为：

```html
<!-- bukit:analytics:{provider-key}:head:start -->
...
<!-- bukit:analytics:{provider-key}:head:end -->
```

GTM Body 块对应使用 `body` 标识：

```html
<!-- bukit:analytics:{provider-key}:body:start -->
...
<!-- bukit:analytics:{provider-key}:body:end -->
```

每次 Transform：

1. 只移除所有格式正确的当前 Bukit Analytics 管理块。
2. 不识别、不移除任何无标记 GA/GTM/Plausible/Umami 脚本。
3. 根据当前配置重新生成管理块。
4. Head 片段写入 `</head>` 前。
5. BodyStart 片段写入带任意属性的 `<body ...>` 起始标签后。
6. 无 `<head>` 时只跳过 Head 片段；无 `<body>` 时只跳过 BodyStart 片段。
7. 格式损坏或不配对的注释视为普通用户 HTML，不猜测、不修复。
8. 重复执行结果必须完全一致。

### 2.7 构建模式和 Preview

增加公开枚举及覆盖值：

```csharp
public enum BuildExecutionMode
{
    Production = 0,
    Development = 1
}
```

`ConfigOverrides.ExecutionMode` 默认 `Production`；`bukit dev` 显式设置 `Development`。

行为矩阵：

| 场景 | `productionOnly: true` | `productionOnly: false` |
|---|---|---|
| `bukit build` | 注入 | 注入 |
| CI build | 注入 | 注入 |
| `bukit dev` | 不注入 | 注入 |
| `bukit preview` | 仅从 HTTP 响应移除当前 Bukit 管理块 | 原样响应 |

Preview/Dev 响应过滤器：

- 只处理本方案定义的管理注释块。
- 不处理旧版无标记 GA 脚本。
- 不写回磁盘。
- 对无管理块响应保持字节级不变。
- 不重新引入 `disableInPreview` 概念。

### 2.8 增量构建依赖

Render Dependency Hash 必须加入以下稳定规范化值：

- Analytics 插件通用开关；
- `enabled`；
- `productionOnly`；
- `BuildExecutionMode`；
- Provider 顺序、类型、唯一键和规范化 options。

禁止加入：

- 时间；
- 随机值；
- 对象 HashCode；
- Logger 状态；
- 任何与渲染结果无关的进程状态。

### 2.9 独立 Analytics 报告

不得修改冻结的 `build-report.v1` 顶层结构。新增独立报告：

```text
.bukit/analytics-report.json
docs/schemas/analytics-report.v1.schema.json
```

报告字段固定为：

```json
{
  "schema": "https://bukit.dev/schemas/analytics-report.v1.json",
  "schemaVersion": "1.0",
  "pluginEnabled": true,
  "analyticsEnabled": true,
  "productionOnly": true,
  "executionMode": "production",
  "providerTypes": ["google-analytics"],
  "processedHtml": 10,
  "injectedHtml": 10,
  "skippedByReason": {
    "incremental_unchanged": 2
  }
}
```

报告约束：

- 统计表示本次 Variant 构建执行情况，不冒充磁盘全量扫描结果。
- Provider 只输出 type，不输出 ID、domain 或 script URL。
- Skip reason 固定为：
  - `plugin_disabled`
  - `analytics_disabled`
  - `no_providers`
  - `development_mode`
  - `head_missing`
  - `body_missing`
  - `incremental_unchanged`
  - `transform_failed`
- 计数器使用 `Interlocked` 和 `ConcurrentDictionary`。
- 多语言构建在各 Variant 输出目录写各自报告。
- `build.report.enabled: false` 时不写 Analytics 报告。
- Analytics 报告由现有 Artifact Manifest 自动收录。

---

## 三、分阶段实施任务

### Task 0：修订计划基线

- 重写目标实施计划中的“内置插件”定义。
- 删除全部兼容期、弃用期、双字段映射和分波发布设计。
- 把 `AnalyticsPlugin` 改成真实注册插件，Transform 改成插件贡献能力。
- 修正报告 Schema、Static HTML、用户指南编号和当前真实文件路径。
- 记录执行基线：配置契约、Engine SEO/Hash、CLI Dev/Preview 定向测试共 106 项通过。
- 此任务只修改计划文档，不修改代码。

完成证据：

- 目标计划不再描述旧字段迁移或兼容。
- `AnalyticsPlugin` 的注册链明确为 `BuiltInPluginSource → PluginRegistry → PluginRunner`。
- `build-report.v1` 明确保持不变。
- Git diff 只包含本计划文档。

### Task 1：完成配置契约硬切换

- 修改 `AnalyticsConfig` 并增加 `AnalyticsProviderConfig`。
- 更新 YAML Loader、严格字段验证、语义验证和动态 JSON Schema。
- 删除旧 GA ID 验证和两个旧属性。
- 增加四种 Provider 的有效、无效、缺字段、多余字段、重复键测试。
- 增加旧字段必须抛出未知字段错误的负向测试。
- 运行 Config 定向测试和显式路径的 `post-change-targeted.sh`。
- 因为这是公开配置契约破坏性变更，门禁通过后立即执行一次只读高风险审计。

### Task 2：实现 Provider 和安全渲染基础设施

- 在 `Bukit.Engine/Analytics` 建立内部 resolved config、Normalizer、Provider Registry、Provider Context 和 Fragment 模型。
- 按静态列表注册 GA4、GTM、Plausible、Umami。
- 实现固定 HTML 模板、编码器和 Provider 唯一键。
- Provider 单元测试使用 Golden HTML，并验证无任意 JavaScript、无网络和文件访问。
- 运行 Analytics Provider 定向测试及路径门禁。

### Task 3：接入真实内置插件生命周期

- 增加 Engine-internal `IHtmlTransformPlugin` 和 Hook 常量。
- 实现 `AnalyticsPlugin`，注册到 `BuiltInPluginSource`。
- 扩展 `PluginRunner` 收集、排序、启停、包装和汇总 HTML Transform。
- 实现双层启用规则和 `pluginFailMode` 行为。
- 增加 Registry 缓存、只注册一次、顺序、通用插件禁用和执行记录测试。
- 运行 PluginRegistry/PluginRunner/Analytics 定向测试及路径门禁。
- 因为修改插件生命周期边界，门禁通过后执行一次只读高风险审计。

### Task 4：统一 HTML Transform Pipeline

- 建立 `HtmlTransformPipeline` 和文档类型 Context。
- 把 SEO 注入从 `SeoHtmlRenderer` 的 Analytics 参数中剥离，并包装成 Core `SeoHtmlTransform`。
- 修改 Render Pipeline，使 Content、List、Static 在写盘前统一经过 Transform。
- Analytics Transform 排在 SEO 后，但不受 SEO 启用状态控制。
- 覆盖无 head/body、复杂 body 属性、注释/script 内容、重复执行和多 Provider 顺序。
- 运行 SEO、Render Pipeline、Static、Analytics 定向测试及路径门禁。

### Task 5：移除 Rendering/Theme 旧表面

- 删除 `AnalyticsModel`、`SiteModel.Analytics`、Scriban Binder 字段和 Known Fields。
- 删除 Starter Theme Analytics Partial。
- 删除 SEO/Theme 模式对 Analytics Model 的参数传递。
- 将原有主题输出测试改为“Analytics 只能由内置插件输出”。
- 增加 Binder 测试，确认 `site.analytics` 不再进入 Scriban Model。
- 运行 Rendering、SEO、Starter Theme 定向测试及路径门禁。
- 该任务属于公开渲染 API 删除，完成后执行只读高风险审计。

### Task 6：实现 Development/Preview 语义

- 增加 `BuildExecutionMode` 和 `ConfigOverrides.ExecutionMode`。
- `bukit dev` 显式使用 Development。
- Build 默认使用 Production。
- Dev/Preview 共享只识别当前管理块的响应过滤器。
- 删除 `ResolveDisableAnalytics` 及所有旧 GA 脚本识别。
- 覆盖响应过滤、磁盘不变、productionOnly 开关和无管理块幂等。
- 运行 CLI Dev/Preview 和 Engine Injection Policy 定向测试及路径门禁。

### Task 7：接入增量依赖和 Analytics 报告

- 使用 resolved Analytics 配置扩展 Render Dependency Hash。
- 实现线程安全 Analytics Build State 和 Summary Snapshot。
- 写入 `analytics-report.json`，增加严格 Schema 和 Schema 契约测试。
- 不增加 `build-report.v1` 字段。
- 验证 Provider、模式、插件开关变化强制重渲染；配置不变可继续增量跳过。
- 验证报告不泄漏 ID、domain 和 URL。
- 运行 Incremental、Reporter、Schema 定向测试及路径门禁。

### Task 8：锁定外部插件边界

新增架构测试，明确验证：

- `AnalyticsPlugin` 来自 `BuiltInPluginSource`，Source 为 `built-in`。
- Analytics 实现 `IBukitPlugin` 和 Engine-internal HTML Hook。
- HTML Transform 和 Analytics Provider 类型没有进入任何公开 Abstractions。
- `PluginRegistry` 仍只加载 `BuiltInPluginSource`。
- `PluginHost` 和 `bukit-plugin-v1` 没有 HTML、页面或输出文件写入能力。
- 没有反射 Provider 发现或运行时程序集加载。
- Native AOT 静态分析不依赖动态类型发现。

运行 Architecture 定向测试及路径门禁。

### Task 9：更新主线文档和示例

更新主线文档：

- `guide/user/04-site-yaml-config.md`
- `guide/user/16-parameter-cheatsheet.md`
- 新增 `guide/user/19-analytics.md` 并更新用户指南索引
- `guide/dev/built-in-plugins.md`
- `guide/dev/rendering-scriban.md`
- `guide/dev/config-site-yaml.md`
- 当前 `docs/seo.md`、Analytics 学习材料及本实施计划

文档必须明确：

- Analytics 是 Core 内置插件，不是外部协议插件。
- Theme 无法读取 `site.analytics`。
- 两级开关的区别和优先级。
- 四种 Provider、Development/Preview 行为和安全限制。
- 两个旧字段已经删除，而非 deprecated。
- 示例不包含真实站点 ID、域名或密钥。

不得修改或引用 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 作为官方来源。

### Task 10：完整定向验收与综合审计

按顺序执行：

1. Config Analytics 契约测试。
2. Provider 和 HTML Pipeline 测试。
3. Plugin Registry/Runner 测试。
4. Rendering/Scriban/SEO 回归。
5. CLI Dev/Preview 测试。
6. Incremental/Report/Schema 测试。
7. Architecture 边界测试。
8. `bash scripts/checks/post-change-targeted.sh -- <本任务全部实际变更路径>`。
9. 使用当前平台 RID 对 `Bukit.Cli` 做一次定向 Native AOT publish，输出到临时目录。
10. 用真实最小站点分别构建四种 Provider，检查最终 HTML 和 Analytics 报告。
11. 创建一个只读子代理审计全部子任务、聚合 diff、跨任务遗漏和无关改动。
12. 主线程处理审计问题，并只重跑受影响门禁及必要的复审。

未经用户另行要求，不运行：

- `scripts/gates/ci-full.sh`
- `scripts/gates/release.sh`
- `scripts/test-all.sh`
- `scripts/smoke-all.sh`
- `dotnet test bukit-test.slnx`
- 任何 whole-solution `.slnx` 测试

---

## 四、测试与验收矩阵

### 4.1 配置验收

- 四种 Provider 有效配置可加载。
- 两个旧字段均严格失败。
- 未知 Provider、未知字段、缺少必填字段、携带其他 Provider 字段均失败。
- ID、UUID、domain、scriptUrl 边界全部覆盖。
- 重复唯一键失败，不同唯一键保持配置顺序。
- 动态配置 Schema 与 Loader、Validator 一致。

### 4.2 插件验收

- Analytics 在 BuiltIn Plugin Source 中恰好出现一次。
- 通用插件开关关闭时不创建 Transform。
- 功能开关关闭时插件存在但不注入。
- Analytics 执行记录 Hook 为 `html-transform`。
- `strict` 与 `warn` 失败模式符合定义。
- 并行渲染下执行时间和失败状态无竞态。

### 4.3 HTML 验收

- GA4、GTM、Plausible、Umami 输出正确。
- GTM 同时输出 Head 和 BodyStart。
- 多 Provider 按配置顺序输出。
- Content、List、Static 全部覆盖。
- SEO disabled、SEO theme/off 模式下 Analytics 仍工作。
- 管理块重复处理幂等。
- 配置删除 Provider 后，旧的当前管理块不残留。
- 无标记第三方脚本保持不变。
- 缺失 head/body 不损坏 HTML。
- HTML 属性和 JavaScript 字符串编码正确。

### 4.4 模式与增量验收

- Production 始终按有效配置注入。
- Development 默认跳过 production-only Provider。
- Preview 只过滤当前管理块且不写磁盘。
- `productionOnly: false` 在 Dev/Preview 保留 Analytics。
- Analytics 配置、模式或插件开关变化触发重渲染。
- 配置不变时增量构建继续命中缓存。

### 4.5 报告与边界验收

- Analytics 报告通过 `analytics-report.v1` Schema。
- 报告不包含任何 Provider 标识值或 URL。
- `build-report.v1` Schema 和结构保持不变。
- 外部插件协议无新增 Hook、DTO、权限或页面 HTML 能力。
- Native AOT 发布成功。
- 主线源码中不存在旧属性或兼容读取逻辑。
- 备份目录无改动。
- 最终只读综合审计无未解决问题。

---

## 五、子任务门禁与审计纪律

- 每个代码子任务完成后，先运行该子任务的定向测试，再运行：

  ```bash
  bash scripts/checks/post-change-targeted.sh -- <该子任务实际变更路径>
  ```

- 工作树存在无关改动时，必须显式传入本子任务路径，不依赖自动 diff 检测。
- 门禁失败时停止进入下一子任务，修复当前子任务并重新运行其定向验证。
- 配置契约、插件生命周期、公开 Rendering API、并发统计等高风险子任务，在定向门禁通过后立即执行一次边界明确的只读审计。
- 全部子任务通过后，执行一次只读子代理综合审计，覆盖每个子任务证据和聚合 diff。
- 子代理只能审计、收集证据和建议定向检查；不得修改文件、提交、启动新用户任务或扩展范围。
- 综合审计发现问题时，由主线程修复，并重新运行受影响门禁和必要复审。

---

## 六、发布、回滚与完成定义

### 6.1 发布方式

- 本变更作为一次原子 breaking release 交付，不拆分兼容波次。
- 不设置旧字段兼容期、弃用期、隐藏 feature flag 或双写路径。
- 文档、Schema、配置模型、运行时和测试必须在同一交付中同步切换。

### 6.2 回滚方式

- 需要回滚时，只能整体回滚本次发布或相关提交。
- 不得通过恢复旧字段读取、旧 Theme Model、旧 Scriban API 或无标记脚本识别实现临时兼容。

### 6.3 完成标准

同时满足以下条件才可声明完成：

- 所有四种 Provider 已实现并通过安全验证。
- Analytics 已由真实内置插件生命周期驱动。
- `AnalyticsPlugin` 已注册到 `BuiltInPluginSource` 并由 `PluginRegistry`/`PluginRunner` 调度。
- Content、List、Static HTML 全部经过统一 Transform Pipeline。
- 旧配置与旧 Scriban/Theme API 已彻底删除。
- 所有定向测试和路径门禁通过。
- Analytics 独立报告通过严格 Schema，`build-report.v1` 保持不变。
- 当前平台 Native AOT 定向验证通过。
- 配置、插件和公开 API 的高风险审计通过。
- 最终聚合只读审计无未解决问题。
- 主线文档、Schema、示例和实现完全一致。
- `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 无改动。
