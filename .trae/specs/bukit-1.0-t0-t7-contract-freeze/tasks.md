# Tasks: Bukit 1.0 T0-T7 契约冻结

## T0：1.0 产品边界最终拍板

- [ ] Task T0.1: 审查当前代码中的 content.provider vs content.sources 实际行为，决定 1.0 唯一语义
  - [ ] 阅读 `src/Bukit.Config/AppConfig.cs` 中 ContentConfig 定义
  - [ ] 阅读 `src/Bukit.Engine/Stages/` 中 provider 解析逻辑
  - [ ] 写出决策：保留双入口还是单一入口，唯一 1.0 语义是什么
  - [ ] 更新 `docs/bukit-1.0-contract-matrix.zh-CN.md`

- [ ] Task T0.2: 审查 Notion 内容源当前实现边界，决定 GA-limited vs Experimental 等级
  - [ ] 阅读 `src/Bukit.Content/Notion/NotionContentProvider.cs`
  - [ ] 评估 Notion API 依赖、缓存、媒体下载、field policy 边界
  - [ ] 写出决策和理由
  - [ ] 更新 contract matrix

- [ ] Task T0.3: 决定 clone/import 定位（重新生成 1.0 项目的工具 vs 正式能力）
  - [ ] 阅读 `src/Bukit.Importing/` 和 clone 相关 CLI 命令
  - [ ] 写出决策
  - [ ] 更新 contract matrix

- [ ] Task T0.4: 决定 external process plugin 等级（GA-limited vs GA-locked）
  - [ ] 审查插件协议、EchoPlugin 测试覆盖、安全边界
  - [ ] 写出决策
  - [ ] 更新 contract matrix

- [ ] Task T0.5: 决定 theme registry/search/install 等级（Experimental 确认）
  - [ ] 审查 theme source/lock 相关代码
  - [ ] 写出决策
  - [ ] 更新 contract matrix

- [ ] Task T0.6: 输出完整 `docs/bukit-1.0-contract-matrix.zh-CN.md`
  - [ ] 汇总所有能力及其 support tier、允许配置、拒绝行为、测试要求
  - [ ] 确保与 trust-plan.zh-CN.md 中 support tiers 表一致

## T1：配置契约实现收口

- [ ] Task T1.1: 审查旧字段当前处理状态
  - [ ] 搜索 AppConfig 和相关 validator 中的 warning-only / deprecated 路径
  - [ ] 列出所有需要升级为 rejected 的旧字段
  - [ ] 列出所有需要移除的旧字段

- [ ] Task T1.2: 删除/拒绝旧字段运行路径
  - [ ] 对旧字段实现 rejected-with-message 行为（带 BKT-000x 诊断码和新写法提示）
  - [ ] 移除 warning-only 兼容路径
  - [ ] 更新 ConfigValidator 和相关检查

- [ ] Task T1.3: 统一 config check / doctor / build-time validation
  - [ ] 确保三者对同一错误给出一致路径和诊断码
  - [ ] 审查 `ConfigValidator.cs`、`DoctorCommand.cs`、build pipeline 中的验证逻辑

- [ ] Task T1.4: 决定 build.report.enabled 默认策略
  - [ ] 在 release/profile 下默认生成 `.bukit/` 审计产物
  - [ ] 更新 BuildConfig 和 BuildReporter

- [ ] Task T1.5: 验证配置测试通过
  - [ ] 运行 `dotnet test tests/Bukit.Config.Tests -c Release --no-restore`
  - [ ] 运行 `dotnet test tests/Bukit.Cli.Tests -c Release --no-restore`

## T2：内容模型与 starter 正式样板收口

- [ ] Task T2.1: 决定 type/collection 唯一 1.0 写法
  - [ ] 审查内容模型中 type 和 collection 的双声明行为
  - [ ] 选择唯一新项目写法（推荐 collection）
  - [ ] 更新 starter 内容消除另一种写法

- [ ] Task T2.2: 决定 starter schema 字段
  - [ ] 审查 `seo_title`、`cover`、`cover_alt`、`tableOfContents` 在 starter 中的使用
  - [ ] 决定是正式 schema 字段、theme-only 字段，还是需移除

- [ ] Task T2.3: 消除 starter 默认 smoke 中的误导性 warning
  - [ ] 修复 publish audit warning（author、updatedAt、summary 等）
  - [ ] 修复 schema extra fields warning
  - [ ] 确保 starter 内容在默认 smoke 下不产生误导性 warning

- [ ] Task T2.4: 验证内容测试通过
  - [ ] 运行 `bash scripts/smoke.sh Release`
  - [ ] 运行 `dotnet test tests/Bukit.Content.Tests -c Release --no-restore`
  - [ ] 运行 `dotnet test tests/Bukit.Engine.Tests -c Release --no-restore`

## T3：路由契约最终语义

- [ ] Task T3.1: 决定 nested route.outputPath 语义
  - [ ] 审查 RouteGenerator 中 nested outputPath 实际行为
  - [ ] 二选一：承认 nested route.outputPath 是正式契约，或直接拒绝
  - [ ] 更新 routing 文档和 skills

- [ ] Task T3.2: 顶层 outputPath 拒绝行为
  - [ ] 保持拒绝旧顶层写法
  - [ ] 添加 BKT-02xx 诊断码和新写法提示

- [ ] Task T3.3: 冻结 collection/type 匹配规则和派生路由默认策略
  - [ ] 明确 list/taxonomy/pagination/archive/static/plugin route 是否进入 search/rss/sitemap
  - [ ] 固定 routes.json schema 字段语义

- [ ] Task T3.4: 补 route inventory golden tests
  - [ ] 为原始内容、派生页、静态 HTML、插件输出创建 golden 快照测试
  - [ ] 确保 route conflict / unsafe path 都有 BKT-02xx 诊断码

## T4：主题接口版本化

- [ ] Task T4.1: 定义 theme.yaml 必填字段和 engine range
  - [ ] 明确 version、engine、min_engine_version 为必填
  - [ ] 更新 ThemeManifestV2 模型和验证

- [ ] Task T4.2: 无 theme.yaml 主题拒绝行为
  - [ ] doctor 拒绝无 manifest 主题并要求生成 manifest
  - [ ] 添加 BKT-010x 诊断码

- [ ] Task T4.3: 冻结 extends/fallbackDir/template capabilities 语义
  - [ ] 审查当前继承和回退行为
  - [ ] 明确 1.0 契约语义

- [ ] Task T4.4: 验证主题测试通过
  - [ ] starter/alt/seo-best-practice 全部 doctor/build/smoke 通过
  - [ ] 运行 `dotnet test tests/Bukit.Theme.Tests -c Release --no-restore`

## T5：插件接口 v2-only 收口

- [ ] Task T5.1: 移除或拒绝 v1 fallback
  - [ ] 审查 ProtocolHandshakeNegotiator 当前行为
  - [ ] 确保 handshake 拒绝非 v2 协议版本
  - [ ] 更新治理文档移除 v2->v1 回退描述

- [ ] Task T5.2: 缺失 capabilities 必须拒绝
  - [ ] 审查 PluginCapabilityEnforcer 行为
  - [ ] 默认拒绝未声明 capabilities 的外部插件

- [ ] Task T5.3: 统一 plugin failure 到 BKT-07xx
  - [ ] 盘点所有外部插件失败路径
  - [ ] 将 InvalidOperationException 等映射到稳定诊断码

- [ ] Task T5.4: 验证插件测试覆盖
  - [ ] ProtocolEchoPlugin 覆盖 success、bad JSON、empty stdout、timeout、ok=false、capability missing、output traversal、stale cleanup
  - [ ] 运行 `dotnet test tests/Bukit.Engine.Tests -c Release --no-restore --filter ExternalProtocolPlugin`

## T6：可复现构建与审计产物

- [ ] Task T6.1: 定义 release artifact bundle 结构
  - [ ] 明确 public output + .bukit/ + version metadata + theme/plugin lock info

- [ ] Task T6.2: 实现 normalized artifact compare
  - [ ] 定义忽略字段（时间、耗时、本地绝对路径）
  - [ ] 实现 compare 逻辑或脚本

- [ ] Task T6.3: 扩展 artifact inventory
  - [ ] 审查 assets.json 覆盖范围
  - [ ] 确保覆盖根级静态文件、feed/sitemap/search/llms 等 publish projection 输出

- [ ] Task T6.4: 真实安全检查结果写入 security-report.json
  - [ ] 审查 BuildReporter 当前 security report 生成逻辑
  - [ ] 将 security-regression.sh 结果集成到 security-report.json

- [ ] Task T6.5: 验证可复现性
  - [ ] clean build 两次 normalized manifest 一致
  - [ ] clean vs incremental public output inventory 一致
  - [ ] .bukit/*.json schema validation 通过

## T7：错误与安全边界最终审查

- [ ] Task T7.1: 盘点无 code 的关键 failure path
  - [ ] 搜索所有 ConfigException / RenderException / plugin failure
  - [ ] 确保每个 GA-locked 失败路径有 BKT-xxxx 诊断码

- [ ] Task T7.2: 统一 CLI 错误输出格式
  - [ ] 人类可读格式：code + 对象路径 + 原因 + 修复建议
  - [ ] 机器可读格式：--json 输出策略

- [ ] Task T7.3: 审查安全边界
  - [ ] route/output path 防逃逸
  - [ ] theme 路径安全
  - [ ] plugin env/output/capability/path 安全
  - [ ] media download SSRF/private network blocking

- [ ] Task T7.4: 验证安全回归测试
  - [ ] 运行 `bash scripts/security-regression.sh Release`
  - [ ] 确认 security-regression.sh 为 release blocker
  - [ ] 运行 targeted security tests

## 最终回归验证

- [ ] Task REGRESSION.1: 全量测试
  - [ ] `dotnet test bukit.slnx -c Release --no-restore`
- [ ] Task REGRESSION.2: 冒烟测试
  - [ ] `bash scripts/smoke.sh Release`
  - [ ] `bash scripts/smoke-all.sh Release`
- [ ] Task REGRESSION.3: 安全测试
  - [ ] `bash scripts/security-regression.sh Release`
- [ ] Task REGRESSION.4: 文档一致性
  - [ ] `bash scripts/check-doc-asset-consistency.sh`

# Task Dependencies

- T1、T3、T5 依赖 T0（产品决策先于实现收口）
- T2 依赖 T0（内容模型决策依赖产品边界）
- T4 依赖 T0（主题接口决策依赖产品边界）
- T6 依赖 T1-T5（artifact 定义需要契约稳定）
- T7 依赖 T1-T6（错误盘点需要完整实现）
- T1/T3/T5 可并行执行
- T2/T4 可并行执行（在 T0 完成后）
- REGRESSION 依赖所有 T0-T7 完成
