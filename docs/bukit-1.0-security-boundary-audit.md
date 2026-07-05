# Bukit 1.0 安全边界审计（Spark 检查结果）

## 范围

本次审计覆盖执行路径：`route` / `theme` / `plugin` / `media` / `output` / `外部输入/URL`，并确认当前实现是否在 GA-locked 失效路径上具备可追踪诊断码与拒绝行为。

## 审计结论（逐域）

### Route / Output

- **边界入口**
  - `src/Bukit.Routing/RouteSecurityValidator.cs`
  - `src/Bukit.Engine/Output/SafePathResolver.cs`
  - `src/Bukit.Engine/BuildPathUtils.cs`
  - `src/Bukit.Engine/BuildPlanner.cs`（构建输出清理边界）
- **已覆盖风险**
  - 内部 URL 规范化与逃逸：`..`、空段、绝对路径、control 字符、协议相对 URL 拒绝。
  - 输出路径安全：强制相对路径、拒绝绝对路径和 drive-qualified 路径；写入路径必须在 output root 之内。
  - 清理安全：clean 模式需 `.bukit-output-marker`，防止清理非 Bukit 目录。
- **结论**
  - **通过：** 核心边界存在且与 `BKT-020x` 约定一致。

### Theme

- **边界入口**
  - `src/Bukit.Engine/ThemePathResolver.cs`
  - `src/Bukit.Engine/ThemeTemplateResolver.cs`
  - `src/Bukit.Cli/Cli/BukitCliThemeSpecs.cs` 与主题命令路径
- **已覆盖风险**
  - 主题名清洗与未知 extends 降级策略；
  - 远程主题路径优先与本地回退；
  - 主题模板目录与 parent 目录解析在 `BuildPathUtils` 约束内；
  - `theme.yaml` 缺失处理与 fallback 路径边界。
- **结论**
  - **通过：** 主题解析与继承层级有明确拒绝/回退路径，但仍建议在 1.0 发布文案中强调远程主题安全前提（lock + 策略）。

### Plugin

- **边界入口**
  - `src/Bukit-Core/Bukit.PluginHost/PluginManifestLoader.cs`
  - `src/Bukit-Core/Bukit.PluginHost/PluginHashVerifier.cs`
  - `src/Bukit-Core/Bukit.PluginHost/PluginProtocolClient.cs`
  - `src/Bukit-Core/Bukit.PluginHost/PluginProcessInvoker.cs`
  - `src/Bukit-Core/Bukit.PluginHost/PluginPermissionEvaluator.cs`
- **已覆盖风险**
  - handshake 协议版本与 capabilities 必填检查；
  - plugin entry hash 校验（`sha256`）；
  - 运行时环境白名单（`BUKIT_*` 透传 + 显式 allow list）；
  - 超时与 stdout/stderr 限制，超限中断；
  - 结果码与错误封装转为 `ConfigException`。
- **结论**
  - **通过：** 插件执行链的关键边界均有拒绝和可观测行为，当前正式路径为 `Bukit.PluginHost` + `bukit-plugin-v1`，legacy Labs Protocol 已删除。

### Media / Download

- **边界入口**
  - `src/Bukit.Content/Media/ImageAssetLocalizer.cs`
  - `src/Bukit.Content/Media/SsrfGuard.cs`
  - `src/Bukit.Shared/SafeUrl.cs`
  - `src/Bukit.Content/Notion/*.cs`（Notion 媒体与富媒体处理）
- **已覆盖风险**
  - SSRF 与私网地址防护（`BlockPrivateNetworks` + `SsrfGuard`）；
  - 内容类型限制与文件大小限制；
  - 默认下载目录 + `defaultImageUrl` 回退，避免无界下载中断；
  - URL 模式白名单（media/link/embed）与降级渲染。
- **结论**
  - **通过：** 媒体边界可控，风险点主要在未开启 `BlockPrivateNetworks` 时对下载源的信任范围，当前 1.0 需保守默认和文档约束。

## 风险清单（仍需决策或追踪）

1. **非 build 命令的 `--log-format` 语义与文档一致性**
   - 已支持作为 CLI 错误 envelope 开关，建议在 `help` 与文档中补充为 `global` 选项。
2. **`security-report.json` 仍需完整接入真实安全扫描结果**
   - 当前写入是占位实现，`BuildReporter.WriteSecurityReport` 需在后续任务补齐真实输入。
3. **CLI JSON envelope 的 code 与诊断对象路径**
   - 当前异常/解析 JSON 已返回 `code`，但 `command` 下游规范建议后续补充 `errorCode` 与 `path` 的字段标准化。


## T7.3 逐模块安全边界执行清单（Spark 审计）

以下清单按任务分解，便于下一步变更审计时逐条复核：

1. Route / Output
- [x] 安全路径边界：`RouteSecurityValidator` 与 `SafePathResolver` 覆盖 `..`、绝对路径、控制字符与 drive 路径。
- [x] 输出清理边界：`BuildPlanner` 仅在 `.bukit-output-marker` 下执行清理，防止误删站外目录。
- [x] 严重风险：`route` 冲突/冲出边界拒绝后会返回结构化 `BKT-020x` 诊断。

2. Theme
- [x] `theme.yaml` 缺失与未知主题名边界：`ThemeBootstrapper` 与 `ThemePathResolver` 给出明确拒绝/回退行为。
- [x] `fallbackDir` 与模板回退：`ThemeTemplateResolver` / `FileTemplateLoader` 约束回退目录不越界。
- [x] 远程/本地主题来源安全：由主题源解析链控制，未配置 `lock` 的构建路径不应作为默认信任入口。

3. Plugin
- [x] `capabilities` 与 `protocol` 边界：`PluginManifestLoader` 与 `PluginProtocolClient` 在 manifest/handshake 不合规时拒绝。
- [x] 运行时隔离边界：`PluginProcessInvoker` 限制进程调用、超时、stdout/stderr 上限。
- [x] 外部输入 hash/签名链：`PluginHashVerifier`、`PluginCiPolicy`、lock/report 执行层记录 plugin entry hash。

4. Media / URL
- [x] URL 协议与私网边界：`SafeUrl` / `SsrfGuard` 拒绝私网与危险协议。
- [x] 媒体下载边界：默认目录与文件大小限制覆盖未受信来源。
- [x] 降级策略：`media/link/embed` 仅允许白名单模式，不安全链接不注入。

## 结论与残留项

- **T7.3 结论：** 路由、主题、插件、媒体四大核心域边界和诊断行为均可追溯，当前阶段可标记为通过（可审计）。
- **残留风险（待下一阶段）：** `security-report.json` 的真实扫描输入尚未与构建结果打通；该项继续列入下个里程碑。
