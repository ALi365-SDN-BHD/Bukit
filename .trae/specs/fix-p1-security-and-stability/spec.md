# 修复 P1 安全与稳定性问题 Spec

## Why

审计报告 `bukit-deep-audit-report-2026-05-29.md` 识别出 5 个 P1 级别的核心稳定性问题：3 个安全漏洞（XSS、SSRF）、1 个性能/可靠性问题（异步阻塞），需要在不引入回归的前提下集中修复。本次仅修复 P1-1、P1-2、P1-3、P1-4、P1-6（P1-5 嵌套并行已在性能审计中单独跟踪）。

## What Changes

- **P1-1 (XSS)**: ShortcodeProcessor 对 shortcode 参数值在替换到模板前进行 HTML 属性安全编码
- **P1-2 (XSS)**: 5 个 BlockRenderer (Callout/ToDo/Toggle/Bookmark/Equation) 替换 `GetBlockColor()` 用法为预编码的 `GetBlockColorClass()` 或对颜色值进行显式编码
- **P1-3 (SSRF, 验证)**: 验证 `MediaConfig.BlockPrivateNetworks` 默认值已为 `true`（实际已修复，需补充测试确认行为）
- **P1-4 (性能/可靠性)**: 消除 `IncrementalBuildEngine.ComputeListItemHash` 中 `ContentBodyResolver.GetHtml` 的 `GetAwaiter().GetResult()` 阻塞调用，改为异步链路
- **P1-6 (SSRF)**: `CloneCommand` 与 `SeoExternalAuditor` 的 `HttpClient` 默认启用 `SsrfGuard.SsrfSafeConnectAsync`

## Impact

- **Affected specs**: core-hardening-p0-p1, body-cache-decorator, incremental-hash-coverage
- **Affected code**:
  - `src/Bukit.Shared/ShortcodeProcessor.cs`
  - `src/Bukit.Content/Notion/BlockRenderers/{Callout,ToDo,Toggle,Bookmark,Equation}BlockRenderer.cs`
  - `src/Bukit.Content/Notion/BlockRenderers/NotionBlockHelpers.cs`
  - `src/Bukit.Engine/Incremental/IncrementalBuildEngine.cs`
  - `src/Bukit.Engine.Abstractions/ContentBodyResolver.cs`
  - `src/Bukit.Engine/PageRenderDispatcher.cs`（消费方调用链）
  - `src/Bukit.Cli/Commands/CloneCommand.cs`
  - `src/Bukit.Cli/Commands/SeoExternalAuditor.cs`
  - 对应测试项目（Bukit.Shared.Tests / Bukit.Content.Tests / Bukit.Engine.Tests / Bukit.Cli.Tests）

## ADDED Requirements

### Requirement: Shortcode 参数 HTML 安全编码
系统 SHALL 在将 shortcode 参数值替换进模板占位符 `{{ $n }}` 之前对其进行 HTML 编码，防止内容作者通过参数注入 HTML/JS。

#### Scenario: 含特殊字符的 shortcode 参数
- **WHEN** 内容作者写 `{% card "<script>alert(1)</script>" %}` 且模板为 `<div>{{ $1 }}</div>`
- **THEN** 输出 HTML 为 `<div>&lt;script&gt;alert(1)&lt;/script&gt;</div>`，不应注入可执行脚本

#### Scenario: 普通文本参数
- **WHEN** 参数为普通字符串如 `"hello world"`
- **THEN** 输出仍为 `hello world`（HTML 编码对普通文本无副作用）

### Requirement: Notion BlockRenderer 颜色 class 输出必须编码
系统 SHALL 在将 Notion 颜色值嵌入 HTML `class` 属性时进行 HTML 编码，防御 class 属性逃逸。

#### Scenario: 正常颜色值
- **WHEN** Notion 颜色为 `blue` 或 `red_background`
- **THEN** 输出 `class="notion-blue"`，与现行测试期望一致

#### Scenario: 含特殊字符的颜色值
- **WHEN** 颜色值含双引号或尖括号（恶意/异常 API 响应）
- **THEN** 输出中相关字符被 HTML 实体化，不能逃逸 class 属性

### Requirement: SSRF 保护对所有外部 HTTP 客户端默认启用
系统 SHALL 在所有从 URL 或路径源下载/检查外部资源的 `HttpClient` 实例上默认启用 `SsrfGuard.SsrfSafeConnectAsync`，阻止访问私有网段。

#### Scenario: CloneCommand 下载外部资产
- **WHEN** 通过 CloneCommand 处理含 `http://127.0.0.1/internal` 或 `http://10.0.0.1/x` 的资源 URL
- **THEN** 连接被 `SsrfGuard` 拒绝（抛出异常或被记录为失败）

#### Scenario: SeoExternalAuditor 审计内部网段链接
- **WHEN** 审计的 HTML 中含指向私有 IP 的链接
- **THEN** 该链接不会被实际请求

### Requirement: IncrementalBuildEngine 消除异步阻塞
系统 SHALL 在增量构建的内容哈希计算路径上使用真正的异步 IO，不得使用 `.GetAwaiter().GetResult()` 阻塞模式。

#### Scenario: 列表项哈希计算
- **WHEN** 增量构建调用 `ComputeListContentHash`
- **THEN** body 加载通过 `await bodyStore.GetAsync(...)` 异步完成，不阻塞线程池线程

## MODIFIED Requirements

### Requirement: MediaConfig SSRF 默认行为
`MediaConfig.BlockPrivateNetworks` SHALL 默认为 `true`。当前实现已满足此要求；本次新增一条断言性测试以防回归。

#### Scenario: 默认构造 MediaConfig
- **WHEN** 创建 `new MediaConfig()` 不显式设置 `BlockPrivateNetworks`
- **THEN** `BlockPrivateNetworks == true`

## REMOVED Requirements

无。

## 行为约束

- 所有现有测试必须继续通过（基线 2847 通过 / 0 失败）
- Release 构建必须保持 0 警告 0 错误
- 不破坏对外公开 API；`ContentBodyResolver.GetHtml`（同步入口）保留但其调用者需迁移到异步版本
- 不引入新的第三方依赖
