Bukit.Plugin.Notion 开发技术书

目标插件：Bukit.Plugin.Notion
目标命令：bukit notion ...
插件类型：跨平台外部进程插件
协议版本：bukit-plugin-v1
上游输入：Bukit.Plugin.Import 生成的 notion-seed/ 与 notion-database-map.yaml
设计原则：Import 只生成 handoff，Notion 插件负责网络与 Token 权限
执行对象：Codex / AI Coding Agent

1. 背景与核心决策

Bukit.Plugin.Import 不负责直接推送 Notion。它只生成本地 handoff artifacts，例如：

sites/<site-name>/notion-seed/*.json
sites/<site-name>/notion-seed/notion-database-map.yaml

现有 ADR 已明确：Import process plugin 不实现 direct Notion push，真实 Notion 写入应由独立 Bukit.Plugin.Notion 或未来 command-level permission 模型承担。

这样做的核心原因是当前插件权限是 plugin-level。如果 Import 插件声明 network 和 NOTION_TOKEN 权限，那么用户即使只做本地 HTML 导入，也必须给 Import 授予网络和 Token 权限，这违背最小权限原则。ADR 中已经明确说明该风险，并要求 Import 保持 network: false、不读取 token。

当前 Import 插件 manifest 也符合这个边界：Network: false，Environment.Read: []。

因此，正式方案是新增：

plugins/Bukit.Plugin.Notion/

由它单独声明：

network: true
environment:
  read:
    - NOTION_TOKEN
2. 总体目标
2.1 最终命令面

按照你确定的顺序，Notion 插件最终提供：

bukit notion validate-seed <seed-dir>

bukit notion validate-database-map <database-map>

bukit notion push \
  --seed <seed-dir> \
  --database-map <database-map> \
  --token-env NOTION_TOKEN \
  --mode create \
  [--dry-run]

bukit notion push \
  --seed <seed-dir> \
  --database-map <database-map> \
  --token-env NOTION_TOKEN \
  --mode upsert \
  [--dry-run]

bukit notion push \
  --seed <seed-dir> \
  --database-map <database-map> \
  --token-env NOTION_TOKEN \
  --mode replace \
  [--dry-run]

并生成：

.bukit/reports/plugin-output/notion/notion-push-report.json
.bukit/reports/plugin-output/notion/notion-push-report.md
2.2 分阶段开发顺序

严格按照以下顺序：

1. Notion plugin skeleton
2. notion validate-seed
3. notion validate-database-map
4. notion push --dry-run
5. notion push --mode create
6. notion push --mode upsert
7. notion push --mode replace
8. notion-push-report

不得跳过前置阶段，不得一次性实现全部 push 模式。

3. Notion API 事实边界

Notion API 当前要求使用 bearer token 认证，请求通过 Authorization header 发送；官方认证文档示例使用 Authorization: Bearer "$NOTION_ACCESS_TOKEN" 和 Notion-Version header。

Notion API 基础地址是 https://api.notion.com，请求和响应体采用 JSON，字段使用 snake_case，且 Notion API 不支持空字符串，空值需要用 null 表示。

Notion 现在在 API 参考中同时列出 Database 与 Data source，并把 Databases (deprecated) 作为单独分组。为了面向新 API 演进，本插件内部模型应优先使用 dataSourceId，同时兼容旧 handoff 文件中的 databaseId 字段。

创建页面 endpoint 是 POST https://api.notion.com/v1/pages。官方文档说明，创建 page 时 parent 可以是 existing page 或 data source；当 parent 是 data source 时，传入的 properties 必须匹配该 data source 的 properties。

4. 强制架构边界
4.1 允许依赖
Bukit.Plugin.Notion
  -> Bukit.Plugin.Abstractions
  -> Bukit.Notion
  -> Bukit.Shared

建议新增：

src/Bukit.Notion/
tests/Bukit.Notion.Tests/
plugins/Bukit.Plugin.Notion/
tests/Bukit.Plugin.Notion.Tests/
4.2 禁止依赖
Bukit.Plugin.Notion -> Bukit.Cli
Bukit.Plugin.Notion -> Bukit.PluginHost
Bukit.Plugin.Notion -> Bukit.Plugin.Import
Bukit.Plugin.Notion -> Bukit.Labs.*
Bukit.Cli -> Bukit.Plugin.Notion
Bukit.PluginHost -> Bukit.Plugin.Notion
Bukit.Engine -> Bukit.Plugin.Notion

Notion 插件只消费 Import 生成的文件，不直接引用 Import 插件实现。

4.3 禁止行为
禁止把 Notion push 放回 Import 插件
禁止让 Import 插件声明 network: true
禁止让 Import 插件读取 NOTION_TOKEN
禁止 Core 直接引用 Notion 插件实现
禁止 Notion 插件引用 Labs
禁止 stdout 输出普通日志
禁止 shell 拼接执行
禁止动态 DLL 插件
禁止 executable 放入 .bukit/
禁止把 secret 写入 report
5. 目标目录结构
5.1 领域库
src/
└── Bukit.Notion/
    ├── Bukit.Notion.csproj
    ├── Seed/
    │   ├── NotionSeedSet.cs
    │   ├── NotionSeedRecord.cs
    │   ├── NotionSeedLoader.cs
    │   ├── NotionSeedValidator.cs
    │   └── NotionSeedValidationResult.cs
    │
    ├── Mapping/
    │   ├── NotionDatabaseMap.cs
    │   ├── NotionDatabaseMapEntry.cs
    │   ├── NotionDatabaseMapLoader.cs
    │   ├── NotionDatabaseMapValidator.cs
    │   └── NotionPropertyMapping.cs
    │
    ├── Push/
    │   ├── NotionPushOptions.cs
    │   ├── NotionPushMode.cs
    │   ├── NotionPushService.cs
    │   ├── INotionPushService.cs
    │   ├── NotionPushResult.cs
    │   ├── NotionPushDiagnostic.cs
    │   ├── NotionPushArtifact.cs
    │   └── NotionPushRecordResult.cs
    │
    ├── Client/
    │   ├── INotionClient.cs
    │   ├── NotionHttpClient.cs
    │   ├── NotionRequestOptions.cs
    │   ├── NotionApiResponse.cs
    │   ├── NotionRateLimitPolicy.cs
    │   └── NotionApiException.cs
    │
    ├── Conversion/
    │   ├── NotionPropertyValueMapper.cs
    │   ├── NotionBlockMapper.cs
    │   ├── MarkdownToNotionBlocks.cs
    │   └── NotionTextChunker.cs
    │
    ├── Report/
    │   ├── NotionPushReport.cs
    │   ├── NotionPushReportWriter.cs
    │   └── NotionPushReportSummary.cs
    │
    └── Security/
        ├── NotionSecretMasker.cs
        └── NotionPathGuard.cs
5.2 插件项目
plugins/
└── Bukit.Plugin.Notion/
    ├── Bukit.Plugin.Notion.csproj
    ├── Program.cs
    ├── NotionPluginApp.cs
    ├── NotionPluginManifestProvider.cs
    ├── NotionCommandSpecFactory.cs
    ├── NotionPluginInvoker.cs
    ├── NotionOptionsMapper.cs
    ├── NotionValidateSeedCommandHandler.cs
    ├── NotionValidateDatabaseMapCommandHandler.cs
    ├── NotionPushCommandHandler.cs
    ├── plugin.yaml.template
    ├── README.md
    └── examples/
        └── minimal/
            ├── .bukit/plugins.yaml
            └── plugins/notion/plugin.yaml
5.3 测试项目
tests/
├── Bukit.Notion.Tests/
│   ├── NotionSeedLoaderTests.cs
│   ├── NotionSeedValidatorTests.cs
│   ├── NotionDatabaseMapLoaderTests.cs
│   ├── NotionDatabaseMapValidatorTests.cs
│   ├── NotionPropertyValueMapperTests.cs
│   ├── MarkdownToNotionBlocksTests.cs
│   ├── NotionPushServiceDryRunTests.cs
│   ├── NotionPushServiceCreateTests.cs
│   ├── NotionPushServiceUpsertTests.cs
│   ├── NotionPushServiceReplaceTests.cs
│   └── NotionPushReportWriterTests.cs
│
└── Bukit.Plugin.Notion.Tests/
    ├── NotionPluginHandshakeTests.cs
    ├── NotionPluginManifestTests.cs
    ├── NotionValidateSeedInvokeTests.cs
    ├── NotionValidateDatabaseMapInvokeTests.cs
    ├── NotionPushDryRunInvokeTests.cs
    ├── NotionPushCreateInvokeTests.cs
    ├── NotionPushUpsertInvokeTests.cs
    ├── NotionPushReplaceInvokeTests.cs
    ├── NotionPluginStdoutTests.cs
    └── NotionPluginPermissionTests.cs
6. 插件配置与 Manifest 规范
6.1 .bukit/plugins.yaml 示例
version: 1

plugins:
  notion:
    enabled: true
    source: plugins/notion
    exposeCommands:
      - notion
    permissions:
      fileSystem:
        read:
          - .
        write:
          - ./.bukit/reports/plugin-output/notion
          - ./.bukit/tmp/notion
      network: true
      environment:
        read:
          - NOTION_TOKEN
    timeout:
      handshakeMs: 5000
      manifestMs: 5000
      invokeMs: 600000
    output:
      stdoutMaxBytes: 4194304
      stderrMaxBytes: 4194304
      responseMaxBytes: 4194304
    failMode: strict
    manifestPolicy: static
    allowInCi: false

说明：

allowInCi 默认 false。
CI 中执行真实 push 必须显式打开，并使用测试 workspace/token。
6.2 plugins/notion/plugin.yaml 示例
id: notion
name: Bukit Notion Plugin
version: 0.1.0
protocol: bukit-plugin-v1
kind: process
distribution: self-contained

platforms:
  linux-x64:
    entry: bin/linux-x64/bukit-plugin-notion
    sha256: "<sha256>"
  osx-arm64:
    entry: bin/osx-arm64/bukit-plugin-notion
    sha256: "<sha256>"
  win-x64:
    entry: bin/win-x64/bukit-plugin-notion.exe
    sha256: "<sha256>"

commands:
  - name: notion
    description: Validate and push Bukit handoff seed data to Notion.
    subcommands:
      - name: validate-seed
        arguments:
          - name: seed-dir
            required: true

      - name: validate-database-map
        arguments:
          - name: database-map
            required: true

      - name: push
        options:
          - name: --seed
            type: string
            required: true
          - name: --database-map
            type: string
            required: true
          - name: --token-env
            type: string
            required: false
          - name: --mode
            type: string
            required: true
            allowedValues:
              - create
              - upsert
              - replace
          - name: --dry-run
            type: flag
            required: false
          - name: --report
            type: string
            required: false

requiredPermissions:
  fileSystem:
    read:
      - .
    write:
      - ./.bukit/reports/plugin-output/notion
      - ./.bukit/tmp/notion
  network: true
  environment:
    read:
      - NOTION_TOKEN
7. 数据契约
7.1 Seed 输入目录

由 Import 插件生成：

sites/<site-name>/notion-seed/
├── pages.json
├── navigation.json
├── posts.json
├── companies.json
├── services.json
└── notion-database-map.yaml

后续可以扩展：

sections.json
faqs.json
media.json
components.json

第一阶段只要求支持：

pages
navigation
posts
companies
services
7.2 Seed record 通用字段
{
  "title": "Example",
  "slug": "example",
  "summary": "Short summary",
  "content": "Markdown or plain content",
  "language": "en",
  "published": true,
  "seo_title": "SEO title",
  "seo_description": "SEO description"
}

集合可有额外字段：

posts:
  category
  tags

companies:
  country
  industry

services:
  service_type
  price
7.3 Database map 格式

推荐保留旧名 notion-database-map.yaml，内部同时支持 databaseId 和 dataSourceId：

databases:
  pages:
    title: Pages
    seed: pages.json
    collection: page
    dataSourceId: ""
    databaseId: ""        # legacy alias, optional
    uniqueField: Slug
    properties:
      Title:
        source: title
        type: title
      Slug:
        source: slug
        type: rich_text
      Summary:
        source: summary
        type: rich_text
      Published:
        source: published
        type: checkbox

规则：

dataSourceId 优先。
databaseId 作为 legacy alias。
至少必须存在 dataSourceId 或 databaseId。
uniqueField 必填。
seed 必须存在。
properties 必须非空。
8. 模块 M0：迁移治理与防漂移
任务 M0.1：Notion 插件迁移状态文档

新增：

docs/plugins/notion-plugin-migration-status.md

内容：

Current Phase
Completed Modules
Pending Modules
Explicit Non-goals
Permission Boundary
Command Surface
Test Commands
Boundary Audit
任务 M0.2：Notion 防漂移清单

新增：

docs/plugins/notion-plugin-anti-drift-checklist.md

必须包含：

[ ] Notion push 不回到 Import 插件
[ ] Import 插件仍保持 network=false
[ ] Import 插件仍不读取 NOTION_TOKEN
[ ] Bukit.Plugin.Notion 不引用 Labs
[ ] Bukit.Plugin.Notion 不引用 Bukit.Cli
[ ] Bukit.Plugin.Notion 不引用 Bukit.PluginHost
[ ] Core 不引用 Bukit.Plugin.Notion
[ ] stdout 只输出 JSON
[ ] stderr 只输出日志
[ ] report 不写 token
[ ] .bukit/plugins.yaml 不含 entry
[ ] executable 不放 .bukit
9. M1：Notion plugin skeleton
目标

建立 Notion 插件最小外部进程闭环。

子任务
新增 src/Bukit.Notion。
新增 plugins/Bukit.Plugin.Notion。
新增 tests/Bukit.Notion.Tests。
新增 tests/Bukit.Plugin.Notion.Tests。
Bukit.Plugin.Notion.csproj 设置：
OutputType=Exe
AssemblyName=bukit-plugin-notion
Nullable=enable
引用：
Bukit.Plugin.Abstractions
Bukit.Notion
Bukit.Shared
不引用：
Bukit.PluginHost
Bukit.Cli
Bukit.Plugin.Import
Labs
实现 Program.cs：
stdin 读 JSON
stdout 写 JSON response
stderr 写日志
实现：
NotionPluginApp
NotionPluginManifestProvider
NotionCommandSpecFactory
NotionPluginInvoker
skeleton 支持：
handshake
manifest
invoke unknown command
Done Criteria
dotnet build 成功
handshake response 正确
manifest response 正确
stdout 只输出 JSON
stderr 可输出日志
无 Core/Labs 依赖漂移
10. M2：notion validate-seed
目标

静态验证 Import 生成的 notion-seed/，不访问网络，不读取 token。

命令
bukit notion validate-seed <seed-dir>
领域模型
NotionSeedSet
NotionSeedCollection
NotionSeedRecord
NotionSeedValidationResult
NotionSeedDiagnostic
子任务
seed-dir 必须存在。
seed-dir 必须位于项目根内。
支持读取：
pages.json
navigation.json
posts.json
companies.json
services.json
至少一个 seed 文件存在。
每个 seed 文件必须是 JSON array。
每个 record 必须是 object。
每个 record 必须有 title 或 name。
每个 record 必须能生成 slug。
published 若存在必须是 boolean。
tags 若存在必须是 array 或 string。
允许额外字段。
输出 diagnostics。
返回 artifact：
type seed-validation
path <seed-dir>
不访问网络。
不读取环境变量。
错误码
notion.seedDirNotFound
notion.seedDirOutsideProject
notion.seedNoFiles
notion.seedInvalidJson
notion.seedInvalidRecord
notion.seedMissingTitle
notion.seedInvalidPublished
notion.seedInvalidTags
测试
valid seed passes
missing seed-dir fails
seed-dir symlink outside project fails
no seed files fails
invalid json fails
record without title fails
invalid published fails
tags array passes
tags string passes
11. M3：notion validate-database-map
目标

静态验证 notion-database-map.yaml，不访问网络，不读取 token。

命令
bukit notion validate-database-map <database-map>
子任务
database-map 文件必须存在。
路径必须位于项目根内。
YAML root 必须是 mapping。
必须包含 databases。
每个 entry 必须包含：
seed
collection
uniqueField
每个 entry 必须包含：
dataSourceId 或 databaseId
seed 必须指向 seed-dir 内存在的文件，或者在 standalone validate-map 时只做相对路径格式校验。
properties 必填且必须是非空 mapping。
properties 中必须至少包含一个 title property。
uniqueField 必须映射到 properties 中的同名 property。
properties.*.type 必须是受支持类型。
properties.*.source 必须非空。
支持 legacy databaseId。
输出 diagnostics。
不访问网络。
支持 property types

第一阶段支持：

title
rich_text
checkbox
number
select
multi_select
url
email
phone_number
date

title 与 rich_text property 都按 rich-text object 数组写入。每个 text.content 最多 2000 字符，超过时必须分块；数组最多 100 项，超过 200000 字符的属性值必须在 planning 阶段失败，不得发送无效 API 请求。

后续支持：

relation
rollup
files
people
status
错误码
notion.databaseMapNotFound
notion.databaseMapOutsideProject
notion.databaseMapInvalidYaml
notion.databaseMapMissingDatabases
notion.databaseMapMissingSeed
notion.databaseMapMissingCollection
notion.databaseMapMissingUniqueField
notion.databaseMapMissingDataSource
notion.databaseMapInvalidProperty
测试
valid map passes
missing file fails
invalid yaml fails
missing databases fails
missing dataSourceId/databaseId fails
legacy databaseId passes
unsupported property type fails
12. M4：notion push --dry-run
目标

完整模拟 push，但不调用 Notion API，不读取 token 或允许读取 token但不使用？推荐：dry-run 默认不需要 token。

命令
bukit notion push \
  --seed ./sites/demo/notion-seed \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --mode create \
  --dry-run
子任务
复用 validate-seed。
复用 validate-database-map。
载入 seed records。
载入 database map。
将 collection 绑定到 seed file。
将 record 转换为 planned page operation。
校验每条 record 的 unique field。
按 collection + seedFile + uniqueField + uniqueValue 检测本地重复值；重复时返回 notion.seedDuplicateUniqueValue，exitCode=2。
校验每条 record 的 property mapping。
property mapping 使用严格 JSON 类型：checkbox 只接受 JSON boolean，number 只接受 JSON number；字符串 "true"/"false"/"3" 不隐式转换。
生成 dry-run report。
返回 diagnostics 和 artifacts。
不调用 Notion API。
不要求 NOTION_TOKEN。
不访问 network。
report 中标记：
dryRun: true
plannedCreate
plannedUpdate
plannedUpsert
plannedReplace
输出 artifact
.bukit/reports/plugin-output/notion/notion-push-report.json
测试
dry-run create plans records
dry-run no token succeeds
dry-run invalid seed fails
dry-run invalid map fails
dry-run writes report
dry-run does not call fake Notion client
13. M5：Notion HTTP client
目标

实现真实 Notion API client，但先只给 create/upsert/replace 使用。

接口
public interface INotionClient
{
    Task<NotionQueryResult> QueryDataSourceAsync(
        string dataSourceId,
        NotionQueryRequest request,
        CancellationToken cancellationToken);

    Task<NotionPageResult> CreatePageAsync(
        NotionCreatePageRequest request,
        CancellationToken cancellationToken);

    Task<NotionPageResult> UpdatePagePropertiesAsync(
        string pageId,
        NotionUpdatePageRequest request,
        CancellationToken cancellationToken);

    Task AppendBlockChildrenAsync(
        string blockId,
        IReadOnlyList<NotionBlock> children,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NotionBlockResult>> ListBlockChildrenAsync(
        string blockId,
        CancellationToken cancellationToken);

    Task DeleteBlockAsync(
        string blockId,
        CancellationToken cancellationToken);
}
子任务
使用 HttpClient。
Base URL 默认 https://api.notion.com。
Authorization: Bearer <token>。
Notion-Version 默认配置项，建议初始使用当前官方示例版本 2026-03-11。
Content-Type: application/json。
支持 pagination。
支持 429 rate limit 退避。
支持 400/401/403/404/409/429/500 error mapping。
不在异常中暴露 token。
report 不写 raw response body，除非脱敏。
所有请求可注入 fake client 做测试。
测试
sets auth header
sets notion version header
does not log token
handles 401
handles 429 retry
handles pagination
maps API errors to diagnostics
14. M6：notion push --mode create
目标

只创建新页面，不查重、不更新。

命令
bukit notion push \
  --seed ./sites/demo/notion-seed \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN \
  --mode create
子任务
校验 seed/map。
读取 token env。
token 缺失返回 notion.tokenMissing。
对每个 collection：
找到 dataSourceId/databaseId。
找到 seed file。
转换 properties。
转换 body blocks。
调用 create page。
记录每条记录：
created
failed
skipped
成功返回 artifacts。
失败返回 diagnostics。
partial failure 策略：
默认继续处理其他记录。
最终 exitCode 为 1 或 2 需要定义。
不支持 update。
不支持 replace。
错误码
notion.tokenMissing
notion.createPageFailed
notion.propertyMappingFailed
notion.blockMappingFailed
notion.dataSourceMissing
测试
create creates one page per record
create maps title property
create maps checkbox/rich_text/select
create maps markdown blocks
missing token fails
API 401 returns diagnostic
partial failure records failed count
15. M7：notion push --mode upsert
目标

根据 uniqueField 查找已有页面；存在则更新，不存在则创建。

命令
bukit notion push \
  --seed ./sites/demo/notion-seed \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN \
  --mode upsert
子任务
读取 map entry 的 uniqueField。
从 seed record 中取 unique value。
unique value 支持 string、number、boolean，并按 property type 生成对应 filter。
查询 data source：
filter unique property equals value。
无匹配：
create page。
单个匹配：
update page properties。
append blocks 或按策略处理 content。
多个匹配：
diagnostic notion.upsertMultipleMatches
默认失败并跳过远端写入，不得静默更新第一个匹配页面。
支持 --dry-run 只计划。
report 中记录：
created
updated
skipped
failed
需要明确的内容更新策略

第一阶段建议：

upsert 只更新 page properties，不替换 page blocks。

否则 upsert 和 replace 边界会混乱。

测试
no match creates
one match updates properties
multiple matches skipped
missing unique field fails
dry-run does not call API
16. M8：notion push --mode replace
目标

高风险模式：根据 uniqueField 找到页面，替换内容 blocks。

命令
bukit notion push \
  --seed ./sites/demo/notion-seed \
  --database-map ./sites/demo/notion-seed/notion-database-map.yaml \
  --token-env NOTION_TOKEN \
  --mode replace
安全策略

replace 必须更严格：

1. 必须显式 --mode replace。
2. 建议再加 --confirm-replace。
3. dry-run 默认建议先执行。
4. 不允许多 match。
5. 删除 block 失败时不得 append 新内容。
6. 替换失败必须 report。
子任务
查询 existing page。
无匹配：
create page 或 skip？必须设计。
建议：replace 模式无匹配时 create=false，返回 skipped 或 diagnostic。
单匹配：
update properties。
list block children。
delete supported child blocks。
append new blocks。
append children 必须按每批最多 100 blocks 顺序提交。
删除 block 失败：
标记 replace-failed。
不 append。
append 失败：
标记 append-failed。
replace 不是原子操作；delete/append 失败诊断必须说明 properties 可能已先更新。
report 记录每一步。
错误码
notion.replaceRequiresConfirmation
notion.replaceNoMatch
notion.replaceMultipleMatches
notion.replaceDeleteFailed
notion.replaceAppendFailed
notion.replaceFailed
测试
replace without confirm fails
replace no match skipped
replace one match deletes then appends
delete failure stops append
append failure reports failed
multiple matches fails
17. M9：notion-push-report
目标

所有 push 模式都生成统一报告。

seed、database map、逐记录 planning、token 和 API 失败路径也必须写 JSON/Markdown failure report。
dry-run upsert 使用 operation=upsert 和 plannedUpsert；plannedUpdate 仅统计已确认的实际 update。
每条 record 必须包含 status、remotePageId、errorCode、errorMessage。真实 push 中途失败时，报告必须按顺序包含已完成的远端写入、当前失败记录，以及所有后续未执行且标记为 skipped 的记录；不得遗留 status=planned 的记录。
status 取值：planned、created、updated、replaced、failed、skipped。

JSON 报告
{
  "schema": "bukit.notion.push.report.v1",
  "dryRun": false,
  "mode": "upsert",
  "startedAt": "...",
  "finishedAt": "...",
  "summary": {
    "collections": 5,
    "records": 42,
    "created": 10,
    "updated": 20,
    "replaced": 0,
    "skipped": 8,
    "failed": 4
  },
  "collections": [],
  "records": [],
  "diagnostics": []
}
子任务
设计 NotionPushReport。
设计 NotionPushReportWriter。
写 JSON report。
写 Markdown report。
token 脱敏。
raw API body 默认不写。
record result 中允许写 remote page id。
写入 .bukit/reports/plugin-output/notion/。
invoke response 返回 artifacts。
execution report 由 PluginHost 自己写，不由 Notion 插件写。
测试
report json written
report md written
token masked
summary counts correct
artifacts returned
no raw token
18. M10：Plugin invoke handlers
Handler 划分
NotionValidateSeedCommandHandler
NotionValidateDatabaseMapCommandHandler
NotionPushCommandHandler
通用要求

每个 handler：

1. 只处理自己的 command.path。
2. 参数缺失返回 exitCode=2。
3. 用户输入错误返回 exitCode=2。
4. API / 网络失败返回 exitCode=1。
5. 成功返回 exitCode=0。
6. diagnostics 使用稳定 code。
7. artifacts 使用项目相对路径。
8. stdout 只输出 JSON。
Options mapper
NotionOptionsMapper.MapValidateSeed()
NotionOptionsMapper.MapValidateDatabaseMap()
NotionOptionsMapper.MapPush()
Push 参数校验
--seed required string
--database-map required string
--mode required string create/upsert/replace
--token-env optional string, default NOTION_TOKEN
--dry-run flag
--report optional string
--confirm-replace flag
19. M11：官方 package 与 release gate
新增 official minimal example
plugins/Bukit.Plugin.Notion/examples/minimal/.bukit/plugins.yaml
plugins/Bukit.Plugin.Notion/examples/minimal/plugins/notion/plugin.yaml
新增 build script
scripts/build/notion-plugin-package.sh

支持：

win-x64
linux-x64
osx-arm64
新增 smoke script
scripts/smoke/notion-plugin-package.sh

Smoke 不应真实访问 Notion API，默认只跑：

bukit plugin validate-config
bukit plugin validate-manifest plugins/notion
bukit notion validate-seed ./sample-notion-seed
bukit notion validate-database-map ./sample-notion-seed/notion-database-map.yaml
bukit notion push --seed ./sample-notion-seed --database-map ./sample-notion-seed/notion-database-map.yaml --mode create --dry-run

真实 API push 只在手动 / protected CI 环境执行。

20. M12：测试矩阵
20.1 Unit tests
Bukit.Notion.Tests
  Seed loader
  Seed validator
  Database map loader
  Database map validator
  Property mapper
  Block mapper
  Push service dry-run
  Push service create
  Push service upsert
  Push service replace
  Report writer
20.2 Plugin tests
Bukit.Plugin.Notion.Tests
  handshake
  manifest
  validate-seed invoke
  validate-database-map invoke
  push dry-run invoke
  push create invoke with fake client
  push upsert invoke with fake client
  push replace invoke with fake client
  stdout only JSON
  stderr logs only
20.3 CLI E2E tests
Bukit.Cli.Tests
  plugin config for notion loads
  notion command exposed
  disabled notion returns command disabled
  validate-seed works through CLI
  validate-database-map works through CLI
  push --dry-run works through CLI
  no lock/report for disabled
  lock/report for enabled invoke
20.4 Release gate
dotnet build bukit.slnx -c Release --no-restore -maxcpucount:1 -nodeReuse:false
dotnet test bukit.slnx -c Release --no-restore --no-build -maxcpucount:1 -nodeReuse:false
bash scripts/checks/official-plugin-packages.sh
bash scripts/gates/release.sh Release
git diff --check
21. 错误码规范
validate-seed
notion.seedDirNotFound
notion.seedDirOutsideProject
notion.seedNoFiles
notion.seedInvalidJson
notion.seedInvalidRecord
notion.seedMissingTitle
notion.seedInvalidPublished
notion.seedInvalidTags
validate-database-map
notion.databaseMapNotFound
notion.databaseMapOutsideProject
notion.databaseMapInvalidYaml
notion.databaseMapMissingDatabases
notion.databaseMapMissingSeed
notion.databaseMapMissingCollection
notion.databaseMapMissingUniqueField
notion.databaseMapMissingDataSource
notion.databaseMapInvalidProperty
push
notion.tokenMissing
notion.modeInvalid
notion.dryRunPlanned
notion.propertyMappingFailed
notion.blockMappingFailed
notion.dataSourceMissing
notion.queryFailed
notion.createPageFailed
notion.updatePageFailed
notion.upsertMultipleMatches
notion.seedDuplicateUniqueValue
notion.recordMissingMappedProperty
notion.recordInvalidMappedPropertyType
notion.recordMissingTitlePropertyValue
notion.replaceRequiresConfirmation
notion.replaceNoMatch
notion.replaceMultipleMatches
notion.replaceDeleteFailed
notion.replaceAppendFailed
notion.rateLimited
notion.apiUnauthorized
notion.apiForbidden
notion.apiNotFound
notion.apiConflict
notion.apiFailed
22. PR 路线图
PR-Notion-001：plugin skeleton
新增 Bukit.Notion
新增 Bukit.Plugin.Notion
handshake / manifest / unsupported invoke
official minimal examples
basic tests
PR-Notion-002：validate-seed
NotionSeedLoader
NotionSeedValidator
validate-seed handler
unit/plugin/CLI tests
PR-Notion-003：validate-database-map
NotionDatabaseMapLoader
NotionDatabaseMapValidator
validate-database-map handler
tests
PR-Notion-004：push dry-run
NotionPushService dry-run
property/block planned conversion
dry-run report
tests
PR-Notion-005：Notion HTTP client
INotionClient
NotionHttpClient
auth headers
pagination
rate limit
API error mapping
fake client tests
PR-Notion-006：push create
create mode
create page
append content
report
tests with fake client
PR-Notion-007：push upsert
query by uniqueField
create/update properties
no block replace yet
report
tests
PR-Notion-008：push replace
confirm replace
delete existing blocks
append new blocks
failure semantics
report
tests
PR-Notion-009：package / smoke / docs
multi-rid package
sha256
official-plugin-packages gate
smoke script
README
plugin docs
23. Codex 总控 Prompt
你是 Bukit 项目的 Codex 执行 agent。

目标：
开发 Bukit.Plugin.Notion，作为独立外部进程插件，负责消费 Import 插件生成的 notion-seed handoff artifacts，并将数据推送到 Notion。

强制边界：
1. 不得把 Notion push 放回 Import 插件。
2. Bukit.Plugin.Import 必须保持 network=false，environment.read=[]。
3. Bukit.Plugin.Notion 不得引用 Bukit.Plugin.Import。
4. Bukit.Plugin.Notion 不得引用 Bukit.Cli。
5. Bukit.Plugin.Notion 不得引用 Bukit.PluginHost。
6. Bukit.Plugin.Notion 不得引用 Labs。
7. Core 不得引用 Bukit.Plugin.Notion。
8. Notion token 只能来自 allowlisted environment variable。
9. stdout 只能输出 JSON response。
10. stderr 只能输出日志。
11. report 不得写 token 或 raw secret。
12. executable 不得放入 .bukit。
13. .bukit/plugins.yaml 不得包含 entry。

执行顺序：
PR-Notion-001 skeleton
PR-Notion-002 validate-seed
PR-Notion-003 validate-database-map
PR-Notion-004 push dry-run
PR-Notion-005 Notion HTTP client
PR-Notion-006 push create
PR-Notion-007 push upsert
PR-Notion-008 push replace
PR-Notion-009 package/smoke/docs

每个 PR 必须：
- build 通过
- tests 通过
- git diff --check 通过
- boundary audit 通过
- 不扩大范围
24. 最终结论

采用方案 A 后，Bukit 的数据流应稳定为：

HTML Demo
  ↓
Bukit.Plugin.Import
  ↓
sites/<site>/notion-seed/
  ↓
Bukit.Plugin.Notion
  ↓
Notion API

这条路线能同时满足：

Import 本地导入保持最小权限
Notion push 拥有独立权限边界
未来可独立演进 Notion 多数据库 / upsert / replace
Core 不被外部集成污染
插件体系不发生职责漂移
