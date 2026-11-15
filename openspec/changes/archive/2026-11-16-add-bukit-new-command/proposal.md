# Add `bukit new` for a buildable starter site

## Why

Bukit Core 目前没有生成初始网站的命令。`guide/user/01-quick-start.md`
要求用户手写 `site.yaml`、内容目录和主题；官方 StarterTheme 只以资源形式嵌在
CLI 里，没有一个受测的“资源 → 可构建站点”的出口。

BukitJalil 因此维护 `templates/starter/` 副本（25 个文件，其中 17 个来自
StarterTheme，8 个是站点级 glue），用 `scripts/make-starter.sh` 从 Bukit tag
生成，并用逐文件 SHA-256 测试防止手改。每次升级 Bukit 都要重新生成、逐字节
核对，且 starter 与内置 Bukit 版本可能漂移。

Labs 里已有一个 `InitCommand` 原型，但它没有接线，配置生成仍是旧契约
（`archive` 对象、`site.url: https://example.com`、硬编码 `zh-CN`、没有
`html.escape`），不能直接提升。`HeaderPartial.html` 里的
`{{-- bukit:brand --}}` 也不是 Scriban 注释，原始资源渲染会抛错。

## What Changes

- 新增 Core 命令 `bukit new <dir> [--title <text>] [--language <tag>]`。
- 命令只生成一个规范的 Markdown starter：`site.yaml`、`content/`、`data/`、
  `themes/starter/`、`.gitignore`。
- 目标目录必须不存在或为空；目标路径先规范化尾分隔符再检查断链/重解析点；
  不覆盖、不 `git init`、不联网、不构建。
- `site.yaml` 从嵌入的 `StarterSite/SiteYaml.yaml` 模板加载，用
  `YamlStream` 设置标题/语言/brand 后保存；不写 `site.url`，设置
  `content.media.onFailure: keep`。
- 删除 StarterTheme 的 `{{-- bukit:brand --}}` 占位符（它不是注释），并修复
  `site.title` / `site.description` / `site.params.*` 的 HTML 转义缺口。
- scaffold 代码放在 `Bukit.Cli` internal，资源由 `Bukit.Cli.csproj` 嵌入；
  Path A 不修改 `Bukit.Cli.Shared`，不新增 public API。
- 错误模型：用户输入/目标错误抛 `CommandArgumentException`，由 Program 映射
  为退出码 2；资源缺失和 IO 错误走全局处理，退出码 1。
- 更新 CLI spec/descriptor、Core 命令白名单、CLI 文档、quick start、
  CHANGELOG、OpenSpec。
- BukitJalil 的迁移是后续独立变更：升级内置 Bukit 后删除 starter 副本和同步
  脚本；本变更不修改 BukitJalil。

## Impact

- 受影响代码：`Bukit.Cli`（spec/descriptor/NewCommand/StarterSiteScaffold/
  资源）、`Bukit.Config`（只读语言校验辅助，如采用）、测试与文档。
- 公共契约：Core CLI 命令面新增 `new`；架构测试白名单需要显式更新。生成的
  starter 结构成为事实契约，后续变更需要兼容或迁移说明。
- 兼容性：不改变现有命令、构建、渲染、配置读取；已有站点不受影响。
- 跨仓影响：BukitJalil 可在包含 `new` 的 Bukit 版本发布后删除
  `templates/starter/`、`scripts/make-starter.sh`、`starter.source.json` 和
  哈希测试；`guide/skills` 同步不在此范围。

## Non-Goals

- 不支持 `--notion`、多数据库、content/data 角色编排；Notion 源由调用方在
  生成后写入。
- 不实现 `bukit init`、多模板向导、主题注册表或远程主题。
- 不执行 `git init`、提交、构建、部署或网络请求。
- 不引入反射式 YAML serializer；不把 scaffold 变成 public API。
- 不修改 BukitJalil 的站点创建流程、权限闸门或测试。
- 不解决 `guide/skills` 的同步负担。
