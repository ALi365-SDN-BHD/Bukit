# Bukit 项目深度代码审查报告

验证基线：基于当前仓库代码审查，并执行 `dotnet test bukit.slnx --no-restore`，结果通过：`2028 passed, 0 failed`。

## 1. 项目整体理解

Bukit 是一个 .NET 静态站点生成器，核心能力包括 Markdown/Notion 内容加载、Scriban 模板渲染、主题/资源处理、SEO/GEO、路由、插件、增量构建和 CLI 工作流。

当前架构大体分层清晰：

- CLI 层：`src/Bukit.Cli/Program.cs`、`src/Bukit.Cli/Commands/BuildCommand.cs`
- Config 层：`src/Bukit.Config/ConfigLoader.cs`、`src/Bukit.Config/ConfigValidator.cs`
- Engine 层：`src/Bukit.Engine/SiteEngine.cs`、`src/Bukit.Engine/BuildPlanner.cs`
- Content 层：`src/Bukit.Content/Markdown/MarkdownFolderProvider.cs`、`src/Bukit.Content/Notion/NotionContentProvider.cs`
- Rendering 层：`src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- Theme 层：`src/Bukit.Theme/ThemeManifestLoader.cs`、`src/Bukit.Theme/ThemeComponentRegistry.cs`
- Routing 层：`src/Bukit.Routing/RouteGenerator.cs`、`src/Bukit.Engine/RouteInventoryValidator.cs`
- Plugin 层：`src/Bukit.Engine/Plugins/PluginRunner.cs`、`src/Bukit.Engine/Plugins/PluginRegistry.cs`

整体方向是健康的，但核心管线仍有几个长期演进会痛的点：主题来源解析和主题目录解析分裂、Scriban 组件渲染含全局静态状态、主题 manifest 错误被吞、模板/组件错误有静默 HTML 注释化倾向。

## 2. 核心流程分析

CLI 执行流程：`Program.cs` 先解析命令，优先走 `BukitCliSpecs` 绑定式命令，再 fallback 到旧 `ArgReader` switch。`build` 最终进入 `BuildCommand.RunAsync`，解析配置、设置 auto-summary 环境变量、创建 `SiteEngine`。

构建流程：`SiteEngine.BuildCoreAsync` 先用 `BuildPlanner.Plan` 应用 overrides、验证配置、准备输出目录；然后 `ContentPipeline.ExecuteAsync` 加载内容和本地化图片；再按语言分支进入单语言或多语言构建。

页面生成流程：`BuildVariantAsync` 中依次处理主题 bootstrap、数据模块、路由、插件派生页面、SEO index、页面渲染、资源复制、after-build 插件和报告输出，核心调用集中在 `SiteEngine.cs` 的 `BuildVariantAsync`。

模板渲染流程：`PageRenderDispatcher` 默认并行渲染页面，调用 `ScribanTemplateRendererAdapter`，底层 `ScribanTemplateRenderer` 带模板缓存和 layout 递归限制。单页、列表页和 static HTML wrapped page 走不同入口，但最终都写入输出目录。

资源处理流程：`AssetPipeline` 负责 static、assets、theme tokens、media copy。路径写入大多经过 `FileWriter` 或 `DirectoryCopy` 的 outputRoot 检查，但主题 manifest 中的 component/section template 路径未统一走 `FileTemplateLoader` 安全边界。

## 3. 关键问题总览

| 编号 | 问题类型 | 严重等级 | 涉及模块 | 问题描述 | 修复优先级 |
|---|---|---|---|---|---|
| BKT-01 | 并发 Bug | 高 | Rendering | 组件渲染使用静态可变字段，默认并行渲染下会串页/串主题上下文 | P0 |
| BKT-02 | 配置/架构缺陷 | 高 | Config/Theme | `ThemeConfig.Source` 存在但 `ConfigLoader` 未读取，且 BuildPlanner 与 ThemeBootstrapper 对远程主题根目录理解分裂 | P0 |
| BKT-03 | 静默失败 | 高 | Theme | `ThemeManifestLoader.Load` catch 后返回 null，主题语法/结构错误会降级为“无 V2 主题” | P0 |
| BKT-04 | 安全 | 中高 | Theme/Rendering | section/component template path 可由 `theme.yaml` 拼接，未强制限制在 layouts 根内 | P1 |
| BKT-05 | 安全/XSS | 中高 | Rendering | `image.img` 直接拼接 `src`、`alt`、`class`，缺少属性转义和 URL 协议约束 | P1 |
| BKT-06 | 架构 | 中 | Engine | `BuildPipeline` 只是 executor 包装，没有阶段模型，核心编排仍集中在 `SiteEngine` | P2 |
| BKT-07 | 性能 | 中 | Rendering | section/theme component 每次 `File.ReadAllText + Template.Parse`，绕过主模板缓存 | P2 |
| BKT-08 | 诊断 | 中 | Theme/Rendering | render_section/component 错误大量转成 HTML 注释，严格模式难以失败退出 | P1 |
| BKT-09 | 工程质量 | 中 | CLI | 新旧 CLI 解析路径并存，`dev` 仍手写解析，命令行为一致性风险较高 | P2 |
| BKT-10 | 可测试性 | 中 | Rendering | 缺少默认并行渲染下 component/theme component 隔离测试 | P1 |

## 4. 深度问题分析

### BKT-01：组件渲染全局静态状态导致并发串扰

问题位置：

- `src/Bukit.Rendering/Scriban/ComponentFunctions.cs:11`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:106`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:438`

问题原因：`ComponentFunctions.Components`、`TemplateLoader`、`ParentGlobals`、`ThemeParentGlobals` 等是 static。页面渲染却在 `PageRenderDispatcher.RenderPagesAsync` 通过 `Parallel.ForEachAsync` 并行执行，默认并行度来自 `SiteEngine` 的 `Environment.ProcessorCount`。

触发场景：多个页面同时调用 `comp.render` 或 `render_component`，页面 A 设置了 `ParentGlobals` 后，页面 B 覆盖该 static，页面 A 后续 component 读取到页面 B 的上下文。

影响范围：输出 HTML 内容错乱、组件拿到错误 `page`/`site` 数据、偶发且难复现。严重，因为默认构建就可能触发。

修复建议：移除 `ComponentFunctions` static 状态，把 component render 封装成实例对象或闭包函数。`RenderComponentFunction` 应持有 registry、loader、parentGlobals，而不是读取 static。

推荐修改方案：

- 新增 `ComponentRenderer` 实例类，保存当前 renderer 的 loader、globals、组件定义。
- `ScribanTemplateRenderer.RenderTemplate` 中注册闭包函数，而不是写入 static。
- `RenderComponentFunction` 构造函数注入依赖。
- 新增默认并行度或显式高并行度测试，证明页面上下文不串扰。

### BKT-02：theme.source 配置链断裂

问题位置：

- `src/Bukit.Config/AppConfig.cs:282`
- `src/Bukit.Config/ConfigLoader.cs:118`
- `src/Bukit.Engine/ThemeBootstrapper.cs:31`
- `src/Bukit.Engine/BuildPathUtils.cs:99`

问题原因：`ThemeConfig.Source` 模型存在，但 `ConfigLoader` 创建 `ThemeConfig` 时没有读取 `source`。同时 `ThemeBootstrapper` 支持 `config.Theme.Source`，但 `BuildPathUtils` 始终按 `root/themes/<name>` 解析 layouts/assets/static。

触发场景：用户在 `site.yaml` 配置远程主题 source，期望 clone 到 `.cache/themes` 后构建。

影响范围：从 YAML 无法真正启用 `theme.source`；即使程序化设置了 `Theme.Source`，主题 manifest bootstrap 和实际渲染/资源目录也可能不一致。

修复建议：把 theme source resolution 前移到 `BuildPlanner`，产出统一 `ThemeResolvedPaths`，供 renderer、assets、static、tokens、manifest bootstrap 共用。`ConfigLoader` 必须读取 `Source = GetOptionalString(themeNode, "source")`，并补 validator/test。

推荐修改方案：

- 在 `ConfigLoader` 读取 `theme.source`。
- 在 `ConfigValidator` 校验 source 格式和 theme name/source 组合。
- 引入 `ResolvedThemePaths`，让 `BuildPlanner` 和 `ThemeBootstrapper` 共享。
- 增加 fake git/theme source 测试，确认 manifest、layouts、assets、static 都来自同一主题根。

### BKT-03：主题 manifest 错误被静默吞掉

问题位置：

- `src/Bukit.Theme/ThemeManifestLoader.cs:12`
- `src/Bukit.Theme/ThemeManifestLoader.cs:24`
- `src/Bukit.Engine/ThemeBootstrapper.cs:48`

问题原因：所有异常 catch 后返回 null。`ThemeBootstrapper` 看到 null 就继续按非 V2 主题构建。

触发场景：`theme.yaml` YAML 语法错误、字段结构错误、继承主题 manifest 损坏。

影响范围：用户以为启用了组件化主题，实际 registry/schema/section plugin 全部关闭；错误只表现为模板变量缺失或页面不完整。

修复建议：区分“文件不存在”和“文件存在但无效”。建议 API 改为 `TryLoad` 加 diagnostics，或 `LoadRequired` 抛 `ThemeManifestException`。

推荐修改方案：

- `ThemeManifestLoader.Load` 保持 missing file 返回 null。
- 如果 `theme.yaml` 存在但 parse 失败，抛 `ThemeManifestException`。
- `ThemeCommand`/`doctor` 捕获后输出诊断。
- 构建路径默认失败，避免静默降级。

### BKT-04：theme.yaml 模板路径缺少边界约束

问题位置：

- `src/Bukit.Theme/ThemeComponentRegistry.cs:37`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:392`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:589`

问题原因：section/component template 直接 `Path.Combine(themeRoot, "layouts", def.Template)`，再 `File.ReadAllText`。没有类似 `FileTemplateLoader` 的 root-boundary check。

触发场景：恶意或错误的 `theme.yaml` 写入 `../outside.html`、绝对路径，或 parent/child theme 混合时路径解析错位。

影响范围：本地文件读取越界，或构建结果不可预测。若远程主题能力恢复，这会从“本地可信配置风险”升级为供应链风险。

修复建议：新增 `ThemeTemplatePathResolver`，统一校验 section/component/page template 必须相对且落在允许 layouts roots 中。

推荐修改方案：

- `ThemeComponentRegistry.ResolveSectionTemplate` 不直接 `Path.Combine`，改用 resolver。
- 禁止 rooted path、`..` segment、encoded traversal。
- 增加安全测试覆盖 section、component、variant template。

### BKT-05：`image.img` helper 可生成不安全 HTML 属性

问题位置：

- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:658`

问题原因：`src`、`alt`、`className` 未 HTML attribute encode；`src` 也没有限制协议。Notion rich text 大体有 encode，Markdown 禁用了 raw HTML，但模板 helper 是另一个输入通道。

触发场景：模板把 Notion/Markdown 字段传入 `image.img`，字段值含引号、事件属性片段、`javascript:` 等。

影响范围：生成 XSS 或无效 HTML。

修复建议：新增 `HtmlAttributeEncode`，限制 `src` 为 `/`、`http://`、`https://` 或可配置白名单；对非法 URL 返回空或 warning。

推荐修改方案：

- 使用 `WebUtility.HtmlEncode` 或专用 attribute encoder。
- `srcset` 每个 URL 同步校验。
- 增加 `javascript:`、引号、尖括号、事件属性注入测试。

### BKT-06：`BuildPipeline` 名义化，核心仍是巨型编排

问题位置：

- `src/Bukit.Engine/BuildPipeline.cs`
- `src/Bukit.Engine/SiteEngine.cs`

问题原因：`BuildPipeline` 只是对 `BuildCoreAsync` 的委托包装，真正阶段状态、错误上下文、缓存策略都仍散落在 `SiteEngine.BuildVariantAsync`。

触发场景：继续扩展插件系统、AI/GEO、增量构建、主题系统时，需要在 `SiteEngine` 内继续堆逻辑。

影响范围：测试困难、阶段复用困难、错误诊断无法标准化。

修复建议：把阶段固化为 `IBuildStage` 或更轻量的 `BuildStageRunner`，至少先拆出 `ThemeResolutionStage`、`RoutingStage`、`RenderingStage`、`AssetStage`、`PluginStage`。

推荐修改方案：

- 先做无行为变化的阶段提取。
- 每个 stage 返回 stage-specific result。
- 保留当前集成测试，增加 stage 单元测试。

## 5. 隐藏 Bug 清单

- 配置缺失：`theme.source`、`theme.componentValidation` 在模型中存在，但 loader 未读取，导致 YAML 配置无效。
- 文件路径异常：theme section/component template 可越过 layouts root，需要进一步补安全测试。
- 空数据：内容为空时 list/index 可构建，但若模板依赖 `site.modules.*`，relaxed access 会静默空值。
- 模板变量缺失：Scriban relaxed access 让拼写错误更难发现；应由 `doctor` 或 lint 增强。
- 数据源字段不一致：schema 默认 warn，真实生产可能继续输出不完整页面；建议关键 collection 默认 strict 或按 CLI flag 提升。
- 输出目录异常：clean marker 机制较好，但 `--output` 指向已有非 Bukit 目录会被拒绝，错误可再补“如何修复”提示。
- 多页面生成冲突：route inventory 已覆盖 content/derived/list/static HTML 冲突，这是当前亮点。
- 主题资源缺失：`theme.yaml` parse error 被当作 no manifest，是最危险的静默失败。

## 6. 架构改进建议

核心构建引擎：把 `SiteEngine.BuildVariantAsync` 拆成阶段结果对象，明确每阶段输入输出和诊断。

数据源抽象：`ContentPipeline` 当前比较稳；下一步可让 source load、media localization、schema validation 变成独立 stages，利于 Notion/Markdown/AI source 扩展。

主题系统抽象：统一 `ThemeResolutionResult`，包含 manifest、layouts/assets/static/tokens roots、parent roots、source metadata。不要让 `BuildPlanner` 和 `ThemeBootstrapper` 各算一遍主题路径。

插件系统预留：process plugin 已有 timeout/output cap/env allowlist，方向不错；建议补 plugin capability 声明，尤其区分 `emit-outputs`、`derive-pages`、`network`。

渲染管线标准化：把 page/list/static wrapped page 都纳入同一 render context，统一错误模式、缓存和 write lock。

错误诊断系统：为 `ThemeManifestInvalid`、`TemplatePathEscapesRoot`、`ComponentRenderFailed` 等定义稳定 diagnostic code，CLI、doctor、report 共享。

增量构建能力：现有 manifest 清理 stale page/assets/media/plugin outputs 比较完整；下一步重点是 template/component/section cache 的一致性和远程 theme source hash。

## 7. 修复优先级路线图

### P0：必须立即修复

- 移除 `ComponentFunctions` static 状态，补并行渲染回归测试。
- 修复 `theme.source`/`componentValidation` loader 缺失，并统一远程主题目录解析。
- `ThemeManifestLoader` 对存在但无效的 `theme.yaml` 抛明确错误。

### P1：核心稳定性增强

- section/component template path root-boundary 校验。
- `image.img` 属性转义和 URL 协议过滤。
- render_section/component 支持 strict fail mode，不再全部 HTML 注释化。
- 补默认并行度下 component/theme component 测试。

### P2：架构优化

- 拆分 `SiteEngine.BuildVariantAsync` 阶段对象。
- 统一 CLI 新旧解析路径，尤其 `dev`。
- section/component 模板接入缓存，避免重复 parse。

### P3：长期能力建设

- 稳定 diagnostic code 体系。
- 主题 source lock 与供应链校验。
- 插件 capability/sandbox 模型。
- 更细粒度增量构建和 stage-level metrics。

## 8. 可交给 Codex 执行的修复任务清单

### 任务 1：修复组件并发串扰

修改目标：移除 `ComponentFunctions` static 上下文。

涉及文件：

- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- `src/Bukit.Rendering/Scriban/ComponentFunctions.cs`
- `tests/Bukit.Rendering.Tests`

修改步骤：

1. 把 component render 改为实例/闭包。
2. `RenderComponentFunction` 注入依赖。
3. 新增并行 50 页渲染测试。

验收标准：`dotnet test tests/Bukit.Rendering.Tests` 通过，新增测试可证明不同页面 component 不串值。

### 任务 2：修复 theme.source 配置链

修改目标：YAML 可读取 `theme.source`，远程主题路径被统一用于 manifest/layout/assets/static。

涉及文件：

- `src/Bukit.Config/ConfigLoader.cs`
- `src/Bukit.Engine/BuildPlanner.cs`
- `src/Bukit.Engine/ThemeBootstrapper.cs`
- `src/Bukit.Engine/BuildPathUtils.cs`

修改步骤：

1. `ConfigLoader` 读取 `theme.source`。
2. `BuildPlanner` 统一解析远程主题。
3. `ThemeBootstrapper` 使用已解析主题根。
4. 添加 fake git/theme source 测试。

验收标准：新增 fake git/theme source 测试，构建实际使用 `.cache/themes/...` 下的模板和资源。

### 任务 3：主题 manifest 错误显性化

修改目标：存在但无效的 `theme.yaml` 不再静默降级。

涉及文件：

- `src/Bukit.Theme/ThemeManifestLoader.cs`
- `src/Bukit.Engine/ThemeBootstrapper.cs`
- `src/Bukit.Cli/Commands/ThemeCommand.cs`

修改步骤：

1. 新增 `ThemeManifestException`。
2. `theme.yaml` 存在但解析失败时抛错。
3. CLI/doctor 输出明确诊断。

验收标准：invalid YAML 构建失败，missing theme.yaml 仍允许 legacy theme。

### 任务 4：主题模板路径安全边界

修改目标：section/component/page template 均不能越过 layouts root。

涉及文件：

- `src/Bukit.Theme/ThemeComponentRegistry.cs`
- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`

修改步骤：

1. 新增主题模板路径 resolver。
2. 禁止绝对路径和 traversal。
3. section、component、variant 都走同一校验。

验收标准：`../secret.html`、绝对路径、encoded traversal 均失败并给出明确错误。

### 任务 5：修复 image helper XSS 风险

修改目标：`image.img` 输出安全属性。

涉及文件：

- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- 可选新增 `src/Bukit.Rendering/Scriban/HtmlHelper.cs`

修改步骤：

1. 对 `src`、`alt`、`className` 做 HTML attribute encode。
2. 限制 `src` 协议。
3. 同步处理 `srcset`。

验收标准：含引号、尖括号、`javascript:` 的 src/alt/class 测试通过，不生成可执行属性。

### 任务 6：渲染错误模式标准化

修改目标：`componentValidation=strict` 或未来 `render.failMode=strict` 下 component/section 错误失败构建。

涉及文件：

- `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs`
- `src/Bukit.Config/ConfigLoader.cs`
- `src/Bukit.Config/ConfigValidator.cs`

修改步骤：

1. 定义 render/component fail mode。
2. warn 模式保留 HTML 注释。
3. strict 模式抛 `RenderException`。

验收标准：warn 模式保留注释，strict 模式抛 `RenderException`。
