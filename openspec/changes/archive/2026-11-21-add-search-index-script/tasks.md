# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。三个决定已于 2026-11-16 确认。

## 0. 准备

- [x] 0.1 用户确认文件名、总是生成、先 `fetch` 后 `<script>` 三个决定；把确认日期写进
      proposal.md 的“批准范围”。
- [x] 0.2 用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包，确认没有未映射
      的文件，并写下本变更的专项测试命令。

## 1. 脚本形式的索引

- [x] 1.1 `SearchIndexBuilder` 两个写索引的方法在写完 `search.json` 后写 `search.json.js`；
      `DefaultSearchIndexBuilderTests` 断言前缀、后缀和中间内容与 `search.json` 一致。
- [x] 1.2 单语言、多语言 split/index、根目录布局 merged 各一条测试，覆盖规范里三个
      “Script form” 场景，并断言没有 `search.index.json.js`。

## 2. 生命周期

- [x] 2.1 `PublicOutputLifecycle.ProjectionPlan` 随 `search` 项加入 `search.json.js`；测试：
      换配置后重建两次（都非增量），不再写某语言索引时，两个文件都被删除。
- [x] 2.2 `I18nRootOutputConflictGuard` 把 `search.json.js` 视为根目录独占输出；测试：
      `static/search.json.js` 报 `i18n-root-output-conflict`。
- [x] 2.3 确认 `PublishRepresentations` 和发布审计不变；`PublishRepresentationRegistryTests`
      和审计相关测试照常通过。Stage7 golden（unix、windows）加入 `search.json.js` 并更新
      `bukit-search.html`；windows 版按与 unix 相同的 CRLF 关系推导，未在 Windows 上运行。

## 3. 搜索界面

- [x] 3.1 `SearchIndexPlugin.GetSearchJs`：`fetch` 失败后加载同源索引的 `<data-search-url>.js`（跨源
      索引只 `fetch`、不执行），两者都失败时显示 "Search is unavailable"；
      `SearchIndexPluginExtendedTests` 断言回退代码和同源检查存在。
- [x] 3.2 `StarterTheme/SearchTemplate.html` 加同样的回退；脚手架测试断言新生成的
      `search.html` 含回退。
- [x] 3.3 浏览器验证（2026-11-16，Playwright 1.65.0，三个浏览器全部通过）：构建 starter 站点，在 Chromium、Firefox、WebKit 里用
      `connect-src 'none'` 的沙箱 CSP 和不带 CSP 两种方式打开搜索页和片段，记录能搜出结果，
      且不带 CSP 时没有请求 `search.json.js`。

## 4. 文档

- [x] 4.1 `guide/user/10-built-in-outputs.md` 和搜索相关配置文档写明 `search.json.js`、加载
      顺序、同源限制，以及已有 `search.html` 的迁移说明（复制整段 `<script>`，见
      `themes/starter/layouts/pages/search.html`）。
- [x] 4.2 CHANGELOG 三语。

## 5. 收尾

- [x] 5.1 专项测试通过；`openspec validate add-search-index-script --strict` 通过。
- [x] 5.2 一次 delta review（独立子代理，2026-11-16）：Critical 0、Important 2（跨源
      `data-search-url` 会被当脚本执行；迁移说明不足以照做），均已修复；Minor 8 条记录不阻塞。
