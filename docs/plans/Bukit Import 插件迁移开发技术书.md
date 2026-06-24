Bukit Import 插件完整迁移开发技术书

执行对象：Codex / AI Coding Agent
目标插件：plugins/Bukit.Plugin.Import
正式命令：bukit import
插件类型：跨平台外部进程插件
协议版本：bukit-plugin-v1
迁移策略：模块化拆分、分阶段上线、严格防漂移、先本地导入后外部集成
当前前置状态：Core Plugin Mechanism v1 baseline 已完成，可进入 Import 业务迁移阶段

1. 当前基础状态

当前插件底座已经具备完整迁移 Import 的基础能力。

.bukit/plugins.yaml 的 schema 现在只要求顶层 version，并且每个 plugin entry 必须声明 enabled/source/exposeCommands/permissions，其中 permissions 已成为显式安全边界。

plugin.yaml manifest schema 已锁定 protocol=bukit-plugin-v1、kind=process、distribution=self-contained，并支持 command / argument / option / subcommand 结构。

CLI 层已经有：

bukit plugin validate-config
bukit plugin validate-manifest

这两个命令只调用 config / manifest loader，不触发插件进程执行。

路径安全已经进入 realpath / symlink hardening 阶段，source 必须 stay under plugins/，entry 必须 stay inside plugin directory，且 real path 也会被校验。

官方 Import minimal example 已存在，包括 .bukit/plugins.yaml 与 plugins/import/plugin.yaml。示例配置使用 source: plugins/import、exposeCommands: [import]、显式 permissions、manifestPolicy: static。
示例 manifest 已声明 id: import、protocol: bukit-plugin-v1、kind: process、distribution: self-contained。

因此下一阶段重点是：在不破坏插件底座边界的前提下，逐步迁移完整 Import 功能。

2. 总体迁移目标
2.1 最终目标

将 Labs / existing Import 能力迁移为正式外部进程插件：

Labs / existing Import capability
  ↓
Bukit.Importing domain layer
  ↓
Bukit.Plugin.Import external process plugin
  ↓
Core PluginHost
  ↓
bukit import ...

最终 Bukit.Plugin.Import 应提供：

bukit import seed <seed-dir> --output <content-dir> [--force]

bukit import html-demo <demo-dir> --theme <theme-name> [options]

并逐步覆盖完整 Import 功能：

seed 导入
HTML demo 导入
route-map 支持
内容抽取
seed 生成
主题 / 站点骨架生成
资源处理
报告生成
安全扫描
dry-run
strict mode
verify
use
Notion handoff / push-notion 策略
2.2 分阶段目标

Import 完整迁移分为三层：

Layer 1：可发布 Import v1
  seed
  html-demo dry-run
  html-demo local import
  report
  static manifest
  lock/report/artifacts

Layer 2：增强 Import v1.x
  route-map
  content extraction
  seed generation
  theme/site generation
  strict mode
  preserve-html
  security scan

Layer 3：集成 Import v2
  verify
  use
  Notion handoff
  optional push-notion 或 Notion Plugin 联动
3. 强制架构边界
3.1 允许依赖
Bukit.Plugin.Import
  -> Bukit.Plugin.Abstractions
  -> Bukit.Importing
  -> Bukit.Shared
3.2 禁止依赖
Bukit.Plugin.Import -> Bukit.Cli
Bukit.Plugin.Import -> Bukit.PluginHost
Bukit.Plugin.Import -> Bukit.Labs.*
Bukit.Cli -> Bukit.Plugin.Import
Bukit.Engine -> Bukit.Plugin.Import
Bukit.PluginHost -> Bukit.Plugin.Import
3.3 禁止行为
禁止恢复 site.externalPlugins
禁止动态 DLL 插件
禁止 Assembly.LoadFrom
禁止把插件 executable 放入 .bukit/
禁止 .bukit/plugins 作为 source
禁止 shell 拼接执行
禁止 stdout 输出非 JSON 日志
禁止直接调用 Labs Command handler
禁止一次性迁移 seed + html-demo + verify + notion + clone
4. Import 功能模块拆分

完整 Import 功能拆分为 12 个模块。

M0：迁移治理与防漂移
M1：Import 插件命令契约
M2：Seed 导入模块
M3：HTML Demo 输入扫描模块
M4：Route Map 与页面映射模块
M5：内容抽取模块
M6：Seed 生成模块
M7：主题与站点生成模块
M8：资源与 HTML 保留模块
M9：Import 报告与安全扫描模块
M10：dry-run / strict / overwrite 策略模块
M11：verify / use 模块
M12：Notion handoff / push-notion 策略模块
M13：跨平台打包、样例与发布门禁
5. 模块依赖关系
M0 迁移治理
  ↓
M1 命令契约
  ↓
M2 Seed
  ↓
M3 HTML 输入扫描
  ↓
M4 Route Map
  ↓
M5 内容抽取
  ↓
M6 Seed 生成
  ↓
M7 主题/站点生成
  ↓
M8 资源/HTML 保留
  ↓
M9 报告/安全扫描
  ↓
M10 dry-run/strict/overwrite
  ↓
M11 verify/use
  ↓
M12 Notion handoff
  ↓
M13 打包/发布门禁

M11 和 M12 不得提前做。必须在 seed + html-demo local import + report 稳定后进入。

6. M0：迁移治理与防漂移模块
目标

确保整个 Import 迁移过程不破坏 Core Plugin Mechanism，不出现架构漂移。

任务 M0.1：建立 Import 迁移状态文档
文件
docs/plugins/import-plugin-migration-status.md
子任务
记录当前 Import 迁移状态。
列出已完成模块。
列出未完成模块。
列出禁止范围。
每个 PR 更新状态。
记录当前测试命令。
记录当前风险。
验收

文档必须包含：

Current Phase
Completed Modules
Pending Modules
Blocked Items
Explicit Non-goals
Boundary Checklist
任务 M0.2：建立 Import 防漂移检查清单
文件
docs/plugins/import-plugin-anti-drift-checklist.md
子任务
写入禁止依赖：
Bukit.Cli -> Bukit.Plugin.Import
Bukit.Plugin.Import -> Bukit.Labs
写入禁止配置：
site.externalPlugins
.bukit/plugins
entry: in .bukit/plugins.yaml
写入禁止执行：
shell
dynamic DLL
写入每个 PR 必跑 grep 命令。
写入 release gate 命令。
验收

每个 Import PR 必须引用该 checklist。

任务 M0.3：新增 Import 迁移分支规则
子任务
每个 PR 只能迁移一个模块。
每个 PR 必须包含测试。
每个 PR 不得扩大 command surface，除非本 PR 明确是 command contract。
每个 PR 必须更新 manifest 和 runtime manifest。
每个 PR 必须通过 official-plugin-packages.sh。
每个 PR 必须通过 plugin validate-config / validate-manifest。
7. M1：Import 插件命令契约模块
目标

定义完整 Import 命令面，但分阶段暴露。先暴露 seed，后续逐步打开 html-demo。

任务 M1.1：定义最终命令树
目标命令
bukit import seed <seed-dir> --output <content-dir> [--force]

bukit import html-demo <demo-dir> --theme <name> [options]
html-demo 目标 options
--theme <name>
--force
--use
--verify
--dry-run
--strict <fail|warn>
--overwrite
--route-map <file>
--site-path <dir>
--language <lang>
--content-source <markdown|json|yaml|notion>
--build-source <markdown|notion>
--no-extract-content
--no-seed
--no-preserve-html
--no-report
--base-url <url>
暂缓 options
--push-notion
--notion-database-id
--notion-database-map
--create-missing-notion-databases
--notion-parent-page-id
--notion-generated-database-map
--notion-token-env
--notion-report
--no-validate-notion-schema

这些进入 M12，不得提前实现。

任务 M1.2：更新 static plugin.yaml
文件
plugins/Bukit.Plugin.Import/examples/minimal/plugins/import/plugin.yaml
子任务
保留基础字段：
id: import
protocol: bukit-plugin-v1
kind: process
distribution: self-contained
commands 中声明 import。
第一阶段只声明 seed 子命令。
后续 PR 再加入 html-demo。
每次新增 runtime command 必须先更新 static manifest。
requiredPermissions 必须最小化。
seed manifest 示例
commands:
  - name: import
    description: Import content into a Bukit site.
    subcommands:
      - name: seed
        description: Convert generated seed data into markdown content.
        arguments:
          - name: seed-dir
            description: Seed directory.
            required: true
        options:
          - name: --output
            type: string
            description: Output content directory.
            required: true
          - name: --force
            type: flag
            description: Overwrite existing markdown files.
            required: false
任务 M1.3：更新 runtime manifest
文件建议
plugins/Bukit.Plugin.Import/ImportPluginManifestProvider.cs
plugins/Bukit.Plugin.Import/ImportCommandSpecFactory.cs
子任务
新建 ImportCommandSpecFactory。
生成顶层 import command。
第一阶段只生成 seed subcommand。
seed-dir 必须 required。
--output 必须 required。
--force 类型必须是 flag。
requiredPermissions 与 static manifest 一致。
不声明 html-demo。
不声明 Notion 相关 options。
保证 runtime manifest 不超出 static manifest。
测试
ImportPluginManifestTests

覆盖：

import command exists
seed subcommand exists
seed-dir required
--output required
--force flag
html-demo absent
notion options absent
network false
environment.read empty
8. M2：Seed 导入模块
目标

迁移 bukit import seed，作为第一个真实业务子命令。

任务 M2.1：确定 seed 输入格式
子任务
审计当前 Bukit.Importing seed 相关逻辑。
明确 seed-dir 内支持的文件类型：
JSON
YAML
Markdown
其他已有格式
明确 seed record schema。
明确输出 markdown front matter 格式。
明确文件名生成规则。
明确 overwrite / force 语义。
明确错误码。
写入文档。
交付
docs/plugins/import-seed-contract.md
任务 M2.2：设计领域模型
文件建议
src/Bukit.Importing/Seed/ImportSeedOptions.cs
src/Bukit.Importing/Seed/ImportSeedResult.cs
src/Bukit.Importing/Seed/ImportSeedDiagnostic.cs
src/Bukit.Importing/Seed/ImportSeedArtifact.cs
子任务
定义 ImportSeedOptions：
ProjectRoot
SeedDirectory
OutputDirectory
Force
定义 ImportSeedResult：
Success
ExitCode
Diagnostics
Artifacts
定义 ImportSeedDiagnostic：
Code
Severity
Message
Path
定义 ImportSeedArtifact：
Type
Path
Description
使用 sealed record。
不引用 CLI。
不引用 PluginHost。
不引用 Labs。
任务 M2.3：实现 IImportSeedService
文件建议
src/Bukit.Importing/Seed/IImportSeedService.cs
src/Bukit.Importing/Seed/ImportSeedService.cs
子任务
校验 SeedDirectory 存在。
校验 OutputDirectory 非空。
校验输出路径不能逃逸项目根。
如果输出目录存在且非空：
Force=false 返回业务失败。
Force=true 允许写入或覆盖。
读取 seed files。
转换为 markdown content。
写入 output。
返回 artifacts。
返回 diagnostics。
不在 domain service 中写 Console。
所有 artifact path 使用项目相对路径。
所有路径输出统一使用 /。
错误码
import.seedDirNotFound
import.seedDirInvalid
import.missingOutput
import.outputOutsideProject
import.outputAlreadyExists
import.seedRecordInvalid
import.seedWriteFailed
任务 M2.4：Seed domain 单元测试
测试项目
tests/Bukit.Importing.Tests
测试覆盖
missing seed-dir -> failure
missing output -> failure
output outside project -> failure
output exists force=false -> failure
output exists force=true -> success
valid seed -> writes markdown
success returns artifacts
artifact paths are relative
diagnostics contain stable codes
任务 M2.5：Plugin invoke mapper
文件建议
plugins/Bukit.Plugin.Import/ImportOptionsMapper.cs
子任务
读取 request.Command.Path。
要求 path 为 ["import", "seed"]。
读取 Arguments[0] 作为 seed-dir。
读取 --output。
读取 --force。
--force 必须是 JSON bool。
--output 必须是 JSON string。
参数缺失返回 diagnostics。
类型错误返回 diagnostics。
不抛 host exception。
任务 M2.6：Plugin seed handler
文件建议
plugins/Bukit.Plugin.Import/ImportSeedCommandHandler.cs
子任务
调用 mapper。
mapper 失败时返回：
success=false
exitCode=2
diagnostics
调用 IImportSeedService。
domain success 映射为 plugin success。
domain failure 映射为 plugin business failure。
domain exception 映射为：
success=false
exitCode=1
diagnostic import.seedImportFailed
artifacts 映射为 PluginArtifact。
diagnostics 映射为 PluginDiagnostic。
stdout 只输出 JSON。
任务 M2.7：Plugin invoke 测试
测试项目
tests/Bukit.Plugin.Import.Tests
覆盖
invoke import seed success
missing seed-dir
missing --output
--force true
--force absent
wrong --force type
wrong command path
domain business failure
domain exception
stdout only JSON
任务 M2.8：CLI E2E 测试
测试项目
tests/Bukit.Cli.Tests
覆盖
bukit import seed ./seed --output ./content --force
exit 0
content generated
plugins.lock.yaml written
execution report written
responseSummary.success true
artifacts present
disabled import no lock/report
bad permissions fail before invoke
validate-config OK
validate-manifest OK
9. M3：HTML Demo 输入扫描模块
目标

迁移 bukit import html-demo <demo-dir> 的输入扫描，不生成最终主题。

任务 M3.1：定义 HTML demo 输入结构
子任务
<demo-dir> 必须存在。
支持 index.html。
支持多页面 HTML。
支持 assets 目录。
支持 CSS / JS / images。
支持 route-map 可选文件。
明确 unsupported input。
写文档。
任务 M3.2：实现 HtmlDemoScanner
文件建议
src/Bukit.Importing/HtmlDemo/HtmlDemoScanner.cs
src/Bukit.Importing/HtmlDemo/HtmlDemoScanResult.cs
子任务
扫描 HTML files。
识别 entry page。
识别 asset references。
识别 local links。
返回 page candidates。
返回 diagnostics。
不写文件。
不生成主题。
不调用 plugin API。
测试
single index.html
multiple pages
missing index
empty directory
broken asset reference
relative links
任务 M3.3：Plugin dry-run 接入
命令
bukit import html-demo <demo-dir> --theme <name> --dry-run
子任务
static manifest 加入 html-demo。
runtime manifest 加入 html-demo。
mapper 解析 demo-dir、--theme、--dry-run。
调用 scanner。
不写主题 / content。
返回 scan report artifact 或 diagnostics。
execution report 记录 responseSummary。
不实现 full import。
10. M4：Route Map 与页面映射模块
目标

支持 --route-map <file>，让 HTML 页面映射到 Bukit 路由和内容模型。

任务 M4.1：定义 route-map schema
子任务
确定 YAML 或 JSON。
定义字段：
source
route
title
type
layout
contentTarget
定义 conflict policy。
定义 invalid route diagnostics。
写 schema。
写文档。
任务 M4.2：实现 RouteMapLoader
文件建议
src/Bukit.Importing/RouteMap/ImportRouteMap.cs
src/Bukit.Importing/RouteMap/ImportRouteMapLoader.cs
子任务
读取 route-map。
校验 source path。
校验 route。
校验重复 route。
校验 source 是否存在。
返回 normalized route map。
输出 diagnostics。
任务 M4.3：Route mapping 集成到 html-demo
子任务
scanner result + route map 合并。
无 route-map 时生成默认 route。
route-map 优先。
route conflict 返回 diagnostic。
strict=fail 时失败。
strict=warn 时继续。
11. M5：内容抽取模块
目标

从 HTML demo 中抽取可维护内容，生成 Bukit content / seed。

任务 M5.1：定义内容抽取模型
子任务
定义 page content model。
定义 front matter。
定义 section model。
定义 metadata extraction。
定义 fallback text extraction。
定义 hardcoded content detection。
任务 M5.2：实现 HtmlContentExtractor
文件建议
src/Bukit.Importing/ContentExtraction/HtmlContentExtractor.cs
子任务
解析 HTML。
抽取 title。
抽取 main content。
抽取 sections。
保留 metadata。
标记不可抽取内容。
输出 diagnostics。
不写文件。
任务 M5.3：写入 Markdown content
文件建议
src/Bukit.Importing/ContentExtraction/MarkdownContentWriter.cs
子任务
生成 front matter。
写 markdown body。
路径安全。
force / overwrite 策略。
artifacts 返回。
测试多语言路径。
测试 slug。
12. M6：Seed 生成模块
目标

从 HTML demo 生成 JSON / YAML seed，服务后续 Notion 或内容生成。

任务 M6.1：定义 seed output 格式
子任务
确定 JSON seed 文件名。
确定 YAML seed 文件名。
定义 page / section / asset 字段。
定义 version。
写 schema。
任务 M6.2：实现 SeedGenerator
文件建议
src/Bukit.Importing/SeedGeneration/ImportSeedGenerator.cs
子任务
输入 extracted content。
输出 seed records。
支持 --no-seed。
支持 content-source。
返回 artifacts。
测试 deterministic output。
13. M7：主题与站点生成模块
目标

将 HTML demo 导入为 Bukit theme / site skeleton。

任务 M7.1：定义生成目标
输出目录
themes/<theme-name>/
sites/<site-name>/
content/
data/
docs/research/
子任务
定义 theme name validator。
定义 site name / path。
定义 overwrite / force。
定义 existing theme conflict。
定义 artifacts。
任务 M7.2：Theme generator
文件建议
src/Bukit.Importing/ThemeGeneration/ImportThemeGenerator.cs
子任务
生成 theme directory。
生成 templates。
迁移 CSS。
迁移 JS。
迁移 assets。
写 theme metadata。
不调用 ThemeCommand。
不调用 Labs。
任务 M7.3：Site config generator
文件建议
src/Bukit.Importing/SiteGeneration/ImportSiteGenerator.cs
子任务
生成 site.yaml。
配置 theme。
配置 content source。
配置 build source。
支持 --site-path。
支持 --language。
原子写入。
artifacts 返回。
14. M8：资源与 HTML 保留模块
目标

处理 CSS / JS / images / original HTML preservation。

任务 M8.1：Asset collector
子任务
收集 local assets。
拒绝路径穿越。
保持目录结构。
复制到 theme assets。
返回 missing assets diagnostics。
不访问网络，除非后续显式模块允许。
任务 M8.2：Preserve original HTML
子任务
默认 preserve original HTML。
--no-preserve-html 关闭。
输出到 docs/research 或 plugin-output。
路径安全。
artifacts 返回。
15. M9：Import 报告与安全扫描模块
目标

生成 import report、diagnostics、安全扫描结果。

任务 M9.1：Import report model
子任务
定义 report model。
包含 pages/assets/content/theme/site。
包含 diagnostics summary。
包含 artifacts。
包含 security findings。
任务 M9.2：Report writer
子任务
写 markdown report。
写 JSON report。
默认输出到 docs/research 或 .bukit/reports/plugin-output/import。
不写 .bukit/reports/plugin-executions，该目录由 Core PluginHost 写。
artifacts 返回。
任务 M9.3：Security scanner
子任务
检测 inline script。
检测 remote script。
检测 external URL。
检测 hardcoded secrets patterns。
检测 unsupported forms。
strict=fail 时失败。
strict=warn 时报告 warning。
16. M10：dry-run / strict / overwrite 策略模块
任务 M10.1：dry-run
子任务
所有服务支持 dry-run。
dry-run 不写目标文件。
dry-run 可以写 PluginHost execution report。
dry-run 返回计划 artifacts。
测试无写入。
任务 M10.2：strict mode
子任务
--strict fail 遇 error 失败。
--strict warn 将 error 降级为 warning 的规则必须明确。
默认策略定义。
diagnostics 稳定。
任务 M10.3：overwrite / force
子任务
统一 --force 和 --overwrite 语义。
定义冲突检测。
未授权覆盖时失败。
force=true 时允许覆盖。
所有写入原子化或可恢复。
17. M11：verify / use 模块
目标

处理 --verify 与 --use，但不得调用 Labs command。

任务 M11.1：--use
子任务
定义 use 语义：导入后将 site.yaml 当前 theme 指向新 theme。
不调用 ThemeCommand。
在 Bukit.Importing 中实现 site.yaml 修改服务。
原子写入。
失败可恢复。
artifacts / diagnostics。
测试 site.yaml 修改。
任务 M11.2：--verify
设计选择

不得直接调用 Labs BuildCommand / DoctorCommand。

可选方案：

方案 A：插件内部使用稳定 domain API 做轻量 verify。
方案 B：未来 Core Host Action 提供 core.build / core.doctor。
当前推荐

第一阶段做轻量 verify：

检查生成文件存在
检查 site.yaml 可解析
检查 theme templates 存在
检查 content files 存在
不执行完整 build

完整 build verify 放到后续 Core Host Action。

18. M12：Notion handoff / push-notion 策略模块
关键设计说明

当前插件权限模型是 plugin-level，不是 command-level。若 Import 插件 runtime manifest 声明 network: true 和 NOTION_TOKEN，则启用 Import 插件的用户即使只用本地导入，也必须授予网络和 token 权限。

这违反最小权限原则。

推荐策略
Phase M12-A：Import 只生成 Notion seed / handoff artifact

允许：

生成 notion seed
生成 database map candidate
生成 report

不允许：

直接 push Notion
读取 NOTION_TOKEN
访问网络
Phase M12-B：独立 Notion Plugin

将真正的 push Notion 放入：

Bukit.Plugin.Notion

或后续协议支持 command-level permissions 后再回到 Import。

任务 M12.1：Notion handoff artifacts
子任务
生成 notion seed JSON。
生成 mapping report。
返回 artifacts。
不读取环境变量。
不访问网络。
任务 M12.2：push-notion 设计 ADR
文件
docs/plugins/import-notion-push-design.md
内容
为什么当前不直接在 Import v1 实现 push-notion
command-level permissions 缺口
Notion Plugin 方案
Host Action 方案
未来迁移路径
19. M13：跨平台打包与发布门禁
任务 M13.1：真实 package build
子任务
生成：
win-x64
linux-x64
osx-arm64
生成 plugins/import/bin/<rid>/bukit-plugin-import。
计算 sha256。
写入 package plugin.yaml。
不把 executable 放入 .bukit。
package layout 符合：
plugins/import/plugin.yaml
plugins/import/bin/<rid>/...
任务 M13.2：release gate
必跑
dotnet build bukit.slnx -c Release --no-restore -maxcpucount:1 -nodeReuse:false
dotnet test bukit.slnx -c Release --no-restore --no-build -maxcpucount:1 -nodeReuse:false
bash scripts/checks/official-plugin-packages.sh
bash scripts/gates/release.sh Release
git diff --check
任务 M13.3：跨平台 smoke
覆盖
Windows x64
Linux x64
macOS arm64
命令
bukit plugin validate-config
bukit plugin validate-manifest plugins/import
bukit import seed ./seed --output ./content --force
bukit import html-demo ./demo --theme demo --dry-run

按阶段逐步打开。

20. 推荐 PR 路线图
PR-Import-001：Import seed command contract
更新 static plugin.yaml
更新 runtime manifest
新增 manifest tests
不实现业务
PR-Import-002：Import seed domain service
新增或封装 Bukit.Importing Seed service
新增 domain tests
不改 plugin invoke
PR-Import-003：Import seed invoke
实现 ImportOptionsMapper
实现 ImportSeedCommandHandler
实现 ImportPluginInvoker 分发
新增 plugin tests
PR-Import-004：Import seed CLI E2E
真实 fixture
lock/report
disabled behavior
validate-config/manifest
permissions failure
PR-Import-005：HTML demo dry-run
HtmlDemoScanner
html-demo manifest
dry-run no write
scan report
PR-Import-006：HTML demo local import
content extraction
theme/site generation
assets local copy
report
PR-Import-007：route-map + strict
route-map loader
route conflict
strict fail/warn
PR-Import-008：report/security scan
import report
security findings
hardcoded content diagnostics
PR-Import-009：use / light verify
site.yaml theme use
light verify
不调用 Labs
PR-Import-010：Notion handoff design
notion seed artifacts
push-notion ADR
不直接访问 network/token
PR-Import-011：package build + cross-platform smoke
multi-rid package
sha256
release gate
docs
21. 每个 PR 的强制验收清单
[ ] 不引用 Labs
[ ] Core 不引用 Bukit.Plugin.Import
[ ] stdout 只输出 JSON
[ ] stderr 只输出日志
[ ] .bukit/plugins.yaml 不含 entry
[ ] plugin.yaml 不含 runtime-only
[ ] permissions 显式声明
[ ] schema / loader / manifest 均通过
[ ] command.path 正确
[ ] lock 正确
[ ] report 正确
[ ] responseSummary 正确
[ ] secrets 不落盘
[ ] disabled command 行为正确
[ ] validate-config / validate-manifest 通过
[ ] git diff --check 通过
[ ] build/test 通过
22. Codex 总控 Prompt
你是 Bukit 项目的 Codex 执行 agent。

目标：
完整迁移 Bukit Import 功能为外部进程插件，但必须按模块逐步执行。

当前只允许执行当前 PR 指定模块，不得扩大范围。

强制边界：
1. Bukit.Plugin.Import 不得引用 Labs。
2. Bukit.Plugin.Import 不得引用 Bukit.Cli 或 Bukit.PluginHost。
3. Bukit.Cli 不得引用 Bukit.Plugin.Import。
4. 不得恢复 site.externalPlugins。
5. 不得使用 Assembly.LoadFrom 或动态 DLL 插件。
6. 不得从 .bukit 执行插件。
7. stdout 只能输出 JSON。
8. 日志只能写 stderr。
9. .bukit/plugins.yaml 不得包含 entry。
10. plugin.yaml 必须 kind=process，protocol=bukit-plugin-v1，distribution=self-contained。
11. permissions 必须显式声明。
12. manifestPolicy 必须 static，runtime manifest 不得超出 static manifest。

迁移顺序：
1. seed command contract
2. seed domain service
3. seed invoke
4. seed CLI E2E
5. html-demo dry-run
6. html-demo local import
7. route-map
8. content extraction
9. theme/site generation
10. report/security scan
11. use/light verify
12. Notion handoff design
13. package build/cross-platform smoke

禁止：
- 不得一次性迁移整个 Import。
- 不得提前迁移 Clone。
- 不得提前实现 push-notion。
- 不得调用 Labs ThemeCommand / NotionCommand / BuildCommand。

每次完成后必须输出：
1. 修改文件列表
2. 完成模块
3. 未完成模块
4. 测试命令和结果
5. 边界审计结果
23. 最终结论

整个 Import 功能可以迁移，但必须按模块拆分推进。

推荐立即开始：

PR-Import-001：Import seed command contract

而不是直接实现完整 html-demo。

完整迁移完成的最终状态应是：

bukit import seed
bukit import html-demo
route-map
content extraction
seed generation
theme/site generation
report/security scan
dry-run/strict/overwrite
use/light verify
Notion handoff
cross-platform package
release gate

但所有阶段都必须保持：

Core 不依赖插件实现
Plugin 不依赖 Labs
插件外部进程化
配置/manifest/schema 一致
安全边界不漂移