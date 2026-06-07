# Bukit 1.0 T0-T7 契约冻结 Spec

## Why

Bukit 1.0 必须从 public preview 收口到"任何人都敢用于正式网站"的稳定版本。执行书定义 T0-T7 八大任务，覆盖产品边界拍板、配置/内容/路由/主题/插件五大核心契约冻结、可复现构建审计、错误与安全边界最终审查。1.0 不采用兼容模式，旧行为只能被删除或拒绝。

## What Changes

### T0：1.0 产品边界最终拍板
- 输出 `docs/bukit-1.0-contract-matrix.zh-CN.md`：每项能力的 support tier、允许配置、拒绝行为、测试要求
- 决定 `content.provider` vs `content.sources` 唯一 1.0 语义
- 决定 Notion 等级（GA-limited vs Experimental）
- 决定 clone/import 定位
- 决定 external process plugin 等级
- 决定 theme registry/search/install 等级

### T1：配置契约实现收口 **BREAKING**
- 删除或拒绝旧字段运行路径
- 将 warning-only 项升级为 rejected-with-message 或移除
- 统一 `config check`、`doctor`、build-time validation
- 给关键拒绝路径补 `BKT-000x` 诊断码
- 定义 `build.report.enabled` 默认策略

### T2：内容模型与 starter 正式样板收口 **BREAKING**
- 决定 `type` / `collection` 唯一写法
- 决定 starter schema 字段：`seo_title`、`cover`、`cover_alt`、`tableOfContents`
- 统一 Markdown/Notion/composite provider parity
- 调整 publish audit 与 starter 内容，消除误导性 warning

### T3：路由契约最终语义 **BREAKING**
- 决定 nested `route.outputPath` 是正式契约还是拒绝
- 顶层 `outputPath` 拒绝行为和诊断码
- collection/type 匹配规则
- list/taxonomy/pagination/archive/static/plugin route 默认策略
- 补 route inventory golden tests
- 固定 `routes.json` schema

### T4：主题接口版本化 **BREAKING**
- 定义 `theme.yaml` 必填字段、version、engine range
- 无 `theme.yaml` 主题拒绝或要求显式生成 manifest
- 决定 `extends`、fallbackDir、template capabilities 正式语义
- theme doctor 输出可定位不兼容点

### T5：插件接口 v2-only 收口 **BREAKING**
- 移除或拒绝 v1 fallback
- 缺失 `capabilities` 的外部插件必须拒绝
- 统一 plugin failure 到 `BKT-07xx`
- 固化 request/response schema
- 保证 stale output cleanup、env isolation、timeout、stdout/stderr limits 都有测试

### T6：可复现构建与审计产物
- 定义 release artifact bundle
- 定义 normalized artifact compare
- 扩展 `assets.json` 或新增 `artifact-manifest.json`
- 将真实安全检查结果写入 `security-report.json`
- clean twice 与 clean vs incremental 加入 CI

### T7：错误与安全边界最终审查
- 盘点无 code 的关键 failure path
- 统一 CLI 人类可读和机器可读错误输出
- 审查 route/output/theme/plugin/media/download 安全边界
- `security-regression.sh` 设为 release blocker

## Impact

- Affected specs: 所有 Bukit 核心契约（config、content、routing、theme、plugin、build、diagnostics、security）
- Affected code: src/Bukit.Config/, src/Bukit.Content/, src/Bukit.Routing/, src/Bukit.Theme/, src/Bukit.Engine/, src/Bukit.Cli/, src/Bukit.Shared/, tests/*, examples/starter/, examples/*/layouts/, docs/schemas/, scripts/

## ADDED Requirements

### Requirement: T0 - 1.0 产品边界契约矩阵
系统 SHALL 提供 `docs/bukit-1.0-contract-matrix.zh-CN.md`，明确每项能力的 support tier、允许配置、拒绝行为、测试要求。

#### Scenario: contract-matrix 覆盖所有能力
- **WHEN** 审查 contract matrix
- **THEN** 每项能力有明确的 GA-locked / GA-limited / Experimental / Out of scope 等级
- **THEN** `content.provider` 与 `content.sources` 有唯一 1.0 语义决策
- **THEN** Notion、clone/import、external plugin、theme registry 等级明确

### Requirement: T1 - 配置契约冻结
系统 SHALL 拒绝所有旧配置字段并给出 `BKT-000x` 诊断码，不保留 warning-only 运行路径。

#### Scenario: 旧字段被拒绝
- **WHEN** site.yaml 包含 1.0 不支持的旧字段
- **THEN** 系统拒绝并输出带 BKT-000x 诊断码的错误信息
- **THEN** 不保留 warning-only 兼容运行

#### Scenario: config check/doctor/build validation 一致
- **WHEN** 对同一错误配置运行 config check、doctor、build
- **THEN** 三者给出相同的错误路径和诊断码

### Requirement: T2 - 内容模型契约冻结
系统 SHALL 定义 type/collection 的唯一 1.0 写法，starter 内容不产生误导性 warning。

#### Scenario: starter 默认 smoke 无误导性 warning
- **WHEN** 运行 `bash scripts/smoke.sh Release`
- **THEN** starter 默认路径不出现 release-blocking 或误导性 schema warning

### Requirement: T3 - 路由契约冻结
系统 SHALL 冻结 nested `route.outputPath` 行为（契约或拒绝），所有 route conflict 有 `BKT-02xx` 诊断码。

#### Scenario: route inventory golden test 通过
- **WHEN** 运行 route inventory golden tests
- **THEN** 输出与 golden 快照一致

### Requirement: T4 - 主题接口版本化
系统 SHALL 要求所有主题包含 `theme.yaml` 且声明 engine range，无 manifest 主题被拒绝。

#### Scenario: 无 theme.yaml 主题被拒绝
- **WHEN** 主题目录不包含 theme.yaml
- **THEN** doctor 输出错误并要求生成 manifest

### Requirement: T5 - 插件接口 v2-only
系统 SHALL 仅支持 external protocol v2，拒绝 v1 fallback，缺失 capabilities 的插件必须拒绝。

#### Scenario: v1 协议被拒绝
- **WHEN** 外部插件仅支持 v1 协议
- **THEN** 系统拒绝并输出 BKT-07xx 诊断码

#### Scenario: 缺失 capabilities 被拒绝
- **WHEN** 外部插件未声明 capabilities
- **THEN** 系统拒绝执行

### Requirement: T6 - 可复现构建
系统 SHALL 保证 clean build 两次输出一致，clean vs incremental 输出 inventory 一致。

#### Scenario: clean build 两次一致
- **WHEN** 对同一输入树连续两次 clean build
- **THEN** normalized artifact manifest 一致

### Requirement: T7 - 错误与安全边界
系统 SHALL 确保所有 GA-locked failure path 有稳定 BKT-xxxx 诊断码，security-regression.sh 为 release blocker。

#### Scenario: 无 code 的 failure path 被修复
- **WHEN** 审查所有 ConfigException/RenderException/plugin failure
- **THEN** 每个 GA-locked 失败路径有稳定诊断码

## REMOVED Requirements

### Requirement: plugin protocol v1 fallback
**Reason**: 1.0 只支持 external protocol v2
**Migration**: 外部插件必须升级到 v2 协议

### Requirement: warning-only 运行路径
**Reason**: 1.0 不保留 warning-only 兼容运行
**Migration**: 旧字段/旧写法直接拒绝并给出新写法提示

### Requirement: 无 theme.yaml 的主题兼容
**Reason**: 1.0 要求显式主题清单
**Migration**: 旧主题需生成 theme.yaml manifest
