# Bukit Core 8 个新问题专项复核、根因分析与受控修复方案

> 日期：2026-07-17
>
> 基线：`main@4103959c9f7ee1b8dfe8db7e34340f4495e7a9ce`
>
> 来源：[Bukit Core 全面审计](./bukit-core-comprehensive-audit-2026-07-15.zh-CN.md)
>
> 状态：修复前设计与验收基线；本文未修改任何 Core 代码，也不表示问题已经关闭。

## 1. 任务边界与结论

本文从全面审计中提取 F-01～F-08，重新对照当前源码、测试、配置、文档契约和相关历史逐项复核，并为后续修复规定最小变更边界、跨模块影响、回归风险、测试先行步骤、问题复审和代码审计要求。

本轮只新增本文档：

- 不修改 `src/Bukit-Core/`、`tests/`、`scripts/`、配置 schema、插件协议或持久化格式；
- 不把方案描述当作修复结果，不把已有测试通过当作 8 个问题已关闭；
- 不进入 `guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/`；
- 后续必须把 8 项作为 8 个独立父任务逐项实现、验证和审计，不允许一次性大改。

复核结论如下：

| ID | 严重度 | 当前判定 | 根因类型 | 推荐处置 |
|---|---:|---|---|---|
| F-01 | P1 | 已确认 Bug | 破坏性操作入口策略分叉 | 无配置 `--dir` 也统一进入现有安全 cleaner |
| F-02 | P1 | 已确认安全 Bug | 不可信内容进入 HTML 解释型 sink | 全面移除动态结果的 `innerHTML`，以 DOM 文本节点高亮 |
| F-03 | P1 | 已确认竞争结构；后果高可信 | 并行任务没有统一目标所有权 | 写入前声明目标；跨类别冲突失败，类别内继承覆盖保留 |
| F-04 | P2 | 已确认安全/策略 Bug | 多套递归枚举器绕过 reparse-point 策略 | 建立统一默认不跟随枚举器并替换发布链 walker |
| F-05 | P2 | 已确认正确性 Bug | 全局缓存生命周期长于文件内容生命周期 | manifest 使用内容指纹缓存；移除无依赖版本的分析缓存 |
| F-06 | P2 | 已确认契约 Bug | 配置值在调用链中丢失 | 显式传播 cap 到单语言、列表和 merged writer |
| F-07 | P2 | 已确认并发契约 Bug | 限流单位是文档而不是下载 | 每次 rewrite 建立共享 download-level gate |
| F-08 | P2 | 已确认可观测性 Bug | 没有 build 诊断收集器和 public output inventory | 保持 v1 schema，填充真实 build 计数与 public 文件清单 |

### 1.1 对原审计结论的三点细化

1. **F-03 不能只把四个 task 改成顺序执行。** 顺序只能消除同一次写入竞争，不能解决 static、assets、media 的增量 manifest 同时拥有同一路径；后续 stale cleanup 仍可能删除另一来源刚写入的文件。正确修复必须先解决目标所有权。
2. **F-04 不应借修复之机扩大 `build.followSymlinks=true` 的能力。** 当前文档承诺的是“supported copy paths”。本次只统一默认 `false` 时的安全行为；显式 follow 仍局限于已有 `DirectoryCopy.Sync` 路径。
3. **F-08 不应直接把 publish/SEO 的 warning 总数复制到 build report。** `guide/dev/observability.md:19-20` 明确区分 build health、SEO、publish 和 security。应统计实际 build 诊断事件，详细分类仍由各专项报告承担。

## 2. 实施顺序与依赖

推荐顺序不是机械的 F-01～F-08，而是：

1. F-01：先关闭破坏仓库的入口；
2. F-02：关闭访客侧 DOM XSS；
3. F-04：提供统一安全枚举基础；
4. F-03：复用 F-04 枚举规则建立可靠目标声明；
5. F-07：修正资源上限；
6. F-05：修正长进程缓存一致性；
7. F-06：兑现 search 配置契约；
8. F-08：复用 F-04 的安全枚举生成最终 public inventory。

```text
F-01 ──独立
F-02 ──独立
F-04 ──> F-03
  └────> F-08
F-07 ──独立
F-05 ──独立
F-06 ──独立
```

任何一项的 targeted gate 或审计未通过时，必须停在该项，不得开始下一项。

## 3. 全局防漂移规则

每个问题开始前必须记录：

```bash
git status --short --branch
git diff --stat
git diff --name-only
```

每个问题只允许修改该节列出的文件。新增文件必须在该问题的允许清单中说明用途。以下行为一律禁止：

- 顺手重命名、格式化或拆分无关类；
- 借安全修复扩大 public API、配置字段、插件协议或报告 schema；
- 把另一个 finding 的修复捆绑进当前 diff；
- 用 catch-all、忽略异常、降低验证标准或删除测试换取通过；
- 使用 backup/reference 目录作为源码、文档或门禁依据；
- 未经用户明确要求运行 full/release、`test-all`、`smoke-all` 或整个 `.slnx` 门禁。

每项修复必须按以下固定流程执行：

1. 先加入最小失败测试，确认失败原因正是当前 finding；
2. 只实现一个根因修复，不并行尝试多个假设；
3. 运行该节列出的窄测试；
4. 运行 `bash scripts/checks/post-change-targeted.sh -- <本项全部变更路径>`；
5. 对高风险 diff 做一次独立、只读代码审计；
6. 按该节“问题复审”重新验证原触发条件和负向场景；
7. 若审计要求改动，回到第 2 步，重跑窄测试和 targeted gate；
8. 只有证据全部通过后，才把该 finding 标为“已关闭”。

如果同一问题连续三次不同修复尝试仍未解决，应停止补丁叠加，重新讨论架构，而不是进行第四次猜测性修改。

---

## 4. F-01：`clean --dir` 可删除 `.git`

### 4.1 复核证据

- `CleanCommand.cs:29-37` 只要求目标位于当前目录内；“.git 位于当前目录内”因此被判定为安全。
- `CleanCommand.cs:40-50` 的 config/site 分支使用 `OutputDirectoryCleaner.CleanIfExists`。
- `CleanCommand.cs:52-55` 的无配置 `--dir` 分支直接 `Directory.Delete(..., recursive: true)`。
- `OutputDirectoryCleaner.cs:20-45` 已拒绝 root、home、项目根、`.git` 和无 marker 的非空目录。
- `CleanCommandTests.cs:97-109` 只覆盖 cwd 逃逸和绝对外部路径，没有 cwd 内敏感目录。
- `git blame` 显示安全 cleaner 是后续加到 config/site 分支，原始 `--dir` 删除分支未一起迁移；这是入口收口不完整，不是 cleaner 本身失效。

### 4.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | 无配置分支绕过 `OutputDirectoryCleaner`，直接递归删除。 |
| 设计原因 | “路径在 cwd 内”被错误等同于“路径是 Bukit 输出目录”。位置约束不能证明目录所有权。 |
| 演化原因 | 同一命令存在 configured output 与 raw `--dir` 两条策略，旧问题只修复了一条。 |
| 测试漏检 | 测试只验证 traversal，不验证 cwd 内的高价值目录和 marker 所有权。 |

### 4.3 最小修复方案

唯一推荐方案：无论 output 来自 config/site、`--dir` 还是默认 `dist`，都调用：

```csharp
OutputDirectoryCleaner.CleanIfExists(rootDir, outputDir);
```

并沿用现有 `ConfigException -> diagnostic -> exit 2` 行为。`.cache` 和 `.bukit` 仍是固定名称的受控清理，不在本项改造为任意目录 cleaner。

不采用以下方案：

- **只 blacklist `.git`**：仍可删除 cwd 内其他用户目录；
- **增加 `--force`**：扩大破坏性公共契约，且本任务没有该产品需求；
- **重写 `OutputDirectoryCleaner`**：现有实现已经覆盖本 finding 所需策略，会扩大 diff。

### 4.4 允许修改范围

- `src/Bukit-Core/Bukit.Cli/Commands/CleanCommand.cs`
- `tests/Bukit.Cli.Tests/CleanCommandTests.cs`
- `guide/user/12-cli-reference.md`：仅在需要说明“非空 `--dir` 必须有 output marker”时修改

默认不修改 `OutputDirectoryCleaner.cs`；若测试证明现有诊断无法用于 `--dir`，只能做诊断文字的最小兼容调整，不能改变 guard 集合。

### 4.5 测试先行步骤

在 `CleanCommandTests` 使用现有 `CurrentDirectoryScope`，并把涉及 cwd 的测试放入 `CWD` collection，避免全局 cwd 并行污染：

1. `RunAsync_WithDir_RefusesGitDirectoryAndPreservesSentinel`：先失败，确认 `.git/sentinel` 未删除；
2. `RunAsync_WithDir_RefusesProjectRoot`：`--dir .` 返回 2；
3. `RunAsync_WithDir_RefusesUnmarkedNonEmptyDirectory`：保留用户文件；
4. `RunAsync_WithDir_CleansMarkedOutputDirectory`：合法 dist 仍成功；
5. `RunAsync_WithDir_CleansEmptyDirectory`：保持 cleaner 现有空目录兼容行为；
6. 保留已有 config/site、path traversal、outside cwd 测试全部通过。

窄测试：

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter 'FullyQualifiedName~CleanCommandTests'
bash scripts/security/security-regression.sh Release
```

### 4.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| Build/recovery clean | 已经使用同一 cleaner；不得改变其行为。 |
| 旧脚本清理自定义非空目录 | 会从“直接删除”变为“无 marker 拒绝”，这是有意的安全收紧；文档需说明迁移。 |
| `.cache`、`.bukit` | 固定目录清理保持原样，避免把本项扩为缓存体系重构。 |
| 并行测试 | cwd 是进程全局状态，必须使用现有串行 collection。 |
| symlink 目录 | 不在本项处理，避免与 F-04 混杂。 |

### 4.7 问题复审与代码审计

- 在 `/tmp` 创建假的 `.git`，运行修复后的 CLI，确认返回非零且 sentinel 存在；严禁对真实仓库 `.git` 做破坏性复现。
- 审计所有 `CleanCommand` 分支，确认不存在第二个 `Directory.Delete(outputDir, recursive: true)`。
- 审计错误路径，确认拒绝后不会继续删除 `.cache`/`.bukit` 或打印误导性的成功消息。
- 审计 diff 不包含 F-04 的 symlink 变更、CLI 新选项或其他命令改动。

关闭标准：上述 5 个新测试、原有 CleanCommand 测试、安全回归和 targeted gate 全部通过，隔离复现由“删除”变为“拒绝且保留”。

---

## 5. F-02：默认 search UI 内容驱动 DOM XSS

### 5.1 复核证据

- `SearchIndexPlugin.cs:27-31` 的 `highlight` 返回带 `<mark>` 的 HTML 字符串。
- `SearchIndexPlugin.cs:47-53` 把 `it.title`、`it.snippet` 拼进 `d.innerHTML`。
- `SearchIndexPlugin.cs:129-154` 把配置的 placeholder 直接放入 HTML attribute。
- `AppConfig.cs:493-500` 表明 `site.search.ui` 默认是 `default`，因此不是边缘 opt-in 路径。
- starter theme 的 `SearchTemplate.html` 使用 `textContent`，仓库内已有安全模式。
- JSON 编码只保证 JSON 语法安全；`fetch().json()` 还原出的字符串进入 `innerHTML` 后仍会被浏览器解释为元素和事件属性。

### 5.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | 内容字段进入 `innerHTML`，高亮实现依赖拼接 HTML。 |
| 设计原因 | 把“高亮需要 `<mark>`”误解为“整个结果需要 HTML 字符串”。 |
| 信任边界原因 | title/snippet 来自 Markdown、Notion、导入或多作者内容，不能视为模板作者级可信输入。 |
| 测试漏检 | 测试验证索引字段和 script stripping，却没有验证浏览器 sink。 |

### 5.3 最小修复方案

重写默认 UI 的结果构造：

- 清空容器使用 `replaceChildren()`；
- title/snippet 使用 `createTextNode`；
- 命中的片段使用 `document.createElement('mark')`，其 `textContent` 设置为命中文字；
- 空结果节点也用 `createElement` + `textContent`；
- placeholder 使用 `System.Text.Encodings.Web.HtmlEncoder.Default.Encode`；
- 生成的默认 UI JavaScript 中不再出现 `.innerHTML`。

目标 helper 形态：

```javascript
function appendHighlighted(parent, text, query) {
  // 只把 query 用于定位；所有原文通过 Text 节点或 mark.textContent 写入。
}
```

不采用“先 HTML encode 再拼 `innerHTML`”：它仍保留危险 sink，后续字段很容易漏编码。也不引入新的浏览器运行时依赖或第三方 sanitizer；本项可以通过不解释 HTML 从根上消除风险。

### 5.4 允许修改范围

- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/SearchIndexPlugin.cs`
- `tests/Bukit.Engine.Tests/SearchIndexPluginExtendedTests.cs`

不得顺带修改 search ranking、URL 路由、索引 schema、默认主题或 search.json 字段。

### 5.5 测试先行步骤

1. `WriteSearchUi_DoesNotUseInnerHtmlForDynamicResults`：生成文件中不含 `.innerHTML`；
2. `WriteSearchUi_UsesTextNodesForTitleSnippetAndMark`：断言生成脚本包含安全 DOM 构造；
3. `WriteSearchUi_EncodesMaliciousPlaceholder`：`" autofocus onfocus=...` 不得突破 attribute；
4. 保留高亮、键盘导航、空结果和 theme 色彩的现有输出契约；
5. 用恶意 title/snippet fixture 生成 search.json，确认 payload 只能作为 JSON 字符串存在，UI 代码没有解释型 sink。

窄测试：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~SearchIndexPluginExtendedTests|FullyQualifiedName~SearchSnippetCapabilityTests'
bash scripts/security/security-regression.sh Release
```

### 5.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| 高亮 UX | 必须保留 `<mark>`，但由 DOM 节点创建。测试大小写、多次命中和无命中。 |
| 键盘导航 | DOM 层级仍是 anchor -> strong/small；保留 class、data-index 和 active 行为。 |
| 性能 | 每条最多 20 个结果；节点构造开销可接受，不引入 sanitizer。 |
| URL | `it.url` 来自路由图；本项不扩展为 URL scheme 重构。审计需确认仍无用户任意 URL 入口。 |
| CSP | 修复不等于完成 CSP；不得把 CSP 议题捆绑进本项。 |

### 5.7 问题复审与代码审计

- 搜索 `innerHTML`，确认默认 search UI 无任何动态或静态使用，防止未来误把安全常量和数据拼接混在一起。
- 逐个审计 title、snippet、placeholder、empty message、URL 五个 sink；记录各自信任级别和编码方式。
- 确认未把 payload 二次 decode、未通过 `insertAdjacentHTML`、`outerHTML`、`document.write` 绕回解释型 sink。
- 审计生成 JS 的正则在 query 为空、多字节字符和特殊正则字符时不会无限循环或抛错。

关闭标准：恶意内容不能形成可执行 DOM，placeholder 不能突破 attribute，原 search UX 测试与安全回归通过。

---

## 6. F-04：递归目录 symlink 绕过默认不跟随策略

> 本项先于 F-03 实施，因为 F-03 的 collision inventory 必须使用可信枚举结果。

### 6.1 复核证据

- `DirectoryCopy.cs:176-207` 的 `SyncFilesRecursive` 使用 `SearchOption.AllDirectories`，枚举完成后才检查最终 file 是否 symlink；目录 symlink 后的普通文件不会被识别为 link。
- `BuildManifestTracker.cs:11-27` 同步和跟踪 media 时有两次递归枚举。
- `StaticFileService.cs:19-101` 和 `RenderEntry.cs:35-65` 递归读取 static HTML/文件。
- `MarkdownFolderProvider.cs:28-42` 递归读取内容 Markdown。
- 其他同类 source/output walker 包括 `ScssCompiler`、`ImageOptimizer`、`ScribanTemplateLinter`、`HashUtil`、`BuildResult` 和 `BuildReporter`。
- 对照实现 `DirectoryCopy.Sync` 在每层目录下降前检查 `ReparsePoint`；`PublicOutputPrivacyCheck` 使用 `EnumerationOptions.AttributesToSkip = FileAttributes.ReparsePoint`。
- 当前文档只承诺 `build.followSymlinks` 影响“supported copy paths”，不能据此默认允许其他 walker 穿透。

### 6.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | `SearchOption.AllDirectories` 把遍历决策交给 BCL，调用方只能在得到文件后检查。 |
| 策略原因 | symlink 策略只封装在 `DirectoryCopy.Sync`，其他模块各自直接枚举。 |
| 架构原因 | “文件枚举”被当成无安全含义的工具操作，没有作为 source trust boundary。 |
| 测试漏检 | 现有 `SyncFilesRecursive_SkipsSymlink` 只创建 file symlink；目录 symlink 测试只覆盖安全的 `Sync`。 |

### 6.3 最小修复方案

在 `Bukit.Shared` 新增一个窄的、只读、`internal` 枚举 helper，例如：

```csharp
internal static class SafeFileEnumerator
{
    public static IEnumerable<string> EnumerateFiles(
        string root, string pattern = "*", bool recurse = true);
}
```

默认递归选项必须至少包含：

```csharp
new EnumerationOptions
{
    RecurseSubdirectories = true,
    AttributesToSkip = FileAttributes.ReparsePoint,
    IgnoreInaccessible = false,
    ReturnSpecialDirectories = false
};
```

然后只替换发布链和报告链中的 raw recursive walker：

- Content：`MarkdownFolderProvider`；
- Static：`StaticFileService`、`RenderEntry` 及 `VariantBuildPipeline` 的存在性 probe；
- Asset tooling：`ScssCompiler`、`ImageOptimizer`、内置 image processing；
- Theme analysis：`ScribanTemplateLinter`；
- Media/incremental：`DirectoryCopy.SyncFilesRecursive`、`BuildManifestTracker`、`HashUtil`；
- Output/report：`BuildResult`、`BuildReporter` 的递归 inventory。

`DirectoryCopy.Sync` 已有显式 `FollowSymlinks` 和 root 内 realpath 检查，应保持不变。不得在本项让 Markdown、templates、media cache 或 report walker 新增 follow=true 行为。

由于 Content 与 Engine 都需要调用该 internal helper，只为 `Bukit.Content` 和 `Bukit.Engine` 增加精确的 `InternalsVisibleTo`；不得把 helper 暴露为新的 public SDK 类型，也不得开放给其他程序集。

### 6.4 允许修改范围

- 新增 `src/Bukit-Core/Bukit.Shared/SafeFileEnumerator.cs`
- `src/Bukit-Core/Bukit.Shared/InternalsVisibleTo.cs`：只增加 Content 与 Engine 两个 friend assembly
- 上一节列出的直接调用文件
- 对应测试：
  - `tests/Bukit.Content.Tests/MarkdownFolderProviderTests.cs`
  - `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
  - `tests/Bukit.Engine.Tests/StaticFileServiceTests.cs`
  - 与 Scss/Image/BuildReporter/HashUtil 对应的现有测试文件
- 必要时增加 architecture test，禁止上述 source-facing 文件重新使用 `SearchOption.AllDirectories`

不得移动 `DirectoryCopy` 到 Shared，不得重构全部 I/O，不得改变 `build.followSymlinks` schema。

### 6.5 测试先行步骤

1. `SyncFilesRecursive_SkipsDirectorySymlinkToExternalRoot`；
2. `MarkdownFolderProvider_DoesNotLoadMarkdownThroughDirectorySymlink`；
3. `StaticFileService_DoesNotRenderOrCopyFilesThroughDirectorySymlink`；
4. `BuildManifestTracker_DoesNotCopyOrTrackMediaThroughDirectorySymlink`；
5. 对 output/report walker 创建 link -> outside，确认清单和 hash 不包含外部文件；
6. 对普通嵌套目录、dotfile allow/deny、Windows/Linux path 行为做正向回归；
7. symlink 创建不受平台支持时可显式 skip，但不得把异常吞掉后伪装成通过。

窄测试至少包括：

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --filter 'FullyQualifiedName~MarkdownFolderProviderTests'
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~DirectoryCopyTests|FullyQualifiedName~StaticFileServiceTests|FullyQualifiedName~BuildReporterTests'
bash scripts/security/security-regression.sh Release
```

### 6.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| 现有内部 symlink 内容 | 默认 false 下将不再发布，这是配置本来承诺的行为。 |
| follow=true 用户 | 仅已有 `DirectoryCopy.Sync` 支持；不要把 helper 误用于该显式路径。 |
| Windows junction/reparse point | 必须与 symlink 一样跳过；测试允许平台差异但不允许策略差异。 |
| 枚举错误 | `IgnoreInaccessible=false`，避免静默生成不完整站点。 |
| F-03/F-08 | 后续 inventory 必须复用 helper，不能重新写 raw walker。 |
| 性能 | `EnumerationOptions` 是流式枚举，不要求先物化全目录；保持稳定排序的位置不变。 |

### 6.7 问题复审与代码审计

- `rg 'SearchOption\.AllDirectories|RecurseSubdirectories'` 建立剩余 walker 台账；每个剩余点必须有“可信、非递归或已有逐层 guard”的书面理由。
- 审计 helper 是否真的在下降目录前跳过 reparse point，而不是重复最终文件检查。
- 审计所有相对路径仍以声明 root 计算，不能因 link 被跳过产生 `..`。
- 审计 follow=true 路径仍执行 final realpath-in-root 检查，防止本项回退既有能力。

关闭标准：content/static/media/report 的 external directory symlink 均无法进入输出或清单，普通目录与已有显式 follow 测试不回归。

---

## 7. F-03：AssetPipeline 重叠目标并行写入

### 7.1 复核证据

- `AssetPipeline.cs:48-71` 同时启动 static、assets、tokens 和 media 四组任务。
- 写入目标分别覆盖 output root、`output/assets`、固定 `assets/css/theme-tokens.css` 和 `assets/uploads`；这些集合不是互斥的。
- `File.Copy(overwrite:true)` 与 token 写入没有统一目标 claim 或 lock。
- `BuildManifestTracker` 分别维护 static、asset、media 所有权；相同目标可被多个 manifest 记录。
- `AssetPipelineTests` 的各来源路径互不重叠，因此只能证明 happy path 并行成功。

### 7.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | 并行任务直接写共享 output tree，没有 destination ownership。 |
| 设计原因 | 并行化按“输入来源”划分，却没有证明“输出集合互斥”。 |
| 增量原因 | manifest 也按来源划分，相同路径可能出现双重 owner。 |
| 测试漏检 | fixture 刻意使用 `robots.txt`、`assets/css/main.css`、token、uploads 等不重叠路径。 |

### 7.3 最小修复方案

在任何 output 写入前建立本次 asset destination plan：

```csharp
internal sealed record AssetOutputClaim(
    string Destination,
    string Category,
    string Source);
```

规则：

1. parent static -> site static、parent assets -> site assets 属于同类别既有继承覆盖，继续允许，最终 owner 是 site；
2. static、assets、generated tokens、media 是不同类别；不同类别映射到同一 normalized destination 时，在写入前抛出稳定 diagnostic；
3. claim 枚举复用 F-04 的安全枚举器、dotfile policy 和平台路径比较；
4. SCSS/image source transform 若会产生新文件，应先完成 source preparation，再建立 claims；
5. token 只有在实际加载到 tokens 时才声明 `assets/css/theme-tokens.css`，不得用“ThemeRoot 存在”制造假冲突；
6. claims 无冲突后，可保留对互斥目标集的并行写入。

需要新增一个明确的 `DiagnosticCode` 时，只新增一个 asset collision code；不要借机重排已有 code 或改变配置 schema。

不采用“只按固定顺序执行四个 task”：它仍保留跨 manifest 双重所有权和 stale-delete 问题，也把冲突变成无提示覆盖。

### 7.4 允许修改范围

- `src/Bukit-Core/Bukit.Engine/AssetPipeline.cs`
- 可新增一个内部 `AssetOutputCollisionDetector.cs` 或 `AssetOutputPlan.cs`
- `src/Bukit-Core/Bukit.Shared/DiagnosticCode.cs`（仅在确需新 code 时）
- `tests/Bukit.Engine.Tests/AssetPipelineTests.cs`
- 必要的 diagnostic contract test

不得改变 `build.followSymlinks`、SCSS/image 配置、manifest 格式或 asset URL 结构。

### 7.5 测试先行步骤

1. static `assets/css/main.css` vs assets `css/main.css`：写入前稳定失败；
2. assets `css/theme-tokens.css` vs generated tokens：稳定失败；
3. static `assets/uploads/a.jpg` vs media `a.jpg`：稳定失败；
4. parent/site 同类别同路径：保留 site override；
5. dotfile 或被 F-04 跳过的 symlink 不得产生幽灵 claim；
6. 冲突失败后不得留下部分目标文件或错误 manifest；
7. 无冲突构建连续运行多次，输出 hash 和 manifest owner 稳定。

窄测试：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~AssetPipelineTests|FullyQualifiedName~BuildManifestTracker'
```

### 7.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| 依赖 F-04 | 没有安全枚举器时不得复制实现；先完成 F-04。 |
| 旧站点有隐式覆盖 | 从非确定覆盖变为明确失败；诊断必须同时列出 destination 和两个来源。 |
| Parent/site theme inheritance | 同类别覆盖是现有能力，必须保留。 |
| 增量 cleanup | 跨类别冲突被拒绝后一个目标只剩一个 manifest owner。 |
| 构建性能 | preflight 增加一次枚举；先正确后优化，复用已获取的 plan 避免重复枚举。 |
| source transform | preflight 时机必须在 SCSS/image 产生文件后，output 写入前。 |

### 7.7 问题复审与代码审计

- 审计所有四类写入都必须经过同一 claim plan；不能只保护 token 路径。
- 审计路径 normalize、大小写 comparer 和 `..` 处理与真实 `FileWriter` 一致。
- 审计冲突检测发生在第一个 output write 之前，避免失败后留下半成品。
- 审计 manifest 不可能为同一 normalized path 产生跨类别 owner。
- 用 collision fixture clean build 两次，确认每次产生相同 code、相同消息排序和相同无部分输出状态。

关闭标准：三类跨来源冲突稳定失败，继承覆盖不回归，无冲突输出可复现，增量 manifest 无双重 owner。

---

## 8. F-07：`content.media.maxConcurrency` 未限制真实下载并发

### 8.1 复核证据

- `ContentImageRewritePipeline.cs:31-64` 的 semaphore 包围 document rewrite。
- `ContentImageRewritePipeline.cs:264-292` 在单文档内为所有 distinct URL 立即调用 `_localizer.LocalizeAsync`，再 `Task.WhenAll`。
- `RewriteBodyHtmlAsync` 直接进入 HTML rewrite，也不经过 document semaphore。
- 现有测试只断言 `MaxConcurrency >= 2`，没有使用非默认 cap 验证上界。

### 8.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | semaphore 的 permit 对应 document，而配置名和用户预期对应 media 工作。 |
| 设计原因 | 后续增加“同文档 URL 并行”时没有重新定义限流单位。 |
| API 原因 | localizer 是实际网络边界，但 gate 位于它的外层批次。 |
| 测试漏检 | 测试只证明有并发，没有证明 `<= maxConcurrency`。 |

### 8.3 最小修复方案

每次 `RewriteAsync` 创建一个共享 download gate，所有文档、HTML pass 和字段 pass 的每次 `_localizer.LocalizeAsync` 都必须：

```csharp
await downloadGate.WaitAsync(cancellationToken);
try
{
    return await _localizer.LocalizeAsync(sourceUrl, cancellationToken);
}
finally
{
    downloadGate.Release();
}
```

现有 document gate 可以保留，用于限制并行文档 transform；它不再承担下载上限语义。`RewriteBodyHtmlAsync` 为自身调用创建同样大小的 operation-local gate。

不得使用 static 或 pipeline-lifetime semaphore，避免 dev 多次 build 之间泄漏 permit/取消状态；不得借机改 retry、SSRF、下载大小或 URL dedupe 规则。

### 8.4 允许修改范围

- `src/Bukit-Core/Bukit.Content/Media/ContentImageRewritePipeline.cs`
- `tests/Bukit.Content.Tests/ContentImageRewritePipelineTests.cs`

### 8.5 测试先行步骤

1. 单文档 6 个 URL、`MaxConcurrency=2`：峰值必须 `<=2` 且应达到 2；
2. 多文档 HTML + fields 混合 URL：全局峰值仍 `<=2`；
3. `RewriteBodyHtmlAsync` 单独调用：峰值受同一 cap；
4. localizer 抛错和 cancellation：permit 在 finally 释放；
5. 重复 URL memoization 调用次数保持不变；
6. `MaxConcurrency=1` 保持输出顺序与映射正确。

窄测试：

```bash
dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release --filter 'FullyQualifiedName~ContentImageRewritePipelineTests|FullyQualifiedName~LocalizedContentBodyStoreTests'
```

### 8.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| 下载吞吐 | 单大文档不再无限 fan-out；这是配置契约的预期收紧。 |
| 文档 CPU 并发 | 保留现有 document gate，避免大量文档 transform 同时运行。 |
| cancellation | `WaitAsync` 和 localizer 都使用原 token，finally 只对已获得 permit 的路径释放。 |
| memo dictionary | 仍按文档私有，不在本项改为跨文档共享缓存。 |
| ContentProviderFactory | 配置默认和校验保持不变。 |

### 8.7 问题复审与代码审计

- 搜索所有 `_localizer.LocalizeAsync` 调用，确认没有绕过 download gate。
- 审计每次成功 `WaitAsync` 恰好一次 `Release`，失败等待不得释放。
- 审计 gate 生命周期局限于一次 public rewrite operation。
- 以 probe localizer 记录 active/peak/call count，复审峰值、失败和取消三条路径。

关闭标准：所有入口实际 localizer 峰值始终不超过配置值，且并发、memo、取消和输出内容测试通过。

---

## 9. F-05：模板能力与静态分析缓存不失效

### 9.1 复核证据

- `TemplateStaticAnalysisService.cs:9-26` 的 static cache key 只有 layoutsDir + template path。
- analyzer 会递归读取 layout/include，但这些依赖没有进入全局 key。
- `TemplateCapabilitiesResolver.cs:10-15,54-57` 永久缓存 manifest load Task；缓存也会永久保留 faulted Task。
- `TemplateCapabilitiesResolver.cs:128-146` 的 fallback cache 同样只有 path key。
- `TemplateRendererBase.cs:75-107` 已使用 last-write、length 和 content hash 验证缓存，是仓库内可参考的正确模式。
- 现有测试只验证首次加载，不在同进程修改文件后再次解析。

### 9.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | cache identity 不包含内容版本和依赖版本。 |
| 生命周期原因 | static cache 与进程同寿命，输入文件与一次 rebuild 同寿命。 |
| 依赖原因 | root template 的结果取决于 layout/include graph，不只取决于 root path。 |
| 测试漏检 | 每个测试使用新 temp path，天然避开旧 cache entry。 |

### 9.3 最小修复方案

采用两种不同策略，不强行用一个复杂缓存解决所有情况：

1. `bukit.templates.yaml` 小且单文件：缓存 entry 保存内容 fingerprint；每次访问先比较 fingerprint，变化后原子替换并重新 parse。fingerprint 使用内容 hash，避免低分辨率 mtime。
2. static analysis 依赖完整 include/layout graph：先移除 process-global `StaticCache`，保留每次 `Analyzer` 内部的局部 cache；正确性优先。只有 benchmark 证明必要时，才另立任务设计 dependency fingerprint cache。
3. 移除 path-only `FallbackCache`，直接读取当前 template；fallback 只在分析不确定时发生，不值得牺牲一致性。

不推荐“只在 dev watcher 回调清 cache”：doctor、测试或其他长进程调用仍会陈旧，且 watcher 与 resolver 形成隐式耦合。也不新增 public `ResetForTests`；生产生命周期和测试生命周期必须一致。

### 9.4 允许修改范围

- `src/Bukit-Core/Bukit.Engine/TemplateCapabilitiesResolver.cs`
- `src/Bukit-Core/Bukit.Engine/TemplateStaticAnalysisService.cs`
- `tests/Bukit.Engine.Tests/TemplateCapabilitiesResolverTests.cs`
- `tests/Bukit.Engine.Tests/TemplateStaticAnalysisTests.cs`
- 必要时 `SiteEngineIntegrationTests.cs` 增加同一 engine 两次 build 的回归

不得修改 watcher、Scriban parser、theme manifest schema 或 renderer cache。

### 9.5 测试先行步骤

1. manifest `needs_page_content: false -> true`，同进程第二次读取立即变化；
2. manifest 首次 invalid 导致异常，修正文件后同进程可恢复，不被 faulted Task 毒化；
3. root template 增删 `.content`，第二次分析变化；
4. include 文件变化，root template 结果变化；
5. layout directive 目标变化，结果变化；
6. 同一 `SiteEngine` 连续 build，search snippet/list content 决策与新文件一致；
7. 并发读取同一 manifest 不返回部分 parse 结果。

窄测试：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~TemplateCapabilitiesResolverTests|FullyQualifiedName~TemplateStaticAnalysisTests|FullyQualifiedName~SearchSnippetCapabilityTests'
```

### 9.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| List page content | 必须在 manifest/template 变化后同步变化。 |
| Pagination/taxonomy/search snippets | 都通过 capabilities resolver，需各有至少一个回归断言。 |
| 性能 | 移除 path-only cache 会增加小文件读取；先记录 build stage metrics，不提前引入复杂依赖图缓存。 |
| 并发 | manifest entry 必须不可变且原子替换；不得在共享对象上原地更新。 |
| AOT | 只使用文件 hash/普通集合，不引入动态 code generation。 |

### 9.7 问题复审与代码审计

- 审计所有 process-global cache key，确认没有仅 path 的 template decision cache。
- 审计 manifest 不存在、出现、删除、invalid 后修复四种状态转换。
- 审计 include cycle、missing include 和 dynamic include 的原语义不变。
- 使用同一 temp root、同一进程和同一 engine 实例复现，禁止通过换目录掩盖问题。

关闭标准：manifest、root、include、layout 四类修改都在同进程下一次调用生效，性能无明显异常，targeted gate 通过。

---

## 10. F-06：`site.search.maxContentLength` 被接受但未消费

### 10.1 复核证据

- `AppConfig.cs:493-500` 默认 8000；`SiteDefaultsApplier.cs:183` 读取用户值。
- strict validator 和 JSON schema 接受该字段，minimum 为 1。
- `SearchIndexBuilder.cs:123-126` 和 `:232` 仍硬编码 8000。
- `SearchIndexPlugin.cs:89-99`、`SearchProjectionWriter` 和 merged root writer 调用链没有传 cap。
- `I18nOutputMerger` 现有 `ISearchIndexBuilder` 参数未被使用是独立架构债务，不应借本项顺手修复。

### 10.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | 配置在 Config 层落地，但 Search writer 签名没有 cap 参数。 |
| 架构原因 | schema/defaults 与 runtime consumer 缺少“非默认值”契约测试。 |
| 多表示原因 | single、list、merged 有多个 writer，字面量被复制。 |
| 测试漏检 | 所有 search 测试都隐式接受默认 8000。 |

### 10.3 最小修复方案

增加一个内部统一 helper：

```csharp
private static string TruncateContent(string value, int maxContentLength);
```

并把 `int maxContentLength` 显式传给：

- `GenerateSingleSearchIndex`；
- `GenerateMergedSearchIndex`；
- `WriteSearchItem`；
- `WriteListRouteSearchItem`；
- `SearchIndexPlugin`、`SearchProjectionWriter`、root merged writer 的所有调用点。

helper 保持“UTF-16 code unit 上限”的现有近似语义，但不得在高代理项与低代理项之间截断，避免小 cap 产生无效字符串。配置只约束 `content` 字段；不得顺手截断 title、summary 或 snippet。

默认 8000、minimum=1、字段名和 schema 全部保持不变。

### 10.4 允许修改范围

- `src/Bukit-Core/Bukit.Engine/SearchIndexBuilder.cs`
- `src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/SearchIndexPlugin.cs`
- `src/Bukit-Core/Bukit.Engine/PublishAggregateProjectionWriters.cs`
- `src/Bukit-Core/Bukit.Engine/I18nOutputMerger.cs`
- 必要的内部 interface/default implementation 调用签名
- `tests/Bukit.Engine.Tests/SearchIndexBuilderTests.cs`
- `tests/Bukit.Engine.Tests/DefaultSearchIndexBuilderTests.cs`
- 相关 i18n/search projection tests

不得修改 config schema、search JSON shape、ranking 或 `ISearchIndexBuilder` 未使用问题，除非签名传播不可避免；即使修改内部 interface，也不能扩大其职责。

### 10.5 测试先行步骤

1. single document，cap=5，content 精确不超过 5；
2. list route，cap=7，使用同一 helper；
3. merged i18n，非默认 cap 对各语言记录生效；
4. split/index mode 的语言 search.json 仍生效；
5. 默认 8000 输出与修复前兼容；
6. cap 边界落在 emoji surrogate pair 中间时不产生 dangling surrogate；
7. title/summary/snippet 不被误截断。

窄测试：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~SearchIndexBuilderTests|FullyQualifiedName~DefaultSearchIndexBuilderTests|FullyQualifiedName~I18nMerged'
```

### 10.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| 索引体积 | 非默认小值开始真正生效，这是预期行为变化。 |
| i18n merged | 使用 root config 的 cap；语言 variant 不应各自回落到 8000。 |
| list routes | 必须与 document 使用同一 truncation helper。 |
| Unicode | 防止切断 surrogate pair；不在本项引入 grapheme-cluster 新契约。 |
| snippet | 维持既有 280/summary 逻辑，避免配置含义漂移。 |

### 10.7 问题复审与代码审计

- `rg '\b8000\b'` 确认 SearchIndexBuilder 不再有 runtime 字面量，默认值只留 Config 层。
- 列出所有 `Generate*SearchIndex` 调用点，确认 cap 没有在某条 representation 路径丢失。
- 比较 single、list、merged、index 四种模式生成 JSON。
- 审计新增参数为 internal contract，不误变成插件公共协议。

关闭标准：所有 search representation 的非默认 cap 生效，默认输出兼容，Unicode 和 snippet 行为无回归。

---

## 11. F-08：build report 健康字段与 generatedFiles 失真

### 11.1 复核证据

- `BuildResult.cs:86-94` 把 `WarningCount`、`ErrorCount` 固定为 0。
- `BuildResultFactory.Create(... generatedFiles = null)` 支持清单，但 `SiteEngine.cs:124,233` 均未传入。
- `BuildReporter.cs:93-117` 只是序列化这些值，本身没有聚合能力。
- `BuildVariantResult` 不携带 build diagnostics；`ILogger` 也没有计数。
- `build-report.v1.schema.json` 已冻结，`docs/bukit-1.0-contract-matrix.zh-CN.md:90` 标记为 `GA-locked`。
- `artifact-manifest.json` 已负责 `.bukit` 内报告 hash；`generatedFiles` 不应通过自引用方式重复替代它。

原审计用 publish audit 的 22 warnings 证明“报告之间观感矛盾”是有效信号，但修复不能把 publish summary 直接复制为 build summary。更准确的 finding 是：build warning/error 没有真实数据源，public generated file inventory 没有建立。

### 11.2 根因链

| 层次 | 原因 |
|---|---|
| 直接原因 | factory 使用占位常量，SiteEngine 未提供 generated files。 |
| 可观测性原因 | 日志是即时输出，没有 per-build diagnostic collector。 |
| 生命周期原因 | BuildResult 在报告写入前创建，没有明确区分 public output 和 internal report artifacts。 |
| 测试漏检 | writer 测试使用同一个 factory 创建期望，验证了“能序列化 0/空数组”，没有独立 oracle。 |

### 11.3 最小修复方案

保持 `build-report.v1` 的字段、层级、schema id 和 public records 不变，只修正值的来源。

#### A. build warning/error

新增 Engine 内部的 per-build diagnostic counter/logger decorator：

```csharp
internal sealed class BuildDiagnosticLogger : ILogger
{
    // 转发原 logger；Warn/Error 使用 Interlocked 增加本次 build 计数。
}
```

- 每次 `SiteEngine.BuildAsync` 建立新的 counter，不能跨 dev rebuild 累计；
- single 和 multi-language variant logger 共享同一个 counter；
- counter 统计实际发出的 build `Warn`/`Error` 事件，不直接复制 SEO/publish/security summary；
- `BuildResultFactory.Create` 接收最终 warning/error count，默认参数只用于现有窄单元测试迁移，生产调用必须显式传值。

#### B. generatedFiles

在报告写入前生成 **public output inventory**：

- 递归枚举 output，复用 F-04 的安全枚举器；
- 排除 `.bukit/` internal reports 和 `.bukit-output-marker`；
- 使用相对 output root、`/` 分隔、稳定排序、去重；
- 把结果传给 BuildResult factory；
- `.bukit` 报告完整性继续由 `artifact-manifest.json` 管理，避免 build report 自引用和 hash 循环。

更新 `guide/dev/observability.md` 或 build report 文档，明确 `warningCount/errorCount` 是 build diagnostic event count，`generatedFiles` 是 public output inventory。

### 11.4 允许修改范围

- `src/Bukit-Core/Bukit.Engine/BuildResult.cs`
- `src/Bukit-Core/Bukit.Engine/SiteEngine.cs`
- 可新增内部 `BuildDiagnosticLogger.cs`、`BuildOutputInventory.cs`
- 只有为传递同一 logger/counter 所必需时，才修改 `VariantBuildPipeline.cs`
- `tests/Bukit.Engine.Tests/BuildReporterTests.cs`
- `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs`
- `guide/dev/observability.md` 或现有 build report 说明文档

明确禁止修改：

- `docs/schemas/build-report.v1.schema.json` 的结构；
- `BuildResult`、`BuildSummary` public record 的字段集合；
- `artifact-manifest.v1` 结构；
- SEO/publish/security 专项报告计数定义。

### 11.5 测试先行步骤

1. 一个会发出真实 build warning 的 fixture，build report warningCount > 0；不能用手工构造 BuildResult 作为唯一 oracle；
2. 无 warning 的最小站点计数为 0；
3. 同一 SiteEngine 连续 build，第二次计数不包含第一次；
4. multi-language 两个 variant 的 warning 聚合准确且线程安全；
5. generatedFiles 与磁盘 public 文件集合完全相等、稳定排序；
6. generatedFiles 不含 `.bukit/*`、marker、外部 symlink 文件；
7. build report 仍通过现有 v1 strict schema，`additionalProperties=false` 不受影响；
8. artifact manifest 中 build-report hash 仍与最终文件一致。

窄测试：

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter 'FullyQualifiedName~BuildReporterTests|FullyQualifiedName~SiteEngineIntegrationTests'
```

### 11.6 跨模块影响与新风险

| 影响 | 判定与控制 |
|---|---|
| 冻结 schema | 只改变原本虚假的值，不新增字段；schema validation 必须保持。 |
| 日志输出 | decorator 必须原样转发，不改变格式、级别过滤和消息文字。 |
| 多语言并发 | 计数器使用原子操作，variant logger 共享本次 build counter。 |
| SEO/publish | 详细计数仍来自独立报告；禁止复制或重复聚合 summary。 |
| artifact hash | inventory 在报告前生成且排除 `.bukit`，不得重写 build report 后让 manifest hash 失效。 |
| F-04 | inventory 必须等 F-04 helper 落地后实现，避免报告跟随 output symlink。 |
| 下游消费者 | 过去依赖固定 0/空数组属于依赖 Bug；发布说明应标记为语义修正而非 schema breaking。 |

### 11.7 问题复审与代码审计

- 对照磁盘 public 文件集合、BuildResult.GeneratedFiles 和 JSON generatedFiles 三方一致。
- 对照 probe logger 的 Warn/Error 调用数与 BuildResult/JSON 计数一致。
- 审计 reporter 写入过程不会被 counter 计入当前已经快照的 build health，避免自增漂移。
- 审计多次 dev rebuild、失败 build、取消 build：失败/取消是否写报告必须维持现行契约，不得暗中新增 partial report。
- 运行 schema strict validation，确认冻结契约无结构变化。

关闭标准：真实 build warning 能被报告、public 清单与磁盘一致、v1 schema 和 artifact hash 不变、连续/并发 build 不串计数。

---

## 12. 每项修复后的严格代码审计模板

每个 finding 修复完成后，审计者必须只读检查完整 diff，并按以下模板输出结论；不能只回复“LGTM”。

### 12.1 范围审计

- 本项允许文件清单与实际 `git diff --name-only` 是否一致；
- 是否夹带重构、格式化、命名或其他 finding；
- public API、配置、schema、协议、持久化格式是否发生未声明变化；
- backup/reference 目录是否保持未修改。

### 12.2 根因审计

- 修复是否位于根因处，而不是在某个调用点屏蔽症状；
- 所有入口是否覆盖，是否存在第二条绕过路径；
- 失败语义是否明确，是否产生 silent fallback；
- 是否引入新的共享可变状态、死锁、TOCTOU 或资源泄漏。

### 12.3 测试审计

- 新测试在修复前是否确实失败；
- 测试 oracle 是否独立于被测实现，避免“factory 生成 expected”；
- 是否同时覆盖正向、负向、边界、取消/失败和重复执行；
- 是否有平台差异被错误地 catch 后当成通过；
- targeted gate 的命令、时间和结果是否记录完整。

### 12.4 跨模块审计

- 调用签名、默认值、i18n、incremental、dev、report、AOT 是否受影响；
- 输出顺序、hash、manifest owner、路径 normalize 是否保持确定；
- 错误诊断是否泄露路径、URL、secret 或内部输出；
- 性能变化是否有合理上界，是否为了微优化重新牺牲正确性。

审计结论只能是：

- **通过**：无未解决问题；
- **有条件通过**：只剩明确、非阻塞、另立任务的证据项；
- **不通过**：存在正确性、安全、契约、范围漂移或测试缺口，必须返回当前 finding 修复。

## 13. Finding 关闭台账格式

后续每完成一项，在独立修复任务中记录：

| 字段 | 必填内容 |
|---|---|
| Finding | F-xx 与原严重度 |
| 修复 commit/diff | 精确 commit 或未提交 diff 基线 |
| 根因 | 一句话，必须与本文一致；若变化需说明新证据 |
| 改动文件 | 完整列表 |
| 失败测试 | 测试名与修复前失败证据 |
| targeted tests | 命令、通过数、失败数、skip 数 |
| repository gate | `post-change-targeted.sh` 结果 |
| 原问题复现 | 修复前/修复后对比 |
| 跨模块回归 | 实际执行的相关测试 |
| 只读代码审计 | 审计者、结论、问题及处理 |
| 兼容性 | public/config/schema/protocol 是否变化 |
| 最终状态 | 已关闭 / 部分关闭 / 未关闭 / 环境阻塞 |

## 14. 整体完成条件

8 项只有在分别完成以下条件后，才可以在总台账中标记全部关闭：

1. 每项有修复前失败测试和修复后通过证据；
2. 每项独立 targeted gate 通过；
3. F-01/F-02/F-04 的安全回归通过；
4. F-03/F-07 的并发与重复执行证据通过；
5. F-05 的同进程 mutate-and-rebuild 通过；
6. F-06 的 single/list/i18n merged 非默认 cap 通过；
7. F-08 的 schema、public inventory、diagnostic count、artifact hash 通过；
8. 每项完成独立只读审计；
9. 全部 8 项完成后，再做一次 aggregate diff 审计，检查跨 finding 重复 helper、相互覆盖、顺序依赖和无关变化；
10. `git diff --check`、路径引用、诊断 code、文档与实际行为一致。

在这些条件满足前，管理层状态应表述为“已分析并有受控修复方案”，不能表述为“8 个问题已修复”。
