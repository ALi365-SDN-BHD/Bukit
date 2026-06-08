# Bukit 1.0 Trae + DeepSeek V4 Pro 执行计划

日期：2026-06-08
来源执行书：`docs/plans/2026-06-08-bukit-1-0-trae-deepseek-v4-pro.zh-CN.md`
来源计划书：`docs/plans/2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md`

## 0. 产品决策汇总（T0 拍板结果）

| 决策项 | 决策 |
|--------|------|
| `content.provider` vs `content.sources` | **仅保留 `sources`**，删除 `provider` 字段。单源也需写 `sources` 数组。 |
| Notion 等级 | **GA-limited** |
| clone/import 等级 | **Experimental** |
| External process plugin 等级 | **GA-limited** |
| Theme registry/search/install 等级 | **Experimental** |
| nested `route.outputPath` | **直接拒绝**，报错提示只用 `route.url` |
| `type` vs `collection` | **只写 `collection`，不写 `type`**。`type` 保留但标记为 deprecated alias。 |
| `build.report.enabled` 默认 | **默认开启**（`true`） |

## 1. 当前状态分析

### 1.1 已验证基线

| 命令 | 状态 |
|------|------|
| `dotnet test bukit.slnx -c Release --no-restore` | ✅ 通过（~3,465 测试） |
| `bash scripts/smoke.sh Release` | ✅ 通过 |
| `bash scripts/security-regression.sh Release` | ✅ 通过 |

### 1.2 工作区未提交改动

- `examples/starter/**`：starter 内容、主题、模板能力清单、taxonomy 示例
- `src/Bukit.Engine/BuildReporter.cs`：`.bukit/` 报告 schema 与 security report
- `tests/Bukit.Cli.Tests/DoctorCommandTests.cs`、`tests/Bukit.Engine.Tests/BuildReporterTests.cs`
- `docs/schemas/*.v1.schema.json`

### 1.3 已具备的 1.0 基础

- 配置模型：`AppConfig` 完整 record 模型（`src/Bukit.Config/AppConfig.cs`）
- 配置验证：`ConfigValidator.Validate()` 含 theme.yaml 1.0 验证（`src/Bukit.Config/ConfigValidator.cs`）
- 诊断码：`DiagnosticCode` 枚举覆盖 BKT-000x ~ BKT-090x（`src/Bukit.Shared/DiagnosticCode.cs`）
- 路由：`RouteGenerator` 已拒绝顶层 `outputPath`，nested `route.outputPath` 当前接受（`src/Bukit.Routing/RouteGenerator.cs`）
- 插件：handshake 已要求 schema version 2，`PluginCapabilityEnforcer` 存在（`src/Bukit.Engine/Plugins/Protocol/`）
- 构建报告：`BuildReporter` 已写 schema 和 schemaVersion（`src/Bukit.Engine/BuildReporter.cs`）
- 安全：`security-report.json` 当前是静态 passed（`BuildReporter.WriteSecurityReport`）
- 主题：`ConfigValidator.ValidateThemeYaml()` 已实现 1.0 验证

## 2. 执行任务（按推荐顺序）

### T0：1.0 产品边界最终拍板 ✅ 已完成（见第 0 节）

输出文件：`docs/bukit-1.0-contract-matrix.zh-CN.md`

### T1：配置契约实现收口

#### T1.1 删除 `content.provider`，仅保留 `content.sources`

**文件：** `src/Bukit.Config/AppConfig.cs`
- 将 `ContentConfig.Provider` 从 `required` 改为可选，添加 `[Obsolete]` 标记
- 在 `ConfigValidator.Validate()` 中添加拒绝逻辑：若 `provider` 被设置且 `sources` 为空，抛出 `ConfigException` 提示迁移到 `sources`

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- 修改验证逻辑：`provider` 存在时给出 BKT-000x 拒绝信息，引导用户迁移到 `sources`
- 确保 `sources` 为空时给出明确错误

**文件：** `src/Bukit.Config/ConfigLoader.cs`
- 检查 `ConfigLoader` 如何处理 `provider` 字段，确保向后兼容读取但验证时拒绝

**文件：** `examples/starter/site.yaml`
- 将 `content.provider: markdown` 迁移为 `content.sources` 写法

**文件：** 所有示例 `site.yaml`（`examples/*/site.yaml`）
- 统一迁移到 `sources` 写法

#### T1.2 拒绝旧字段运行路径

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- 盘点所有 warning-only 项，升级为 rejected-with-message
- 添加 `site.rssMode` 旧字段拒绝（当前默认 `"split"`，检查是否有旧值兼容）
- 添加 `site.searchMode` 旧字段拒绝
- 添加 taxonomy legacy 模板配置拒绝

**文件：** `src/Bukit.Shared/DiagnosticCode.cs`
- 补充缺失的诊断码：
  - `ConfigDeprecatedField = 0x0005`（已废弃字段）
  - `ConfigRemovedField = 0x0006`（已移除字段）
  - `ConfigProviderRemoved = 0x0007`（provider 已移除，请用 sources）

#### T1.3 统一 config check / doctor / build-time validation

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- 确保 `Validate()` 和 `ValidateThemeYaml()` 对同一错误给出一致路径和诊断码
- 给关键拒绝路径补 `BKT-000x` 诊断码

**文件：** `src/Bukit.Cli/Commands/ConfigCommand.cs`
- 检查 `config check` 命令是否调用 `ConfigValidator.Validate()`

**文件：** `src/Bukit.Cli/Commands/DoctorCommand.cs`
- 检查 doctor 命令是否调用 `ConfigValidator.ValidateThemeYaml()`

#### T1.4 定义 `build.report.enabled` 默认策略

**文件：** `src/Bukit.Config/AppConfig.cs`
- `BuildReportConfig.Enabled` 默认值已为 `true`（第 389 行），确认无需修改

**验证：**
```bash
dotnet test tests/Bukit.Config.Tests -c Release --no-restore
dotnet test tests/Bukit.Cli.Tests -c Release --no-restore
```

---

### T2：内容模型与 starter 正式样板收口

#### T2.1 决定 `type` / `collection` 唯一写法

**文件：** `src/Bukit.Routing/RouteGenerator.cs`
- 当前 `GetType()` 和 `GetCollection()` 都从 fields 读取
- 添加逻辑：当同时声明 `type` 和 `collection` 时，优先使用 `collection`，`type` 作为 fallback
- 添加 warning：当只有 `type` 没有 `collection` 时，提示迁移到 `collection`

**文件：** `examples/starter/content/**/*.md`
- 将所有 front matter 中的 `type:` 替换为 `collection:`
- 移除同时声明 `type` 和 `collection` 的情况

**文件：** `src/Bukit.Engine/ContentModelSchema*`
- 检查 schema 验证中对 `type` 和 `collection` 的处理

#### T2.2 决定 starter schema 字段

**文件：** `examples/starter/content/**/*.md`
- 盘点所有 front matter 中使用的字段
- 决定 `seo_title`、`cover`、`cover_alt`、`tableOfContents` 是正式字段还是 theme-only 字段
- 将这些字段纳入 schema 或从示例中移除

**文件：** `src/Bukit.Config/AppConfig.cs`
- 若需要，在 `ContentModelSchemaConfig` 中添加这些字段的定义

#### T2.3 统一 Markdown / Notion / composite provider parity

**文件：** `tests/Bukit.Content.Tests/`
- 检查现有 provider parity 测试
- 补充缺失的 parity 测试

#### T2.4 调整 publish audit 与 starter 内容

**文件：** `examples/starter/content/**/*.md`
- 消除默认示例产生的误导性 warning：
  - 添加 `author` 字段
  - 添加 `updatedAt` 字段
  - 添加 `summary` 字段
  - 确保 entity/source 满足最低发布标准

**验证：**
```bash
bash scripts/smoke.sh Release
dotnet test tests/Bukit.Content.Tests -c Release --no-restore
dotnet test tests/Bukit.Engine.Tests -c Release --no-restore
```

---

### T3：路由契约最终语义

#### T3.1 拒绝 nested `route.outputPath`

**文件：** `src/Bukit.Routing/RouteGenerator.cs`
- 在 `TryApplyPartialRouteOverride()` 中（第 147-167 行），当前 `HasNestedRouteMap` 检查后允许 `outputPathOverride`
- 修改为：当检测到 nested `route.outputPath` 时，抛出 `ConfigException` 拒绝，提示用户只用 `route.url`

**文件：** `src/Bukit.Shared/DiagnosticCode.cs`
- 添加 `RouteNestedOutputPathRejected = 0x0209`

#### T3.2 顶层 `outputPath` 拒绝行为

**文件：** `src/Bukit.Routing/RouteGenerator.cs`
- 第 49-52 行已有拒绝逻辑，确认诊断码为 `RouteInvalidPattern`
- 改为更明确的 `RouteNestedOutputPathRejected` 或新增专用码

#### T3.3 补 route inventory golden tests

**文件：** `tests/Bukit.Engine.Tests/` 或 `tests/Bukit.Routing.Tests/`
- 创建 route inventory golden test：
  - 覆盖原始内容路由
  - 覆盖派生页路由（taxonomy、pagination、archive）
  - 覆盖静态 HTML 路由
  - 覆盖插件输出路由
  - 覆盖 list/taxonomy 是否进入 search/rss/sitemap

#### T3.4 固定 `routes.json` schema

**文件：** `docs/schemas/routes.v1.schema.json`
- 检查并固定 schema 字段语义

**文件：** `src/Bukit.Engine/BuildReporter.cs`
- 确认 `WriteRoutes()` 输出与 schema 一致

#### T3.5 route conflict / unsafe path 诊断码

**文件：** `src/Bukit.Routing/RouteSecurityValidator.cs`
- 确保所有 route conflict 和 unsafe path 失败都有稳定 `BKT-02xx` 诊断码

**验证：**
```bash
dotnet test tests/Bukit.Routing.Tests -c Release --no-restore
dotnet test tests/Bukit.Engine.Tests -c Release --no-restore
```

---

### T4：主题接口版本化

#### T4.1 定义 `theme.yaml` 必填字段

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- `ValidateThemeYaml()` 已实现基本验证（第 220-271 行）
- 补充验证：
  - `engine` 字段必填
  - `min_engine_version` 与当前 bukit 版本兼容性检查
  - `extends` 继承链验证

#### T4.2 无 `theme.yaml` 主题拒绝

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- `ValidateThemeYaml()` 已对无 `theme.yaml` 返回 BKT-0100 错误
- 确认 doctor 命令调用此验证

#### T4.3 决定 `extends`、fallbackDir、template capabilities 正式语义

**文件：** `src/Bukit.Theme/`
- 检查 `ThemeManifestLoader` 如何处理 `extends`
- 检查 `TemplateCapabilitiesResolver` 如何处理 template capabilities

**文件：** `src/Bukit.Engine/TemplateCapabilitiesResolver.cs`
- 确认 template capabilities 的正式语义

#### T4.4 theme doctor 输出可定位不兼容点

**文件：** `src/Bukit.Cli/Commands/DoctorCommand.cs`
- 确保 theme doctor 输出能直接定位：
  - 缺失模板
  - 能力声明不匹配
  - 继承冲突
  - engine version 不兼容

**验证：**
```bash
dotnet test tests/Bukit.Theme.Tests -c Release --no-restore
bash scripts/smoke.sh Release  # starter / alt / seo-best-practice 全部通过
```

---

### T5：插件接口 v2-only 收口

#### T5.1 移除 v1 fallback

**文件：** `src/Bukit.Engine/Plugins/Protocol/ProtocolHandshakeNegotiator.cs`
- 第 91-94 行已要求 schema version 2，拒绝其他版本
- 确认无 v1 fallback 代码路径

**文件：** `src/Bukit.Engine/Plugins/Protocol/ExternalProtocolPluginSource.cs`
- 确认 `Version => "protocol-v2"` 已固定

#### T5.2 缺失 `capabilities` 的外部插件拒绝

**文件：** `src/Bukit.Engine/Plugins/Protocol/PluginCapabilityEnforcer.cs`
- 需要定位此文件（搜索结果显示不存在，可能在别处）
- 搜索 `Capabilities` 相关验证逻辑

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- 在 `ExternalPluginsValidator` 中添加：若 `Capabilities` 为空或 null，拒绝

#### T5.3 统一 plugin failure 到 `BKT-07xx`

**文件：** `src/Bukit.Shared/DiagnosticCode.cs`
- 补充缺失的插件诊断码：
  - `PluginCapabilityMissing = 0x0704`
  - `PluginHandshakeV1Rejected = 0x0705`
  - `PluginOutputTraversal = 0x0706`
  - `PluginStaleOutputCleanupFailed = 0x0707`

**文件：** `src/Bukit.Engine/Plugins/Protocol/`
- 盘点所有 `InvalidOperationException` 包装场景，映射到稳定 `BKT-07xx`

#### T5.4 固化 request/response schema

**文件：** `src/Bukit.Engine.Abstractions/Plugins/Protocol/`
- 检查协议 request/response 模型
- 确保 schema 稳定

#### T5.5 保证测试覆盖

**文件：** `tests/Bukit.Engine.Tests/`
- 确认 ProtocolEchoPlugin 覆盖：
  - success
  - bad JSON
  - empty stdout
  - timeout
  - ok=false
  - capability missing
  - output traversal
  - stale cleanup

**验证：**
```bash
dotnet test tests/Bukit.Engine.Tests -c Release --no-restore
```

---

### T6：可复现构建与审计产物

#### T6.1 定义 release artifact bundle

**文件：** `src/Bukit.Engine/BuildReporter.cs`
- 当前 `WriteIfEnabled()` 写入 5 个报告文件
- 确认 release artifact bundle 包含：public output + `.bukit/` + version metadata

#### T6.2 定义 normalized artifact compare

**文件：** `scripts/build-repro.sh`
- 检查现有 `build-repro.sh` 脚本
- 定义 deterministic compare：忽略时间/耗时/本地绝对路径

#### T6.3 扩展 `assets.json` 为完整 artifact inventory

**文件：** `src/Bukit.Engine/BuildReporter.cs`
- `WriteAssets()` 当前只枚举 `assets/` 目录（第 257-271 行）
- 扩展为覆盖完整 public output：
  - 根级静态文件
  - feed/sitemap/search/llms 等 publish projection 输出
  - 插件输出文件

#### T6.4 security report 接入真实检查结果

**文件：** `src/Bukit.Engine/BuildReporter.cs`
- `WriteSecurityReport()` 当前是静态 passed（第 179-200 行）
- 修改为接收真实安全检查结果参数
- 从构建过程中收集安全检查结果

**文件：** `scripts/security-regression.sh`
- 确保其结果能写回 `.bukit/security-report.json`

#### T6.5 clean twice 与 clean vs incremental 加入 CI

**文件：** `.github/workflows/ci.yml`
- 添加 clean build 两次的 deterministic compare 步骤
- 添加 clean vs incremental 的 output inventory 比较步骤

**验证：**
```bash
# clean build 两次 normalized manifest 一致
# clean vs incremental public output inventory 一致
# .bukit/*.json schema validation 通过
```

---

### T7：错误与安全边界最终审查

#### T7.1 盘点无 code 的关键异常

**文件：** 全局搜索 `throw new ConfigException` 不带 `DiagnosticCode` 的情况
- 给所有 GA-locked failure path 补诊断码

**文件：** `src/Bukit.Shared/DiagnosticCode.cs`
- 确保所有诊断码范围完整

#### T7.2 统一 CLI 错误输出

**文件：** `src/Bukit.Cli/Cli/CliErrorRenderer.cs`
- 统一人类可读格式：code + 对象路径 + 原因 + 修复建议
- 统一机器可读格式：`--json` 输出

#### T7.3 审查安全边界

**文件：** `src/Bukit.Routing/RouteSecurityValidator.cs`
- 审查 route/output path 安全边界

**文件：** `src/Bukit.Config/ConfigValidator.cs`
- 审查 theme name/path/source/ref/lock 安全

**文件：** `src/Bukit.Content/`
- 审查 media download SSRF/private network blocking

**文件：** `src/Bukit.Engine/Plugins/Protocol/`
- 审查 plugin env/output/capability/path/sha256 安全

#### T7.4 `security-regression.sh` 设为 release blocker

**文件：** `scripts/security-regression.sh`
- 确认覆盖所有安全面
- 确保结果可写回 security report

**验证：**
```bash
bash scripts/security-regression.sh Release
dotnet test tests/Bukit.Engine.Tests -c Release --no-restore --filter "Security"
```

---

## 3. 执行顺序

1. **T0** ✅ 已完成（产品决策已拍板）
2. **T1** → 配置契约收口（删除 provider、拒绝旧字段、统一验证）
3. **T3** → 路由契约（拒绝 nested outputPath、补 golden tests）
4. **T5** → 插件 v2-only（移除 v1 fallback、拒绝无 capability、补诊断码）
5. **T2** → 内容模型与 starter（collection 唯一写法、消除 warning）
6. **T4** → 主题接口版本化（theme.yaml 必填字段、doctor 输出）
7. **T6** → 可复现构建（artifact inventory、security report 真实结果）
8. **T7** → 错误与安全边界（补诊断码、统一 CLI 输出、安全审查）

## 4. 最终 Release Gate 验证

```bash
# 1. 全量测试
dotnet test bukit.slnx -c Release --no-restore

# 2. 冒烟测试
bash scripts/smoke.sh Release

# 3. 全量冒烟
bash scripts/smoke-all.sh Release

# 4. 安全回归
bash scripts/security-regression.sh Release

# 5. 文档一致性
bash scripts/check-doc-asset-consistency.sh

# 6. 确定性构建
bash scripts/build-repro.sh Release

# 7. Schema 验证
# .bukit/*.json against docs/schemas/*.v1.schema.json
```

## 5. 禁止事项（来自执行书）

- ❌ 不把 BukitJalil 纳入 1.0
- ❌ 不新增大功能来掩盖信任缺口
- ❌ 不降低 doctor/smoke/security 严格度来过关
- ❌ 不保留 warning-only 运行路径
- ❌ 不为历史文件、历史网站、旧主题、旧插件协议保留运行时兼容模式
- ❌ 不把 Experimental 能力写成 GA

## 6. 交付格式

每个任务完成后按以下格式记录：

```md
## Trae + DeepSeek V4 Pro 执行结果
- 任务编号：T1.1
- 是否完成：✅
- 修改文件：src/Bukit.Config/AppConfig.cs, src/Bukit.Config/ConfigValidator.cs
- 实现修复：删除 content.provider，仅保留 content.sources
- 契约澄清：provider 字段在 1.0 中被移除，用户必须迁移到 sources
- 产品决策：仅保留 sources

## 验证
- 命令：dotnet test tests/Bukit.Config.Tests -c Release --no-restore
- 结果：通过
- 未运行原因：N/A

## 风险
- 剩余风险：需要检查所有示例 site.yaml 是否已迁移
- 需要人工拍板：无

## 下一步
- 建议继续任务：T1.2
```
