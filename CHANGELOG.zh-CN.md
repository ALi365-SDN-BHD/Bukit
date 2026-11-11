# 更新日志

Bukit 所有重要变更都将记录在此文件中。

## [Unreleased]

- **llms curation 诊断恢复生效**：页面级 `geo.llms` curation 写错时（未知键、`visibility` 或 `tier` 取值错误、`priority` 不是整数（如 `"10"`）或超出 -100 到 100、`llms: false`，或非空的 `geo:` 不是映射），构建现在会对每个路由页面（包括 noindex 页面）输出 `geo.llms_field_unknown`、`geo.llms_visibility_invalid`、`geo.llms_tier_invalid`、`geo.llms_priority_invalid` 或 `geo.llms_shape_invalid`（`geo` 或 `llms` 的值不是映射），均附带 `route=<输出路径>`，在 `site.seo.diagnostics: strict` 下构建失败。自发布投影重构以来这些检查一直没有执行，这类页面会在没有任何警告的情况下从 `llms.txt` 和 `llms-full.txt` 中消失。使用 `strict` 且存在错误 curation 的站点现在会构建失败，请修正 front matter 或改用 `visibility: exclude`。正确的 curation 和输出不变。空的（或只含空白的）`geo:` 或 `llms:` 键现在视为不存在：这类页面会进入 `llms.txt` 和 `llms-full.txt`，并且不再导致 `strict` 构建失败（之前是静默排除）。

## [2.0.15] - 2026-11-11

- **过期图片链接立即失败**：图片下载被拒绝（HTTP 400、401 或 403），且网址自身的签名已过期（AWS SigV4、Google Cloud Storage V4、AWS SigV2/CloudFront 或 Notion 文件代理）时，不再按 `content.media.maxRetries` 重试，因为过期签名不可能成功。主要加快仍含过期链接的 `readonly` Notion 缓存构建。失败记录、`onFailure` 结果和已保存图片的复用不变，警告增加 `signature=expired`。其他失败照常重试。
- **共享数据中的签名链接会给出警告**：增量构建时，如果 `site.modules`、`site.data`、`site.data_index`、`site.related_pages`、`site.data_files`、路由元数据源或页面字段（键 `pages`）里还有已识别签名方案的链接（AWS SigV4、Google Cloud Storage V4、AWS SigV2/CloudFront、Notion 文件代理），Bukit 会输出一条 `event=incremental.signed_links` 警告，列出每个读取键及其链接数，多语言构建时还带 `language=`。这些链接每次拉取都可能变，读取它们的页面每次构建都可能重渲染；用 `content.media` 把文件本地化即可避免。哈希与输出不变。
- **不再显示其他页面的 `bodyFingerprint`**：`pages` 列表和 section `source` 条目的 `fields` 不再包含内部字段 `bodyFingerprint`。它标识正文，而这些列表并不跟踪正文，打印它的页面可能在其他页面正文修改后保留旧文本。页面自身的 `page.fields` 不变。
- **插件名与选项名的大小写不再触发全站重渲染**：插件名和顶层选项名本就不区分大小写，增量哈希现在也一样处理，只改大小写不再重渲染每个页面。

## [2.0.14] - 2026-11-10

- **Notion 正文跟随渲染器变化与同一分钟内的编辑**：Notion 正文缓存（格式 2）记录渲染器版本，以及取自 Notion `Date` 响应头的抓取时间。`readwrite` 模式下，只有渲染器版本和 `last_edited_time` 相同、链接未过期、且抓取时间不早于编辑所在分钟结束后 5 秒时才复用缓存，因此抓取后同一分钟内的再次编辑不再丢失。增量正文指纹改为 `{last_edited_time}:renderer={版本}[:unsettled]:content={on|off}`；没有 `last_edited_time` 的页面改为根据正文计算内容哈希。升级后第一次 `readwrite` 构建会重新拉取全部 Notion 正文，大型数据库耗时较长。`readonly` 仍复用匹配的条目，旧版本条目汇总为一条 `notion.cache.renderer_mismatch` 警告；切回 `readwrite` 时请用 `--clean` 构建一次。

## [2.0.13] - 2026-11-10

- **大型站点渲染提速**：以前每渲染一页，都要把全站页面索引（`pages`）和站点模型（`site`，含 `site.data.pages_by_id`）重新转换一遍，渲染耗时随页数平方增长。现在每次渲染只转换一次并共享。只读取这些值的模板输出不变。行为变化：`site`、`pages`、列表页的 `items` 及其内部所有层级现在都是只读的，模板给它们添加新键或按下标给数组赋值（如 `pages[0] = x`）会报只读错误；在组件和区块内，该错误按 `theme.componentValidation` 处理。
- **渲染与报告计时**：`--metrics` 现在记录真实的 `pageRender`、`listBuild`、`staticRender` 耗时（以前一直为 0），并新增 `renderCall`、`htmlTransform`、`renderWrite`、`renderModelBind`、`renderDispatch`，以及渲染前的变体步骤 `variantSetup`、`mediaResolve`、`renderPlan`、`assetPrepare`、`seoPrepare`。渲染之后的每个报告步骤输出一条带耗时的 `event=build.report.timing` 日志，每个范围以 `step=total` 结尾。报告文件与 `metrics.json` 结构不变。
- **大型站点报告阶段提速**：判断每个路由是否出现在 `sitemap.xml`、`search.json`、`agent-manifest.json`、`llms.txt` 等汇总文件里时，现在每个文件只扫描一遍，不再每个路由扫描一遍；发布审计只读取、解析一次 `agent-manifest.json`，不再每篇文档一次；媒体输出校验只计算一次生效的媒体配置，不再每个链接一次。在 4651 页的站点上，这三步占了构建的大部分时间。输出、报告文件和审计问题都不变。
- **重新签名的图片沿用原文件**：本地化图片的文件名不再随签名变化。使用 AWS SigV4、Google Cloud Storage V4、AWS SigV2/CloudFront（`Signature` 加 `AWSAccessKeyId`、`GoogleAccessId` 或 `Key-Pair-Id`）签名的网址，以及 Notion 文件代理（`notion.so`、`*.notion.so`、`*.notion.site`）的网址，签名和过期参数不再参与文件身份；其余参数照常参与，下载仍使用完整的签名网址。因此 Notion 图片只下载一次，不再在链接过期后每次构建都重新下载。同一张图有一处链接已过期、另一处是新签名时，使用新签名下载。升级后带签名的图片会以新的稳定文件名再下载一次；但旧版本已保存的文件仍用于它来源的那个完整签名网址，因此链接已过期的 `readonly` Notion 缓存仍能使用本地图片；未签名的图片文件名不变。
- **增量构建识别引擎与插件变化**：模板哈希现在包含 Bukit 引擎的版本和源码提交，升级后第一次 `--no-clean` 构建会重新渲染全部页面（报告中页面显示 `template_changed`，列表显示 `list_render`），不再保留旧引擎渲染的页面。启用的内置插件的选项现在也计入渲染输入，改动后全站重新渲染；键的顺序不影响结果。没有 git 信息或含未提交修改的构建与其所基于的版本身份相同，需要使用 `--clean`。
- **渲染原因 `metadata_changed`**：页面因记录或自定义字段变化而重新渲染时，`.bukit/incremental-manifest.json` 的 `renderReasons` 现在计为 `metadata_changed`；`content_changed` 只表示正文变化，包括 `bodyFingerprint` 变化（例如修改 Markdown 正文）。编辑 Notion 页面时其 `last_edited_time` 字段也会变化，因此 Notion 的编辑显示为 `metadata_changed`。读取 `content_changed` 的脚本需要同时读取 `metadata_changed`。
- **改一篇页面不再重渲染全站**：`--no-clean` 构建现在按页面追踪共享数据。Bukit 记录每个页面的模板（布局、`include`、组件和区块）读取了哪些集合：某个 `site.data`、`site.modules` 或 `site.data_index` 键，`site.related_pages`、`site.data_files`，或 `pages`（带 `source` 的区块也读取它）。修改一篇文章时，只重渲染这篇文章和读取了它所改变数据的页面（例如 `site.data.pages_by_id`、列表、遍历 `pages` 的页面），这些页面计为新的渲染原因 `data_dependency_changed`；全站设置变化仍计为 `render_dependency_changed`。模板整体使用 `site` 或 `site.data`、使用未知全局变量、`this` 或区块插件时，按读取全部集合处理，自定义渲染器也是如此。遍历 `pages` 的页面现在也会在其他页面变化时重渲染，以前会保留过时的 HTML。`build-manifest.json` 的条目新增可选字段 `renderReads` 和 `renderBaseHash`；升级后第一次 `--no-clean` 构建会渲染全部页面。

- **robots.txt 支持 Content Signals**：新增 `site.seo.robotsTxt.contentSignal`（`search`、`aiInput`、`aiTrain`，各为 `true` 或 `false`），在 `User-agent: *` 组写入一行 `Content-Signal:`，例如 `Content-Signal: search=yes, ai-input=yes, ai-train=no`，用来声明 AI 公司可以如何使用网站内容。只列出设置了的项。不配置时 `robots.txt` 不变。

## [2.0.12] - 2026-11-10

- **百分号编码的网址**：`url`、`route.url` 字段或 `aliases` 里填入已编码的网址（如 `/%e8%81%94%e7%b3%bb%e6%96%b9%e5%bc%8f/`）时，现在会解码一次（此例为 `/联系方式/`），输出目录与浏览器请求的地址一致，不再 404。会改变路径的编码（`/`、`\`、`.`、`%`）、控制字符、不可见或易混淆字符（零宽字符、方向控制符、不换行空格）和不合法的 UTF-8 保持原样。构建会输出 `event=route.url.decoded` 或 `event=route.alias.decoded` 并附原值，便于回到内容源修正。不含 `%` 的网址不受影响。

## [2.0.11] - 2026-11-09

- **全站同时间排序**：新增 `content.ordering.tieBreakField`（`url`、`title` 或任意内容字段，如 `ID`）和 `content.ordering.tieBreakOrder`（`asc` | `desc`）。发布时间相同的文章在所有列表里按这个字段排序：首页 `pages`、集合列表和分页、归档、全站和集合 feed（RSS、Atom、JSON Feed，以及审计预测的 feed 范围）、`llms.txt`。`taxonomy.tieBreakField` 不设置时，分类页也用它，同一个站点的首页和分类页不再出现同一批文章顺序不同。`title` 在所有列表里都按文章标题、不区分大小写比较，和分类页的内置顺序相同：`title` `asc` 写不写，分类页和增量缓存都一样；`title` 配 `desc` 是它的完全反转（以前会退回正序）。整数按精确值比较，超过 15 位的 ID 也不会排错。归档月份页中发布时间相同的文章和集合列表顺序一致。只写 `taxonomy.tieBreakOrder` 时，会作用于沿用的字段（或标题），不再被忽略。分类 RSS 中发布时间相同的文章改为和分类页一致（排序字段、标题、网址），不再只按网址排。不配置时，其他输出和增量缓存都不变。
- **空 RSS feed**：没有文章的 feed 不再把 `lastBuildDate` 写成 `Thu, 01 Jan 1970`，这个可选字段直接省略。Atom 仍保留必需的 `<updated>`。
- **分类页同时间排序**：新增 `taxonomy.tieBreakField`（`title`、`url` 或任意内容字段，如 `ID`）和 `taxonomy.tieBreakOrder`（`asc` | `desc`）。发布时间相同的文章在分页前按这个字段排序，从 WordPress 迁移的站点跨页也能保持旧站的 ID 顺序。数字按数值比较，没有该字段的文章排在后面，分类 RSS 也按同样规则排列同一时间的文章。不设置或写 `title` `asc` 时保持内置的标题顺序，输出和增量缓存都不变。
- **大页面输出不再被截断**：模板不再使用 Scriban 默认的 1 MiB 字符串上限。在 `include` 里这个上限实际只有约 52.4 万字符，超出后页面其余部分全部变空。ItemList JSON-LD 最多列 100 条，超出时给出 `numberOfItems` 总数，关了分页的列表页也不会过大。页面有 `<html>` 却没有 `</html>` 时，审计报 `render.output_truncated`，不再报 `seo.document_title_missing`。
- **面包屑首页与分类上级**：新增 `site.seo.schema.breadcrumbHome`，开启后 BreadcrumbList JSON-LD 以首页开头；新增 `site.seo.schema.breadcrumbParent`（填分类法类型，如 `categories`），内容页会以所属的第一个分类页作为上级，`/2596/` 这类扁平网址也能得到“首页 › 分类 › 文章”。分类法类型不存在时配置校验报错；增量构建时，面包屑任一级的名称或网址变化都会重新渲染该页。两项默认关闭，不配置时输出和增量缓存都不变。
- **SEO 默认作者**：新增 `site.seo.defaultAuthor` 和 `site.seo.defaultAuthorType`（默认 `Person`，可设为 `Organization`）。页面没有作者关联、`author` 字段和 GEO 作者时，文章 JSON-LD 的作者使用这个值。不配置时，输出和增量缓存都不变。

## [2.0.10] - 2026-11-09

- **图片本地化控制**：新增 `content.media.onFailure: placeholder | keep`（默认 `placeholder`），`keep` 在下载失败时保留原地址；新增 `content.media.downloadHosts` 和 `skipHosts`，按域名限定下载（支持 `*.` 通配）。类型为 `application/octet-stream` 或缺失的响应，按文件签名识别类型，并须通过现有图片校验。
- **大站构建**：渲染依赖哈希的节点上限提高到 10,000,000；Notion 摘要预取输出 `event=notion.progress` 进度；构建失败或取消时保留已暂存的 Notion 缓存（`event=build.cache_preserved`）；Ctrl+C 会干净地取消构建，再按一次立即退出；模板运行时错误会附上根本原因。
- **告警汇总**：同类告警只显示 5 条，之后输出一条 `event=diagnostics.summary` 给出总数。
- **隐私检查**：发布前的隐私扫描改为线性时间，不再随"标识数 × 文本量"增长。
- **分类**：新增 `taxonomy.emptyTerms` 控制空分类页；百分号编码的分类名会被解码；标签索引不再混入分类项。
- **SEO 诊断**：head 标签里多出的引号不再遮住 `<title>`；页面没有输出 JSON-LD 时只报一条 `seo.json_ld_not_rendered`，不再逐字段报 schema 问题；非 strict 模式下标题问题只由发布审计报告一次；标题和描述长度按显示宽度计算（中日韩字符计 2）；自动生成的描述截断到宽度 160 并加"…"（显式 `seo_desc` 不截断）；主题模式下模板没有输出 `seo.document_title` 或 `seo.json_ld` 时，`bukit doctor` 会提示。
- **Analytics 的 consent 改为可选**：配置 GA、GTM 时不再必须写 `site.analytics.consent.google`。不写时，Bukit 不输出 consent 默认命令，由 Google 按其默认行为加载（相当于全部 granted）；写了 consent 却没有 Google provider 仍会报错。已有配置的输出不变。

## [2.0.8] - 2026-11-07

- **多语言媒体暂存**：自定义 `content.media.downloadDir` 现在只暂存一份，由所有语言工作进程共享。新下载和 `.media-index.json` 只有在所有语言都成功后，才与输出、缓存一起提交。媒体变化按路径、大小和修改时间判断；媒体目录内的符号链接会被拒绝。这是行为变化：媒体目录只要包含符号链接，多语言构建就会失败。
- **构建中断恢复**：下一次构建会恢复该输出最后一份完整事务记录，包括输出、缓存、暂存媒体和外部指标。未知临时文件会被保留；目标被修改或备份缺失时会停止恢复并保留现场，供人工检查。显式多语言构建现在与进程内构建共享构建资源租约。必需输入的符号链接必须是相对路径且位于仓库内；绝对路径符号链接会被拒绝。
- **Analytics 审计修复**：head 开头的统计片段会排在页面编码声明之后；CSP 需求报告新增 `connectSrc` 和 `imgSrc`；任一 Google consent 默认值为 denied 时，不再输出 GTM 的 `<noscript>` iframe。
- **SEO 审计修复**：内容页以 `/` 开头的 canonical 会按 `site.url` + `baseUrl` 补全，且不会重复添加已有的 base 路径；其他相对写法保留并输出 `seo.canonical_relative`；`og:locale` 使用下划线格式，例如 `zh_CN`，裸语言会输出 `seo.og_locale_region_missing` 诊断，即使开启 strict 诊断也只会警告而不会导致构建失败。
- **AI 爬虫策略**：`robots.txt` 按用途（训练、AI 搜索、用户抓取）区分 AI 爬虫，新增 `aiBotMode: block-training`（它也会禁止 `Google-Extended`，该标识同时控制 Gemini 的训练和 grounding，构建会输出 `event=robots.ai_grounding_blocked`）。行为变化：`selective` 模式配合 `aiBotAllowList` 时，其他已知爬虫（含 AI 搜索爬虫）都会被禁止，构建会输出 `event=robots.ai_search_blocked` 警告。配置了 AI 策略但无法写入时，构建会输出 `event=robots.ai_policy_inactive` 警告。
- **Sitemap 范围**：sitemap 只列出位于 `site.url` + `baseUrl` 下的 canonical（默认端口和主机大小写已规范化），每个只列一次；发布审计不再要求 canonical 指向站外的页面出现在 sitemap 中。
- **AI agent 发现**：GEO 开启且为 `inject` 模式时，可索引的内容页会用 `<link rel="alternate">` 链接自己的 Markdown 和 JSON 版本，`llms.txt` 的 `## Optional` 一节会链接 `agent-manifest.json`。增量构建中切换 `site.seo.geo.enabled` 现在会重新渲染页面。`theme` 和 `off` 模式下，布局可以从 `page.representation_urls` 读取同样的网址，只有已发布且不是 `noindex` 的页面才有这个变量。`agent-manifest.json` 现在不再带 UTF-8 BOM。

## [2.0.7] - 2026-11-04

- **版本号**：`Directory.Build.props` 中的产品版本改为 2.0.7，与发布线一致（v2.0.6 发布时该文件仍为 2.0.4，发布构建通过 `-p:VersionPrefix` 覆盖版本）。
- **安全回归自测**：`scripts/security/security-regression.sh` 中的每个 selector 现在必须匹配带 `[Fact]`/`[Theory]` 的文件中的真实测试声明；无法解析的条目会失败而不是被跳过；自测已纳入 CI fast gate，并支持含空格的检出路径。
- **发布文档**：补充 2.0.0 至 2.0.6 的更新日志段落和 2.0 内部发布清单。

## [2.0.6] - 2026-11-03

- 集成 Core 阶段 6-10（Engine 输出与审计收口、管线端到端测试、CLI 与插件覆盖、架构/覆盖率/三平台门禁）、插件错误与密钥遮蔽契约，以及测试门禁审计修复（PR #66-#86）。

### SEO/GEO 内部集成

- 新增已核验的组织 publisher 契约（`type`、绝对 HTTP(S) URL 输出与
  `sameAs`）、跨源 relation 投影、relation-backed author、空列表
  indexability、企业实体，以及确定性的 publish URL snapshot/change-set
  schema。
- 新增隔离的 IndexNow process plugin、最小项目示例，以及带真实 SHA-256
  的 `osx-arm64` Core/插件内部安装清单。该产物仅供内部使用，不代表公开
  release 或 release gate 结果。

### 审计修复

- 关闭 14 项审计问题：非法 Markdown front matter 改为 fail-closed，不再丢失草稿标记；部署拒绝通过 `CNAME`/`.nojekyll` 符号链接写入；Notion upsert 读取真实唯一字段值，并拒绝缺失、不支持或多页命中；WeChat 提交结果不确定时停止重试并保留恢复状态。
- 媒体本地化只处理图片引用；递归 `**/` glob 包含内容根目录文件；Seed 数组输出为 YAML 序列；Dev 监听覆盖自定义 Markdown 与继承主题输入；Notion 缓存刷新过期签名链接和在线关联目标；多语言数据准备响应取消；Markdown 转发自动摘要配置。
- 复用共享 canonical 路径解析，删除重复/未使用的路径和字段读取逻辑；补充回归测试与 `DraftSubmitting` 恢复手册。

### 安全与可靠性修复

- **安全输出清理（F-01）**：显式/配置式 clean、构建清理和 recovery 共用一个受保护 cleaner；危险 root、`.git` descendant、项目外与 symlink/reparse target、无 marker 非空目录都会被拒绝。
- **默认搜索 UI DOM 安全（F-02）**：内容 title/snippet 使用 text node 与显式 `<mark>` 渲染，不再进入 HTML 解释型 sink；placeholder 会被编码。
- **确定性输出所有权（F-03）**：static、assets、media、generated tokens 和 render destination 在发布写入前统一 preflight；跨类别、结构性冲突以及大小写不敏感 volume 上的 case-variant 冲突返回 `BuildAssetOutputCollision`。
- **安全递归发现（F-04）**：默认 content、static、media、hash 和 report inventory 路径跳过目录 symlink/reparse point；显式 follow 仍只限受支持 copy path。
- **模板决策刷新（F-05）**：capability manifest 使用内容指纹，静态依赖分析按调用隔离；同进程 rebuild 能观察 manifest/root/include/layout 变化，且可变返回值不会污染缓存。
- **Search content cap 生效（F-06）**：既有 `site.search.maxContentLength` 对 document、list、plugin、publish projection 和多语言 search `content` 生效；默认值与 schema 形状不变。
- **下载级媒体并发（F-07）**：既有 `content.media.maxConcurrency` 在 rewrite operation 和 localized body store 范围内限制实际本地化下载。
- **真实 build health（F-08）**：既有 `build-report.v1` 字段写入本次构建诊断计数和稳定 public `generatedFiles` inventory；冻结 schema 形状不变。

### 变更
- **SiteEngine 重构**：856 → 592 行编排器，拆分为 8 个独立 Pipeline 类（`BuildPipeline`、`ContentPipeline`、`RoutePipeline`、`RenderPipeline`、`AssetPipeline`、`SeoPipeline`、`PluginPipeline`、`BuildReportPipeline`），外加 `ThemeBootstrapper`、`BuildOptionsMapper`、`FixedContentProviderFactory`。双 `BuildAsync` 路径统一为单一 pipeline 链。消除所有反射测试 helper（零 `BindingFlags` 残留）。新增性能回归测试。

### 新增
- **Taxonomy v3.0.0**：分类系统重大升级，新增 7 项功能
  - 层次化分类：`taxonomy.kinds[].hierarchical: true` 启用父子 term 关系（`ParentSlug`），自动计算 `children` 和 `ancestors`
  - Term 元数据：`_index.md` 约定（Hugo 风格），路径 `content/_taxonomy/<kind>/<slug>/_index.md`，支持 per-term description、image、weight、parent
  - RSS 2.0 feed：每个 term 自动生成 `<output>/<kind>/<slug>/feed.xml`
  - Slug 音译：Unicode NFD 分解（`é→e`、`ß→ss`、`æ→ae`、`œ→oe`、`ø→o`），CJK 字符保留
  - 别名重定向：`Aliases` 字段自动生成 HTML `<meta http-equiv="refresh">` 重定向页
  - Term 可见性控制：`IsVisible` 和 `Weight` 字段，用于排序和过滤
  - `taxonomy.json` schema 升级至 v2（包含 `children` 和 `ancestors` 数组）
- **SlugHelper**：共享 slug 生成工具（`Bukit.Shared`），合并 3 份重复实现，支持拉丁字符音译

### 变更
- **TaxonomyPlugin** 从 1194 行重构至 245 行 — 提取 7 个内部辅助类：`TaxonomyIndexBuilder`、`TaxonomyPageCreator`、`TaxonomyDataWriter`、`TaxonomyTemplateResolver`、`TaxonomySortHelper`、`TaxonomyHierarchyBuilder`、`TaxonomyMetadataLoader`
- **TaxonomyTerm 模型** 丰富化：新增 `Description`、`Image`、`Weight`、`IsVisible`、`ParentSlug`、`Aliases`、`Pages` 字段
- `TaxonomyKindConfig` 新增 `Hierarchical` 布尔字段（默认 `false`）

### 测试
- 新增 5 个测试文件（38 用例）：`SlugHelperTests`（22）、`TaxonomyHierarchyBuilderTests`（3）、`TaxonomyMetadataLoaderTests`（6）、`TaxonomyFeedWriterTests`（3）、`TaxonomyRedirectWriterTests`（4）
- 全部 1311 测试通过（Shared 67、Engine 793、Content 451）

## [2.0.5]

- 版本跳过：不存在 `v2.0.5` tag 或 GitHub Release。

## [2.0.4] - 2026-09-13

- **构建事务**：补充构建事务文档并保护暂存输出路径；修复构建事务分析回归。
- **发布审计**：投影审计现在追踪已发布 URL。
- **原生打包**：精简原生包，并拒绝不完整制品。

## [2.0.3] - 2026-09-12

- **响应式图片**：隔离响应式图片源发现，修复响应式图片生命周期，保留空的图片格式列表，并在图片格式变化时刷新增量媒体输出。
- **内部路径**：内部路径不再进入公开输出。
- **CI**：加固 CI 门禁与发布证据校验；澄清发布 tag SHA 校验措辞。

## [2.0.2] - 2026-09-12

- **图片**：新增响应式图片处理（带 ImageSharp 回退）；优化图片处理正则与尺寸归一化。

## [2.0.1] - 2026-09-12

- **输出安全**：公开输出生命周期改为 fail-closed；保留 `404.html` 静态输出路径；强制插件使用相对路径。
- **Preview**：Preview 关闭内部文件服务与 LAN 暴露；内部路径策略按解析后的物理终态分类，闭合 root 内 symlink 别名绕过。
- **进程执行**：macOS 外部工具通过进程组 wrapper 运行，消除 shell 插值。
- **内容与 SEO**：Markdown 正文投影保留链接；修复 Notion 缓存与文本解析；集中 robots.txt 审计规则；修正 HTML 路由与媒体发现；Google Analytics 配置包含页面标题。
- **CI**：保留唯一覆盖率结果并限制平台 fixture；`bukit-test.slnx` 补齐 `Bukit.Content.Notion.Tests`。

## [2.0.0] - 2026-08-07

- **公开 API**：接受经审查的 2.0 公开 API 面并定义 2.x 稳定性策略；保留插件进程与 manifest record 的二进制契约。
- **SSRF 与路径**：共享 SSRF 防护在主机解析不安全时 fail-closed，并重试公共连接地址；默认路径策略原子发布；规范化 rooted 绝对路径。
- **进程组**：Linux 使用 `setsid` 管理进程树。
- **引擎**：转义简单页面 URL 属性；遵循解析后的输出复制路径。

## [2.0.0-RC.1] - 2026-07-24

- 首个 2.0 候选版本。Core 重新定位为企业内部优先的确定性发布编译器（见 Unreleased 中的产品定位）。

## [1.0.6] - 2026-05-21

### 新增
- **Shortcodes 系统**：`theme.shortcodes` 配置 — 定义可复用片段（youtube、callout 等），同时支持 Markdown（`{% name args %}`）和 Scriban 模板（`{{ shortcode }}`）
- **内容 Schema 校验**：collection 配置中的 `schema` — 按类型校验 Front Matter 字段（string/number/bool/date/list），支持 warn/strict 两种失败模式
- **SCSS 编译管道**：`theme.scss` — 构建时自动编译 `.scss` → `.css`，自动检测系统 `sass`/`dart-sass` CLI
- **主题继承**：`theme.extends` — 子主题级联父主题；模板查找（子优先→父回退）、静态文件/资源合并
- **组件化模板**：`theme.components` — 在 site.yaml 中声明带 props 的可复用组件，Scriban 中使用 `{{ comp.render "Name" args }}`
- **图片优化管道**：`theme.images` — 自动 WebP/AVIF 转换（自动检测系统 `cwebp`/`magick` CLI），内置 `ImageOptimizer.BuildSrcset()` 响应式图片生成
- **HMR 开发服务器**：`bukit dev` 命令 — 文件监控、增量重构建（300ms 去抖）、WebSocket 实时刷新所有浏览器
- **Layout 指令解析器共享化**：`ScribanLayoutDirectiveParser` 提取至 `Bukit.Shared`，消除渲染器与静态分析器的 DRY 违规
- **核心管道异步 I/O**：`TemplateCapabilitiesResolver` 改用 `File.ReadAllTextAsync` + `Task<T>` 缓存

### 变更
- **重构上帝类**：`SiteEngine` 从 1122 行缩减至 558 行（提取 `SeoAlternatesService`、`RobotsTxtWriter`、`StaticFileService`）；`PageRenderDispatcher` 从 581 行缩减至 491 行（提取 `SpecialListRouteBuilder`）
- **ScribanTemplateRenderer** 支持运行时注入 `shortcodes` 和 `components` 字典
- **FileTemplateLoader** 支持级联查找：主目录优先，可选回退目录（用于主题继承）
- **BuildVariantContext** 新增 `ParentLayoutsDir`、`ParentAssetsDir`、`ParentStaticDir`
- **ConfigLoader** 新增 YAML 反序列化：`ReadComponents`、`ReadImageOptimizationConfig`、`ReadScssConfig`、`ReadSchema`

## [1.0.0] - 2026-05-05

### Preview

HTML Demo 导入（`bukit import html-demo`）、seed 导入（`bukit import seed`）和 Notion 迁移（`bukit notion push`）属于 Preview 功能，不纳入 Bukit 1.0 核心稳定性承诺。这些命令在后续版本中可能发生变化。

### 新增
- Bukit 首发版本，基于 .NET 10 Native AOT 的静态站点生成器
- Markdown 内容源，支持 Front Matter
- Notion 内容源，支持数据库映射与字段归一化
- 多源内容聚合（`markdown` + `notion`）
- Scriban 模板引擎，支持 AOT 兼容的 vendored 构建
- Collections 驱动的路由系统，带 permalink 兼容层
- 内置插件：sitemap、RSS、search JSON、taxonomy、pagination、archive、pages-index
- 多语言站点支持，含 split/merged/index 模式
- 增量构建，基于 manifest 的变更检测
- 主题系统，含 `theme list` / `theme use` 命令
- Modules 数据源（`mode=data`），用于结构化内容
- 外部插件协议 v1/v2（process 和 WASM 运行时）
- 插件源码生成器，实现零反射注册
- `doctor` 诊断命令，用于环境与配置检查
- `webhook` 命令，用于 Notion 到 GitHub Actions 的 repository dispatch
- Intent 系统，用于 AI 辅助站点配置
- AOT 发布，单文件输出
- 性能指标输出（`--metrics`）
- 并行渲染，可配置并发度（`--jobs`）
- CI 模式（`--ci`），支持 JSON 结构化日志
- 用户文档（16 章，3 语言）
- 开发者文档（35+ 文件，覆盖架构、CLI、插件等）
- ChatGPT prompt 包，用于对话式建站
