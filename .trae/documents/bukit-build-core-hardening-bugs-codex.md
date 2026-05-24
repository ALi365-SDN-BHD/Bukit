# Bukit Build Core Hardening：Bug 修复任务文档

> 适用仓库：`https://github.com/ALi365-SDN-BHD/bukit`  
> 目标用途：交给 Codex / AI Coding Agent 执行修复  
> 文档目标：把前期 Code Review 中发现的架构、性能、安全性、功能完整性问题整理成可执行的 Bug 修复任务。  
> 建议分支：`bugfix/build-core-hardening`

---

## 1. 总体目标

Bukit 当前是面向 **Notes-as-CMS、AI Agent 自动化、GEO-ready websites** 的 `.NET Native AOT` 静态网站生成引擎。

本轮修复不要新增大功能，不要重写整个架构，优先完成以下目标：

1. 修复静态文件路由、主题继承、增量构建、媒体复制等高置信 Bug。
2. 强化路由、输出路径、插件、远程主题的安全边界。
3. 补齐可复现测试，防止后续功能迭代引入回归。
4. 为后续 BukitJalil、本地控制面、主题市场、AI 自动生成主题打牢 Build Core。

---

## 2. Codex 执行总 Prompt

可以直接把以下 Prompt 交给 Codex：

```text
你正在修复 ALi365-SDN-BHD/bukit 仓库。

任务目标：
1. 修复 Build Core 中的 P0/P1 Bug。
2. 不要新增与本次无关的新功能。
3. 不要大规模重写项目结构。
4. 保持现有公开 CLI 行为尽量兼容。
5. 每修复一个 Bug，必须补充或更新单元测试 / 集成测试。
6. 所有路径、路由、输出文件写入必须经过统一安全校验。
7. 修复后必须运行：
   - dotnet test bukit.slnx -c Release
   - dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
   - dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
8. 如果 Native AOT 发布测试存在于仓库 CI 或脚本中，也要确保不破坏 AOT 兼容性。

优先级：
先修 P0，再修 P1，最后修 P2。

请按下面文档中的 Bug ID 修复，并在提交信息中引用 Bug ID。
```

---

## 3. 修复优先级总览

### P0：必须优先修复

| Bug ID | 标题 | 类型 | 严重级别 |
|---|---|---|---|
| BUG-001 | `static/about.html` URL 生成错误 | 功能 / SEO | 高 |
| BUG-002 | `static/.html` 触发 `IndexOutOfRangeException` | 稳定性 | 高 |
| BUG-003 | 静态 HTML 路由未进入统一 Route Inventory | 功能 / 一致性 | 高 |
| BUG-004 | 父主题 assets/static 可能覆盖子主题 | 主题系统 | 高 |
| BUG-005 | 子主题无 assets 时父主题 assets 可能不复制 | 主题系统 | 高 |
| BUG-006 | 增量构建删除页面后旧 HTML 文件残留 | 构建正确性 | 高 |
| BUG-007 | media cache 只复制顶层文件，嵌套媒体丢失 | 功能 | 高 |
| BUG-008 | 主题 tag checkout 失败后构建仍继续 | 供应链 / 可复现构建 | 高 |
| BUG-009 | git 超时处理不完整，可能卡死或异常 | 稳定性 | 高 |
| BUG-010 | 默认 `outputPathEncoding=none` 导致 unsafe slug 进入输出路径 | 安全 | 高 |
| BUG-011 | `NormalizeUrl` 允许协议型 / 协议相对 URL | 安全 / SEO | 高 |
| BUG-012 | `route.outputPath` 部分覆盖可能被忽略 | 功能 | 中高 |

### P1：高风险，建议本轮一起修

| Bug ID | 标题 | 类型 | 严重级别 |
|---|---|---|---|
| BUG-013 | 模板 hash 未覆盖父主题 / 用户覆盖模板 | 增量构建 | 高 |
| BUG-014 | 非 clean build 不清理删除的 assets/static | 构建一致性 | 高 |
| BUG-015 | `DirectoryCopy.SyncFile` 用 size + time 判断，可能漏更新 | 构建一致性 | 中高 |
| BUG-016 | 多语言构建并发倍增，可能 OOM | 性能 | 中高 |
| BUG-017 | process 插件继承宿主环境变量，可能泄露 token | 安全 | 高 |
| BUG-018 | process 插件 stdout/stderr 无大小限制，可能内存 DoS | 安全 / 稳定性 | 高 |
| BUG-019 | 插件输出文件未进入 manifest，旧文件可能残留 | 插件 / 增量 | 中高 |

### P2：工程硬化与长期改造

| Bug ID | 标题 | 类型 | 严重级别 |
|---|---|---|---|
| BUG-020 | 非 HTML 静态文件复制绕过 `FileWriter` 安全保护 | 工程一致性 | 中 |
| BUG-021 | `build --clean` 直接删除 outputDir，缺少 marker 保护 | 安全 / 数据保护 | 高 |
| BUG-022 | 编码后的路径未二次安全校验 | 安全 | 中高 |
| BUG-023 | 远程主题未锁 commit，构建不可复现 | 供应链 | 高 |

---

# 4. P0 Bug 详细任务

---

## BUG-001：`static/about.html` URL 生成错误

### 问题描述

`StaticFileService` 处理 HTML 静态文件时，如果通过 `Path.GetDirectoryName(relativeOutputPath)` 推导页面 URL，会导致普通 HTML 文件 URL 错误。

### 复现结构

```text
static/
  index.html
  about.html
  about/
    team.html
```

### 当前风险

可能出现：

```text
static/index.html        -> /
static/about.html        -> /              错误
static/about/team.html   -> /about/        错误
```

### 预期结果

```text
static/index.html        -> /
static/about.html        -> /about/
static/about/team.html   -> /about/team/
```

### 修复要求

新增一个独立方法：

```csharp
private static string BuildUrlFromStaticHtmlPath(string relativeOutputPath)
```

规则：

1. `index.html` -> `/`
2. `docs/index.html` -> `/docs/`
3. `about.html` -> `/about/`
4. `about/team.html` -> `/about/team/`
5. 路径统一使用 `/`
6. 输出 URL 必须以 `/` 开头，以 `/` 结尾
7. 输出 URL 必须通过 Route URL 安全校验

### 测试要求

新增测试：

```csharp
[Theory]
[InlineData("index.html", "/")]
[InlineData("about.html", "/about/")]
[InlineData("about/team.html", "/about/team/")]
[InlineData("docs/index.html", "/docs/")]
public void StaticHtml_ShouldGenerateCorrectUrl(string path, string expectedUrl)
{
}
```

---

## BUG-002：`static/.html` 触发 `IndexOutOfRangeException`

### 问题描述

如果 `StaticFileService` 里直接访问 `fileName[0]`，当文件名为空时会崩溃。

### 复现

```bash
mkdir static
touch static/.html
dotnet run --project src/Bukit.Cli -c Release -- build --clean
```

### 修复要求

1. 对 `Path.GetFileNameWithoutExtension(file)` 结果做空值校验。
2. 对非法 HTML 文件名给出明确 warning。
3. 默认跳过非法静态 HTML 文件，而不是崩溃。
4. 不允许生成空 slug、空 URL 或空 outputPath。

### 测试要求

```csharp
[Fact]
public async Task StaticHtml_WithEmptyFileName_ShouldNotCrash()
{
}
```

验收：

- build 不崩溃；
- 日志包含 warning；
- 不生成对应页面；
- 不污染 sitemap / route inventory。

---

## BUG-003：静态 HTML 路由未进入统一 Route Inventory

### 问题描述

内容页、插件派生页、静态 HTML 页应该进入同一个 route inventory。当前静态 HTML 可能在单独阶段处理，导致冲突无法统一检测。

### 复现结构

```text
content/posts/about.md   -> /about/
static/about.html        -> /about/
```

### 风险

1. 内容页和静态页互相覆盖。
2. sitemap 与最终输出不一致。
3. canonical URL 指向错误页面。
4. search index 收录的内容与最终页面不一致。

### 修复要求

引入统一路由收集模型：

```text
Content Routes
Static HTML Routes
Plugin Derived Routes
System Routes
        ↓
RouteInventoryValidator
        ↓
Render / Copy / Write
```

最低要求：

1. 静态 HTML 页生成 `RouteInfo` 或等价 inventory item。
2. `RouteInventoryValidator` 检测静态页与内容页的 URL 冲突。
3. `RouteInventoryValidator` 检测静态页与内容页的 outputPath 冲突。
4. 冲突时 build fail，错误信息包含两个来源。

### 测试要求

```csharp
[Fact]
public async Task StaticHtmlRoute_ShouldConflictWithContentRoute()
{
}
```

---

## BUG-004：父主题 assets/static 可能覆盖子主题

### 问题描述

主题继承中，父主题应该作为默认基础，子主题应该覆盖父主题。当前复制顺序如果是 child -> parent，会导致父主题覆盖子主题。

### 复现结构

```text
parent-theme/
  assets/main.css    内容：parent

child-theme/
  assets/main.css    内容：child
```

### 预期

```text
dist/assets/main.css 内容应该是 child
```

### 修复要求

复制顺序改为：

```text
1. parent theme assets/static
2. child theme assets/static
3. project-level override assets/static
```

如果当前项目存在 root-level assets/static，也应该最后覆盖主题资源。

### 测试要求

```csharp
[Fact]
public async Task ChildThemeAssets_ShouldOverrideParentThemeAssets()
{
}

[Fact]
public async Task ChildThemeStatic_ShouldOverrideParentThemeStatic()
{
}
```

---

## BUG-005：子主题无 assets 时父主题 assets 可能不复制

### 问题描述

如果 parent theme 有 assets，但 child theme 没有 assets，父主题 assets 仍然应该输出。不能把 parent assets 的复制逻辑包在 child assets 存在判断里。

### 复现结构

```text
parent-theme/
  assets/main.css

child-theme/
  layouts/index.sbn
  # 没有 assets/
```

### 预期

```text
dist/assets/main.css 存在
```

### 修复要求

parent assets 和 child assets 必须独立判断：

```csharp
if (Directory.Exists(parentAssetsDir))
    Copy(parentAssetsDir, outputAssetsDir);

if (Directory.Exists(childAssetsDir))
    Copy(childAssetsDir, outputAssetsDir);
```

### 测试要求

```csharp
[Fact]
public async Task ParentThemeAssets_ShouldBeCopied_WhenChildThemeHasNoAssets()
{
}
```

---

## BUG-006：增量构建删除页面后旧 HTML 文件残留

### 问题描述

非 clean build 时，如果源内容被删除，manifest 可能移除对应 key，但 dist 里的旧 HTML 文件没有删除。

### 复现步骤

第一次构建：

```text
content/posts/a.md -> dist/blog/a/index.html
content/posts/b.md -> dist/blog/b/index.html
```

删除：

```text
content/posts/b.md
```

第二次构建：

```bash
dotnet run --project src/Bukit.Cli -c Release -- build
```

### 预期

```text
dist/blog/b/index.html 被删除
```

### 实际风险

旧页面仍然在线。

### 修复要求

1. manifest entry 必须保存最终 `OutputPath`。
2. 构建时识别 removed routes。
3. 对 removed routes 调用安全删除。
4. 删除后清理空目录，但不能删除 output root 外目录。
5. 删除操作必须经过统一 `IOutputFileSystem` / safe path guard。

### 测试要求

```csharp
[Fact]
public async Task IncrementalBuild_ShouldDeleteRemovedPages()
{
}
```

---

## BUG-007：media cache 只复制顶层文件，嵌套媒体丢失

### 问题描述

media cache 复制时如果只使用 top-level file enumeration，嵌套媒体目录不会输出。

### 复现结构

```text
.cache/media/
  cover.png
  posts/
    2026/
      article-cover.png
```

### 预期输出

```text
dist/assets/uploads/cover.png
dist/assets/uploads/posts/2026/article-cover.png
```

### 修复要求

1. media sync 必须递归复制。
2. 保持相对目录结构。
3. 必须跳过不应该输出的临时文件。
4. 必须支持 stale cleanup，防止已删除媒体残留。

### 测试要求

```csharp
[Fact]
public async Task MediaSync_ShouldCopyNestedFiles()
{
}
```

---

## BUG-008：主题 tag checkout 失败后构建仍继续

### 问题描述

远程主题指定 tag / ref 后，如果 checkout 失败，构建必须失败，不能继续使用 repo 当前状态。

### 复现配置

```yaml
theme:
  name: my-theme
  source: https://github.com/example/theme.git@not-exist-tag
```

### 预期

构建失败：

```text
Theme version not found: not-exist-tag
```

### 修复要求

1. `git checkout <versionTag>` 失败必须 hard fail。
2. fetch tags 后重试一次即可。
3. 第二次仍失败，抛出明确异常。
4. 错误信息必须包含 source 和 versionTag。
5. 不允许 fallback 到 main / master / 当前 checkout。

### 测试要求

```csharp
[Fact]
public async Task ThemeResolve_ShouldFail_WhenVersionTagDoesNotExist()
{
}
```

---

## BUG-009：git 超时处理不完整

### 问题描述

`git clone` / `git pull` / `git checkout` 等子进程如果超过超时限制，必须 kill process tree 并返回失败，不能继续读取未退出进程的 ExitCode。

### 修复要求

为 git 命令执行封装：

```csharp
private static async Task<GitResult> RunGitAsync(
    string args,
    string workingDirectory,
    TimeSpan timeout,
    CancellationToken cancellationToken)
```

要求：

1. 超时后 kill entire process tree。
2. 返回 stdout、stderr、exitCode、timedOut。
3. 不阻塞主线程。
4. 支持 cancellation token。
5. 对 CI 友好。

### 测试要求

可通过 fake process runner 注入测试，不建议在测试中真的跑长时间 git。

```csharp
[Fact]
public async Task GitRunner_ShouldKillProcess_WhenTimedOut()
{
}
```

---

## BUG-010：默认 `outputPathEncoding=none` 导致 unsafe slug 进入输出路径

### 问题描述

默认不编码输出路径时，内容 slug 可能携带 `../`、`..\`、绝对路径、Windows 保留名等危险字符。

### 风险 slug

```text
../evil
..\evil
a/../../x
CON
aux
a%2Fb
```

### 修复要求

新增 `RouteSecurityValidator`：

```text
RouteSecurityValidator
  - reject ../
  - reject ..\
  - reject absolute path
  - reject protocol URL
  - reject //example.com
  - reject Windows reserved names
  - reject control chars
  - reject empty segment
```

最低要求：

1. 即使 `outputPathEncoding=none`，也必须做安全校验。
2. 错误必须在 route validation 阶段抛出，不能等 FileWriter 阶段。
3. 错误信息必须包含内容来源、slug、route url、outputPath。

### 测试要求

```csharp
[Theory]
[InlineData("../evil")]
[InlineData("..\\evil")]
[InlineData("a/../../x")]
[InlineData("CON")]
[InlineData("aux")]
public void RouteValidator_ShouldRejectUnsafeSlug(string slug)
{
}
```

---

## BUG-011：`NormalizeUrl` 允许协议型 / 协议相对 URL

### 问题描述

内部 route URL 应该只能是站内路径，例如 `/about/`。不能允许：

```text
https://evil.com
//evil.com
javascript:alert(1)
data:text/html,...
```

### 修复要求

新增或增强 URL 校验：

```csharp
public static void ValidateInternalUrl(string url)
```

规则：

1. 必须以单个 `/` 开头。
2. 不允许 `//` 开头。
3. 不允许包含 URI scheme。
4. 不允许 `javascript:`、`data:`、`vbscript:`。
5. 规范化后必须以 `/` 结尾。
6. 空路径规范化为 `/`。

### 测试要求

```csharp
[Theory]
[InlineData("https://evil.com")]
[InlineData("//evil.com")]
[InlineData("javascript:alert(1)")]
[InlineData("data:text/html,test")]
public void NormalizeUrl_ShouldRejectExternalOrDangerousUrl(string url)
{
}
```

---

## BUG-012：`route.outputPath` 部分覆盖可能被忽略

### 问题描述

partial route override 中，如果只设置 `outputPath`，没有设置 `url`，配置可能被忽略。

### 复现配置

```yaml
route:
  outputPath: custom/about/index.html
```

### 预期

只覆盖 outputPath，不强制要求同时覆盖 url。

### 修复要求

partial override 支持以下组合：

```text
只改 url
只改 outputPath
只改 template
url + outputPath
url + template
outputPath + template
url + outputPath + template
```

### 测试要求

```csharp
[Fact]
public void RoutePartialOverride_ShouldApplyOutputPathOnly()
{
}
```

---

# 5. P1 Bug 详细任务

---

## BUG-013：模板 hash 未覆盖父主题 / 用户覆盖模板

### 问题描述

增量构建时，如果 template hash 只覆盖 child layouts，而实际渲染还使用 parent layouts 或 user layouts，则修改 parent/user 模板可能不会触发重新渲染。

### 修复要求

升级为 composite template fingerprint：

```text
templateHash =
  hash(child layouts)
+ hash(parent layouts)
+ hash(user layouts)
+ hash(components)
+ hash(theme.yaml)
+ hash(section schemas)
+ hash(renderer version)
```

### 测试要求

```csharp
[Fact]
public async Task IncrementalBuild_ShouldRerender_WhenParentLayoutChanges()
{
}

[Fact]
public async Task IncrementalBuild_ShouldRerender_WhenUserLayoutOverrideChanges()
{
}
```

---

## BUG-014：非 clean build 不清理删除的 assets/static

### 问题描述

`DirectoryCopy.Sync` 只复制或更新文件，不删除目标端多余文件。删除源资源后，dist 中旧资源仍可能残留。

### 修复要求

引入 Asset Manifest：

```json
{
  "assets/main.css": "sha256...",
  "static/favicon.ico": "sha256..."
}
```

或者在 sync 时支持 mirror 模式：

```csharp
DirectoryCopy.Sync(source, target, deleteExtra: true)
```

要求：

1. 默认不危险删除 output root 外文件。
2. 只删除由 Bukit 管理过的资产文件。
3. 不能误删用户手动放到 dist 的非 Bukit 文件，除非 clean 模式。
4. 删除记录写入 build report。

### 测试要求

```csharp
[Fact]
public async Task IncrementalBuild_ShouldDeleteRemovedAssets()
{
}
```

---

## BUG-015：`DirectoryCopy.SyncFile` 使用 size + LastWriteTime 判断，可能漏更新

### 问题描述

如果文件内容变化但大小和时间戳相同，sync 会跳过复制。

### 修复要求

增加 hash mode：

```yaml
build:
  assetHashMode: size-time | sha256
```

最低要求：

1. 默认可以保持 `size-time` 以兼顾性能。
2. CI / release / deterministic build 支持 `sha256`。
3. 测试中使用 `sha256` 确保内容变化被识别。

### 测试要求

```csharp
[Fact]
public async Task DirectoryCopy_ShouldCopy_WhenContentChangedButSizeAndTimeSame_InSha256Mode()
{
}
```

---

## BUG-016：多语言构建并发倍增

### 问题描述

多语言 variant 并发 + 页面渲染并发可能导致实际并发数变成 `languages * jobs`。

### 修复要求

引入全局并发预算：

```text
totalConcurrency = jobs
languageConcurrency = min(languages.Count, max(1, jobs / 2))
pageConcurrency = max(1, jobs / languageConcurrency)
```

或者更简单：

1. 多语言 outer loop 默认串行。
2. 页面渲染使用 `jobs`。
3. 显式参数允许开启语言并发。

### 测试要求

```csharp
[Fact]
public async Task MultiLanguageBuild_ShouldRespectGlobalConcurrencyBudget()
{
}
```

---

## BUG-017：process 插件继承宿主环境变量

### 问题描述

process 插件如果继承宿主环境，可能读取 `NOTION_TOKEN`、`GITHUB_TOKEN`、`OPENAI_API_KEY` 等 secrets。

### 修复要求

默认清空环境变量：

```csharp
startInfo.Environment.Clear();
```

只传 allowlist：

```text
BUKIT_PROJECT_ROOT
BUKIT_PLUGIN_NAME
BUKIT_PLUGIN_HOOK
BUKIT_OUTPUT_DIR
```

可选配置：

```yaml
plugins:
  allowEnvironment:
    - BUKIT_*
```

不允许默认传入：

```text
NOTION_TOKEN
GITHUB_TOKEN
OPENAI_API_KEY
AWS_SECRET_ACCESS_KEY
```

### 测试要求

```csharp
[Fact]
public async Task ProcessPlugin_ShouldNotReceiveHostSecretsByDefault()
{
}
```

---

## BUG-018：process 插件 stdout/stderr 无大小限制

### 问题描述

插件如果输出超大 stdout/stderr，宿主读取 `ReadToEndAsync` 可能导致内存暴涨。

### 修复要求

增加配置：

```yaml
plugins:
  maxStdoutBytes: 1048576
  maxStderrBytes: 1048576
```

要求：

1. 超限后 kill plugin process。
2. 返回明确错误。
3. 错误中不要包含完整超大输出。
4. 保留前 N KB 用于调试。

### 测试要求

```csharp
[Fact]
public async Task ProcessPlugin_ShouldFail_WhenStdoutExceedsLimit()
{
}
```

---

## BUG-019：插件输出文件未进入 manifest

### 问题描述

插件本次不再输出某个文件时，旧插件输出文件可能留在 dist。

### 修复要求

manifest 中记录 plugin outputs：

```json
{
  "pluginOutputs": [
    {
      "plugin": "xxx",
      "hook": "after-build",
      "path": "plugin/a.json",
      "hash": "..."
    }
  ]
}
```

构建结束：

1. 当前插件输出与上次 manifest 对比。
2. 删除上次存在但本次不存在的 plugin output。
3. 删除必须经过 path guard。

### 测试要求

```csharp
[Fact]
public async Task PluginOutputs_ShouldDeleteStaleFiles()
{
}
```

---

# 6. P2 Bug 详细任务

---

## BUG-020：非 HTML 静态文件复制绕过 `FileWriter`

### 问题描述

`FileWriter.WriteUtf8` 有 output root 保护，但静态文件复制等路径可能直接 `File.Copy`，导致安全策略不统一。

### 修复要求

新增统一输出文件系统：

```csharp
public interface IOutputFileSystem
{
    Task WriteTextAsync(string relativePath, string content, CancellationToken ct);
    Task CopyFileAsync(string sourcePath, string relativePath, CancellationToken ct);
    Task DeleteFileAsync(string relativePath, CancellationToken ct);
    string GetSafeFullPath(string relativePath);
}
```

要求：

1. 所有输出写入必须通过它。
2. 所有删除必须通过它。
3. 所有 relativePath 必须经过 canonicalization。
4. 拒绝 path traversal、absolute path、drive-qualified path。

---

## BUG-021：`build --clean` 直接删除 outputDir，缺少 marker 保护

### 问题描述

`--clean` 是破坏性操作，不能只依赖配置校验。应该使用 marker 防止误删错误目录。

### 修复要求

首次输出时创建：

```text
dist/.bukit-output-marker
```

clean 删除前：

1. outputDir 为空，可以删除 / 初始化。
2. outputDir 存在且包含 `.bukit-output-marker`，允许 clean。
3. outputDir 存在但没有 marker，拒绝 clean。
4. 提供明确错误提示。
5. 禁止删除 project root、home、filesystem root、`.git` 等目录。

### 测试要求

```csharp
[Fact]
public async Task Clean_ShouldRefuseDirectoryWithoutBukitMarker()
{
}
```

---

## BUG-022：编码后的路径未二次安全校验

### 问题描述

即使 `outputPathEncoding=slug` 或 `sanitize`，编码后的路径也必须再次验证，防止 `..`、空 segment、保留名等边界问题。

### 修复要求

路径生成流程必须是：

```text
raw route
  ↓
normalize
  ↓
encode
  ↓
validate encoded path
  ↓
write
```

### 测试要求

```csharp
[Theory]
[InlineData("..")]
[InlineData(".")]
[InlineData("CON")]
[InlineData("AUX")]
public void EncodedOutputPath_ShouldBeValidatedAgain(string slug)
{
}
```

---

## BUG-023：远程主题未锁 commit，构建不可复现

### 问题描述

远程主题如果每次 build 自动 `git pull`，同一个项目不同时间构建可能输出不同结果。

### 修复要求

新增 `bukit-theme.lock` 或等价机制：

```yaml
themes:
  theme-name:
    source: https://github.com/org/theme.git
    ref: v1.0.0
    commit: abc123
    resolvedAt: 2026-05-24T00:00:00Z
```

最低要求：

1. `build` 默认不自动更新远程主题。
2. `theme update` 才更新远程主题。
3. build 时校验当前 checkout commit 与 lock 一致。
4. 不一致时 fail 或 warning，默认建议 fail。
5. 支持显式参数 `--update-theme` 或独立命令更新。

---

# 7. 统一测试矩阵

## 7.1 路由安全 Fuzz 测试

```csharp
[Theory]
[InlineData("../evil")]
[InlineData("..\\evil")]
[InlineData("a/../../x")]
[InlineData("//evil.com")]
[InlineData("https://evil.com")]
[InlineData("javascript:alert(1)")]
[InlineData("CON")]
[InlineData("aux")]
[InlineData("")]
public void RouteSecurity_ShouldRejectUnsafeInputs(string value)
{
}
```

## 7.2 静态 HTML 测试

```text
static/
  index.html
  about.html
  about/team.html
  docs/index.html
  .html
```

验收：

```text
/index.html       -> /
about.html        -> /about/
about/team.html   -> /about/team/
docs/index.html   -> /docs/
.html             -> skipped with warning
```

## 7.3 主题继承测试

```text
parent theme assets + child theme assets
parent theme static + child theme static
parent exists, child missing
child exists, parent missing
project-level override exists
```

验收顺序：

```text
parent < child < project override
```

## 7.4 增量构建测试

场景：

1. 删除内容页。
2. 删除静态 HTML。
3. 删除 assets。
4. 删除 media。
5. 删除 plugin output。
6. 修改 parent layout。
7. 修改 user layout override。
8. 修改 theme config。
9. 修改 site config 中影响输出路径的字段。

验收：

```text
该重渲染的必须重渲染
该删除的必须删除
不该重渲染的尽量不重渲染
```

## 7.5 插件安全测试

场景：

1. 插件超时。
2. 插件 stdout 超限。
3. 插件 stderr 超限。
4. 插件输出非法 JSON。
5. 插件返回 unsafe outputPath。
6. 插件读取环境变量。
7. 插件本次不再输出旧文件。

---

# 8. 建议代码改造点

## 8.1 新增 `RouteSecurityValidator`

建议位置：

```text
src/Bukit.Routing/RouteSecurityValidator.cs
```

职责：

```text
ValidateInternalUrl
ValidateOutputPath
ValidateSlugSegment
ValidateNoTraversal
ValidateNoReservedName
ValidateNoControlChars
```

## 8.2 新增 `IOutputFileSystem`

建议位置：

```text
src/Bukit.Engine/Output/IOutputFileSystem.cs
src/Bukit.Engine/Output/SafeOutputFileSystem.cs
```

替换：

```text
FileWriter
DirectoryCopy direct writes
StaticFileService direct File.Copy
Plugin output direct writes
Clean delete
Stale cleanup
```

## 8.3 新增 `BuildManifest v2`

建议 manifest 保存：

```json
{
  "version": 2,
  "pages": [
    {
      "routeKey": "...",
      "url": "/blog/a/",
      "outputPath": "blog/a/index.html",
      "contentHash": "...",
      "templateHash": "...",
      "configHash": "..."
    }
  ],
  "assets": [
    {
      "path": "assets/main.css",
      "hash": "..."
    }
  ],
  "media": [
    {
      "path": "assets/uploads/a.png",
      "hash": "..."
    }
  ],
  "pluginOutputs": [
    {
      "plugin": "xxx",
      "path": "plugin/a.json",
      "hash": "..."
    }
  ]
}
```

## 8.4 新增 `BuildReport`

建议输出：

```text
dist/.bukit/build-report.json
```

包含：

```json
{
  "routesAdded": 0,
  "routesUpdated": 0,
  "routesDeleted": 0,
  "assetsCopied": 0,
  "assetsDeleted": 0,
  "mediaCopied": 0,
  "mediaDeleted": 0,
  "pluginOutputsWritten": 0,
  "pluginOutputsDeleted": 0,
  "warnings": [],
  "errors": []
}
```

---

# 9. 分阶段执行计划

## 阶段 1：P0 Build Correctness

优先修：

```text
BUG-001
BUG-002
BUG-003
BUG-004
BUG-005
BUG-006
BUG-007
```

完成后运行：

```bash
dotnet test bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
```

## 阶段 2：P0 Security / Supply Chain

优先修：

```text
BUG-008
BUG-009
BUG-010
BUG-011
BUG-012
```

完成后运行：

```bash
dotnet test bukit.slnx -c Release
```

重点看：

```text
Routing tests
Theme source tests
CLI build tests
```

## 阶段 3：P1 Incremental / Plugin Hardening

优先修：

```text
BUG-013
BUG-014
BUG-015
BUG-016
BUG-017
BUG-018
BUG-019
```

完成后运行：

```bash
dotnet test bukit.slnx -c Release
```

## 阶段 4：P2 Structural Hardening

优先修：

```text
BUG-020
BUG-021
BUG-022
BUG-023
```

完成后运行完整验证。

---

# 10. 提交建议

建议拆成多个 commit：

```text
fix(static): correct static html route url generation
fix(static): skip invalid empty html file names
fix(routes): include static html routes in route inventory
fix(theme): ensure child assets override parent assets
fix(build): delete stale pages during incremental builds
fix(media): recursively sync media cache
fix(theme): fail when remote theme ref checkout fails
fix(git): kill timed out git process
fix(routes): add route security validation
fix(plugins): restrict process plugin environment
fix(plugins): limit plugin stdout and stderr size
fix(build): introduce safe output filesystem
```

---

# 11. 最终验收标准

本轮修复完成后，必须满足：

1. `static/about.html` 输出 URL 为 `/about/`。
2. `static/about/team.html` 输出 URL 为 `/about/team/`。
3. `static/.html` 不会导致 build 崩溃。
4. 静态 HTML 与内容页 URL 冲突时 build fail。
5. child theme assets/static 覆盖 parent theme。
6. child theme 无 assets 时 parent assets 仍会输出。
7. 非 clean 增量构建会删除已移除内容页的旧 HTML。
8. 嵌套 media cache 文件会正确输出。
9. 远程主题 ref/tag checkout 失败时 build fail。
10. git 命令超时会 kill process tree。
11. unsafe slug 在 route validation 阶段失败。
12. `//evil.com`、`https://evil.com`、`javascript:` 等不能进入内部 route。
13. partial route override 可以只覆盖 outputPath。
14. 修改 parent/user layout 会触发增量重渲染。
15. 删除 assets/static/media/plugin output 后，dist 不残留旧文件。
16. process 插件默认无法读取宿主 secrets。
17. process 插件 stdout/stderr 超限会失败并终止。
18. 所有输出写入和删除统一经过 safe output path guard。
19. `--clean` 不会误删没有 Bukit marker 的目录。
20. build 结果可复现，不会在普通 build 中隐式 `git pull` 改变主题版本。

---

# 12. 后续建议：完成本轮后再做的升级

本轮修复完成后，再考虑以下增强：

1. `bukit-theme.lock`
2. `BuildManifest v2`
3. `BuildReport`
4. `Content Schema Layer`
5. `Section Component Schema`
6. `Theme Registry`
7. `Visual Regression Testing`
8. `BukitJalil Local App Integration`
9. `AI generated site.yaml / theme.yaml validation`
10. `SEO / GEO Audit Report`

---

# 13. 给 Codex 的收尾要求

```text
修复完成后，请输出：

1. 已修复的 Bug ID 列表
2. 每个 Bug 修改的主要文件
3. 新增或修改的测试文件
4. 尚未完成或需要人工确认的问题
5. dotnet test 结果
6. example starter build 结果
7. 是否影响公开 CLI 参数或配置格式
8. 是否存在 breaking change
```
