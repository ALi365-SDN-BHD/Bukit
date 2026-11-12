# Bukit 多语言站点根路径问题与默认语言根目录布局提案

- 日期：2026-11-12
- 基线：`33c20cd7bc2105191a936516e70530ecbc3cd235`
- 范围：`site.languages` 多语言构建的输出目录结构、根目录聚合文件、Worktree 构建、跳转页
- 规范性提案：`openspec/changes/add-i18n-default-language-root/`（proposal、design、specs、tasks；`openspec validate --strict` 通过）
- 本轮性质：**只分析和提案，不实施**。未修改源码、测试、配置、CI/gate；未运行 `dotnet test`。全部设计决策已由用户确认；实施须作为单独的 Core 任务开展。

---

## 1. 问题

配置 `site.languages` 后，Bukit 把每种语言（包括默认语言）都输出到 `dist/<lang>/`，baseUrl 为 `<baseUrl>/<lang>`。根目录只放聚合文件（sitemap、feed、search、`llms.txt`、`robots.txt`、`agent-manifest.json`），**没有 `index.html` 和 `404.html`**。现有文档的说法是"在主机或 CDN 上配重定向"。

GitHub Pages 等纯静态托管不支持服务端重定向，因此：

1. 站点首页 `/` 直接返回 404，即使配置了 `defaultLanguage` 也一样。
2. 自定义 404 页不生效：`404.html` 被生成到 `dist/<lang>/404.html`，而 GitHub Pages 只使用根目录的 `404.html`。

仓库自带的 `examples/github-pages-workflow.yml` 直接上传构建输出，按它部署的多语言站点打开就是坏的。Netlify、Cloudflare Pages、S3 等平台也必须另外配置规则才能用。

## 2. 当前多语言目录结构

源目录所有语言共用一份：

- 页面语言只来自 front matter `language`（Notion 用 `propertyMap.Language`），不按文件名或文件夹推断。没写语言的页面归默认语言。
- `content/<lang>/` 子目录只在同一语言内文件同名、需要用相对路径作 slug 时，用来去掉语言目录前缀（`MarkdownFolderProvider.ResolveVariantDirectory`），不决定页面语言。
- 界面文案放在 `data/<lang>/*.yaml`，通过 `site.data_files.<lang>.<file>` 读取；`layouts/`、`assets/`、`static/` 所有语言共用一套。

输出目录：

```
dist/
├── en/            ← baseUrl = <url>/en，默认语言也带前缀
├── zh-CN/
├── sitemap.xml / feed / search / llms.txt / robots.txt / agent-manifest.json  ← 根目录聚合
└── （没有 index.html，也没有 404.html）
```

## 3. 方案对比与选择

| 方案 | 做法 | 结论 |
|---|---|---|
| A | 保持所有语言带前缀，Core 在根目录生成跳转页 | 改动小，但只解决首页，不解决根目录 404 |
| B | 默认语言不带前缀，直接放在根目录，其他语言保留前缀 | 解决首页和 404，SEO 友好，与主流工具一致；改动面大 |
| C | 只改文档和示例工作流，构建后用脚本补根目录文件 | 每个用户都要自己处理，问题本身还在 |

**选择：方案 B 作为可选布局，并把方案 A 并入同一设计。** 参照 Hugo 和 Astro，两种布局各自生成对应的跳转页，使任何布局下的首页都不再 404。

## 4. 主流工具的做法（2026-11 调研）

| 产品 | 默认语言默认位置 | 跳转处理 | 与本设计相关的做法 |
|---|---|---|---|
| Hugo | 根目录（`defaultContentLanguageInSubdir: false`） | 带前缀时生成根目录跳转到 `/<lang>/`；放根目录时生成 `/<lang>/` 跳转到 `/`；均可关闭 | 放根目录时，根 `sitemap.xml` 是索引，默认语言 sitemap 仍在 `/<lang>/sitemap.xml`，地址不带前缀 |
| Astro | 根目录（`prefixDefaultLocale: false`） | 开启前缀时根 `index` 页生成 meta refresh 跳转 | 根 `index` 页必需 |
| Docusaurus | 根目录，其他语言 `/<locale>/` | 交给托管层 | 每种语言独立构建；GitHub Pages 上只能显示默认语言 404 |
| VitePress / Starlight | `root` 语言键表示根目录 | VitePress 不自动跳转 `/` | — |
| Zola | 根目录，其他语言 `/{code}/` | — | feed、搜索索引按语言分别开关 |
| Jekyll Polyglot | 根目录，其他语言带前缀 | — | `exclude_from_localization` 指定只放根目录的文件；每种语言独立进程 |
| Nuxt i18n | 多种策略可选 | — | `prefix_and_default` 让默认语言有两个地址，有重复内容风险 |
| Eleventy | 无内置方案 | 文档给出 Netlify 跳转写法 | — |

结论：多数工具默认把默认语言放在根目录，Bukit 现在"所有语言都带前缀"的做法是少数。各工具文档都没有提到路由与语言前缀冲突的校验，本设计的保留前缀检查更严格。

## 5. 已确认的设计决策

| # | 决策 | 结果 |
|---|---|---|
| 1 | 布局配置项 | `site.defaultLanguageInSubdir`，布尔，默认 `true`（现状）；与 Hugo 命名一致 |
| 2 | 跳转页配置项 | `site.defaultLanguageRedirect`，布尔，**默认 `true`** |
| 3 | index 模式默认语言 sitemap | 写在 `/<lang>/sitemap.xml`，地址不带前缀（与 Hugo 一致） |
| 4 | `search.mode: merged` | 本版本不支持，与根目录布局组合时配置校验失败 |
| 5 | 实施方式 | 单独的 Core 任务；确认决策不等于授权实施 |

## 6. 设计要点

### 6.1 两种布局

| | `defaultLanguageInSubdir: true`（默认） | `false` |
|---|---|---|
| 默认语言输出 | `dist/en/`，baseUrl `/en` | `dist/`，baseUrl `/` |
| 其他语言 | `dist/zh-CN/` | `dist/zh-CN/` |
| 跳转页（`defaultLanguageRedirect: true`） | 根目录 `index.html` → `/en/` | `/en/index.html` → `/` |
| 根目录 `404.html` | 无（不复制，Hugo 也不做） | 默认语言的 404 |

跳转页带 meta refresh、canonical、`<title>` 和 `noindex`，配置了 `site.url` 时用绝对地址，不进入 sitemap、搜索、llms、agent-manifest 和 feed。根目录布局下，如果用户已有输出占用 `/en/index.html`，以用户内容为准。现有 `AliasPlugin` 和 `TaxonomyRedirectWriter` 两份跳转页代码合并为一份。

### 6.2 单一布局策略

新增内部策略 `I18nOutputLayout`，统一计算每种语言的 baseUrl、输出目录、相对前缀、报告目录和保留前缀。当前散落的拼接点全部改为调用它：`CombineBaseUrlWithLanguage` 的 3 个调用点、`SiteEngine`/`WorktreeBuildRun` 的输出目录、`WorktreeBuildPaths` 的 manifest 路径，以及 `I18nMergedVariantState`、`SeoAuditReportWriter` 中把语言代码直接当输出子目录的 3 处（漏改会让默认语言的审计悄悄读错路径）。

### 6.3 根目录文件归属（根目录布局）

| 文件 | 处理 |
|---|---|
| `agent-manifest.json`、`robots.txt`、`llms.txt`、`llms-full.txt` | 只由根目录聚合生成，默认语言不计划、不写出 |
| sitemap | split：默认语言写 `/sitemap.xml`；merged：只有根目录；index：根目录是索引，默认语言写 `/<lang>/sitemap.xml` |
| feed | split：默认语言写在根目录；merged：只有根目录 |
| search | split / index：默认语言写 `/search.json`；merged：不支持 |
| `.bukit/` 报告 | 默认语言 5 种报告（seo、publish-audit、seo-route-map、geo、analytics）改写到 `.bukit/variants/<lang>/` |
| 默认语言 `static/` | `robots.txt` 作为整站 robots；与其他根目录独占文件同名时报 `i18n-root-output-conflict`；`404.html`、`CNAME` 等正常落到根目录 |

所有语言的计划输出清单按当前 sitemap/feed mode 生成，与插件实际写出行为一致。现有 `AssetOutputPlan.Validate` 重复检查保留为最后防线。

### 6.4 路由保留前缀

根目录布局下，默认语言所有输出（内容、列表/分页、分类、alias、派生文档、static、插件输出）的第一个路径段不得等于其他语言代码，报 `i18n-route-reserved-language-prefix`。

- 在任何平台上都不区分大小写，避免 macOS 与 Linux CI 结果不一致。
- 写文件前检查：现有 `AssetOutputPlan.Validate` 不覆盖路由 HTML，Worktree 父进程的路径校验是事后的。
- 默认语言自身代码不保留：允许 `/en/` 跳转页、`/en/sitemap.xml` 和用户为旧地址写的 alias。

### 6.5 Worktree 构建

默认语言 Worker 输出到 staging 根目录，其他 Worker 仍输出到 `staging/<lang>/`。现状下各 Worker 写入区域互不重叠，根目录布局下默认语言的区域包含其他语言，因此：默认语言 Worker 在渲染前做保留前缀检查；父进程 `I18nWorktreeProtocol.ValidateOutputPaths` 拒绝越界结果；禁止在语言输出目录上以裁剪模式调用 `DirectoryCopy.Sync`。规范上修改 `i18n-worktree-build` 的"Worker 结果必须完整且可验证"和"根级输出必须保持现有语义"两条要求，加入根目录布局下 Worktree 与进程内构建等价的场景。

### 6.6 布局切换和增量构建

两个新配置都只在非默认值时写入增量站点哈希，未启用的站点升级后不会全量重渲染。布局变化时走现有受保护清理。已核实的安全点：恢复标记只在根目录写；整体清理只在根目录这一层执行；`PublicOutputPrivacyCheck`、`BuildOutputInventory` 只在根目录这一层扫描。

## 7. 影响与风险

### 7.1 默认值下的升级变化

- 现有多语言站点升级后，根目录新增跳转页 `index.html`。纯静态托管上首页从 404 变为跳转，这是想要的修复。
- Netlify 等平台已配置未加 `force` 的 `/` → `/<lang>/` 301 时，新文件会盖过这条规则，跳转退化为 meta refresh；这类站点应设 `defaultLanguageRedirect: false`。CHANGELOG 标为行为变化。
- alias 和分类跳转页新增 `<title>` 和 `noindex`。
- 页面输出、URL、报告路径和增量哈希不变。

### 7.2 启用根目录布局后的站点侧影响

- 默认语言旧地址 `/<lang>/...` 失效（旧首页由跳转页兜底）；需要托管层 301，GitHub Pages 只能用 meta refresh。
- CDN 已有的 `/` → `/<lang>/` 规则要删除，改为 `/<lang>/*` → `/*`；清理 CDN 缓存。
- `seo diff` 基线要重建；按 `/<lang>/` 路径筛选的 Analytics 报表失效；写死 `dist/<lang>` 的 CI 脚本要修改。
- split 模式下默认语言 sitemap 地址变化，需重新提交 Search Console。
- `bukit seo audit --dir dist/<lang>` 改为 `--report dist/.bukit/variants/<lang>/seo-report.json`。
- `feed.mode: merged` 时默认语言的 `/rss.xml` 变为多语言合并 feed。
- 默认语言作者不能使用与其他语言代码相同的 slug、标签、分类；**以后新增语言可能让已有内容报错**。
- 插件写到根目录的 service worker 作用范围会覆盖所有语言；插件不应把 `context.OutputDir` 当作站点根目录。

### 7.3 主要风险

| 编号 | 风险 | 缓解 |
|---|---|---|
| R1 | 切换布局后旧地址失效 | 默认不启用；旧首页跳转页；托管层 301 文档；alias |
| R2 | Worktree 写入区域重叠 | 写前检查 + 父进程事后检查 |
| R3 | 根目录 llms、agent-manifest 只保留合并版本 | 与其他语言处境一致，接受 |
| R4 | 计划输出清单变化 | 测试确认不误删、不全量重建 |
| R5 | 大小写与文件系统差异 | 统一不区分大小写比较 |
| R6 | 新增语言变成破坏性操作 | 报错指明新增的语言代码；文档说明 |
| R7 | 默认值新增根目录文件 | 可关闭；CHANGELOG 标为行为变化 |

## 8. 实施范围

- 代码：约 20 个 Core 文件，涉及 `Bukit.Config`、`Bukit.Engine`、`Bukit.Engine.Audit`、`Bukit.Engine.Analytics`、`Bukit.Cli`；`SiteConfig` 新增两个属性（新增性变更，更新公共 API 基线）；新增两个报错码。
- 测试：复核约 24 个 i18n 相关测试文件（默认值下保持通过作为对照），新增 Config、Engine、CLI（Worktree）专项用例。
- 文档：i18n skill、用户指南、配置文档、站点迁移清单、插件和主题作者说明、中英马 CHANGELOG。
- 详细清单见 `openspec/changes/add-i18n-default-language-root/tasks.md`。

## 9. 后续事项（不在本提案范围）

- 为全部旧地址 `/<lang>/*` 自动生成迁移跳转。
- `search.mode: merged` 与根目录布局的组合。
- 资源目录在各语言间共用。
- `examples/github-pages-workflow.yml` 上传了 `.bukit/` 报告（现有问题）。
