# Bukit Core Hardening P0-P1 Spec

> 来源：`.trae/documents/Bukit_Core_Hardening_Codex_Fix_Plan.md` 第 2-11 节  
> 审计结果：`.trae/specs/core-hardening-p0-p1/` 基于实际代码逐项对账

## Why
当前 Bukit 增量构建仅比较 TemplateHash/MetadataHash/RouteHash/ContentHash 四个维度，修改 site.title 或 theme.params 等配置后，页面可能被错误跳过。static 文件复制、assets 复制、媒体复制等路径未统一经过安全输出校验。dotfile 默认发布。配置解析失败静默忽略。这些正确性、安全性、可诊断性缺陷需要通过 P0-P1 修复补齐，使 Bukit 从"能构建"升级为"每次构建都可预测、可验证"。

## What Changes
- **P0**: 新增 `RenderDependencyHash` 加入增量构建 skip 判断（正确性）
- **P0**: 无论是否配置 `staticTemplate` 都检测 static HTML 与 generated page 冲突（正确性）
- **P0**: 统一所有输出写入/复制/删除经过 `SafeOutputFileSystem`（安全性）
- **P0**: 默认禁止发布敏感 dotfile（.env/.git/.DS_Store 等），保留 .well-known（安全性）
- **P1**: `ValidateInternalUrl` 增加路径遍历段检查（安全性）
- **P1**: **BREAKING** top-level `outputPath` 报错，引导迁移到 `route.outputPath`（可诊断性）
- **P1**: `collections.yaml` YAML 语法错误时抛出 `ConfigException` 而非静默忽略（可诊断性）
- **P1**: 配置 bool/int/long/double 解析失败时抛出 `ConfigException` 而非返回 null（可诊断性）
- **P1**: draft 字段统一 bool coercion（`true`/`"TRUE"`/`"yes"`/`1`/`"on"`）（一致性）
- **P1**: `--jobs` 贯穿 `RenderSpecialListsAsync`/`BuildPageInfosAsync`（一致性）

## Impact
- Affected specs: 无
- Affected code:
  - `src/Bukit.Engine/Incremental/BuildManifest.cs` — 新增 RenderDependencyHash 字段
  - `src/Bukit.Engine/Incremental/IncrementalBuildEngine.cs` — 新增 RenderDependencyHasher
  - `src/Bukit.Engine/PageRenderDispatcher.cs` — 增量跳过加入 RenderDependencyHash
  - `src/Bukit.Engine/SiteEngine.cs` — static HTML 冲突检查解耦、RenderDependencyHash 计算
  - `src/Bukit.Engine/DirectoryCopy.cs` — 接入 SafeOutputFileSystem、dotfile 默认拒绝
  - `src/Bukit.Engine/StaticFileService.cs` — 非 HTML 复制接入 SafeOutputFileSystem
  - `src/Bukit.Engine/AssetPipeline.cs` — 接入 SafeOutputFileSystem
  - `src/Bukit.Engine/Output/SafeOutputFileSystem.cs` — 扩展 CopyFileAsync/DeleteFileAsync 覆盖
  - `src/Bukit.Routing/RouteSecurityValidator.cs` — ValidateInternalUrl 增加段校验
  - `src/Bukit.Routing/RouteGenerator.cs` — top-level outputPath 报错
  - `src/Bukit.Config/ConfigLoader.cs` — collections.yaml 异常传播、严格 bool/int 解析
  - `src/Bukit.Engine/ContentPipeline.cs` — draft 统一 bool coercion
  - `src/Bukit.Shared/` — 新增 ValueCoercion 工具类

---

## ADDED Requirements

### Requirement: RenderDependencyHash for incremental builds
系统 SHALL 在增量构建中计算 `RenderDependencyHash`，包含影响最终 HTML 输出的配置维度，并加入页面和列表页的 skip 判断。

#### Scenario: site.title 变更后页面重新渲染
- **GIVEN** 前次构建已完成，manifest 中有 `RenderDependencyHash: "abc123"`
- **WHEN** 修改 `site.title` 而不修改任何内容文件或模板
- **AND** 执行 `bukit build`
- **THEN** 新计算的 `RenderDependencyHash` 与 manifest 中的值不同
- **AND** 页面被重新渲染
- **AND** 构建报告显示 `render_dependency_changed` 或等效原因

#### Scenario: theme.params 变更后页面重新渲染
- **GIVEN** 前次构建已完成
- **WHEN** 修改 `theme.params.someOption` 而不修改任何内容文件
- **AND** 执行 `bukit build`
- **THEN** 页面被重新渲染

#### Scenario: 旧 manifest 升级不错误跳过
- **GIVEN** 旧版本 manifest 中某条目的 `RenderDependencyHash` 为 null 或空
- **WHEN** 执行增量构建
- **THEN** 该条目不跳过，被重新渲染
- **AND** 构建不因 manifest 字段缺失而崩溃

#### Scenario: RenderDependencyHash 内容维度
- **WHEN** 计算 `RenderDependencyHash`
- **THEN** 其输入至少包含：`site.title/description/baseUrl/language/analytics`、`site.seo.*`、`theme.params`、`theme.shortcodes`、`theme.components`、`build.listPageContentMode`、collections 摘要、taxonomy 摘要、插件启用状态、externalPlugins 配置摘要、siteModel.Data 摘要、siteModel.Modules 摘要

#### Scenario: 序列化确定性
- **WHEN** 计算 `RenderDependencyHash`
- **THEN** 字典按 key 排序后序列化
- **AND** 相同配置多次构建产生相同的 hash 值

---

### Requirement: Static HTML conflict detection without staticTemplate
系统 SHALL 无论是否配置 `theme.staticTemplate`，都检测 static HTML 文件的输出目标与 generated/list/derived 页面的输出路径冲突。

#### Scenario: 无 staticTemplate 时 static HTML 与 content page 冲突
- **GIVEN** `theme.staticTemplate` 未配置
- **AND** `content/posts/a.md` 生成路由 `/blog/a/` → 输出 `blog/a/index.html`
- **AND** `static/blog/a/index.html` 存在
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败，抛出路由冲突异常
- **AND** 错误信息包含两个冲突来源与输出路径

#### Scenario: 有 staticTemplate 时 static HTML 与 list page 冲突
- **GIVEN** `theme.staticTemplate: "pages/static.html"` 已配置
- **AND** collection `posts` 生成列表页 `/blog/` → 输出 `blog/index.html`
- **AND** `static/blog/index.html` 存在
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败，抛出路由冲突异常

#### Scenario: static 非 HTML 文件覆盖 generated page 输出路径
- **GIVEN** `static/assets/data.json` 被复制到 `assets/data.json`
- **AND** 某个 generated page 也输出到 `assets/data.json`
- **WHEN** 执行 `bukit build`
- **THEN** 构建失败

---

### Requirement: Unified Safe Output FileSystem
系统 SHALL 确保所有输出写入、复制、删除操作均经过 `SafeOutputFileSystem` 或等效的安全校验，禁止路径逃逸输出目录。

#### Scenario: 非 HTML static 文件复制经过安全校验
- **GIVEN** `staticDir` 下存在非 `.html` 文件
- **WHEN** `StaticFileService.RenderStaticFiles` 复制该文件
- **THEN** 目标路径经过 `SafeOutputFileSystem.GetSafeFullPath` 或等效校验

#### Scenario: DirectoryCopy 经过安全校验
- **WHEN** `DirectoryCopy.Sync` / `SyncFiles` / `SyncFilesRecursive` 写入文件
- **THEN** 每个目标路径经过输出根目录逃逸检查

#### Scenario: AssetPipeline 复制经过安全校验
- **WHEN** `AssetPipeline` 复制 static/assets/media 文件
- **THEN** 所有写入操作使用 `SafeOutputFileSystem` 或 `FileWriter`

#### Scenario: 路径穿越被拒绝
- **WHEN** 任何输出操作的目标路径包含 `..` 穿越段或绝对路径
- **THEN** 操作被拒绝，抛出安全异常

---

### Requirement: Default dotfile deny list
系统 SHALL 默认不发布敏感 dotfile（.env/.git 等），可通过配置显式允许，`.well-known` 默认允许。

#### Scenario: .env 默认不发布
- **GIVEN** `static/.env` 存在
- **WHEN** 执行 `bukit build`
- **THEN** `.env` 不出现于输出目录
- **AND** 构建日志提示已跳过

#### Scenario: .well-known 默认允许
- **GIVEN** `static/.well-known/security.txt` 存在
- **WHEN** 执行 `bukit build`
- **THEN** `.well-known/security.txt` 正常输出

#### Scenario: 显式启用 dotfile 发布
- **GIVEN** 配置 `build.publishDotFiles: true`
- **WHEN** 执行 `bukit build`
- **THEN** `.env` / `.git/**` 等 dotfile 可正常发布

#### Scenario: 默认 deny list 覆盖项
- **WHEN** 应用默认 dotfile deny list
- **THEN** 至少包含：`.env`、`.env.*`、`.git/`、`.github/`、`.svn/`、`.hg/`、`.DS_Store`、`Thumbs.db`、`*.pem`、`*.key`、`*.pfx`、`*.p12`、`.npmrc`、`.yarnrc`

---

### Requirement: URL path segment validation in ValidateInternalUrl
系统 SHALL 在 `ValidateInternalUrl` 中增加 URL 路径段的遍历检查，拒绝 `.`/`..`/编码后的 `..`/反斜杠/编码斜杠。

#### Scenario: 拒绝 .. 段
- **WHEN** 验证 URL `/../admin/`
- **THEN** 校验失败

#### Scenario: 拒绝编码后的 .. 段
- **WHEN** 验证 URL `/%2e%2e/private/` 或 `/%2E%2E/private/`
- **THEN** 校验失败

#### Scenario: 拒绝反斜杠
- **WHEN** 验证 URL `/a\b`
- **THEN** 校验失败

#### Scenario: 正常中文 slug 通过
- **WHEN** 验证 URL `/博客/文章标题/`
- **THEN** 校验通过

---

## MODIFIED Requirements

### Requirement: Top-level outputPath is deprecated
**原行为**：top-level `outputPath` 在部分路由覆盖时条件性忽略，不报错。  
**新行为**：top-level `outputPath` 在 Front Matter 顶层出现时，构建失败并给出迁移指引。

#### Scenario: top-level outputPath 报错
- **GIVEN** Markdown Front Matter 包含 `outputPath: custom/index.html`
- **WHEN** 执行 `bukit build`
- **THEN** 抛出 `ConfigException`
- **AND** 错误信息包含 "deprecated" 及 `route.outputPath` 迁移指引

#### Scenario: route.outputPath 正常工作
- **GIVEN** Front Matter 包含 `route.outputPath: custom/index.html`
- **WHEN** 执行 `bukit build`
- **THEN** 输出路径使用 `custom/index.html`

---

### Requirement: collections.yaml parse failure is not silent
**原行为**：`TryReadCollectionsFile` 捕获 YAML 异常后静默返回 null。  
**新行为**：YAML 语法错误或结构错误时抛出 `ConfigException`。

#### Scenario: collections.yaml 语法错误抛出
- **GIVEN** `collections.yaml` 存在但包含非法 YAML
- **WHEN** 执行 `bukit build`
- **THEN** 抛出 `ConfigException`，信息包含文件路径

#### Scenario: collections.yaml 不存在正常回退
- **GIVEN** `collections.yaml` 不存在
- **WHEN** 执行 `bukit build`
- **THEN** 正常回退，不抛出异常

---

### Requirement: Config bool/int/long/double strict parsing
**原行为**：`GetOptionalBool/Int/Long/Double` 解析失败返回 null。  
**新行为**：新增 strict 变体，解析失败抛出 `ConfigException` 包含配置路径、期望类型、实际值。

#### Scenario: 非法 bool 抛出
- **GIVEN** 配置 `clean: fasle`（拼写错误）
- **WHEN** 解析
- **THEN** 抛出 `ConfigException`，信息包含 "expected boolean"

#### Scenario: 非法 int 抛出
- **GIVEN** 配置 `pageSize: ten`
- **WHEN** 解析
- **THEN** 抛出 `ConfigException`，信息包含 "expected integer"

#### Scenario: 合法 yes/no 解析
- **WHEN** 解析 `enabled: yes` 或 `enabled: no`
- **THEN** 正确返回 boolean 值

---

### Requirement: Draft field unified bool coercion
**原行为**：仅识别 `true`(bool)、`"true"`、`"True"`。  
**新行为**：新增 `ValueCoercion` 工具类，统一识别 truthy/falsy 值。draft 过滤使用统一工具。

#### Scenario: "TRUE" 被识别为 draft
- **GIVEN** Front Matter 包含 `draft: "TRUE"`
- **WHEN** 执行 `bukit build`
- **THEN** 该页面不参与构建

#### Scenario: "yes" 被识别为 draft
- **GIVEN** Front Matter 包含 `draft: yes`
- **WHEN** 执行 `bukit build`
- **THEN** 该页面不参与构建

#### Scenario: "0" 不被识别为 draft
- **GIVEN** Front Matter 包含 `draft: 0`
- **WHEN** 执行 `bukit build`
- **THEN** 该页面正常参与构建

---

### Requirement: --jobs concurrency through all render stages
**原行为**：`--jobs` 仅影响 `RenderPagesAsync`，`RenderSpecialListsAsync` 使用调用方线程。  
**新行为**：`--jobs` 贯穿 `RenderSpecialListsAsync` 和 `BuildPageInfosAsync`。

#### Scenario: --jobs 1 单并发渲染列表页
- **GIVEN** `--jobs 1`
- **WHEN** 执行构建
- **THEN** 列表页面渲染使用单并发

#### Scenario: --jobs 0 或负值回退到 Environment.ProcessorCount
- **WHEN** `--jobs` 为 0 或负数
- **THEN** 使用 `Environment.ProcessorCount`
