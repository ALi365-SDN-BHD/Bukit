# Bukit 1.0 Trae + DeepSeek V4 Pro 执行书

日期：2026-06-08  
来源计划书：[2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md](./2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md)

## 0. 执行定位

Trae + DeepSeek V4 Pro 负责架构判断、跨模块实现、契约冻结、安全边界、release gate 和最终产品语义。它必须读完整计划书，并以 Bukit 1.0 全新项目为唯一目标面。

Bukit 1.0 不采用兼容模式，不承诺历史文件、历史网站、旧配置、旧主题或旧插件协议可继续运行。旧行为只能被删除、拒绝或给出新项目重写提示。

## 1. 核心任务

### T0：1.0 产品边界最终拍板

目标：确定每个能力的 1.0 等级。

必须决策：

- `content.provider` 与 `content.sources` 是否保留双入口，若保留，唯一 1.0 语义是什么。
- Notion 是 `GA-limited` 还是继续 `Experimental`。
- clone/import 是重新生成 1.0 项目的工具，还是正式能力。
- external process plugin 是 `GA-limited` 还是核心 `GA-locked`。
- theme registry/search/install 是否明确 `Experimental`。

输出：

- `docs/bukit-1.0-contract-matrix.zh-CN.md`
- 每项能力的 support tier、允许配置、拒绝行为、测试要求。

### T1：配置契约实现收口

目标：让 `site.yaml` 成为 1.0 全新项目契约。

任务：

- 删除或拒绝旧字段运行路径。
- 将 warning-only 项升级为 rejected-with-message 或移除。
- 统一 `config check`、`doctor`、build-time validation。
- 给关键拒绝路径补 `BKT-000x` 诊断码。
- 定义 `build.report.enabled` 在 1.0 release/profile 下的默认策略。

验证：

- `dotnet test tests/Bukit.Config.Tests -c Release --no-restore`
- `dotnet test tests/Bukit.Cli.Tests -c Release --no-restore`

### T2：内容模型与 starter 正式样板收口

目标：让 starter 成为正式网站可信样板。

任务：

- 决定 `type` / `collection` 唯一写法。
- 决定 starter schema 字段：`seo_title`、`cover`、`cover_alt`、`tableOfContents`。
- 统一 Markdown / Notion / composite provider parity。
- 调整 publish audit 与 starter 内容，避免默认示例产生误导性 warning。

验证：

- `bash scripts/smoke.sh Release`
- `dotnet test tests/Bukit.Content.Tests -c Release --no-restore`
- `dotnet test tests/Bukit.Engine.Tests -c Release --no-restore`

### T3：路由契约最终语义

目标：冻结 1.0 路由行为。

必须决策：

- nested `route.outputPath` 是正式 1.0 契约还是拒绝。
- 顶层 `outputPath` 拒绝行为和诊断码。
- collection/type 匹配规则。
- list/taxonomy/pagination/archive/static/plugin route 是否进入 search/rss/sitemap 的默认策略。

任务：

- 补 route inventory golden tests。
- 固定 `routes.json` schema 与字段语义。
- 确保 route conflict / unsafe path 都有 `BKT-02xx`。

### T4：主题接口版本化

目标：冻结 1.0 主题接口。

任务：

- 定义 `theme.yaml` 必填字段、版本字段、engine range。
- 明确无 `theme.yaml` 主题是直接拒绝，还是要求显式生成 manifest。
- 决定 `extends`、fallbackDir、template capabilities 的正式语义。
- 让 theme doctor 输出可定位不兼容点。

验证：

- starter / alt / seo-best-practice 全部 doctor/build/smoke 通过。
- theme tests 通过。

### T5：插件接口 v2-only 收口

目标：1.0 只支持 external protocol v2，不保留 v1 fallback。

任务：

- 移除或拒绝 v1 fallback。
- 缺失 `capabilities` 的外部插件必须拒绝。
- 统一 plugin failure 到 `BKT-07xx`。
- 固化 request/response schema。
- 保证 stale output cleanup、env isolation、timeout、stdout/stderr limits 都有测试。

验证：

- ProtocolEchoPlugin 覆盖 success、bad JSON、empty stdout、timeout、ok=false、capability missing、output traversal、stale cleanup。
- `dotnet test tests/Bukit.Engine.Tests -c Release --no-restore`

### T6：可复现构建与审计产物

目标：让正式网站构建可审计、可比较、可回滚。

任务：

- 定义 release artifact bundle。
- 定义 normalized artifact compare。
- 扩展 `assets.json` 或新增 `artifact-manifest.json`，覆盖完整 public output。
- 将真实安全检查结果写入 `security-report.json`。
- clean twice 与 clean vs incremental 加入 CI。

验证：

- clean build 两次 normalized manifest 一致。
- clean vs incremental public output inventory 一致。
- `.bukit/*.json` schema validation 通过。

### T7：错误与安全边界最终审查

目标：所有 GA-locked failure path 都有稳定诊断和安全边界。

任务：

- 盘点无 code 的关键 `ConfigException` / `RenderException` / plugin failure。
- 统一 CLI 人类可读和机器可读错误输出。
- 审查 route/output/theme/plugin/media/download 安全边界。
- 将 `security-regression.sh` 设为 release blocker。

验证：

- `bash scripts/security-regression.sh Release`
- targeted security tests

## 2. 推荐执行顺序

1. T0：先拍板 contract matrix。
2. T1 / T3 / T5：处理 config、routing、plugin 的核心拒绝语义。
3. T2 / T4：完成内容模型和主题接口。
4. T6：完成 artifact、determinism、rollback。
5. T7：完成错误、诊断、安全边界。
6. 审核 Spark 的扫描、文档、starter 和小测试改动。
7. 运行最终 release gate 子集。

## 3. 禁止事项

Trae + DeepSeek V4 Pro 不允许：

- 把 BukitJalil 纳入 1.0。
- 新增大功能来掩盖信任缺口。
- 降低 doctor/smoke/security 严格度来过关。
- 保留 warning-only 运行路径。
- 为历史文件、历史网站、旧主题、旧插件协议保留运行时兼容模式。
- 把 Experimental 能力写成 GA。

额外要求：

- 所有 breaking cleanup 必须有拒绝行为、诊断码、测试和 changelog。
- 所有跨模块改动必须跑完整 release gate 子集。
- 所有“需要产品决定”的点必须写入决策记录。

## 4. 交付格式

```md
## Trae + DeepSeek V4 Pro 执行结果
- 任务编号：
- 是否完成：
- 修改文件：
- 实现修复：
- 契约澄清：
- 产品决策：

## 验证
- 命令：
- 结果：
- 未运行原因：

## 风险
- 剩余风险：
- 需要人工拍板：

## 下一步
- 建议继续任务：
```
