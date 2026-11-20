## Context

动机见 proposal.md。现状：

- `search.json` 由 `SearchIndexBuilder.GenerateSingleSearchIndex`（各语言、单语言）和
  `GenerateMergedSearchIndex`（根目录合并索引，经 `I18nRootSearchWriter`）写出；增量路径
  `PublishAggregateProjectionWriters` 也调 `GenerateSingleSearchIndex`。
- 路径规则在 `I18nOutputLayout` / `PublicOutputLifecycle.SearchPlanPath`；投影计划决定构建
  清单归属和过期文件清理；`I18nRootOutputConflictGuard` 拒绝与根目录独占输出同名的静态文件。
- 片段 JS 在 `SearchIndexPlugin.GetSearchJs`；Starter 搜索页在
  `Bukit.Cli/Resources/StarterTheme/SearchTemplate.html`。

## Goals / Non-Goals

**Goals:**
- 每个 `search.json` 旁边有内容一致的 `search.json.js`，由同一处代码写出，路径不可能不一致。
- 发布后的网站加载行为不变（仍是一次 `fetch`）。

**Non-Goals:**
- 不引入新配置项、新诊断码或新的发布表示。

## Decisions

1. **在写 `search.json` 的两个方法末尾写脚本形式。** 写完 JSON 后把同一份字节包上前缀和
   后缀写到 `search.json.js`。所有调用方（插件、增量投影、根目录合并）自动得到它，不用逐个
   改调用方。备选：在每个调用方另写一次，容易漏掉一条路径，不选。
2. **内容是 `self.bukitSearchIndex=<JSON>;`。** `Utf8JsonWriter` 输出的 JSON 本身就是合法
   的 JS 表达式（默认编码器转义 HTML 敏感字符；ES2019 起 U+2028/2029 也合法）。用固定的
   全局变量，不按 URL 建表：两个内置界面每页只加载一个索引。备选：JSONP 回调或按 URL 分键，
   更复杂，现在没有需要。
3. **文件名 `search.json.js`。** 一眼看出跟随哪个索引，不容易和主题的 `static/search.js`
   冲突。
4. **先 `fetch`，失败再插入 `<script>`。** 发布后的网站不多发请求、行为不变。备选：先
   `<script>`，能免掉 CSP 报错，但改变所有网站的加载方式，不选。
5. **生命周期跟随 `search.json`。** `ProjectionPlan` 在加入 `search` 项的地方同时加入
   `search.json.js`（kind 例如 `search-script`），这样清单归属、过期清理随之生效；
   `I18nRootOutputConflictGuard` 把它加进根目录独占文件列表。`PublishRepresentations` 和审计
   读取路径不改。

## Risks / Trade-offs

- [输出多一份与索引同样大的文件] → 托管压缩后代价有限；在 CHANGELOG 写明。如用户不接受
  “总是生成”，需要回到提案改成配置项。
- [已脚手架的 `search.html` 仍只用 `fetch`] → CHANGELOG 和指南给出可复制的回退代码。
- [禁止 `connect-src` 的页面控制台多一条 CSP 报错] → 预期行为，在指南里说明。
- [第三方脚本也定义 `bukitSearchIndex`] → 名字带 `bukit` 前缀；界面只接受数组。

## Migration Plan

新增输出，无需迁移。回滚即撤销本变更，下一次构建会按投影计划清理旧的 `search.json.js`。
BukitJalil 升级内置 Bukit 后，在三个浏览器的预览里验证搜索页能出结果。
