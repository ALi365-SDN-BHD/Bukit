既然 Bukit 仍处于开发阶段、没有旧站点包袱、不需要向后兼容，那核心策略应该是：

不保留历史字段、不保留旧命令、不保留迁移 fallback、不保留半实现代码。
只保留 1.0 正式契约需要的最小核心能力。

一句话结论

Bukit 现在应该做一次 破坏式清理 / contract freeze 前重构：

删除所有旧配置兼容逻辑
删除所有未启用 / 半实现 / 注释封存代码
把 Preview 功能从 Core CLI 中移出或隐藏
用严格 schema + fail fast 替代迁移兼容
最终只留下干净、稳定、可测试的 1.0 Core
1. 新的清理原则
1.0 Core 只保留这些

README 当前已经把 1.0 Stable 范围写得很清楚：build / doctor / config / preview / clean、content.sources[]、Markdown / Notion、content.media、集合路由、Scriban、安全输出、SEO / RSS / sitemap / JSON Feed、GEO、构建报告、增量构建、Native AOT CLI、GitHub Pages 部署。

这些可以作为 1.0 核心边界。

不属于 Core 的全部移出

clone-to-theme、import html-demo、import seed、notion push、外部插件生态、插件市场、BukitJalil、高级 AI 自动化都不属于 1.0 稳定承诺。

另外，上传的 BukitJalil 文档也明确 BukitJalil 是 Bukit 上层控制系统，属于 UI + AI Layer，不直接渲染 HTML，而是把结构交给 Bukit 构建。
所以 Bukit Core 里不应混入 BukitJalil、AI Agent、模板生成器、工作流编排等上层概念。

2. 配置系统应该彻底清理
2.1 删除 ConfigRemovedFieldScanner

现在不需要“旧字段迁移提示”。
ConfigRemovedFieldScanner 当前专门扫描旧字段，例如 content.provider、content.markdown、content.notion、site.rssMode、site.searchMode 等。

开发阶段不需要这个。

建议

删除：

src/Bukit.Config/ConfigRemovedFieldScanner.cs

同时删除：

ConfigRemovedFieldScanner.RejectRemovedFields(root);

当前调用在 ConfigLoader 中。

替代方案

不要迁移提示，改为：

strict unknown field validation

也就是：

site.yaml 中出现未定义字段，直接报错。

这样核心更干净，行为更确定。

2.2 删除 content.provider 特判

ConfigLoader 现在单独判断 content.provider 并提示迁移到 content.sources[]。

既然没有旧配置，不需要。

建议

删除这段：

var provider = ConfigYamlHelpers.GetOptionalString(contentNode, "provider");
if (!string.IsNullOrWhiteSpace(provider))
{
    throw new ConfigException(
        "content.provider is removed in Bukit 1.0. Use content.sources[] instead.",
        DiagnosticCode.ConfigProviderRemoved);
}

以后 content.provider 只作为未知字段报错。

2.3 删除 collections.yaml fallback

当前 ConfigLoader 支持：

ReadCollections(siteNode) ?? TryReadCollectionsFile(path)

也就是如果 site.collections 不存在，会自动找外部 collections.yaml。

开发阶段不需要这种双入口。

建议

只允许：

site:
  collections:

删除：

TryReadCollectionsFile(...)

也删除 ConfigCollectionReader.TryReadCollectionsFile 整个方法。

收益
配置入口唯一
文档更简单
测试更少
AI 生成 site.yaml 不容易走错路径
2.4 删除扁平 source 写法

当前 Notion / Markdown 支持两种写法：

- type: notion
  notion:
    databaseId: xxx

也支持扁平写法：

- type: notion
  databaseId: xxx

原因是 ReadNotionConfigFrom 使用 GetOptionalMapping(..., "notion") ?? contentNode，Markdown 也一样。

建议

只保留标准嵌套写法：

content:
  sources:
    - type: notion
      name: posts
      collection: posts
      notion:
        databaseId: xxx

删除 fallback：

?? contentNode

改为：

var notionNode = ConfigYamlHelpers.GetMapping(sourceNode, "notion");

Markdown 同理。

2.5 删除 modelSchema 字段别名

当前存在很多别名兼容：

标准字段	兼容字段
canonicalField	field
fieldScopes	scopedFields
fieldType	type
entityType	type
relationType	type
targetField	labelField
targetIdField	idField
reference	referenceRule
reference.labelField	nameField

这些在 ConfigCollectionReader 里多处使用 ?? fallback。

建议

全部删除别名，只保留标准字段：

content:
  modelSchema:
    canonicalMappings:
      - canonicalField: title
        rawKey: Title

    fieldScopes:
      posts:
        - name: author
          fieldType: string

    entityMappings:
      - rawKey: Company
        entityType: organization

    relationMappings:
      - rawKey: RelatedCompany
        relationType: mentions
        targetField: name
        targetIdField: id

    reference:
      targetType: company
      idField: id
      labelField: name
2.6 配置解析全部 strict

当前 GetOptionalBool 支持 yes/no。
GetOptionalInt 解析失败会返回 null，然后上层吃默认值。

开发阶段建议更严格。

建议

配置字段统一：

类型	规则
bool	只接受 true / false
int	非法直接 fail
long	非法直接 fail
double	非法直接 fail
enum/string union	非法直接 fail
unknown field	直接 fail

不要让错误配置悄悄变成默认值。

2.7 AssetHashMode / FingerprintMode 二选一

BuildConfig 当前同时存在 AssetHashMode 和 FingerprintMode。

这不干净。

建议

只保留：

build:
  fingerprintMode: size-time

删除：

AssetHashMode

原因：fingerprint 语义更完整，覆盖 asset hash 与输出资源指纹。

3. CLI 应该拆干净
3.1 Core CLI 只注册核心命令

当前 CLI registry 最终注册了很多命令，包括 clone、import、notion、intent、visual、webhook、route、data、docs 等。

这会让 Core 变复杂。

建议 1.0 Core CLI 只保留
bukit build
bukit doctor
bukit config
bukit preview
bukit clean
bukit version
bukit completion

可选保留：

bukit theme
bukit template
bukit seo
bukit geo
bukit publish

但这几个最好确认是否属于 Core。

建议移出 Core
bukit clone
bukit import
bukit notion push
bukit intent
bukit webhook
bukit visual
bukit docs
bukit data
bukit route

尤其 clone 和 import html-demo 在当前 CLI spec 中直接注册，且选项非常多。
这些应该拆成独立插件或独立包。

3.2 删除 --json 旧别名

Program.cs 当前把全局 --json 转换成 --log-format json。

如果不考虑兼容，可以删除。

统一为
bukit build --log-format json

删除：

bukit build --json

这样 CLI 参数更少、更标准。

3.3 保留 --site 多站点，但不做隐式魔法

ConfigPathResolver 当前支持：

默认 site.yaml
--config
--site xxx → sites/xxx.yaml
如果 --config 指向 sites/<site>/site.yaml，自动把 rootDir 回退到项目根目录。

这里建议保留多站点，但减少隐式规则。

建议

保留：

bukit build --config site.yaml
bukit build --site silkroad

但明确规定：

--site silkroad => sites/silkroad/site.yaml

或者：

--site silkroad => sites/silkroad.yaml

二选一，不要同时支持两种结构。

我建议选：

sites/<site>/site.yaml

因为更适合多站点工程。

4. Engine 保留安全代码，不是兼容代码

有些代码虽然看起来“保守”，但不是兼容，而是稳定性核心。

必须保留
输出目录 marker

BuildPlanner 会拒绝清理项目根目录、用户 home、磁盘根目录、.git，并要求已有输出目录必须有 .bukit-output-marker。

这个必须保留。

这是防止误删文件的核心安全机制。

CI 禁用危险能力

ConfigApplier 在 CI 下会设置：

ExternalPluginPolicy = Deny
FollowSymlinks = false

这个也必须保留。

它不是兼容层，是安全边界。

5. 插件系统应该收缩
5.1 外部插件生态不进 1.0 Core

README 已经明确外部插件生态不属于 1.0 stable。

建议
保留最小内置插件能力：
sitemap
rss
search
pagination
taxonomy
archive
外部 process plugin 暂时隐藏或实验性保留。
WASM 插件直接删除。
5.2 删除 WASM 残留

WasmPluginInvoker.cs 整个文件被 #if false 包住，说明已经禁用。

ExternalProtocolPluginSource 里也有 #if false 的 wasm runtime 分支。

建议直接删除
src/Bukit.Engine/Plugins/Protocol/WasmPluginInvoker.cs

同时删除所有：

// DESKTOP-REMOVED
#if false

相关代码。

5.3 修复或删除 Sha256

ExternalPluginConfig 有 Sha256 字段。
ExternalProtocolPluginSource 也会校验 sha256。
但 ReadExternalPlugins 没有从 YAML 读取 sha256。

二选一

如果 1.0 不支持外部插件：

删除 Sha256 字段和相关校验

如果保留 process plugin：

Sha256 = ConfigYamlHelpers.GetOptionalString(pluginNode, "sha256")

我的建议：1.0 Core 不保留外部插件生态，只保留内置插件。

6. 主题系统清理
6.1 theme.yaml 强校验保留

theme.yaml 当前要求 name、version、engine 等字段。

这个应该保留。

它不是兼容代码，而是 1.0 主题契约。

6.2 主题路径规则简化

当前 ThemePathResolver 支持：

项目根 layouts
themes/<name>/layouts
remote theme source
theme extends
user root layouts override

开发阶段建议简化为：

themes/<themeName>/
  theme.yaml
  layouts/
  assets/
  static/

暂时不要支持太多路径组合。

建议
1.0 只支持本地 theme。
暂停 remote theme.source。
暂停 theme.extends，或者标为 experimental。
不允许 root-level layouts 覆盖 theme。
theme.layouts/assets/static 默认固定，不建议用户自定义。

这样 1.0 核心会稳定很多。

7. Taxonomy / Feed / Search 清理
7.1 Taxonomy 只保留一种配置形态

当前 Taxonomy 同时有：

taxonomy.template
taxonomy.indexTemplate
taxonomy.termTemplate
taxonomy.templates.tags
taxonomy.templates.categories
taxonomy.kinds[]

这太多。

建议只保留
taxonomy:
  kinds:
    - key: tags
      title: Tags
      template: taxonomy-term.html
      indexTemplate: taxonomy-index.html

删除：

taxonomy:
  template:
  indexTemplate:
  termTemplate:
  templates:
    tags:
    categories:

这样 taxonomy 模型统一。

7.2 Feed / Search 保留新结构

保留：

site:
  feed:
    formats: [rss]
    limit: 20
    path: feed

  search:
    mode: split
    ui: default

删除所有旧概念：

rssMode
searchMode
plugins.rss

不需要 scanner，直接 unknown field fail。

8. 仓库层面立即清理
8.1 删除 .lscache

.csproj.lscache 文件自己说明这是 C# Dev Kit 语言服务缓存，可以安全删除，会自动生成。

删除
find . -name "*.lscache" -delete
.gitignore 增加
*.lscache
8.2 删除 solution 中注释掉的项目

slnx 中有已注释的 PluginSourceGenerator 项目。
测试项目也被注释。

建议

直接删除注释，不保留历史痕迹。

9. 最终清理清单
A. 立即删除
*.lscache
ConfigRemovedFieldScanner.cs
WasmPluginInvoker.cs
PluginSourceGenerator 注释引用
DESKTOP-REMOVED 注释块
#if false 代码块
content.provider 特判
collections.yaml fallback
source 扁平配置 fallback
modelSchema 字段别名 fallback
--json 全局别名
B. 从 Core CLI 移出
clone
import
notion push
intent
webhook
visual
docs
data
route

这些可以后续作为：

Bukit.Plugins.Clone
Bukit.Plugins.Import
Bukit.Plugins.NotionPush
Bukit.Experimental.*
C. 保留但收紧
build
doctor
config
preview
clean
version
completion
content.sources[]
Markdown provider
Notion provider
content.media
collections route
Scriban rendering
SEO/GEO
RSS/sitemap/json-feed
incremental build
output marker safety
CI safety policy
theme.yaml manifest
10. 推荐的 1.0 Core 契约

最终 Bukit 1.0 可以收敛成这样：

site:
  name: silkroad
  title: Silkroad Biz
  url: https://example.com
  baseUrl: /
  language: zh-CN

  collections:
    posts:
      permalink: /insights/{slug}/
      template: post.html
      listRoute: /insights/
      listTemplate: posts.html

content:
  sources:
    - type: notion
      name: posts
      collection: posts
      notion:
        databaseId: ${NOTION_DATABASE_ID}

  media:
    downloadToLocal: true
    downloadDir: assets/uploads

theme:
  name: silkroadbiz

build:
  output: dist
  clean: true
  fingerprintMode: size-time

只允许这一套，不再接受旧写法。