新发现 / 仍未闭环的高优先级问题
P0-1：CI 覆盖率质量门存在严重不一致

这是本轮最明显的问题。

quality-gate.sh 默认阈值已经改为 80：

coverage_threshold="${COVERAGE_THRESHOLD:-80}"

但 .github/workflows/ci.yml 里显式设置：

env:
  COVERAGE_THRESHOLD: "70"

由于环境变量优先级更高，CI 实际执行时仍然是 70% 阈值，不是 80%。这与最新提交说明中的“默认覆盖率阈值从 71% 至 80%，匹配 CI 与项目规则”不一致；最新提交消息也确实声称做了质量门强化。

影响

这会造成：

本地直接跑 scripts/quality-gate.sh → 80%
GitHub Actions CI → 70%
项目规则 → 可能以为是 80%

最终结果是：CI 质量门虚高描述、实际偏低执行。

修复建议

把 .github/workflows/ci.yml 改为：

env:
  COVERAGE_THRESHOLD: "80"

或者删除该 env，让脚本默认 80 生效。

P0-2：BodyCache 在多 source 场景存在 BodyKey 碰撞风险

当前 BodyCacheDecorator 使用：

var key = item.BodyKey ?? item.Id;

作为缓存 key。

但 CompositeContentProvider 在多 source 聚合时，只修改了 Id：

Id = $"{sourceKey}:{item.Id}"

没有同步重写 BodyKey。

更危险的是 addToCollections 复制内容时，也只是修改了 Id 和 Meta，同样没有处理 BodyKey。

隐藏 bug 场景

假设有两个内容源：

content:
  sources:
    - type: markdown
      name: blog
      markdown:
        dir: content/blog

    - type: markdown
      name: docs
      markdown:
        dir: content/docs

如果两个 provider 内部 item 的 BodyKey 都是类似：

index.md

那么进入 BodyCacheDecorator 后：

blog:index.md → BodyKey=index.md
docs:index.md → BodyKey=index.md

缓存会错误复用第一个内容源的 body。

风险等级

P0。

这是“内容串源”级别 bug，可能导致：

页面正文错乱；
Notion/Markdown 多源混用时内容污染；
addToCollections 下多 collection 内容共享错误 body；
search/rss/list/taxonomy 使用错误正文。
修复建议

在 CompositeContentProvider 生成新 item 时，必须重写 BodyKey：

BodyKey = item.BodyKey is null
    ? $"{sourceKey}:{item.Id}"
    : $"{sourceKey}:{item.BodyKey}"

addToCollections 场景也要保持同一个源级 body key，而不是 collection 级重复 body：

BodyKey = item.BodyKey is null
    ? $"{sourceKey}:{item.Id}"
    : $"{sourceKey}:{item.BodyKey}"

同时给 BodyCacheDecoratorTests 增加：

Composite sources with same BodyKey should not share cached body.
AddToCollections duplicated route should share same source body safely.
P0-3：RenderDependencyHasher 已补强，但覆盖深度仍不足

你已经新增了 RenderDependencyHasher，这是很关键的修复。

它已覆盖：

site title
description
baseUrl
language
url
languages
defaultLanguage
sitemap/rss/search mode
analytics
seo enabled/renderMode/defaultImage/twitterSite
theme params
theme shortcodes/components
collections 的 permalink/template/listRoute/listTemplate
plugin enabled/runtime/entry
modules/data summary

但是目前仍有明显遗漏。

关键遗漏 1：taxonomy 配置只 hash 了 kind.key

源码中 taxonomy 只追加了：

kind.Key

没有覆盖：

taxonomy.outputMode
taxonomy.pageSize
taxonomy.template
taxonomy.indexTemplate
taxonomy.termTemplate
taxonomy.templates.tags.*
taxonomy.templates.categories.*
taxonomy.itemFields
taxonomy.pinField
taxonomy.pinOrderField
taxonomy.kinds[].template
taxonomy.kinds[].hierarchical
taxonomy.kinds[].title
影响

用户修改：

taxonomy:
  pageSize: 20
  termTemplate: pages/tag-detail.html

可能不会触发相关页面重新渲染。

关键遗漏 2：collections 只 hash 了 4 个字段

当前 collection 只 hash：

key
permalink
template
listRoute
listTemplate

但没有覆盖：

pagination.enabled
pagination.pageSize
pagination.urlPattern
output.rss
output.sitemap
output.archive
filteredLists
schema
schemaFailMode
archiveDetail

这些都会影响 list、pagination、rss、archive、taxonomy 或模板上下文。

修复建议

新增稳定序列化：

AppendStableConfig(hasher, config.Taxonomy);
AppendStableConfig(hasher, config.Site.Collections);
AppendStableConfig(hasher, config.Site.Seo);
AppendStableConfig(hasher, config.Site.Plugins);
AppendStableConfig(hasher, config.Theme);

不要手写只挑字段，建议建立 AOT-friendly 的显式 append 方法，但必须覆盖完整子结构。

P1-1：propertyMap 范围不完整，缺少 SEO 字段映射

当前 NotionPropertyMapConfig 只有：

Title
Slug
Type
PublishAt
Language
I18nKey
Summary
Collection

但上次建议中的：

seoTitle
seoDesc

还没有进入 propertyMap。

影响

真实 Notion 内容库里常见字段：

SEO Title
SEO标题
SEO Description
Meta Description
OG Image
Canonical

如果用户希望明确映射 SEO 字段，目前只能依赖 field normalization 和模板读取，不能进入统一的 SEO 模型。

建议补充
public string? SeoTitle { get; init; }
public string? SeoDescription { get; init; }
public string? SeoImage { get; init; }
public string? Canonical { get; init; }

并在 Notion provider 中将其标准化为 fields 或 meta 中稳定字段：

seo_title
seo_desc
seo_image
canonical
P1-2：propertyMap 与 includeSlugs 仍是两套字段系统

当前 NotionPropertyMapConfig 有 Slug，但 includeSlugs 查询仍使用独立的 IncludeSlugProperty，默认是 "Slug"。

NotionDatabaseSchemaResolver 也只读取：

options.IncludeSlugProperty

没有回退到 options.PropertyMap?.Slug。

隐藏 bug 场景

用户配置：

content:
  notion:
    propertyMap:
      slug: URL Slug
    includeSlugs:
      - about

用户直觉会认为 includeSlugs 用 URL Slug 过滤，但实际仍然会找默认 Slug 字段。

修复建议

在 resolver 中改为：

var includeSlugProp =
    options.IncludeSlugs is { Count: > 0 }
        ? (options.IncludeSlugProperty ?? options.PropertyMap?.Slug ?? "Slug").Trim()
        : null;

并加测试。

P1-3：CollectionWarningStage 没有检测 collection/type 冲突

当前 CollectionWarningStage 只在 没有 collection 且 type=post/page 时输出 legacy warning。

但上次要求更重要的场景是：

type: post
collection: companies

或者：

type: page
collection: article

这类 type 和 collection 指向不同路由模型 的情况，目前不会 warning。

影响

虽然 RouteGenerator 已经正确优先使用 collection，但用户不会知道 type 已经被弱化，很容易误判模板、RSS、taxonomy 行为。

修复建议

新增 warning：

[WARN] Content "xxx" defines both type=post and collection=companies.
Collection routing takes precedence; type is treated as legacy metadata.
P1-4：template doctor 尚未实现

TemplateCommand 当前支持：

create
list
show
validate
snippets
hints
sync

没有 doctor。

当前 validate 只是 Scriban parse 语法检查。

缺口

还没有检查：

include 文件是否存在；
layout 文件是否存在；
page.fields.xxx 是否有 schema 来源；
site.modules.xxx 是否有 data source；
list/taxonomy/search 模板是否使用了正确上下文；
模板引用变量是否拼错。
结论

上次“模板 contract test”建议尚未闭环。

P1-5：route inspect 尚未实现

Program.cs 当前有 data、docs、template 等命令，但没有 route 命令。

虽然 RouteGenerator 已有 GenerateWithSource 和 RouteSource 枚举，可以知道路由来自 FullOverride / PartialOverride / Collection / Permalink / BuiltinFallback。

但这个信息还没有暴露为 CLI 调试工具。

建议

新增：

bukit route inspect
bukit route inspect --json
bukit route inspect --collection companies

输出：

url
outputPath
template
collection
type
language
source
routeSource
derived/plugin
P2：质量门与测试策略仍需加强
P2-1：CI 没有关联到最新提交的 workflow run

我检查到最新提交对应的 workflow run 为空。也就是说，从工具返回的信息看，没有看到该 commit 已经通过 GitHub Actions 的证据。

这不一定代表失败，可能是 workflow 尚未触发、分支不匹配、或者 connector 未返回。但从审计角度，不能把“已提交”视为“已通过 CI”。

建议

在仓库保护规则中要求：

CI / build-and-test
CI / aot-check

必须通过后才能合并。

P2-2：DataCommand 已实现 inspect/dump，但输出还偏轻量

data inspect 已经实现，可以按 module type 汇总 data items，并支持 --module 查看详情。

data dump --format json 也已实现。

但目前 summary 基于：

meta["type"]

作为 module 名称。

建议增强

输出中增加：

collection
sourceKey
sourceId
sourceMode
language / locale
field count
whether used by templates
route impact: none / data-only