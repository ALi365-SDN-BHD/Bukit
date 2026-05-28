深度问题与隐藏风险

下面是核心问题清单，按优先级排序。

P0：必须优先修复的问题
P0-1：文档编码异常，说明仓库存在编码治理漏洞

guide/dev/README.md 第 5 行出现明显乱码：

Language versions: English (current) | [简体中文(./README.zh-CN.md)

这是典型 UTF-8 / GBK / 编码转换错误。

风险

这不是小问题，因为 Bukit 是面向：

多语言站点
中文内容
Notion 内容
AI Agent 自动化
文档驱动开发

如果仓库文档都出现编码问题，后续可能影响：

中文 README
中文模板
中文 SEO 字段
Notion 中文内容
sitemap / rss / search.json 中的中文
AI Agent 读取技能文档时误解上下文
建议修复

增加仓库级编码检查：

dotnet test
pwsh ./scripts/check-encoding.ps1

检查范围：

*.md
*.yaml
*.yml
*.json
*.html
*.scriban
*.cs
*.txt

规则：

全部必须 UTF-8 without BOM 或统一 UTF-8
禁止出现常见 mojibake 字符串：
绠€
浣撲
鈫
嘳
CI 中强制失败
P0-2：collections 与 post/page 兼容层长期并存，会继续制造路由分叉

当前官方 review 已经指出：

collections 是主路径，post/page 默认规则是兼容层，策略需要收敛。

当前隐患

如果继续保留两套模型：

site.collections
site.permalinks
content.type = post/page
content.collection

后续会产生大量边界问题：

场景	潜在 bug
type=post 但 collection=news	模板和路由可能不一致
Notion Type 和 Collection 同时存在	谁优先不清楚
permalink 与 collection permalink 冲突	用户难以理解
listRoute 和默认 /blog/ 同时生成	可能重复页面
taxonomy 绑定 collection 还是 type	派生页归属混乱
建议修复

制定强规则：

v1：允许 type/post/page 兼容。
v2：collection 优先。
v3：collection 成为唯一推荐模型。

引擎行为建议：

如果 content.collection 存在：
  使用 collection
否则：
  用 type 映射到 legacy collection

并输出 warning：

[DEPRECATED] content.type=post/page legacy routing is enabled.
Please migrate to content.collection and site.collections.
P0-3：Notion 字段映射与 schema validation 存在潜在错位

Notion provider 当前映射：

Published → filter
Title → ContentItem.Title
Slug → ContentItem.Slug
Type → meta.type
PublishAt → ContentItem.PublishAt
language → meta.language
i18n_key → meta.i18nKey
tags/categories → meta
Collection → meta.collection
custom fields → page.fields.*
潜在问题

Notion 数据库字段通常会出现这些真实情况：

Publish At
PublishAt
publish_at
Published At
Date
语言
Language
Lang
分类
Category
Categories
SEO Title
SEO标题

如果字段映射过于固定，会导致：

日期为空
slug 为空
collection 为空
i18n key 丢失
SEO 字段进不了模板
schema validation 报 unknown_field
建议修复

增加 content.notion.propertyMap：

content:
  notion:
    propertyMap:
      title: Title
      slug: Slug
      publishAt: PublishAt
      collection: Collection
      language: language
      i18nKey: i18n_key
      summary: Summary
      seoTitle: SEO Title
      seoDesc: SEO Desc

并增加 doctor 检查：

bukit doctor --notion-schema

输出：

Missing required Notion property: Slug
Unknown mapped property: SEO Desc
Property type mismatch: Published expected checkbox, got status
P0-4：多语言默认语言过滤属于高风险区域，需要补完整回归测试

最近 PR 明确出现“默认语言内容过滤逻辑，移除冗余非默认语言内容”。

这说明多语言构建曾经或正在存在输出污染问题。

高风险场景

假设：

site:
  languages: [en, zh]
  defaultLanguage: en

内容：

slug	language	i18nKey
about	en	about
about	zh	about
contact	zh	contact
service	en	service

可能出现：

zh-only 内容错误出现在默认语言根路径
en 内容没有出现在 /en/ 或根路径
search index 合并重复
sitemap 出现错误 hreflang
RSS 包含非默认语言
homepage list 混入其他语言内容
taxonomy term 页面混入其他语言
必须新增测试矩阵
1. 默认语言 en + zh 翻译完整
2. 默认语言 en + zh-only orphan content
3. 默认语言 zh + en secondary content
4. 无 language 字段内容
5. language 字段大小写不一致：zh-CN / zh-cn / zh
6. i18nKey 缺失
7. listRoute 多语言输出
8. sitemap/rss/search merged mode
9. taxonomy 多语言页面
10. pagination 多语言页面
P1：核心稳定性问题
P1-1：BodyStore 延迟读取已经引入，但 read amplification 未治理

官方 architecture review 已经明确指出：

Body loading 已经 deferred，但 rendering/search/RSS/pagination 仍会在不同路径触发读取，需要量化 read amplification。

本质问题

当前架构中 body 可能被这些阶段读取：

render page
auto summary
search index
RSS
list page excerpt
taxonomy item excerpt
plugin derive pages
external protocol plugin

如果每个阶段都独立读取 body，会造成：

1000 篇文章 × 5 个阶段 = 5000 次 body read

Notion 场景更严重：

cache miss 慢
markdown parse 重复
HTML body 多次分配
AOT 下内存压力更明显
建议修复

增加构建级 body read cache：

IBuildBodyCache
{
    ValueTask<string?> GetHtmlAsync(BodyKey key);
}

并在 metrics 中输出：

{
  "bodyRead": {
    "totalRequests": 5240,
    "cacheHits": 4210,
    "cacheMisses": 1030,
    "uniqueBodies": 1000,
    "amplification": 5.24
  }
}

目标：

amplification <= 1.5
P1-2：增量构建 hash 可能没有覆盖“插件配置”和“SEO 配置”

当前 skip 条件覆盖了 templateHash、contentHash、routeHash。

但从静态站点引擎角度，仅这些可能不够。

可能漏掉的影响源
变更	是否应触发重渲染
site.seo 修改	应触发
site.analytics 修改	应触发
theme.params 修改	应触发
taxonomy 配置修改	应触发
site.baseUrl 修改	应触发
site.url 修改	sitemap/rss/OG 应触发
plugin config 修改	应触发派生页
renderer options 修改	应触发
建议修复

将当前 hash 拆成：

contentHash
routeHash
templateHash
siteRenderHash
pluginHash
seoHash
assetHash

其中 siteRenderHash 至少覆盖：

site.title
site.description
site.baseUrl
site.url
site.language
site.languages
site.defaultLanguage
theme.params
site.analytics
site.seo
taxonomy
plugins config

否则会出现：

用户改了 SEO / 主题参数，但页面因为增量构建被跳过，输出仍是旧内容。

P1-3：build.clean 默认值和安全 clean 机制需要统一解释

README 说：

build.clean requires .bukit-output-marker before cleaning and refuses to clean dangerous directories.

配置文档显示：

build.clean | bool | true

潜在问题

如果 build.clean=true 是默认值，那么新用户首次 build 时：

dist 不存在：正常
dist 存在但无 marker：可能拒绝
dist 指向错误目录：拒绝
CI 中复用旧目录：可能失败

这不是坏设计，但需要明确用户体验。

建议修复

doctor 中增加：

Output directory safety:
- output exists: yes
- marker exists: no
- clean requested: yes
- result: refuse
- fix: run bukit clean --init-marker or choose a dedicated dist directory

并在错误信息里不要只说 “refuse to clean”，要说明：

Bukit refuses to clean this directory because it does not contain .bukit-output-marker.
This prevents accidental deletion of non-Bukit files.
P1-4：External Plugin 与 Native AOT 的生态路线仍不够坚实

官方 review 已经指出：

AOT eliminates external DLL loading; protocol-based external extensions need strengthening.

问题本质

Bukit 的定位是 Native AOT，但传统 .NET 插件生态依赖：

AssemblyLoadContext
Reflection
Dynamic loading

这些都与 AOT 有冲突。

如果插件系统没有强约束，很容易出现：

Debug 模式插件可用
AOT 发布后插件不可用
本地可用，GitHub Actions 不可用
用户安装主题后插件失效
外部插件安全风险
建议修复

明确插件分层：

插件类型	AOT 支持	用途
built-in plugin	是	官方核心能力
generated plugin	是	编译期生成
external protocol plugin	是	Node/Python/HTTP 子进程
external DLL plugin	否 / 非推荐	仅非 AOT

对外统一推荐：

AOT-first plugin = external protocol plugin

并提供协议规范：

{
  "hook": "derive-pages",
  "input": {
    "site": {},
    "content": [],
    "routes": []
  },
  "output": {
    "pages": [],
    "assets": [],
    "diagnostics": []
  }
}
P2：功能完整性问题
P2-1：Clone any website design into Bukit theme 目前风险很高

README 已经公开了：

bukit clone --tokens tokens.json --theme my-clone

并描述为 “Clone any website's design into a Bukit theme”。

高风险点

这个功能非常有吸引力，但也是 Bug 密集区：

风险	说明
tokens schema 不稳定	AI 或用户生成 tokens 结构不一致
生成主题缺少必要模板	build 失败
静态资源路径不完整	CSS / 图片丢失
baseUrl 未适配	GitHub Pages 子路径失效
HTML 结构不可组件化	主题难以维护
版权/合规风险	“clone any website” 表述过强
建议调整定位

从：

Clone any website's design

改为：

Extract design tokens and scaffold a Bukit-compatible theme

命令也建议分阶段：

bukit clone inspect --url https://example.com
bukit clone tokens --input snapshot.json --output tokens.json
bukit clone theme --tokens tokens.json --theme my-theme
bukit theme validate my-theme

这样更稳，也更适合 AI Agent 分步执行。

P2-2：模板系统需要 contract test，而不仅是 Scriban syntax validate

README 里已有：

bukit template validate
bukit template hints
bukit template sync

但还缺少更关键的测试

Scriban 语法正确，不代表模板可用。

需要检查：

模板引用变量是否存在
page.fields.xxx.value 是否可能为空
list template 是否拿到 pages
taxonomy template 是否拿到 term
i18n template 是否拿到 alternates
partial/include 是否存在
theme params 是否声明
建议新增
bukit template doctor

输出：

[ERROR] pages/post.html references page.fields.cover.value but field cover is not declared in schema.
[WARN] pages/list.html references site.modules.hero but no data source provides module hero.
[ERROR] include components/card.html not found.
P2-3：schema validation 的 unknown_field 策略可能误伤 AI/Notion 扩展字段

配置文档显示 schema 支持：

unknown_field

并且 unknown_field 会跳过一些系统字段。

潜在问题

AI Agent 或 Notion 经常会增加临时字段：

seo_keywords
ai_summary
source_url
original_url
cover_prompt
generation_notes

如果 schema strict 太早，会阻碍自动化内容流。

建议

增加 schema mode：

build:
  schemaFailMode: warn

site:
  schemaUnknownFieldPolicy: allow | warn | strict

并支持按 collection 配置：

site:
  collections:
    post:
      schemaUnknownFieldPolicy: warn
    companies:
      schemaUnknownFieldPolicy: strict
P2-4：mode=data 很关键，但需要更强的数据模块调试能力

内容文档说明：

mode=content generates routes
mode=data injects into site.modules

这对企业官网非常重要，例如：

hero
features
services
team
testimonials
companies
destinations
pricing
faq
当前风险

用户不知道 data 是否成功进入：

site.modules.hero
site.modules.services
site.modules.companies

模板报错时也可能不知道数据缺失还是模板写错。

建议新增
bukit data inspect
bukit data inspect --module hero
bukit data dump --format json

输出：

{
  "modules": {
    "hero": {
      "count": 1,
      "source": "notion:homepage_sections",
      "fields": ["title", "subtitle", "image", "cta_text"]
    }
  }
}
P3：工程治理与质量门禁问题
P3-1：测试策略还偏“可运行验收”，缺少系统性回归矩阵

测试文档说当前策略偏向 runnable acceptance，并建议核心路径变更时增加 smoke。

这很好，但对 Bukit 这种 SSG 还不够。

建议增加 Snapshot Golden Tests

对每个 examples：

examples/starter
examples/notion
examples/i18n
examples/taxonomy
examples/enterprise
examples/modules

执行：

bukit build --clean
compare dist with snapshots

检查：

HTML 路由存在
sitemap 正确
rss 正确
search.json 正确
taxonomy.json 正确
hreflang 正确
asset 路径正确
P3-2：README、guide、skills 三套知识源可能漂移

README 提到：

src/skills/ 是 agent-facing Bukit knowledge layer，不是 runtime source directory。

开发文档又说：

guide/dev 是 maintainer-facing contract。

这说明当前至少有三套知识源：

README
guide/dev
src/skills
风险

当 CLI 参数变化时，可能出现：

README 写 A
guide/dev 写 B
skills 写 C
Codex 按旧规则执行
建议

增加 docs consistency test：

bukit docs check

检查：

CLI 命令是否存在
site.yaml 字段是否真实存在
skills 中命令是否与 CLI help 一致
guide/dev 是否引用不存在的文件
README 示例是否能执行
推荐下一阶段修复路线
第一阶段：稳核心

优先处理这些：

1. 修复文档编码乱码，并加编码 CI 检查
2. 补默认语言过滤回归测试
3. 补 collections/type 兼容策略文档和 warning
4. 增加 Notion propertyMap
5. 增量构建 hash 覆盖 site/theme/plugin/seo 配置
第二阶段：稳构建
1. BodyStore read amplification metrics
2. build clean 安全提示优化
3. route inventory 输出调试报告
4. data modules inspect 命令
5. template doctor 命令
第三阶段：稳生态
1. external protocol plugin 标准化
2. theme validate 加强
3. clone 功能拆分为 inspect/tokens/theme/validate
4. docs/skills/README 一致性检查
5. golden snapshot acceptance
可直接交给 Codex 的修复 Prompt

下面这段可以直接给 Codex 执行：

# Bukit Core Hardening Task

你正在维护 ALi365-SDN-BHD/Bukit，这是一个 .NET 10 Native AOT Static Site Engine。目标是强化项目核心稳定性，优先修复会影响构建正确性、多语言输出、Notion 内容流、增量构建、主题生态和 AI Agent 使用体验的问题。

## 总目标

围绕以下方向进行代码审查、测试补齐和修复：

1. 修复文档编码治理问题。
2. 强化 collections 主模型与 post/page 兼容层治理。
3. 补齐默认语言过滤、多语言 sitemap/rss/search/taxonomy 的回归测试。
4. 增强 Notion 字段映射能力。
5. 修复或补强增量构建 hash 覆盖范围。
6. 增加 BodyStore 读取放大 metrics。
7. 增强 template / data / route 调试能力。
8. 保持 Native AOT 兼容。

## 必须遵守

- 不破坏现有 CLI。
- 不破坏现有 examples/starter。
- 不引入 AOT 不兼容的动态反射/动态加载。
- 新增功能必须有测试。
- 新增配置必须有文档。
- 修改路由、i18n、增量构建、Notion provider 时必须增加回归测试。

## 任务 1：文档编码检查

新增脚本或测试，扫描以下文件类型：

- `.md`
- `.yaml`
- `.yml`
- `.json`
- `.html`
- `.scriban`
- `.cs`
- `.txt`

检查文件是否为有效 UTF-8，并检测常见 mojibake 字符串：

- `绠€`
- `浣撲`
- `鈫`
- `嘳`

如果发现乱码，在 CI 中失败。

同时修复 `guide/dev/README.md` 中的乱码语言链接。

## 任务 2：collections 兼容层治理

明确规则：

- 如果 `ContentItem.Meta.collection` 存在，优先使用 collection。
- 如果 collection 不存在，才使用 legacy `type=post/page`。
- 如果同时存在 `type` 和 `collection` 且二者可能映射不同路由，输出 warning。
- 为 legacy post/page 路由输出 deprecation warning，但不破坏现有行为。

增加测试：

- collection 优先于 type
- type fallback 正常
- collection + permalink 冲突时行为明确
- listRoute 与默认 `/blog/` 不重复生成

## 任务 3：默认语言过滤回归测试

针对多语言构建新增测试矩阵：

- defaultLanguage=en，内容包含 en/zh
- defaultLanguage=zh，内容包含 zh/en
- zh-only orphan content
- language 缺失内容
- language 大小写不一致
- i18nKey 缺失
- listRoute 多语言输出
- sitemap/rss/search split/merged/index 模式
- taxonomy 多语言页面
- pagination 多语言页面

要求：

- 默认语言根路径不得混入非默认语言内容。
- 非默认语言内容只进入对应语言输出。
- sitemap/rss/search 不重复、不串语言。
- hreflang 正确。

## 任务 4：Notion propertyMap

新增配置：

```yaml
content:
  notion:
    propertyMap:
      title: Title
      slug: Slug
      publishAt: PublishAt
      collection: Collection
      language: language
      i18nKey: i18n_key
      summary: Summary
      seoTitle: SEO Title
      seoDesc: SEO Desc

要求：

不配置时保持当前默认映射。
配置后使用 propertyMap 覆盖默认字段名。
doctor 能检查 mapped property 是否存在、类型是否匹配。
增加 Notion provider 单元测试。
任务 5：增量构建 hash 补强

当前 skip 条件包含 templateHash/contentHash/routeHash。请检查是否覆盖以下影响渲染输出的配置：

site.title
site.description
site.baseUrl
site.url
site.language
site.languages
site.defaultLanguage
site.seo
site.analytics
theme.params
taxonomy
site.plugins
plugin config
renderer version/options

如果未覆盖，新增：

siteRenderHash
pluginHash
seoHash

确保这些配置变化会触发相关页面重新渲染或相关固定输出重新生成。

增加测试：

修改 theme.params 后页面重新渲染
修改 site.seo 后 HTML head/sitemap/search 正确更新
修改 analytics id 后页面重新渲染
修改 taxonomy 配置后 derived pages 重新生成
任务 6：BodyStore read amplification metrics

为 BodyStore / BodyKey 延迟读取增加构建级缓存和 metrics：

{
  "bodyRead": {
    "totalRequests": 0,
    "cacheHits": 0,
    "cacheMisses": 0,
    "uniqueBodies": 0,
    "amplification": 0
  }
}

目标：

渲染、RSS、search、summary、pagination、taxonomy 共用构建级 body cache。
metrics 写入 --metrics 输出。
增加测试，确保同一 BodyKey 多次请求只触发一次底层读取。
任务 7：增强调试命令

新增或增强以下命令：

bukit route inspect
bukit data inspect
bukit template doctor
route inspect

输出：

url
outputPath
template
collection
language
source
derived/plugin 来源
conflict 信息
data inspect

输出：

site.modules 下有哪些 module
每个 module 来源
count
fields
source mode
template doctor

检查：

include/partial 是否存在
模板引用的 page.fields.* 是否在 schema 或数据中存在
site.modules.* 是否有来源
list/taxonomy/i18n 模板必要变量是否存在
任务 8：测试与验收

运行：

dotnet build bukit.slnx -c Release -warnaserror
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release
dotnet format bukit.slnx --verify-no-changes

并新增 smoke/golden tests：

starter build
i18n build
taxonomy build
notion mock build
modules/data build
GitHub Pages baseUrl build

最终输出修复报告：

修改文件列表
新增测试列表
修复问题说明
未解决风险
后续建议

---

# 最终判断

Bukit 当前已经具备一个优秀 SSG 内核的形态：

```text
配置契约清晰
内容模型统一
路由系统独立
渲染系统抽象
插件生命周期明确
AOT 方向正确
AI Agent 知识层开始成型

但要成为真正“坚实的项目核心”，下一步不应该继续堆功能，而应该集中治理：

多语言正确性
路由唯一性
增量构建正确性
Notion 字段稳定性
BodyStore 性能闭环
模板/数据/路由调试能力
文档与 Agent skills 一致性

最优先的切入点是：

默认语言过滤 + collections 收敛 + 增量 hash 补强 + Notion propertyMap + 编码治理