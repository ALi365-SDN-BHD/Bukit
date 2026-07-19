# Bukit Core 八项修复文档覆盖矩阵

> 日期：2026-07-19
>
> 技术状态来源：[八项最终 Aggregate 关闭审计](bukit-core-eight-findings-final-aggregate-closure-audit-2026-07-19.zh-CN.md)
>
> 范围：F-01～F-08 的现行用户、开发者、Agent、安全、兼容性、契约、故障排查和变更记录文档

## 1. 覆盖结论

八项修复已从只存在于审计材料的状态，补充到所有相关正式文档类型：

| 文档类型 | 正式入口 | 覆盖内容 |
|---|---|---|
| 用户行为 | `guide/user/20-core-safety-reliability.md` | 八项用户可观察行为、操作建议和排除边界 |
| CLI/配置参考 | `guide/user/04-site-yaml-config.md`、`12-cli-reference.md`、`16-parameter-cheatsheet.md` | clean、search cap、media concurrency、symlink 配置语义 |
| 输出说明 | `guide/user/10-built-in-outputs.md` | 默认 search DOM、search cap、build health 和 public inventory |
| 主题/模板 | `guide/user/08-themes-templates.md` | `bukit.templates.yaml` 与下一次调用刷新语义 |
| 故障排查 | `guide/user/14-troubleshooting.md` | collision、clean refusal、symlink、cache、cap、media、report 差异 |
| 开发架构 | `guide/dev/core-safety-reliability-invariants.md` | 八项内部不变量、实现锚点、测试面和不可扩张边界 |
| 模块开发文档 | `guide/dev/architecture.md`、`cache-clean.md`、`content.md`、`theme.md`、`rendering-scriban.md`、`incremental-build.md`、`built-in-plugins.md`、`engine-outputs.md`、`observability.md`、`config-site-yaml.md` | 各模块调用链、生命周期和数据来源 |
| Agent skills | `guide/skills/bukit-cli-reference`、`bukit-config`、`bukit-debug`、`bukit-content`、`bukit-templating` | 自动化任务使用的可执行约束与排错入口 |
| 安全策略 | `SECURITY.md`、`SECURITY.zh-CN.md`、`SECURITY.ms.md`、`docs/bukit-1.0-security-boundary-audit.md` | F-01、F-02、F-04 及排除范围 |
| 兼容性/契约 | `docs/compatibility-governance*.md`、`docs/bukit-1.0-contract-matrix.zh-CN.md` | 八项行为收紧、BKT-0604、schema/API/protocol 不变 |
| 发行说明 | `CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md` | `Unreleased` 八项修复摘要 |
| 审计证据 | 修复前全面审计、专项方案、最终关闭台账 | 历史发现、受控方案、技术关闭与过程偏差 |

README 三语入口已链接统一用户安全/可靠性章节；用户无需从历史审计中推断当前行为。

## 2. Finding 到文档映射

| Finding | 用户文档 | 开发/架构文档 | 安全/兼容性/发行 |
|---|---|---|---|
| F-01 | safe clean、CLI、troubleshooting | cleanup authority、cache-clean | SECURITY 三语、security audit、compatibility、CHANGELOG 三语 |
| F-02 | search UI 文本边界、built-in outputs | search DOM invariant、built-in plugin/output | SECURITY 三语、security audit、CHANGELOG 三语 |
| F-03 | output ownership、collision troubleshooting | preflight、filesystem identity、manifest owner | contract matrix、compatibility、CHANGELOG 三语 |
| F-04 | symlink boundary、配置与 troubleshooting | safe enumeration、content/theme/output | SECURITY 三语、security audit、contract/compatibility、CHANGELOG 三语 |
| F-05 | template capability manifest、live decision | fingerprint、call-scoped analysis、cache isolation | compatibility、CHANGELOG 三语 |
| F-06 | config、search output、troubleshooting | propagation、UTF-16/surrogate boundary | contract/compatibility、CHANGELOG 三语 |
| F-07 | config、media budget、troubleshooting | operation/store gate、cancellation boundary | contract/compatibility、CHANGELOG 三语 |
| F-08 | built-in outputs、report troubleshooting | logger/inventory/schema/hash ordering | contract/compatibility、CHANGELOG 三语 |

## 3. 防止文档过度承诺

所有正式文档统一保留以下边界：

- F-01 不承诺 handle-based 原子删除或恶意 symlink-swap 防护；
- F-02 只保证 Core 默认 search UI，不代表全站 sanitizer/CSP；
- F-03 不覆盖任意 after-build 第三方插件输出；
- F-04 不宣称仓库所有 walker 已统一，也不把 `followSymlinks` 写成全局开关；
- F-05 不宣称所有 cache 已移除、存在 eviction，或 watcher 瞬时送达；
- F-06 使用 UTF-16 code unit，不承诺 grapheme cluster；
- F-07 是 operation/store scope，不是 process-wide/site-wide 网络预算；
- F-08 的 build counts 与 SEO/publish/security 分离，`generatedFiles` 不是内部报告或部署证明。

## 4. 未发生的契约变化

本次文档补充没有修改：

- Core 源码或公共 API；
- `site.yaml` schema 或字段默认值；
- `build-report.v1` 及其他 JSON schema；
- 外部插件协议、manifest/config schema；
- asset URL 或持久化格式；
- `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 备份树。

## 5. 后续维护规则

修改八项相关实现时，应同时复核用户章节、开发不变量、相关 skill、SECURITY/compatibility、CHANGELOG 和本矩阵。若能力范围扩大，例如建立全局第三方插件 output ownership，必须另立契约任务，不能直接改写 F-03 已关闭范围。
