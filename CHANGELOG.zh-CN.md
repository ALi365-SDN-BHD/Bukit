# 更新日志

Bukit 所有重要变更都将记录在此文件中。

## [Unreleased]

- **内部文件不再进入部署；build-report 路径可移植**：`bukit deploy`（GitHub Pages）不再把 `*.bukit-freshness.json` 图片新鲜度旁路文件复制到发布分支，发布校验和也不再列出它们；`.bukit/` 目录（包括 `<lang>/.bukit/`）以及根目录的状态与标记文件此前已被排除。`build-report.json` 的 `project.root` 现在写为 `.`，`project.output` 写为相对项目根目录的路径（绝对的 `build.output` 或 worktree 构建的暂存目录不再泄露），不再包含本地绝对路径。这些文件仍保留在输出目录中，供审计和增量构建读取；自行上传输出目录的部署方应排除 [10 内置输出](guide/user/10-built-in-outputs.md#public-and-internal-output-boundary) 中列出的内部文件。

- **分类种类标签按语言设置，派生页带上语言**：`taxonomy.kinds[].i18n.<lang>` 可为某一语言的构建设置种类的 `title`、`description` 和 `singularTitlePrefix`，因此种类标签为中文的站点在英文构建中，词条页标题是 `Tag: Malaysia` 而不是 `标签: Malaysia`，HTML 与 Markdown、JSON 表示一致。未设置的标签沿用种类本身的值；语言键必须是 `site.languages` 中的条目。派生的分类、分页和归档页面现在带有派生它们的构建语言，因此多语言站点中它们的表示、agent manifest 条目和 JSON-LD 写出该语言而不是 `und`；升级后第一次增量构建会重新渲染这些页面。见 [11 I18n And SEO](guide/user/11-i18n-seo.md#taxonomy-metadata-language)。

- **内容引用字段**：`content.links` 把页面字段里存的键解析成对应的文档。配置 `field: cities` 后，`cities: [penang, kl]` 的页面会在原字段旁多出按原顺序排列的 `cities_links`。每条链接含 `key`、`title`、`slug`、`url`（没有页面的 data 文档为 null）、`collection`，以及 `select` 列出的目标字段。经过结构化字段的嵌套路径（如 `days[].attractions`）会在每个 day 对象里写 `attractions_links`，另在顶层写一个每个目标只出现一次的 `days_attractions_links`。目标按 `slug` 或 `target.key` 指定的字段（文本或整数）匹配，只在页面所属语言内匹配；`url` 与 `page.url` 一样相对于站点 base URL。找不到的键、有多个目标的键、不是文本的键、以及已存在的输出字段，都报告在引用它的页面上，按页面所在集合的 `schemaFailMode` 处理；Bukit 不会覆盖已有字段。目标的标题、URL 或 select 字段变化时，增量构建会重建引用它的页面。暂不支持 media 目标。见 [04 site.yaml 配置](guide/user/04-site-yaml-config.md#content-links)。

- **CloudBase 结构化字段**：CloudBase 的 jsonb 对象、以及含非字符串元素的数组，现在按原结构保留，不再被丢掉，模板可以读 `page.fields.days.value[0].title`，或遍历 `page.fields.months.value` 用 `m == 4` 判断。对象的键按序数排序，JSON 类型保持不变（数字仍是数字；Markdown front matter 的列表元素是字符串，这一点两种源不同；`array.contains 4` 匹配不到这些数字，要用 `==` 比较），一个字段最多嵌套 8 层、JSON 最大 256 KiB。白名单里的列整体公开；硬性剔除的个人字段名同样从嵌套对象的键里删除；不在 `fileFields` 里的列只要嵌套值里出现 `cloud://`，就整列丢掉（`BKT-0C0B`）。超深或超大的列被丢掉，并以 `cloudbase.structured_field_invalid` 按集合的 `schemaFailMode` 处理。字符串数组的行为不变。`fieldPolicy.mode: all` 下这些列会作为新字段出现，开启 `content.modelSchema.rejectUnknownRawKeys` 时需要在模型里声明。内容校验日志对 CloudBase 行只输出行 id 的哈希。见 [27 CloudBase Content](guide/user/27-cloudbase-content.md#structured-fields)。

- **CloudBase 内容源**：`content.sources[].type: cloudbase` 通过 PostgREST 读取 CloudBase PG 模式的表或视图，字段投影硬性剔除个人列，Markdown 正文使用不可信链接模式渲染，`fileFields` 对象和正文 `cloud://` 图片会批量签名，文件换链失败时落到 `content.media.defaultImageUrl`。构建阶段的隐私检查会拦截公开产物里的 envId、`cloud://`、网关/存储主机和签名链接；部署阶段拦截带 `token` 的签名链接、URL 位置的 `cloud://`（以及传入时的 envId）。`doctor` 检查凭据 role、表可达性、RLS 空结果和存储签名。见 [27 CloudBase Content](guide/user/27-cloudbase-content.md)。

- **WebP 图片生成响应式变体**：启用 `theme.images.enabled` 时，本地静态 WebP 图片（例如从 Notion 或微信下载的封面和正文图片）会按配置中小于原图宽度的档位生成缩小的 WebP 变体，`img` 也会加上 `srcset`，与 JPEG/PNG 一致，但不包 `<picture>`。动图 WebP、文件名已是 `-<宽度>w.webp` 形式的文件、以及旁边有同名 JPEG/PNG 的 WebP 保持原样。WebP 源图始终用 SkiaSharp 缩放。含 WebP 媒体的站点会多出变体文件及其 freshness 记录，相关 `img` 多出 `srcset` 和 `decoding="async"`；升级后第一次增量构建会重新渲染页面，因为图片指纹现在包含 WebP 文件。见 [04 site.yaml 配置](guide/user/04-site-yaml-config.md)。

- **RSS 频道语言与条目绝对网址**：`rss.xml` 和集合 RSS 在所有条目声明同一种语言时写出 `<language>`（忽略空值和 `und`，所以混合语言的合并 feed 不写）；`content:encoded` 里根相对的 `src`、`href`、`srcset` 会按 `site.url` 转成绝对网址。多语言根目录合并 feed 中，没有声明语言的条目使用所属语言版本的语言。

- **合法的 Open Graph locale**：`og:locale` 现在输出合法的 Open Graph locale。带地区的语言直接转换（`zh-CN` → `zh_CN`），常见的无地区代码补默认地区（`en` → `en_US`，`pt` → `pt_BR`），未知的无地区代码不输出该标签。页面还会按 hreflang 交替链接输出 `og:locale:alternate`。`site.language` 没有对应的 Open Graph locale 时警告 `seo.og_locale_region_missing`（即使 `strict` 也只是警告）。见 [SEO](docs/seo.md)。

- **noindex 页面不再输出 hreflang**：noindex 页面不再输出 hreflang 交替链接（也不输出 `og:locale:alternate`）；可索引页面、sitemap 的 `xhtml:link` 和 `page.seo.alternates` 也不再列出 noindex 的翻译页或开启 `noindexWhenEmpty` 的空列表页。

- **面包屑分类上级排在网址祖先之后**：开启 `site.seo.schema.breadcrumbParent` 时，BreadcrumbList 改为「首页 › 网址祖先 › 分类页 › 当前页」，不再把分类页放在网址祖先前面；分类页本身就是网址祖先时只出现一次。见 [11 I18n And SEO](guide/user/11-i18n-seo.md)。

- **JSON-LD 语言与日期**：`WebSite` JSON-LD 用 `inLanguage` 写出 `site.language`，`WebPage`/`CollectionPage` 写出页面语言。`datePublished`、`dateModified`、`dateReviewed` 改为按 `site.timezone` 输出、精确到秒（如 `2026-06-05T18:00:00+08:00`），不再使用往返格式；`article:published_time`、`article:modified_time` meta 标签以及模板值 `page.seo.article.published_time`/`modified_time` 同样如此，sitemap 的 `<lastmod>` 也改为 `site.timezone` 下的日期。发布审计的 `publish.jsonld_date_mismatch` 按 JSON-LD 日期自带的时区比较发布时间所在的日期，同一时刻不再误报。

- **sitemap 网址百分号编码**：sitemap 的 `<loc>` 和 `xhtml:link` 的 `href` 会按 RFC 3986 把主机之后的非 ASCII 字符和其他不允许出现在 URI 中的字符编码为 UTF-8 百分号形式（`/新闻/` → `/%E6%96%B0%E9%97%BB/`）。HTML 的 canonical 和 hreflang 标签不变。发布审计仍能查出出现在 sitemap 中的非 ASCII 网址 noindex 页面。

- **图片尺寸与延迟加载**：启用 `theme.images.enabled` 时，Bukit 能读取尺寸的媒体图片在没有 `width` 和 `height` 时会按显示尺寸（经 EXIF 方向校正）补上这两项，页面上除第一张以外的媒体图片在没有 `loading` 属性时补 `loading="lazy"`，第一张（通常是首屏最大的图片）仍立即加载；作者写的值一律不改。媒体图片高度变化时增量构建会重新渲染相关页面；升级后的第一次增量构建会重新渲染所有页面。见 [04 site.yaml Config](guide/user/04-site-yaml-config.md)。

- **不再使用 ImageMagick**：响应式图片缩放和 AVIF 资源转换都不再调用 ImageMagick（`magick` 或 `convert`），即使已安装也不调用；AVIF 照旧跳过（`image_avif.skip`），但不再白跑一次转换；缩放一律使用内置的 SkiaSharp 编码器，无需任何外部图片工具。此前由 ImageMagick 生成的缩放变体会在下次构建时重新生成一次。见 [04 site.yaml Config](guide/user/04-site-yaml-config.md)。

- **搜索页不再带 BOM**：`bukit-search.html` 以不带字节顺序标记（BOM）的 UTF-8 写出。

- **更小的 PNG 图片变体**：启用 `theme.images.enabled` 时，没有透明像素的 PNG 源图（包括像素全部不透明的 RGBA 文件）缩放后的变体改为写出 RGB PNG 而非 RGBA，不再携带无用的透明通道；带透明度的源图仍为 RGBA。文件名、网址和 HTML 不变。已有变体会在下次构建时重新生成一次。

- **缩放变体只为媒体图片生成**：启用 `theme.images.enabled` 时，原格式缩放变体（例如 `logo-480w.png`）只为 `content.media.urlBase` 下的媒体图片生成，因为只有媒体图片会得到原格式 `srcset`。`assets/` 下其他位置的主题图片和静态图片不再生成这些没有引用的文件；启用 WebP 时它们仍会得到 WebP 候选和 `<picture>`，此前构建为它们生成的缩放变体会在下次构建时删除。见 [04 site.yaml Config](guide/user/04-site-yaml-config.md)。

- **结构化的 Markdown 正文**：每个 `content/<page>.md` 内容表示和 `llms-full.txt` 的正文保留标题（降两级，`<h1>` 为 `###`，最低到 `######`，始终低于投影自身的标题和分节）、段落、换行、嵌套列表、引用块、分隔线（`* * *`，不会与 `llms-full.txt` 的 `---` 文档分隔符相同）和代码块（围栏代码块，内容原样保留，围栏长于代码中任何连续反引号），不再挤成一行。链接仍是 Markdown 链接，图片仍不输出，script、style、template 和注释内容仍被丢弃。`search.json` 的内容不变。发布审计现在只读取 `.md` 文件的元数据头部（到 `## Entities` 或 `## Body` 为止），因此正文中 `- Source: ...` 这样的列表项不再触发 `publish.representation_markdown_mismatch`。

- **Agent manifest 1.1：真实发布日期与列表路由**：`agent-manifest.json` 的 `schemaVersion` 改为 `1.1`。**`publishedAt` 含义改变**：现在它是发布日期，最后编辑时间改放在新增的可选字段 `updatedAt`（仅当记录有编辑时间时出现）；此前只要有编辑时间，`publishedAt` 就写编辑时间。`generatedAt` 仍是最新变更时间（最新的 `updatedAt`；记录没有 `updatedAt` 时用其 `publishedAt`）。新增可选的 `lists` 数组，列出 sitemap 收录的首页、集合列表和分类路由（`route`、`language`、`kind`、`lastModified`，以及与 documents 相同 `{kind, url}` 形式、带页面网址的 `representations`）；同一 manifest 中 `lists` 的路由与 `documents` 的路由采用相同的路由空间；没有 `site.url` 时 `lists` 为空。多语言站点的根合并 manifest 包含所有语言的列表。`docs/schemas/agent-manifest.v1.schema.json` 保留原 `$id`，同时接受 `1.0` 和 `1.1`；`wechat-sync` 可读取任意 `1.x` 版本。见 [10 Built-In Outputs](guide/user/10-built-in-outputs.md)。

- **集合 feed 不再重复唯一的站点 feed；RSS 作者改用 `dc:creator`**：新增 `site.collections.<name>.output.feed`，决定集合是否输出自己的 feed；`output.rss` 现在只表示把该集合纳入站点 feed。`output.feed` 不设置时，只有两个及以上集合设了 `output.rss: true` 才为 `true`。**只有一个 RSS 集合的站点不再输出 `<feed.path>/<name>/rss.xml`**（以及对应的 Atom 和 JSON 文件），因为它与 `/rss.xml` 完全相同；需要保留时设 `output.feed: true`。RSS 条目的作者改为带 Dublin Core 命名空间的 `<dc:creator>`，不再使用 `<author>`，因为 RSS 2.0 中 `<author>` 只能填电子邮件地址。

- **Markdown 头部补充日期、作者和原文来源**：每个 `content/<page>.md` 的头部在 `- Canonical:` 之后新增 `- Published:`、`- Updated:`（仅在设置时）、`- Author:` 和 `- Original Source:`，与 JSON 投影一致。日期使用 `site.timezone`，格式与 `article:published_time` 相同；`Original Source` 是公开的原文 URL，绝不是私有的 `source` 来源信息；这些值中的换行会替换为空格，无法伪造其他头部行。

- **按语言设置站点标识**：新增 `site.i18n.<lang>`，多语言站点可以为某一语言设置 `title`、`description` 和 `organization`（`name`、`url`、`logo`、`sameAs`）。该语言的构建会把它们用于 `<title>` 后缀、`og:site_name`、默认描述、JSON-LD 的 `WebSite` 和 `Organization`、该语言的 feed 与 `llms.txt`，以及模板中的 `site.title`；organization 字段逐项覆盖 `site.seo.organization`。`titleSeparator` 在该语言的页面标题中替换 `site.seo.titleSeparator`（例如中文用全角 ` ｜ `，英文用 ` | `）。根目录的合并输出保留顶层标识。语言键必须是 `site.languages` 中的条目，且至少需要两种语言。

- **本地化的分类词条页可跨语言配对**：分类词条可在 `content/_taxonomy/<kind>/<slug>/_index.md` 的 front matter 中，或在 `sourceKey: tags|categories` 数据文档上声明 `i18nKey`。种类、键和页码都相同的词条页现在会在 hreflang 和 sitemap alternates 中互相列出，因此 `/tags/马来西亚/` 和 `/en/tags/malaysia/` 可以配对；没有键时仍按 URL 配对。同一语言中同一种类的两个词条共用一个键时，记录 `taxonomy.term.i18n_key_duplicate`，并退回按 URL 配对。该语言的页面使用的词条属于该语言，否则以词条可选的 `language` 元数据为准；与其他语言词条（例如提供给所有语言的 Notion 选项）共用的键，不再让当前语言的词条失去键。

- **语言目录下不再生成 robots.txt**：多语言构建不再在非根语言目录（如 `zh-CN/robots.txt`）中写入 `robots.txt`，因为爬虫只读取主机根目录下的这个文件，而根目录写入器已经在那里生成了站点的 robots 文件。各语言的本地化 `404.html` 保留不变，现已写入文档；另有测试确认 `site.sharedAssets.paths` 中的单个文件条目（如 `favicon.ico`）可以共享。

- **文章 schema 默认类型与作者资料**：`site.seo.schema.articleType` 和 `site.collections.<name>.seo.articleType` 为未声明类型的文章页设置 JSON-LD 类型（优先级：页面、集合、站点，最后是 `BlogPosting`）。`site.seo.authors` 把作者名或 `authoredBy` id 映射到作者资料（`name`、`type`、`url`、`image`、`sameAs`），这样文本署名、GEO 作者和默认作者都能带上作者 URL；Markdown 中的 `authoredBy: <id>` 只要有对应条目，就不再中断构建。

- **为生成的 srcset 添加 `sizes`**：`theme.images.sizesAttribute`（默认不设置）会在 Bukit 为媒体图片生成的 `srcset` 旁加上 CSS `sizes` 值，浏览器就不会再默认按 `100vw` 下载过大的候选图。懒加载图片得到 `sizes="auto, <值>"`，首张立即加载的图片得到原值。作者手写的 `sizes` 保持不变，该值也会传到 WebP 的 `<source>`。

- **og:image 尺寸与 WebP 警告**：社交分享图是本站文件时，Bukit 会写出 `og:image:width`、`og:image:height` 和 `og:image:type`（主题和入门主题也可通过 `page.seo.og.image_width`、`image_height`、`image_type` 读取），让平台在下载图片之前就能排好预览。分享图为 WebP 时会给出 `seo.og_image_webp` 警告，因为部分平台不显示 WebP 预览。

- **仅生成 WebP 图片变体**：生成 WebP 时，`theme.images.fallbackVariants: none`（默认 `all`）会跳过 JPEG/PNG 媒体图片的原格式缩放变体。`<picture>` 保留 WebP 候选图，回退用的 `<img>` 只保留原始 `src`。不需要为不支持 WebP 的浏览器提供 srcset 的站点，可以借此减小输出体积、缩短构建时间。

## [2.2.8] - 2026-11-23

- **数据索引支持对象值**：`content.sources[].dataIndex` 的行新增值类型 `object`（一个映射）和 `object[]`（映射列表），模板可以读 `site.data_index.<源>.<scope>.<key>.<字段>`，声明了 `object` 的小程序 `dataIndex` 数据集也能拿到真正的对象。Markdown 行用 YAML 写值；Notion 行用 JSON 文本写值（解析不用反射，Native AOT 二进制可用）。布尔值和数字保留类型，所以 `if info.open` 遇到 `false` 为假，`info.days > 3` 按数字比较：Notion JSON 保留原有类型（JSON 字符串如 `"5"` 仍是文本）；Markdown 标量为 `true`/`false`（YAML 核心写法）时视为布尔值，写成 JSON 数字形式时视为数字，`0123`、`+60`、`yes` 等其他文本保持文本，在 Markdown 里加引号也不能让 `"5"` 保持文本。64 位范围内的整数存为整数，其他数字存为保留原始位数的小数（`12.50`），所以同样的值无论来自哪个源，构建出的模型和增量哈希都相同，小程序 `dataIndex` 数据集也会输出 JSON 布尔值和数字。键按序数排序且不能为空白，嵌套最多 8 层（每个映射或列表算一层），给 `object[]` 单个映射时视为只有一项的列表（与小程序 `object[]` 字段一致），超出 double 范围的数字、无效 JSON、重复键或形状不对都会导致构建失败，报错写明源、内容 id 和 `scope.key`。空的对象值是空映射或空列表；只含空值的映射或列表，`requiredKeys` 也视其为空。**行为变化**：`text`、`multiline`、`email`、`phone`、`url` 行如果值是 YAML 映射或列表，以前会静默构建并输出 .NET 类型名（`System.Collections.Generic.Dictionary...`），现在会构建失败；这些文本行里的数字和日期改为按固定区域格式化，不再随构建机器的区域设置变化。见 [07 多源数据](guide/user/07-multi-source-data.md#object-values)。

- **图片本地化深入 front matter 里的对象**：`content.media.fieldKeys` 现在也作用于 front matter 映射和列表的任意层级，按每个值自己的键判断，适用于所有页面和数据记录，不只是小程序数据集。像 `hero: { image: https://cdn.example.com/a.jpg }` 或 `features: [{ icon: …, image: … }]` 这样的页面，构建时会下载这些图片（`content.media.downloadToLocal` 开启时，默认开启），模板拿到的是本地 `/assets/uploads/...` 路径；在 `onFailure: placeholder` 下下载失败则得到 `content.media.defaultImageUrl`。对象里本地化后的链接会像顶层一样复制到输出目录。嵌套的 `icon` 键下不是网址的文本会记录 `media.skip_non_http` 并原样保留。想保留嵌套的远程链接，就换一个不在 `fieldKeys` 里的键名。见 [04 site.yaml Config](guide/user/04-site-yaml-config.md)。

- **小程序数据集支持对象字段**：`miniprogram` 数据集的 `types` 新增 `object` 和 `object[]`，路线的 `days: [{title, text, stay, meals, sights, exps}]` 现在会完整写入详情文件，不再导致构建失败。类型也可以写成 `{ type, fields }` 映射，`fields` 用同样的写法声明嵌套字段类型，且至少要声明一个有类型的字段（空的 `fields` 会在配置校验时报错，而不是把每个对象写成 `{}`；想保留全部键就省略 `fields`）；声明了 `fields` 的对象只输出这些键，未声明的对象保留全部键并按现有规则推断值的类型。嵌套的 `image`、`url` 同样转成绝对 http(s) 地址，签名链接会报错；对象里的远程图片只要它自己的键（任何层级）列在 `content.media.fieldKeys` 里就会下载，对象里本地化后的媒体文件也会复制到输出目录；嵌套字符串只当纯文本（只有 `body` 是 HTML）；对象键按序数排序，哈希只取决于内容；任何层级的 `NaN` 都会报错；嵌套超过 8 层时以 `SchemaValidationFailed` 失败，并指出数据集、记录和字段路径。`dataIndex` 数据集同样支持这些类型。用到对象字段的 catalog 的 `schemaVersion` 和 `minClientSchema` 为 `1.1`；没有用到的站点仍为 `1.0`，输出逐字节不变。对象数组建议只放进 `detailFields`，以免列表文件过大。见 [25 Mini-Program Data](guide/user/25-miniprogram-data.md#object-fields)。

- **Markdown front matter 里的 YAML null 现在是真正的 null**：front matter（内容源和 `mode: data` 的 Markdown 源）里未加引号的 `null`、`Null`、`NULL`、`~` 或空值，以前会变成字符串 `"null"`、`"~"` 或 `""`，现在在任何层级都是 null：顶层键的值为 null 时视为未填写，映射或嵌套列表里的 null 保持为 null。加了引号的 `"null"` 和带 `!!str` 标签的值（`!!str null`）仍是字符串。对模板的影响：Scriban 里非空字符串都是真值，所以以前 `subtitle: null` 时 `if page.subtitle` 为真并输出 `null`；现在该字段不存在，条件为假。顶层文本列表里的 null 项（`sights: [a, null]`，或空的 `- ` 项）会被丢掉，和以前的空项一样，所以该字段对模板、标签、小程序 `string[]` 字段和图片本地化来说仍是文本列表；只含 null 的顶层列表视为未填写，和顶层 null 一样。映射里的列表保留 null 的位置；写入小程序 `string[]` 时丢掉 null，只含 null 的列表算作空值（写成 `null`，`required` 会拒绝）。确实需要字面文本的站点请加引号。

- **模板里可以安全输出 JSON**：`util.json_string` 现在还会把 `<`、`>`、`&`、U+2028、U+2029 转义成 `\u003c`、`\u003e`、`\u0026`、`\u2028`、`\u2029`，值里出现 `</script>`（比如来自 Notion）时不会再提前结束主题写的 `<script type="application/ld+json">`。输出仍是合法 JSON，解码后的值不变，但凡是含这些字符的值，输出字节会变。新增 `util.to_json value`，可序列化对象、数组、字符串、数字、布尔、null 和日期，转义规则相同，对象键按序数排序（转成文本后相同的字典键，如 `1` 和 `"1"`，只保留第一个），不使用反射，Native AOT 版 `bukit` 也能用。Scriban 的 `object.to_json` 以前在 AOT 下报 BKT-0399，现在改用同一实现；它输出的对象键改为排序，不再按插入顺序。`util.*` 函数现在在主题组件和 section 里也能用，此前这些模板里没有 `util` 对象。见 [08 Themes And Templates](guide/user/08-themes-templates.md#json-in-templates)。

- **增量构建报出的页面警告与全量构建一致**：`seo.theme_json_ld_replaced`、`seo.canonical_missing` 及其他逐页 HTML 检查这类按页面产生的渲染警告，以前只会在重新渲染的页面和列表上报出，所以跳过这些页面的增量构建报出的警告比全量构建少。现在构建缓存 manifest（`.cache/build-manifest.json`）会把每个页面和列表的警告存为 `renderWarnings`，跳过未改动的页面时会重新报出。页面重新渲染时会替换存下的警告，所以已经修好的警告会消失；修改 `site.seo.diagnostics` 现在会让所有页面重新渲染，使重新报出的警告符合新设置。

- **注入模式保留带标记的主题 JSON-LD，删除其余的前会给出警告**：`site.seo.renderMode: inject` 下，head 里的 `<script type="application/ld+json" data-bukit-seo="keep">` 会留在主题写的位置，主题因此可以输出 Core 不生成的类型（`TouristTrip`、`Product`、`Event` 等）。未标记的 head JSON-LD 仍由 Core 的替换，但内容和 Core 生成的每一块都不同时，构建会警告 `seo.theme_json_ld_replaced route=<url> count=<n>`，不再静默删除；多页重复时汇总成 `event=diagnostics.summary`。`site.seo.diagnostics: strict` 下它仍只是警告，现有 strict 构建不受影响。主题在 head 里输出 `page.seo.json_ld` 不会触发警告，即使经过格式化或转义方式不同。保留的脚本如果顶层 `@type` 与该页 Core 生成的某段 JSON-LD 声明的类型重复（比如出现两份 `BreadcrumbList`），会警告 `seo.theme_json_ld_duplicate_type route=<url> types=<types>`，在 `strict` 下同样只是警告，多页重复时同样汇总。发布审计会校验保留下来的 head JSON-LD（`seo.json_ld_invalid`），并把其类型计入该路由的 `schemaTypes`；现在每一段缺少 `@type` 的 JSON-LD 都会报 `seo.json_ld_type_missing`（注明“block <n> of <total>”，Core 生成的和主题保留的 head JSON-LD 分别计数），以前排在有类型的块之后的块不会被报出。body 里的 JSON-LD 以及 `theme`、`off` 模式行为不变。

- **模板循环上限可配置**：新增 `build.templateLoopLimit`（正整数，默认 1000，与 Scriban 自身默认值相同），设定页面、组件和 section 模板允许的循环次数。Scriban 统计的是一个最外层循环内的全部迭代，包括嵌套循环以及在循环里调用的函数和 include 中的循环，所以 40 个页面 × 30 个标签的两层循环会在 1200 次时失败，尽管单层都不到 1000。这种情况下的 `Render failed` 错误现在会说明原因，并给出 `build.templateLoopLimit` 及其当前值；组件和 section 的诊断信息带有同样的提示。用 array.filter 或 array.map 加匿名函数过滤不计入上限。不设置该项的网站构建结果与以前完全相同。见 [08 Themes And Templates](guide/user/08-themes-templates.md#loop-limit)。

- **可在 front matter 中指定面包屑父页面**：页面可以设置 `parent_url: /malaysia/routes/`（某个已生成页面的站内路径，不含 base URL 和语言前缀），把该页面作为 BreadcrumbList 的父级。面包屑变为「父页面的面包屑 + 当前页」，并逐级向上递归，所以 `/routes/klang/` 这样的路线页可以得到 首页 › 马来西亚 › 路线 › 巴生谷三日游；`parent_url: /` 表示以首页为父级。网址位于被改写页面之下的页面会跟着使用它的面包屑：`/malaysia/routes/` 设了 `parent_url: /` 时，`/malaysia/routes/klang/` 显示为 首页 › 路线 › 巴生谷。父页面不是已生成的页面、不是站内路径或就是页面自身时，警告 `seo.breadcrumb_parent_missing`；父级之间形成循环、或父页面的面包屑里已包含当前页时，警告 `seo.breadcrumb_parent_cycle`；这些页面继续使用按网址推断的面包屑。字段名特意和 `site.seo.schema.breadcrumbParent` 区分开，后者是对所有页面生效的分类类型。没有设置 `parent_url` 的页面不受影响。见 [11 I18n And SEO](guide/user/11-i18n-seo.md)。

## [2.2.7] - 2026-11-21

- **小程序版本化数据（需开启）**：`site.yaml` 新增 `miniprogram` 配置节用来声明数据集，每次构建把它们以 JSON 写到每种语言输出根目录下的 `mp/`：内容集合（一个列表加每页一个详情文件）、数据模块、数据索引的某个 scope、整个 data 源（取代 `data/*.yaml`）、分类和 `site.menus` 菜单，另外总有一个 `site` 数据集。每个文件按其字节的 SHA-256 命名，并连同哈希列在 `mp/catalog.json` 里，小程序启动时只请求目录，只下载变了的文件。只输出声明过的字段；重复构建、跨操作系统输出字节一致；正文清洗成微信 `rich-text` 可用的 HTML 子集；图片和链接都是绝对地址。必填字段缺失、类型不对、图片仍是未本地化的签名 URL 时，构建以 `SchemaValidationFailed` 失败；必须配置 `https://` 的站点 URL。Bukit 不生成小程序项目。见 [25 Mini-Program Data](guide/user/25-miniprogram-data.md)。没有这个配置节的站点不受影响。

- **禁止 `connect-src` 的页面也能搜索**：每个 `search.json` 旁边新增 `search.json.js`（内容为 `self.bukitSearchIndex=<同样的 JSON>;`；`search.index.json` 清单没有对应脚本）。`bukit-search.html` 片段和 Starter 主题的搜索页仍先用 `fetch` 读取索引，失败时才加载脚本，所以 CSP 为 `connect-src 'none'` 的沙箱预览也能搜索，正常托管时仍只发一次请求。脚本索引在构建清单归属和过期文件清理上跟随对应的 `search.json`；默认语言的 `static/search.json.js` 与根目录合并索引冲突（`i18n-root-output-conflict`）。只从页面自己的来源加载脚本形式，所以指向别的网站的 `data-search-url` 仍只 `fetch`、不会被执行。已经生成到主题里的搜索页不会被改写；要获得回退，从新生成的 `themes/starter/layouts/pages/search.html` 复制整段 `<script>`。每个搜索索引会多一个同样大小的文件。

- **拒绝多文档 YAML**：`site.yaml`、`theme.yaml`、`bukit.templates.yaml` 和插件配置/清单不得包含多于一个 YAML 文档。以前 `...` 或 `---` 分隔符之后的第二个文档会被忽略，现在会报错而不是被忽略；`bukit new` 不再写入结尾的 `...`。

## [2.2.5] - 2026-11-16

- **Starter 主题改版**：改为更简洁的内容型布局，深色模式跟随系统设置，窄屏有折叠菜单（不运行 JavaScript 时链接照常显示）。界面文字读 `theme.params.labels.*`，缺省时用英文；`bukit new --language zh-CN`（以及其他简体中文标签，包括 `zh-Hans-HK`）写入中文文字。以 `/` 开头的导航模块链接和标签索引链接现在带上 `site.baseUrl`，内容标题、摘要和标签做 HTML 转义，列表卡片不再显示 `country`、`industry`。已有网站使用自己的主题副本，不受影响；如果把新模板复制到子路径网站，而导航链接里已经写了 base 路径，要把链接里的这段前缀删掉。

## [2.2.4] - 2026-11-16

- **`bukit new` 生成可构建的初始网站**：`bukit new <dir> [--title <文本>] [--language <标签>]` 写入 `site.yaml`、Markdown 内容和内嵌 starter 主题；不执行 git、构建或网络请求。生成的网站通过 `config check`、`doctor` 和 `build`。

## [2.2.3] - 2026-11-15

- **部署改用本地凭据代理**：`--secrets-stdin` 传入的 token 由凭据代理交给 git 的 credential helper，不再放入 git 子进程的环境变量或命令行。从环境变量读取的 token 仍留在 Bukit 的环境中，并被 git 子进程继承，和以前一样。
- **Windows Native AOT 二进制可复现，且不再嵌入构建路径**：两次干净的 `win-x64` 打包现在得到完全相同的 `bukit.exe`。此前打包用的路径映射与 Windows 路径不匹配，构建机器上的临时构建目录被编进每个程序集，链接器还会写入链接时间；打包现改为映射本机路径并以 `/Brepro` 链接。Linux、macOS 和 Windows 的发布打包在上传前都会用另外两次干净构建校验已打包的文件。
- **离线构建可以固定审核过的 Notion 字段和过滤设置**：`build`、`config check`、`doctor` 在 `--notion-offline` 下可以加 `--notion-pin <文件>`。文件格式是 `{"format":1,"sources":{"<源名>":{…}}}`，核对每个 Notion 源的 `databaseId`，并用其中的值替换 `renderContent`、`fieldPolicy`、`propertyMap`、过滤、排序、`includeSlugs` 和 `maxItems`，没审核的 `site.yaml` 就不能让快照里隐藏的字段或草稿出现在页面上。文件里没写的键取 Bukit 默认值。文件里缺某个 Notion 源报 `BKT-0B19`，文件格式不对报 `BKT-0B18`；有改动的键输出 `event=notion.offline.pinned`。公开枚举 `DiagnosticCode` 新增 `NotionPinInvalid`、`NotionPinSourceMissing`，public API 基线已更新。
- **macOS Native AOT 二进制可复现**：两次干净的 `osx-arm64` 打包现在得到完全相同的 `bukit`。此前 Apple 链接器对一个 `objc_msgSend` stub 槽位的分配不确定，同一提交的发布二进制校验和可能在两次构建间变化；打包现改用 `-objc_stubs_small` 链接。二进制保留 `LC_UUID`。

- **共享资源路径条目校验统一**：`site.sharedAssets.paths` 现在在所有平台都拒绝前导 `\` 或盘符前缀，不再在 POSIX 上接受；空白条目改为报错而不是静默丢弃；`./fonts/` 会归一化为 `fonts/` 并可正常匹配。前导 `/` 仍视为绝对路径并拒绝。

## [2.2.2] - 2026-11-15

- **SEO 图片地址只保留一层 base 前缀**：已本地化的 `record.Media` 和自定义图片字段不再在 `og:image`、Twitter 图片地址中重复语言 base。覆盖非默认语言和 `/repo` 这类子路径站点；根 base 下的共用占位图地址也能正确识别。
- **默认共用资源集为空时直接报错**：`site.sharedAssets.enabled: true` 且未写 `paths` 时，如果 `content.media.urlBase` 覆盖全部默认前缀（`assets/` 或已启用的 SCSS 输出），现在会校验失败，不再静默地什么也不共用。单语言站点仍按文档记录忽略警告。
- **开发服务器监听根 `data/` 目录**：`bukit dev` 现在会在站点根 `data/` 下文件变化时重新构建，与 Worktree 输入清单一致。

- **多语言根布局站点可共用主题资源**：`site.sharedAssets.enabled: true`（默认关闭）让主题资源、SCSS 输出、`assets/css/theme-tokens.css` 和复制的 `static/assets/**` 只在默认语言的根输出中保留一份。非默认语言不再写出这些副本，改用新的模板变量 `site.asset_base_url` 指向根；Starter 主题的样式表已改用它。`site.sharedAssets.paths` 可覆盖默认的 `assets/` 加上已启用的 SCSS 输出目录，媒体前缀不能共用。`enabled: true` 搭配 `defaultLanguageInSubdir: true` 在任何语言数量下都会校验失败，单语言站点也不例外；单语言根布局站点会忽略共用并记录 `i18n.config.shared_assets_ignored`。开启共用时，默认语言的整个构建先完成，因此 `build.languageJobs > 1` 时墙钟时间大约增加一次默认语言构建。

## [2.2.1] - 2026-11-14

- **快照查询目录被替换或读不了时返回带码的错误**：并发录制构建可能在存在性检查和目录读取之间替换快照；离线构建现在报 `BKT-0B14`（快照整体消失时报 `BKT-0B10`），不再是未编码的 IO 错误。

## [2.2.0] - 2026-11-14

- **快照提交中断时保留最后一份可用快照**：录制构建在两次目录重命名之间被杀时，旧快照会留在目标旁边，离线打开会说明存在备份。下一次成功录制会生成新目标，之后再录制才清理旧备份。
- **Notion 离线快照只校验格式版本**：快照仍记录 `bukitVersion` 供排查，但升级 Bukit 不再让快照失效；只有 `NotionSnapshotSession.FormatVersion` 变了才需要重新录制。录制构建会复用旧快照里渲染好的正文；渲染器版本（`NotionBlocksRenderer.ContractVersion`）不同的正文会重新渲染。
- **Notion 和快照失败带稳定错误码 `BKT-0Bxx`**：Notion 接口的 401/403/404/429/其他 HTTP/网络/无效 JSON、映射字段不存在，以及每类离线快照失败都有稳定错误码。`build` 写进 cli-error JSON；`config check` 和 `doctor` 打印 `[BKT-0Bxx]`；多语言子进程把码和原因传给父进程。顶层配置、内容和渲染错误的 `code` 改用文档约定的 `BKT-xxxx` 写法，不再用枚举名。单语言 `build` 的快照错误退出码是 2；多语言子进程失败保持 1。公开枚举 `DiagnosticCode` 新增 16 个 Notion 成员（0B01–0B08、0B10–0B17），public API 基线已更新。

## [2.1.0] - 2026-11-14

- **破坏性变更：`site.languages` 是唯一的语言配置**：`site.yaml` 不再接受 `site.language`，写了会报错 `site.language was removed; write languages: [<code>] instead, for example languages: [en].`。请把 `language: en` 改成 `languages: [en]`。`site.languages` 不写或为空时等于 `[zh-CN]`；所有条目都是空白的列表会报错；`defaultLanguage` 不在 `site.languages` 中时报错 `site.defaultLanguage must be included in site.languages.`，不再记录 `i18n.config.default_language_ignored`。模板仍用 `{{ site.language }}` 读取当前语言。`bukit labs init`、Labs intent 和 HTML 演示导入器现在写 `languages`；含 `site.language` 的 intent 会被拒绝，应改为 `languages.supported: [<code>]`，只有一种语言时可以省略 `languages.default`。直接在进程内使用 Labs 程序集的代码也要修改：`SiteIntentSite.Language` 已删除，`SiteIntentLanguages.Default` 改为可选。
- **破坏性变更：`site.languages` 只有一项时是单语言站点**：`languages: [en]` 现在与不写语言列表的站点一样构建到输出根目录，不再生成 `dist/en/` 下的单语言变体。设置 `defaultLanguage` 时必须等于这一项。此类站点原先指向 `/<lang>/` 的托管跳转或链接需要调整。此类站点的 `content/en/` 和 `data/en/` 现在是普通目录，不再是语言目录和语言覆盖：`content/en/` 下的同名文件在回退 slug 中保留 `en/`（例如 `en/a/foo`），`data/en/` 以嵌套键 `en` 读取，页面 ID 必须在整个站点内唯一。从 `language: en` 改为 `languages: [en]` 的站点会保留增量缓存。
- **破坏性变更：`site.defaultLanguageInSubdir` 默认改为 `false`**：未设置该项的多语言站点现在把默认语言构建到输出根目录（`dist/`，使用站点根 baseUrl），其他语言保留 `dist/<lang>/`，因此增加第二种语言不会改变默认语言的 URL。要让所有语言都保留 `/<lang>/` 前缀，请写 `defaultLanguageInSubdir: true`。布局改变后的第一次构建会对输出目录执行受保护的清理，需要 `dist/.bukit-output-marker`。该项现在只在多语言站点设为 `true` 时计入增量哈希；单语言站点只有设为 `true` 时才记录 `i18n.config.default_language_layout_ignored`。在默认设置下，给单语言站点增加第二种语言也算布局改变，会执行一次这种清理，默认语言的 URL 不变；设为 `true` 时布局不变、不清理，默认语言改到 `/<lang>/` 下。
- **默认语言在根目录时，合并审计不再误报 manifest 不一致**：默认语言在根目录且 sitemap 或搜索为 merged 时，合并的 agent manifest 同时列出默认语言的 `/index/` 和 `/zh-CN/index/`，发布审计曾把 `zh-CN` 页面与默认语言的条目比较并记录 `publish.manifest_mismatch`。现在只有加上语言前缀后才匹配的路由，还必须语言一致。
- **单语言站点中没有语言的页面使用站点语言**：这类页面在 `agent-manifest.json`、每页的 `.json` 和 `.md` 投影以及 JSON Feed 条目中现在带站点语言，不再是 `und`，与 `search.json` 一致，发布审计也按此检查。多语言站点在这些位置仍为 `und`。
- **发布审计优先使用完全相同的 agent manifest 路由**：只有加上页面第一个输出路径段后才匹配的条目（例如 `/posts/foo/` 匹配到 `/foo/`）可能先于页面自己的条目被选中，并误报 `publish.manifest_mismatch`。
- **跳转页不再在每次修改内容后重新渲染**：别名页和根目录布局的旧首页跳转页被记录为读取所有共享集合，因此修改任何页面都会重新渲染它们。现在它们不读取任何集合，是否变化仍由跳转目标决定。
- **删除的语言不再留下 `dist/<lang>/`**：多语言站点变为单语言或删除一种语言后，原来的 `dist/<lang>/.bukit` 报告会随过期页面一起删除，空的 `dist/<lang>/` 不再残留。
- **默认语言在根目录时不再误报 `geo.llms_txt_missing`**：默认语言自己的审计曾报告 `llms.txt` 和 `llms-full.txt` 缺失，而根目录会在所有语言之后写入这两个文件。
- **单语言站点的搜索记录使用站点语言**：`search.json` 中没有页面语言的记录现在带站点语言，不再是 `und`；只有不知道语言时才用 `und`。
- **`metrics.json` 记录实际生效的语言**：`site.yaml` 未设置 `site.languages` 时，`site.languages` 由 `null` 变为 `["zh-CN"]`（在代码中构建且未设置 `Languages` 的配置记录 `[site.language]`），`site.language` 是站点的默认语言（单语言站点就是那一种语言）。
- **合并审计的语言回退**：默认语言带前缀的多语言构建中，合并审计无法判断语言的路由现在回退到默认语言。
- **可用 `--secrets-stdin` 从标准输入传入 token**：`build`、`dev`、`doctor`、`config check` 和 `deploy` 新增 `--secrets-stdin` 参数。使用后，Bukit 在做任何其他事情之前把标准输入读到结束，内容须是一个不超过 64 KiB 的 JSON 对象，键为名称、值为非空字符串，例如 `{"NOTION_TOKEN":"…","GITHUB_TOKEN":"…"}`。Bukit 需要的每个 token（`NOTION_TOKEN`，以及 `deploy` 中 `--token-env` 指定的名称，默认 `GITHUB_TOKEN`）先从这个对象里取，取不到再查环境变量。在 macOS 和 Linux 上，同一用户的其他程序可以读取正在运行的进程的环境变量，因此桌面应用等调用方不必再把 token 放进环境变量。从标准输入读到的值不会写入任何环境变量，Bukit 启动的 `git`、Sass、图片工具和插件都不会继承它们；多语言构建的 worker 通过自己的标准输入接收；日志、顶层错误和插件报告中会替换为 `***`。输入无法使用时（为空、不是合法 JSON 或 UTF-8、值不是非空字符串、名称不合法或重复、超过 64 KiB、标准输入是终端，或命令不支持该参数），以退出码 2 和错误码 `secrets-stdin-invalid` 失败，错误信息不会回显输入内容。不加该参数时行为不变，现有错误信息也不变。已知限制：`deploy` 仍通过每个需要认证的 `git` 进程的环境变量（`BUKIT_GITHUB_TOKEN`，由临时 `GIT_ASKPASS` 脚本读取）传递 GitHub token，`git` 运行期间同一用户的其他程序可以在那里读到它。
- **Notion 离线快照**：带 `NOTION_TOKEN` 运行 `build --notion-snapshot <目录>` 时，记录这次构建的 Notion 接口原始数据（数据库结构、查询到的每个页面的全部字段和 Notion 服务器时间、关联目标、跨源关联兜底、分类可选项、`pages_index` 页面）和渲染好的正文。之后 `build`、`config check`、`doctor` 加上 `--notion-snapshot <目录> --notion-offline` 就不要 token、不创建 Notion 客户端、不发任何请求。快照里缺的数据一律报错，不再静默跳过；快照里没有的 `databaseId` 会失败，改了过滤或排序时仍用记录的页面并输出 `event=notion.snapshot.query_changed` 警告，改 `propertyMap` 或模板照常生效。图片只用已有的媒体缓存。快照只在构建成功后才替换，权限私有（`0700`/`0600`），不含 token，带格式版本和 Bukit 版本。`--notion-snapshot` 覆盖 `cacheMode` 和 `cacheDir`，不再往网站目录里写缓存。详见 Notion 指南。
- **每个内容源的页数**：每次构建为每个源输出 `event=content.source.loaded source=<名称> count=<页数>`。
- **多语言构建在每种语言里都会解析 `pages_index` 的 Notion 关联**：各语言的 worker 从共享内容里读回关联 id 等列表字段时，类型变成了普通的对象列表，所以多语言构建里 `theme.params.pages_index.resolve_notion` 悄悄什么都没解析（离线构建也会悄悄跳过 Notion 快照里缺的关联页面）。现在字符串列表保持原来的类型，worker 和单语言构建一样解析关联，快照缺项时构建失败。
- **合并发布审计为每个路由匹配它自己的译文**：多语言构建中，共用同一 ID 的译文按路由第一段匹配内容记录，匹配不上就退回 `site.language`，而多语言构建并不使用这个值。在 `/docs` 这类 `baseUrl` 下第一段是 `docs`；默认语言放在根目录时，它的路由没有语言段。这些路由因此可能取到另一种语言的记录：合并后的 `publish-audit-report.json` 和 `.bukit/seo-route-map.json` 显示该语言的语言、摘要、作者和日期，`seo-report.json` 中的表示和可信度检查也拿它比对，feed 窗口检查也用了它的发布日期。现在先去掉 base 路径再读取语言段，没有语言段的路由使用写在输出根目录的语言。生成的页面不变。
- **缺少占位图时，图片下载失败不再导致构建失败**：默认 `content.media.onFailure: placeholder` 下，下载失败的图片会被换成 `content.media.defaultImageUrl`（默认 `/assets/images/noneimg-news.jpg`），主题和站点都没有这个文件时，构建会以 `Referenced localized media output is missing` 失败。现在 Bukit 在本地化图片前先检查主题和站点（含父主题）的 `static/`、`assets/` 目录。缺少占位图时，下载失败的图片保留原地址（与 `onFailure: keep` 相同），并按页面对每张图片记一次 `event=media.placeholder_missing`，写明图片地址（协议、主机和路径，不含 userinfo、查询串和片段）、所在页面、占位图路径和两种处理办法。占位图存在时照常使用，`onFailure` 的默认值不变。`bukit doctor` 和 `bukit config check` 会在 `onFailure: placeholder` 指向不存在的文件时给出警告。页面仍链接到缺失的占位图时（例如模板直接引用它），报错第一行仍为 `Referenced localized media output is missing: <路径>`，其后接一个空格、引用它的页面和处理办法；读取冒号后全部文字的工具需改为只读到第一个空白字符。
- **行为变更：`site.language` 必须是安全的语言代码**（该项随后已删除，见上文 `site.languages` 条目）：未设置 `site.languages` 时，`site.language` 现在只能由字母和数字组成、用连字符分隔子标签（例如 `en`、`zh-CN`、`zh-Hant-TW`），与 `site.languages` 已有的规则一致。`en_US` 或包含引号、`/`、`<` 的值现在会校验失败，报错 `site.language must use safe alphanumeric subtags separated by hyphens.`，不再写进 `<html lang>`。请把 `en_US` 改为 `en-US`。空值仍然允许；设置了 `site.languages` 时不检查 `site.language`。HTML 演示导入器现在写入安全的语言代码：把页面 `lang` 中的 `zh_CN` 转成 `zh-CN`，转换后仍不安全时回退为 `zh`；不安全的 `--language` 直接报错，不再写出无法构建的 `site.yaml`。
- **Starter 主题搜索只显示当前语言**：Starter 主题的搜索页（`layouts/pages/search.html`，由主题脚手架和 clone 写出）请求 `{{ site.base_url }}/search.json`。默认语言放在根目录且 `search.mode: merged` 时，这个文件是合并索引，默认语言的搜索结果会混入所有语言。现在页面只保留 `language` 与 `site.language` 相同（不区分大小写）或为 `und` 的记录。已有主题不会被改写；要获得此行为，把过滤逻辑复制到已生成的 `search.html` 中。
- **每条搜索记录都带语言**：页面没有语言的列表和分类记录过去在 `search.json` 中缺少 `language`，没有语言的页面在多语言站点的某个语言里也记为 `und`。现在每条记录都有 `language`：自身没有语言的记录，在多语言站点取写出它的语言，其他情况为 `und`。前端可以直接按 `language` 过滤合并索引，无需特殊处理。
- **行为变更：搜索 UI 加载本语言的索引**：`bukit-search.html` 过去写死请求 `/search.json`，站点部署在 `/repo/` 这类 `baseUrl` 下时搜不到任何结果；多语言站点里每种语言都去请求根目录的文件（`split` 和 `index` 模式下该文件不存在，`merged` 模式下拿到的是合并索引）。现在片段从输入框的 `data-search-url` 属性读取索引地址，值为 `<语言 base URL>/search.json`（例如 `/repo/zh-CN/search.json`；默认语言在根目录且 `search.mode: merged` 时为 `/<默认语言>/search.json`）；删除该属性时仍回退到 `/search.json`。`search.mode: merged` 下片段改为只搜索本语言；如需改用其他索引，请把片段标记复制到模板中，并在那里设置 `data-search-url`（生成的文件每次构建都会重写）。索引加载失败或内容不是数组时，片段显示“Search is unavailable”，不再显示空结果；索引加载期间输入的查询会在加载完成后执行。
- **可选：为默认语言的全部旧地址生成跳转**：新增 `site.defaultLanguageLegacyRedirects`（默认 `false`），帮助在不支持 301 的托管平台（如 GitHub Pages）上把已上线的默认语言切到根目录布局。`defaultLanguageInSubdir: false` 时，为默认语言每个 HTML 页面（内容页、集合列表与分页、分类页、static HTML 路由，以及直接跳到目标页面的 alias）在旧地址 `/<默认语言>/...` 写一个 noindex meta refresh 跳转页。首页仍由 `defaultLanguageRedirect` 控制；已有输出占用的路径以已有输出为准；非 HTML 文件和分类别名不生成跳转。构建输出一条 `i18n.legacy_redirects.derived` 汇总日志；路由计数和报告会按跳转数量增加。带前缀布局下该配置被忽略，并记 `i18n.config.legacy_redirects_ignored`。alias 跳转文档现在会记录目标页面，HTML 不变。不开启的站点输出不变。
- **默认语言可以放在输出根目录**：`site.defaultLanguageInSubdir` 当时默认 `true`（现已改为 `false`，见上文），所有语言仍构建到 `dist/<lang>/`；设为 `false` 时默认语言构建到 `dist/` 并使用站点根 baseUrl，其他语言保留 `dist/<lang>/`。`site.defaultLanguageRedirect` 默认 `true`，会写一个 noindex 首页跳转页，使纯静态托管上的 `/` 不再 404：带前缀布局为 `dist/index.html` → `/<默认语言>/`，根目录布局为 `dist/<默认语言>/index.html` → `/`。跳转页不进入 sitemap、搜索、feed、llms 和 agent-manifest。**行为变化**：现有多语言站点升级后根目录会新增 `dist/index.html`；已在托管层或 CDN 配置 `/` → `/<语言>/` 跳转的站点应设 `defaultLanguageRedirect: false`。根目录布局下，各语言报告移到 `.bukit/variants/<lang>/`，`search.mode: merged` 的合并索引仍在 `/search.json`，默认语言自己的索引写到 `/<defaultLanguage>/search.json`，默认语言不能把其他语言代码用作第一段路径（`i18n-route-reserved-language-prefix`），默认语言 `static/robots.txt` 覆盖生成的 robots，其他与根目录独占文件同名的 static 文件会报 `i18n-root-output-conflict`。
- **行为变化：merged 模式的 sitemap 和 feed 不再生成各语言副本**：配置了 `site.languages` 时，`sitemapMode: merged` 以前仍会由语言级发布投影在每种语言下写出 `dist/<lang>/sitemap.xml`，而文档约定的是只有一份根目录 sitemap；`feed.mode: merged` 同样会写出各语言的 `rss.xml`、`<feed.path>/atom.xml`、`<feed.path>/feed.json` 和 collection feed。现在两种布局下都不再生成这些各语言文件，语言级 `publish-audit` 报告也把对应表示标为未生成。请向搜索引擎提交根目录 `sitemap.xml`，并把 feed 链接（包括主题里用 `{{ site.base_url }}/rss.xml` 拼出的链接）改为根目录的合并 feed。
- **单语言站点拥有 merged 模式的 sitemap 和 feed**：未配置 `site.languages` 时，`sitemapMode: merged` 和 `feed.mode: merged` 本来就会写出 `sitemap.xml` 和 feed，但构建清单没有记录它们。现在它们是受管输出：之后关闭该功能会清理过期文件，同名 `static/` 文件会像 split 模式一样以 `BuildAssetOutputCollision` 失败。
- **行为变化：跳转页统一不被索引**：alias 页和根目录布局下的旧首页跳转页（`/<defaultLanguage>/`）现在带 `robots: noindex` 生成，并排除出 sitemap、搜索和 feed。所有使用 `aliases` 的站点，这些页面都不再出现在 `llms.txt` 中，发布审计也不再把它们当作可索引页面（不再为它们报 `publish.sitemap_missing_route` 或 `publish.search_missing_route`）。
- **static 检查遵循 `build.followSymlinks`**：根输出冲突检查和旧首页跳转页对 `static/` 中符号链接的判断与复制步骤一致：关闭 `followSymlinks` 时忽略，开启时只在链接指向 static 目录内时才算存在。复制步骤会跳过的符号链接不再导致构建失败或抑制跳转页。主题 token 样式表（`assets/css/theme-tokens.css`）也纳入保留语言前缀检查：默认语言在根目录且存在语言代码 `assets` 时报 `i18n-route-reserved-language-prefix`。
- **只有名称完全一致的 `static/robots.txt` 才覆盖生成的 robots**：默认语言在根目录时，名为 `Robots.txt`（或其他大小写）的 static 文件现在报 `i18n-root-output-conflict`，不再抑制生成的 `robots.txt`。爬虫只请求小写路径，之前在区分大小写的托管上站点会没有 robots 规则。根输出冲突检查也会跳过复制步骤跳过的点开头路径（除非开启 `build.publishDotFiles`），默认语言审计判断 static `robots.txt` 是否存在时也遵循同样的点文件和符号链接规则。
- **跳转页改为完整原始 HTML**：alias 页不再经过 HTML 转换管线，SEO 标题/analytics 注入不会再修改它们，而是直接写出完整跳转文档。alias 和分类跳转页同时新增 `<title>` 和 `<meta name="robots" content="noindex">`。因此默认带前缀布局的 alias 输出会变化；跳转目标和输出位置不变。
- **共用 slug 的翻译页不再导致严格 schema 校验失败**：设置了 `site.languages` 时，`canonical_duplicate_id` 现在只在同一语言内查重；没写语言的页面算作默认语言，单语言站点仍要求 id 全站唯一。各语言用相同文件名或 slug 的翻译页（例如 `content/en/index.md` 和 `content/zh/index.md`）按设计共用同一个 id，但此前每对翻译都会报一次重复 id 警告，并在 `build.schemaFailMode: strict` 下导致构建失败。`bukit doctor` 也不再把这类翻译对当作阻断性的 schema 错误。同一语言内两个页面 id 相同时仍会报警告，消息中会写明语言。
- **多语言构建中每条审计问题只输出一次**：`seo.audit`、`publish.audit` 和 `geo.audit` 问题此前会输出两遍：先由每个语言变体输出一次，路由不带语言前缀（因此 `/index/` 每种语言各出现一次），再由合并审计带完整路由输出一次。现在语言变体只写报告文件，由父进程把合并审计的问题和各语言自己的问题（只在单个语言里执行的检查，例如 `split` 模式下各语言的 sitemap；路由补上语言前缀）合在一起，每条不同的问题以 `/en/index/` 这样的路由只输出一次。构建报告的警告数和错误数不再重复计入这些问题，因此多语言站点的计数会下降。各语言的 `.bukit/seo-report.json` 不变。如果构建在写根目录输出时失败，各语言自己的问题和签名链接警告仍会在报错前输出。
- **多语言构建中签名链接警告只输出一次**：`event=incremental.signed_links` 此前每种语言各输出一次，而各语言读取同一份共享数据，内容通常相同。现在各语言把检测结果交给父进程，检测结果相同的语言合成一行，用逗号分隔列出，例如 `language=en,zh-CN`；结果不同的语言仍各占一行。解析日志时需注意 `language=` 现在可能是逗号分隔的列表；构建报告的警告数会相应减少（每少一行重复警告减一）。单语言构建不变。
- **i18n 丢弃与重复内容诊断**：构建现在会输出 `i18n.document.language_unknown`（页面 `language` 不在 `site.languages` 中；单语言构建中不等于 `site.language`）、`i18n.document.i18n_key_duplicate`（同一语言内两个页面使用相同 `i18nKey`）和 `i18n.config.default_language_ignored`（设置了 `site.defaultLanguage` 但未配置 `site.languages`；该警告随后已删除，见上文 `site.languages` 条目）。这些页面或配置以前会被静默丢弃、覆盖或忽略。故意从混合语言内容源中只挑一种语言的单语言站点，现在每次构建都会为其他语言输出 `language_unknown` 警告；每类警告最多显示 5 条并附一行汇总，构建报告的警告数可能上升。除下文 `i18n_key` public ID 变化外，其他有效站点的输出不变。
- **doctor 检查所有已配置语言**：`bukit doctor` 现在会为 `site.languages` 中的每种语言构建并校验路由，不再只检查默认语言；只存在于非默认语言的路由冲突或缺失模板会在 doctor 阶段失败，而不是拖到构建阶段。
- **`i18n_key` 现在决定 public ID**：使用 `i18n_key` 别名的页面，其 `agent-manifest.json` 和公开内容投影中的 `id`、`canonicalId` 现在使用该值，与 `i18nKey` 一致。以前该别名会回退到 slug，导致同一翻译组在不同语言下得到不同的 public ID。使用 `i18nKey` 或 slug 回退的站点输出不变。
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
