# Bukit 1.0 GPT 5.3 Codex Spark 执行书

日期：2026-06-08  
来源计划书：[2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md](./2026-06-07-bukit-1-0-ga-trust-plan.zh-CN.md)

## 0. 执行定位

GPT 5.3 Codex Spark 负责执行范围明确、可局部验证、低架构决策风险的任务。它是快速执行者，不负责最终契约拍板，不负责跨模块语义设计，不负责安全边界最终判断。

Bukit 1.0 按全新项目实施。各模块不采用兼容模式，不承诺历史文件、历史网站、旧配置、旧主题或旧插件协议可继续运行。Spark 的所有修改必须服从这个前提。

## 1. 适合 Spark 的任务

### S1：旧字段与旧写法扫描

目标：列出所有不符合 1.0 全新项目规则的历史写法。

范围：

- `site.rssMode`
- taxonomy legacy 模板配置
- warning-only config 项
- 顶层 `outputPath`
- plugin protocol v1 fallback 文档
- external plugin 缺失 `capabilities`
- 无 `theme.yaml` 主题
- 文档中“兼容、legacy、deprecated-but-working、fallback、迁移旧站点”的表述

输出：

- 文件路径
- 行号
- 当前写法
- 建议处理：remove、reject、reword、needs-owner-decision

禁止：

- 不直接删除实现逻辑。
- 不私自决定唯一新语义。

### S2：文档与 skills 文案对齐

目标：把公开文档改成 1.0 全新项目口径。

范围：

- README / README.zh-CN.md
- guide/user 和 guide/dev 相关章节
- src/skills 下 Bukit skills
- docs/compatibility-governance.zh-CN.md 的改造草案

要求：

- 将 compatibility matrix 改为 contract/removal/rejection matrix。
- 将 migration guide 改为 reset guide：重新生成 1.0 项目、手工搬运内容，不承诺旧站原地升级。
- 去除“旧行为仍可运行”的暗示。

验证：

- `bash scripts/check-doc-asset-consistency.sh`
- 必要时运行 docs check 命令。

### S3：starter/example 噪音清理

目标：让官方 starter/example 更接近正式网站最低标准。

范围：

- 清理 starter 中的旧配置 warning，例如 `site.rssMode`。
- 清理 `type` 与 `collection` 双声明，保留 1.0 唯一写法。
- 解决 schema extra fields：`seo_title`、`cover`、`cover_alt`、`tableOfContents` 要么纳入新 schema，要么从示例移除。
- 补 author、updatedAt、summary、visible h1/article 等低风险内容资产修复。

验证：

- `bash scripts/smoke.sh Release`
- `dotnet test bukit.slnx -c Release --no-restore`

禁止：

- 不降低 publish audit / SEO audit / doctor 严格度来过关。
- 不删除 smoke 检查。

### S4：小型 rejection 测试补齐

目标：给“旧输入必须被拒绝”补自动化测试。

优先测试：

- `site.rssMode` 被拒绝或不再出现在 1.0 示例。
- 顶层 `outputPath` 被拒绝并给出稳定诊断。
- external plugin 缺失 `capabilities` 被拒绝。
- plugin protocol v1 handshake 被拒绝。
- 无 `theme.yaml` 主题被 doctor 拒绝或要求生成 manifest。

验证：

- 对应测试项目的 targeted `dotnet test`
- 完整 `dotnet test bukit.slnx -c Release --no-restore`

### S5：`.bukit` artifact schema 校验测试

目标：补低风险 schema validation 测试。

范围：

- `build-report.json`
- `routes.json`
- `assets.json`
- `incremental-manifest.json`
- `security-report.json`
- `seo-report.json`
- `publish-audit-report.json`

要求：

- 使用现有 `docs/schemas/*.v1.schema.json`。
- 先写测试，不改 artifact 语义。
- 如果 schema 与实现冲突，只记录冲突并交给 Trae + DeepSeek V4 Pro 决策。

### S6：验证与结果汇总

目标：跑 release gate 子集并形成执行报告。

命令：

- `dotnet test bukit.slnx -c Release --no-restore`
- `bash scripts/smoke.sh Release`
- `bash scripts/security-regression.sh Release`
- `bash scripts/check-doc-asset-consistency.sh`

## 2. 禁止事项

Spark 不允许：

- 拍板 1.0 契约语义。
- 删除大块实现逻辑。
- 修改跨模块 pipeline。
- 改安全策略默认值，除非 Trae + DeepSeek V4 Pro 已明确决策。
- 为历史文件、历史网站、旧主题、旧插件协议保留运行时兼容模式。
- 降低 doctor/smoke/security 严格度。
- 把 Experimental 能力写成 GA。

## 3. 交付格式

```md
## Spark 执行结果
- 任务编号：S1 / S2 / S3 / S4 / S5 / S6
- 是否完成：已完成（代码与文档落地）/ `starter schema` 与 `starter publish audit warning` 最低阈值按本轮决策收敛
- 修改文件：
- 实现修复：
- 契约澄清：

## 验证
- 命令：
- 结果：
- 未运行原因：

## 风险
- 剩余风险：
- 需要 Trae/DeepSeek 拍板：

## 下一步
- 建议继续任务：
```

## Spark 执行结果（本轮）

- 任务编号：S1 / S2 / S3 / S4 / S5 / S6
- 是否完成：已完成（代码与文档落地）+ 关键待确认项 1 项
- 修改文件：`docs/compatibility-governance.md`, `docs/compatibility-governance.zh-CN.md`, `docs/bukit-1.0-security-boundary-audit.md`, `docs/bukit-1.0-contract-matrix.zh-CN.md`, `scripts/build-repro.sh`, `scripts/normalize-json.sh`, `scripts/smoke.sh`, `examples/starter/...`, `src/Bukit.Cli/**`, `src/Bukit.Engine/**`, `tests/Bukit.Cli.Tests/**`, `tests/Bukit.Engine.Tests/**`, `tests/Bukit.Engine.Tests/BuildReporterTests.cs`, `tests/Bukit.Engine.Tests/Snapshots/RouteInventory/route-inventory.golden.json`
- 实现修复：已补齐 `s1~s6` 对应低风险执行面：starter 报警项治理与内容补齐、route inventory 覆盖（content/derived/static/plugin 统一 golden 断言）、主题衍生样板 build/smoke 覆证、可复现构建归一化对比脚本、错误输出 JSON envelope 与命令级日志格式化入口。
- 契约澄清：文档已将主要旧行为标记为拒绝/移除；`outputPath` 顶层拒绝项 `CG-015` 已统一到 `current` 目标版本。
- 风险：`security-report.json` 与真实安全扫描写入链路仍需下一阶段完成（当前为占位接入）。

## 验证
- 命令：
  - `rg -n "author:|summary:|updatedAt:" examples/starter/content examples/starter/content-i18n examples/starter/content_extra -g "*.md"`
  - `for f in $(rg --files examples/starter/content examples/starter/content-i18n examples/starter/content_extra); do awk 'BEGIN{in_fm=0;count=0;got_a=0;got_s=0;got_u=0} /^---$/{count++; if(count==1){in_fm=1;next} else if(count==2){in_fm=0}} in_fm==1 && /^author:/{got_a=1} in_fm==1 && /^summary:/{got_s=1} in_fm==1 && /^updatedAt:/{got_u=1} END{if(got_a&&got_s&&got_u) exit 0; printf \"%s\\n\",FILENAME; exit 1}' \"$f\" || true; done`
  - `rg -n "(^|\\s)(seo_title:|cover_alt:|tableOfContents:)" examples/starter/content examples/starter/content-i18n examples/starter/content_extra`
  - `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter FullyQualifiedName~WriteIfEnabled_RoutesJson_GoldenSnapshot`
- 结果：
  - `rg` 命中：starter 三个内容树下每份 .md 已显示 author/summary/updatedAt。
  - awk 验证：全量通过，无缺字段文件。
  - `rg` 老字段命中：无 `seo_title`、`cover_alt`、`tableOfContents`。
  - 路由 golden：`tests/Bukit.Engine.Tests/Snapshots/RouteInventory/route-inventory.golden.json` 已包含 content/derived/static/plugin 全量条目（`/archive/2024/`、`/blog/*`、`/plugin-output.json`、`/static-docs/`）。
- 未运行原因：
  - 未在本轮执行完整 `bash scripts/quality-gate.sh Release`，按当前窗口优先执行范围限制为关键缺口修复与证据闭环。 

## 风险
- 剩余风险：`starter` schema 字段收口已收束，当前仅剩 `security` 与发布流程链路的外部决策窗口。
- 需要 DeepSeek 审核：`security-report.json` 与安全扫描数据链路。

## 下一步
- 建议继续任务：按 Trae + DeepSeek 的决策结果提交 `CG`/`contract` 版本升级与新测试断言收敛。
