# Bukit 1.0 当前 Diff Review

日期：2026-06-08  
审查对象：当前 worktree 未提交改动  
相关决策记录：[2026-06-08-bukit-1-0-decision-record.zh-CN.md](./2026-06-08-bukit-1-0-decision-record.zh-CN.md)

## 结论

当前 diff 已经大范围触及配置、路由、插件、主题、报告、文档、示例和测试。2026-06-08 本轮已修复配置、路由、诊断码、build report 默认值、external process capabilities、BuildOptions 默认 content source，以及 Engine/Config/CLI/Rendering 回归测试中暴露的 1.0 契约不一致。

剩余风险不在核心实现路径。2026-06-08 本轮已完成公开 guide、AI prompt、demo/import 资料和 skills 的 1.0 契约 sweep，并补跑 Release/smoke/security/doc asset/repro gate。后续收口重点转为 `.bukit/*.json` schema 审计和最终 clean worktree 分组。

这些问题不宜靠继续堆新改动绕过去，应先修到契约、实现、测试、文档同向。

## Findings

### 1. `content.provider` 仍被自动升级为 `content.sources`，违反 1.0 “无兼容模式”

严重级别：High  
状态：已修复并通过 `Bukit.Config.Tests`
位置：[ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:83)

现状：

- `ConfigLoader` 在没有 `content.sources` 时读取 `content.provider`。
- 如果 provider 存在，会调用 `AutoUpgradeProviderToSources(...)`。
- 这会让旧配置继续运行，而不是被 1.0 拒绝。

为什么有问题：

- 已拍板：1.0 只支持 `content.sources`，拒绝 `content.provider`。
- 自动升级是典型 compatibility layer，和全新项目规则冲突。
- 后续 validator 看见的是升级后的 `sources`，无法证明旧输入被拒绝。

建议：

- 删除 `AutoUpgradeProviderToSources`。
- 在 `content.provider` 存在时直接抛 `ConfigException`，使用 `DiagnosticCode.ConfigProviderRemoved`。
- 补 `content.provider` rejected-with-message 测试。

### 2. `build.report.enabled` 默认值实现不一致

严重级别：High  
状态：已修复并通过 `Bukit.Config.Tests`
位置：[ConfigLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Config/ConfigLoader.cs:128)

现状：

- `BuildReportConfig.Enabled` record 默认值已被改为 `true`。
- 但 `ConfigLoader` 构造 `BuildReportConfig` 时使用：
  `Enabled = buildReportNode is not null && (...)`
- 当配置没有 `build.report` 节点时，loader 仍会把 enabled 设为 `false`。

为什么有问题：

- 已拍板：1.0 默认开启 build report。
- 当前实现会让默认项目不生成 `.bukit/` 报告，削弱可审计/可回滚目标。

建议：

- loader 默认应为 `true`。
- 只有显式 `build.report.enabled: false` 才关闭。
- 补默认生成 `.bukit` 报告测试。

### 3. 顶层 `outputPath` 拒绝码与契约矩阵不一致

严重级别：Medium  
状态：已统一为 `BKT-0209`，并通过 `RouteGeneratorTests`
位置：[RouteGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Routing/RouteGenerator.cs:48)

现状：

- 顶层 `outputPath` 使用 `DiagnosticCode.RouteNestedOutputPathRejected`。
- 契约矩阵写的是顶层 `outputPath` 使用 `BKT-0203`，nested `route.outputPath` 使用 `BKT-0209`。

为什么有问题：

- 用户看到的诊断码和契约矩阵不一致。
- 顶层 outputPath 和 nested route.outputPath 是两个不同旧行为，应该有稳定且可解释的映射。

建议：

- 二选一统一：
  - 方案 A：顶层和 nested 都使用 `BKT-0209`，并更新矩阵。
  - 方案 B：新增/复用单独 code 给顶层 outputPath，并更新实现。
- 同步 guide/skills/troubleshooting。

### 4. full route override 的顶层 `url/outputPath/template` 仍可能保留旧 outputPath 运行路径

严重级别：High  
状态：已修复；顶层和 nested `outputPath` 在 route 判断前统一拒绝
位置：[RouteGenerator.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Routing/RouteGenerator.cs:125)

现状：

- `TryReadFullRouteOverride` 对顶层 `url/outputPath/template` 仍会进入 full override 分支。
- 当前只在 nested route map 时拒绝 `route.outputPath`。
- 因为 full override 发生在顶层 `outputPath` 检查之前，顶层三字段完整时可能仍被接受。

为什么有问题：

- 已拍板：顶层 `outputPath` 拒绝。
- 这会留下一个绕过路径：只要同时写 `url`、`outputPath`、`template`，旧行为仍可运行。

建议：

- 在任何 full override 判断前先扫描并拒绝顶层 `outputPath`。
- 或让 `TryGetRouteFields` 对顶层 outputPath 返回拒绝，而不是返回 full override。
- 补“顶层 url/outputPath/template 三字段完整也拒绝”的测试。

### 5. 契约矩阵仍把 `type` 写成 deprecated alias，和 starter 唯一写法不一致

严重级别：Medium  
状态：已修复；RouteGenerator 不再用 `type` 驱动路由，契约矩阵改为 starter 只认 `collection`
位置：[docs/bukit-1.0-contract-matrix.zh-CN.md](/Users/ali/mydev/Git/Github/Bukit/docs/bukit-1.0-contract-matrix.zh-CN.md:37)

现状：

- 契约矩阵写 `collection` 是推荐写法。
- 但同时写 `type` 是 `GA-locked`、允许但 deprecated。
- 旧行为处理策略中仍允许 `type + collection` warning 继续运行。

为什么有问题：

- 已拍板：starter 只写 `collection`，`type` 不作为 starter 公开写法。
- 1.0 不保留 warning-only 运行路径。
- 如果仍允许 `type` alias，应明确它不是 starter 路由入口；否则应拒绝参与 routing。

建议：

- 将矩阵改成：starter 和 routing contract 只认 `collection`。
- 如保留 `type`，仅作为非路由内容语义字段，并不得影响 collection routing。
- 移除“warning 后继续运行”的表述，或明确这是非 GA starter 行为。

### 6. 无 `theme.yaml` 主题被拒绝的契约可能破坏现有内置/fixture，需要完整 gate 验证

严重级别：Medium  
状态：已修复并通过非沙箱 smoke gate；`seo-best-practice` 已补齐 taxonomy template 声明
位置：[docs/bukit-1.0-contract-matrix.zh-CN.md](/Users/ali/mydev/Git/Github/Bukit/docs/bukit-1.0-contract-matrix.zh-CN.md:60)

现状：

- 契约矩阵要求 `theme.yaml` 的 `name/version/engine` 必填。
- `ConfigValidator` 已新增 `engine` 检查。
- 但 worktree 中主题、fixtures、examples 改动很广，需要确认所有官方主题和测试 fixture 都已补齐。

为什么有问题：

- 这是合理方向，但 blast radius 大。
- 如果任何官方 fixture 仍缺 theme manifest，会让 smoke 或 tests fail。

建议：

- 跑 theme/doctor/smoke。
- 对 starter、alt、seo-best-practice、theme-inheritance-site 做显式检查。

### 7. 文档仍残留旧 `content.provider` 与 `route.outputPath` 指引

严重级别：Medium  
状态：已完成公开 guide、AI prompt、demo/import 和 skills sweep；可复制旧 `provider:` 配置已清零，`content.provider` 仅保留在“已移除/禁止生成”说明中
位置示例：

- [guide/dev/config-site-yaml.zh-CN.md](/Users/ali/mydev/Git/Github/Bukit/guide/dev/config-site-yaml.zh-CN.md:129)
- [guide/user/16-parameter-cheatsheet.zh-CN.md](/Users/ali/mydev/Git/Github/Bukit/guide/user/16-parameter-cheatsheet.zh-CN.md:47)
- [guide/dev/routing.zh-CN.md](/Users/ali/mydev/Git/Github/Bukit/guide/dev/routing.zh-CN.md:71)
- [src/skills/bukit-config/SKILL.md](/Users/ali/mydev/Git/Github/Bukit/src/skills/bukit-config/SKILL.md:785)
- [guide/ai/chatgpt/prompt_site_yaml.zh-CN.md](/Users/ali/mydev/Git/Github/Bukit/guide/ai/chatgpt/prompt_site_yaml.zh-CN.md:30)

现状：

- 旧 `provider:` 配置示例已迁移到 `content.sources[]`。
- routing 文档不再把 `route.outputPath` 作为可用输入；保留的命中均为拒绝说明或内部/输出模型字段。
- AI demo/import 配置契约改为只生成 `content.sources[]`，`content.provider` 出现即为非法字段。

为什么有问题：

- 1.0 计划已经进入全新项目口径。
- 文档残留会直接误导 Spark/Trae 或用户生成旧配置。

建议：

- 建立 docs rejection sweep，删除或改写旧入口。
- 对 AI prompt 尤其要同步，否则后续生成器会继续产出 `provider`。

### 8. `FileTemplateLoader` fallback 优先级变更需要契约确认

严重级别：Low  
状态：已验证；`FileTemplateLoaderTests` 覆盖 override -> child/root -> parent/fallback
位置：[FileTemplateLoader.cs](/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Rendering/Scriban/FileTemplateLoader.cs:43)

现状：

- lookup 顺序改成 override -> root -> fallback。
- 如果旧行为是 override -> fallback -> root，这会改变主题继承解析。

为什么需要确认：

- 这个变更可能是正确的：child/root 应优先于 parent/fallback。
- 但它属于主题接口行为，必须和 theme contract/测试一致。

建议：

- 确认 `FileTemplateLoaderTests` 覆盖 override/child/parent 三层优先级。
- 在主题契约文档写明最终顺序。

## 建议修复顺序

1. 已修 `content.provider` auto-upgrade，旧入口现在被拒绝。
2. 已修 `build.report.enabled` 默认值冲突。
3. 已修 `RouteGenerator` full override 绕过顶层 `outputPath` 拒绝的问题。
4. 已统一 outputPath 诊断码与契约矩阵。
5. 已改契约矩阵中 `type` 的状态，去掉 warning-only 运行口径。
6. 已完成公开 guide、AI prompt、demo/import 资料和 skills 的 1.0 契约 sweep。
7. 已修复 CLI 编译/回归问题，并完成 Engine/Config/CLI/Rendering/Importing 单项 gate、full Release test、非沙箱 smoke、安全回归、doc asset gate 和 build repro gate。

## 尚未验证

本轮已运行并通过：

- `dotnet test tests/Bukit.Config.Tests/Bukit.Config.Tests.csproj`
- `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj`
- `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`
- `dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj`
- `dotnet test tests/Bukit.Importing.Tests/Bukit.Importing.Tests.csproj --no-restore`
- `dotnet test bukit.slnx -c Release --no-restore`
- `bash scripts/smoke.sh Release`（非沙箱）
- `bash scripts/security-regression.sh Release`（非沙箱）
- `bash scripts/check-doc-asset-consistency.sh`（非沙箱）
- `bash scripts/build-repro.sh Release`（非沙箱）

仍需 release manager 最终确认：

- `.bukit/*.json` schema validation
- clean worktree 分组、提交拆分和 release artifact 签收

在 schema 审计和最终提交分组完成前，不建议把当前 worktree 视为最终 release candidate；但 8 个 diff finding 的实现/文档/回归收口已完成。
