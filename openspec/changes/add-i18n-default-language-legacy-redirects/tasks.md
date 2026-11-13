# Tasks

唯一实施清单，测试编号对应 `docs/analysis/bukit-core-i18n-default-language-migration-redirects-proposal-2026-11-13.zh-CN.md` 第 8.2 节。只有取得实际证据的事项才勾选。

## 1. 配置

- [x] `SiteConfig.DefaultLanguageLegacyRedirects`（默认 `false`）；YAML 读取、严格字段白名单、JSON Schema（T1、T2）。
- [x] 公共 API 基线新增该属性。

## 2. 警告

- [x] 单语言站点：`WarnLayoutSettingsWithoutLanguages` 的提前返回条件加入新配置，事件字段加入新配置（T3）。
- [x] 多语言带前缀布局开启时记 `i18n.config.legacy_redirects_ignored`（T3）。

## 3. alias

- [x] alias 跳转文档记录 `redirectTarget`（页面路由 URL）。

## 4. 派生

- [x] 旧首页跳转占用检查加入列表路由（F1）。
- [x] 批量派生：来源、排除首页与 404 与非 HTML、按输出路径去重、HashSet 占用检查、alias 迁移与跳过、ID/slug 短哈希、汇总日志、`defaultLanguageRedirect: false` 时的 Info（T4–T6、T11、T14–T17、T19、T20）。
- [x] 写入前逐段拒绝符号链接（T13）。

## 5. 增量与 Worktree

- [x] 增量关闭清理、内容替换跳转、布局切换（T7–T9）。
- [x] 新配置不进入站点哈希，且普通页面不重新渲染（T12、T18）；任一不成立则退回 D7 b。
- [x] Worktree 与进程内构建等价（T10）。

## 6. 文档

- [x] 用户指南、配置文档、i18n skill、站点迁移清单、中英马 CHANGELOG。

## 验证证据（2026-11-13）

- `codex-workflow.py closure` 生成的专项命令：Config Release 过滤 194/194、Config 全项目 534/534、Engine Release 过滤（另加 `SiteEngineLogger`）684/684、Cli Worktree 过滤 68/68、Architecture 422/422；`public-api-drift.sh check`、drift 自测、skills/cli-docs/active-links/size-policy、两条 spec 与本变更 `openspec validate --strict` 全部通过。
- 实施说明：多语言警告放在新方法 `I18nLanguagePolicy.WarnIgnoredLegacyRedirects`；符号链接逐段检查抽为 `BuildTransaction.RejectLinkSegments`，由 `RejectGeneratedOutputLinks` 与迁移派生共用（4.8 方案 a）；仓库无独立迁移清单文件，该项写入 `guide/user/11-i18n-seo.md` 的 "Migrating an existing site to the root layout" 小节。
- 未运行：closure 列出的 Engine 与 Cli 全项目测试，以及任何全量 gate。
