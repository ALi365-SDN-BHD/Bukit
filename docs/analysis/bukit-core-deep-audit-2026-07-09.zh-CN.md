# Bukit Core 全方位深度审计报告 - 2026-07-09

## 1. 审计范围与结论

- 分支：`1.0.8`
- 范围：git-tracked 主线 `src/Bukit-Core/`、Core 测试、`scripts/` 当前门禁、`guide/dev/` 当前文档、既有 `docs/analysis/` 报告。
- 排除：`guide-0.1/`、`guide-0.2/`、`scripts-0.1/`、`scripts-0.2/` 仅作为备份/历史目录，本报告不把它们作为官方行为或门禁依据。
- 审计性质：只读工程审计；本轮只新增本报告，不修复 Core 源码。

结论：当前 Core 架构边界、CLI 命令面、插件宿主隔离、Notion token 来源、dotfile/符号链接复制控制和安全回归脚本整体有较强的主线约束；未发现 P0。主要风险集中在“输出目录清理/写入”的同一信任边界：两个 P1 可导致配置或残留状态触发非预期递归删除，另有输出目录内部符号链接写入逃逸的 P2 风险。安全面另有媒体 URL 脱敏遗漏、GitHub Pages token askpass 落盘、IPv6 SSRF 私网判断不完整、dev server 暴露 `.bukit` 报告等问题。性能面主要风险是媒体下载整文件进内存和若干大站点同步 I/O 热点。

严重度汇总：

| Severity | Count | 摘要 |
|---|---:|---|
| P0 | 0 | 未发现可直接造成默认远程代码执行、默认凭据泄露或默认任意文件破坏的证据。 |
| P1 | 2 | 构建恢复自动清理绕过输出目录安全 guard；`bukit clean --config` 绕过输出目录 marker/unsafe guard。 |
| P2 | 4 | 输出目录内部符号链接可让写入/删除落到 output root 外；媒体失败日志泄露 URL；GitHub token askpass 落盘且 shell/batch 插值；SSRF guard IPv6 私网覆盖不完整。 |
| P3 | 2 | dev server 可服务 `.bukit` 报告；媒体下载默认 50MB 上限内整文件缓冲，存在大站点内存压力。 |

## 2. Core 当前事实图

### 2.1 Core 项目与职责

`bukit-core.slnx` 当前只包含 12 个 Core 项目，行 2-14 明确列出 `src/Bukit-Core` 下的项目。`guide/dev/architecture.md:3-18` 对这些项目职责的描述与源码引用矩阵一致。

| Project | 官方职责 | 主要引用 |
|---|---|---|
| `Bukit.Cli` | 用户命令入口、命令绑定、dev server、deploy provider | `Bukit.Cli.Shared`、`Bukit.Engine`、`Bukit.Config`、`Bukit.PluginHost`、`Bukit.Shared` |
| `Bukit.Cli.Shared` | CLI metadata/parser/help/config path resolver | `Bukit.Shared` |
| `Bukit.Config` | strict YAML、defaults、schema、validation | `Bukit.Shared`、`YamlDotNet` |
| `Bukit.Content` | Markdown/Notion、body stores、media localization | `Bukit.Engine.Abstractions`、`Bukit.Config`、`Bukit.Shared`、`Markdig` |
| `Bukit.Engine.Abstractions` | content/routing/plugin models | `Bukit.Config`、`Bukit.Shared` |
| `Bukit.Engine` | build orchestration、routing、rendering pipeline、plugins、reports | `Bukit.Engine.Abstractions`、`Bukit.Config`、`Bukit.Content`、`Bukit.Rendering`、`Bukit.Routing`、`Bukit.Shared` |
| `Bukit.Plugin.Abstractions` | external plugin config/manifest/protocol/runtime/security DTOs | 无 Core 项目引用 |
| `Bukit.PluginHost` | process plugin validation、protocol invocation、permissions、locking | `Bukit.Plugin.Abstractions`、`Bukit.Shared`、`YamlDotNet` |
| `Bukit.Rendering` | Scriban renderer/template models | `Bukit.Engine.Abstractions`、`Bukit.Config`、`Bukit.Shared`、`Bukit.Theme`、`Scriban` |
| `Bukit.Routing` | route generation/path safety | `Bukit.Engine.Abstractions`、`Bukit.Shared` |
| `Bukit.Shared` | diagnostics/exceptions/URL/path/Notion helpers | 无 Core 项目引用 |
| `Bukit.Theme` | theme manifest/components/sections/tokens/catalog/doctor helpers | `Bukit.Config`、`Bukit.Engine.Abstractions`、`Bukit.Shared`、`YamlDotNet` |

质量规范事实：

- `Directory.Build.props:3-7` 启用 nullable、latest analysis、code style in build、warnings as errors、禁用 shared compilation。
- `Directory.Packages.props:3-20` 使用 central package versions，关键包包括 `YamlDotNet 16.3.0`、`Markdig 0.38.0`、`Scriban 7.2.5`、`NetArchTest.Rules 1.3.2`。
- `tests/Bukit.Architecture.Tests/DependencyMatrixTests.cs:23-160` 用 NetArchTest 固化 Shared/Config/Routing/Content/Rendering/Engine/Cli/Labs 依赖边界。
- `tests/Bukit.Architecture.Tests/DependencyMatrixTests.cs:179-260` 固化 `InternalsVisibleTo` 白名单、Core internal section plugin 和 template contributor 不外露。

### 2.2 CLI 命令面

`BukitCliSpecs.CreateRegistry()` 当前注册 12 个 Core 命令：`build`、`doctor`、`config`、`preview`、`dev`、`clean`、`version`、`completion`、`seo`、`geo`、`publish`、`deploy`，源码见 `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs:7-210`。`tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs:13-34` 用白名单测试锁定该命令面。

命令边界事实：

- Core CLI 不包含 `CloneCommand`、`ImportCommand`、`NotionCommand`、`IntentCommand`、`VisualCommand`、`WebhookCommand`、`DataCommand`、`ThemeCommand`、`PluginCommand` 等实验命令，见 `CoreBoundaryTests.cs:36-60`。
- `deploy` 仅支持显式 `github-pages` provider，`DeployConfig` 不暴露 `Options` bag，见 `CoreBoundaryTests.cs:129-146`。
- `seo diff`/`publish diff` 不暴露 `--allow-cross-schema`，见 `CoreBoundaryTests.cs:195-204`。

### 2.3 Build/Content/Rendering/Theme/PluginHost/Deploy/Dev Server 数据流

构建主链路：

1. `Program` 绑定 CLI 后进入 `BuildCommand`。
2. `BuildCommand` 加载 `site.yaml`，应用 CLI overrides 后调用 `SiteEngine.BuildAsync`。
3. `SiteEngine.BuildCoreAsync` 先走 `BuildPlanner.Plan`，再执行 `ContentPipeline.ExecuteAsync`，见 `src/Bukit-Core/Bukit.Engine/SiteEngine.cs:95-107`。
4. 单语言构建在 `SiteEngine.cs:115-130` 写 metrics、build/security reports、输出 marker 和 completed state。
5. 多语言构建使用并行 language jobs，随后写 merged SEO/report/marker/completed state，见 `SiteEngine.cs:210-235`。

内容源事实：

- Markdown 源使用 `Directory.GetFiles(..., "*.md", AllDirectories)` 递归枚举、排序、可按 include paths/globs/maxItems 过滤，逐文件异步读取并生成 `RawContentDocument`，见 `src/Bukit-Core/Bukit.Content/Markdown/MarkdownFolderProvider.cs:28-171`。
- Markdown HTML pipeline 使用 Markdig 并显式 `.DisableHtml()`，见 `src/Bukit-Core/Bukit.Content/Markdown/BasicMarkdownToHtml.cs:10-19`。
- Notion token 只从环境变量 `NOTION_TOKEN` 读取，`EnvironmentHelper.cs:5-12`、`ContentProviderFactory.cs:122-128` 和 `ProviderValidators.cs:106-112` 均显示配置文件不能内联 token。
- Notion 客户端有 429 retry、`Retry-After` 处理、maxRps throttle 和统计，见 `src/Bukit-Core/Bukit.Content/Notion/NotionApiClient.cs:74-130`。

渲染事实：

- Scriban renderer 对 template 文件做 mtime/length/content hash cache，`MaxLayoutDepth = 10`，见 `src/Bukit-Core/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs:15-31`、`:80-95`、`:157-187`。
- Scriban 模板是站点/主题作者控制的 HTML 输出面；Core 不把模板输出当作不可信用户输入自动 sanitize。Notion block renderer 在链接、iframe、code 等位置普遍使用 `WebUtility.HtmlEncode`。

插件宿主事实：

- `SystemProcessRunner` 使用 `ProcessStartInfo.ArgumentList`、`UseShellExecute=false`、清空继承环境、按 manifest/environment 注入变量，见 `src/Bukit-Core/Bukit.PluginHost/SystemProcessRunner.cs:83-116`。
- 插件进程有 timeout、stdout/stderr byte limit、超限 kill，见 `SystemProcessRunner.cs:28-80`、`:161-207`。
- `PluginPathValidator` 要求 plugin source 为 `plugins/<id>`，entry 必须 stay inside plugin dir 且 real path 不进入 `.bukit/`，见 `src/Bukit-Core/Bukit.PluginHost/PluginPathValidator.cs:15-38`、`:53-87`。
- `PluginPermissionEvaluator` 禁止 environment wildcard，验证 network/filesystem/environment 权限子集，见 `src/Bukit-Core/Bukit.PluginHost/PluginPermissionEvaluator.cs:24-37`、`:55-79`。
- `PluginExecutionReporter` 对 stderr、diagnostics、artifact descriptions 和 environment 做 secret masking，见 `src/Bukit-Core/Bukit.PluginHost/PluginExecutionReporter.cs:69-88`；测试 `tests/Bukit.PluginHost.Tests/PluginLockAndReportTests.cs:70-130` 证明 `NOTION_TOKEN` 和 stderr secret 被替换为 `***`。

dev server 事实：

- `bukit dev` 默认 host 为 `localhost`，非 loopback 需要 `--allow-lan` 或 `--public`，见 `src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs:13-23`、`:36-45`。
- `DevRequestHandler` 先用 `DevPathGuard.TryResolveWithinRoot` 做路径约束，然后服务 outputDir 内任意存在文件，包括 `.json`、`.map`、`.pdf`，见 `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs:31-74`、`:140-160`。

## 3. Findings

### F-01 P1 - 构建恢复自动清理绕过输出目录 marker/unsafe guard

影响：当 `build.clean: false` 且 output 目录内存在 `.bukit-build-state.json` 且状态为 `started` 时，`BuildPlanner` 会递归删除整个 outputDir，但没有调用与 `build.clean: true` 相同的安全检查。由于配置层只拒绝绝对路径和 `..`，不拒绝 `build.output: "."` 或 `.git`，攻击者或误配置可用残留 state 文件触发删除项目根目录、`.git` 或其他非 Bukit output 目录。

证据：

- `src/Bukit-Core/Bukit.Engine/BuildPlanner.cs:63-67`：`build.clean: true` 删除前调用 `EnsureOutputDirectoryCanBeCleaned`。
- `src/Bukit-Core/Bukit.Engine/BuildPlanner.cs:69-73`：`!build.clean && HasIncompleteBuild(outputDir)` 直接 `Directory.Delete(outputDir, recursive: true)`，没有 marker/root/home/rootfs/`.git` guard。
- `src/Bukit-Core/Bukit.Engine/BuildPlanner.cs:83-108`：guard 会拒绝 rootDir、home、filesystem root、`.git` 和缺少 `.bukit-output-marker` 的非空目录。
- `src/Bukit-Core/Bukit.Engine/BuildRecoveryTracker.cs:7-21`：incomplete build 只由 outputDir 下 `.bukit-build-state.json` 的 `status: started` 判定。
- `src/Bukit-Core/Bukit.Config/ConfigValidator.cs:117-123`、`src/Bukit-Core/Bukit.Config/ProviderValidators.cs:166-197`：`build.output` 仅要求非空、相对路径、无 `..`，未拒绝 `.`/`.git`。
- `src/Bukit-Core/Bukit.Engine/SiteEngine.cs:125-130`、`:231-235`：`.bukit-output-marker` 和 completed state 只在构建尾部写入。

复现/验证方式：

1. 构造临时站点，`site.yaml` 设置 `build.output: "."` 和 `build.clean: false`。
2. 在站点根写入 `.bukit-build-state.json`，内容为 `{"status":"started"}`。
3. 调用 `SiteEngine.BuildAsync` 或 `bukit build --no-clean`。
4. 预期安全行为：拒绝清理 unsafe output；当前行为：进入 `Directory.Delete(outputDir, recursive: true)`。

已有测试覆盖：不足。`tests/Bukit.Engine.Tests/BuildPlannerCleanErrorTests.cs:10-89` 只覆盖 `build.clean: true` 的 marker/unsafe 拒绝；`tests/Bukit.Engine.Tests/BuildRecoveryTrackerTests.cs` 只覆盖 state 判断，不覆盖 autoClean 的安全 guard。

建议修复方向：

- 将 `EnsureOutputDirectoryCanBeCleaned(rootDir, outputDir)` 提取为共享 output cleanup guard，并在 recovery autoClean 分支同样调用。
- 或将 recovery cleanup 限定为带 `.bukit-output-marker` 且不为 root/home/rootfs/`.git` 的目录。
- 增加回归测试：`build.clean=false + incomplete state + output "."/.git/non-marker non-empty dir` 必须抛 `BuildOutputUnsafe` 或 `BuildOutputNoMarker`。

### F-02 P1 - `bukit clean --config` 绕过 marker/unsafe guard 直接递归删除配置输出目录

影响：`bukit clean --config site.yaml` 会直接读取 `config.Build.Output` 并递归删除该目录，不复用 `BuildPlanner` 的 output marker/unsafe guard。若配置为 `build.output: "."`、`.git` 或其他非 Bukit 输出目录，命令可删除项目根目录或仓库元数据。该命令是显式 clean，但当前行为和 Core 已建立的“非 marker 目录不清理”安全契约不一致。

证据：

- `src/Bukit-Core/Bukit.Cli/Commands/CleanCommand.cs:17-23`：`--config`/`--site` 分支从配置计算 `outputDir`。
- `src/Bukit-Core/Bukit.Cli/Commands/CleanCommand.cs:37-40`：存在即 `Directory.Delete(outputDir, recursive: true)`。
- `src/Bukit-Core/Bukit.Cli/Commands/CleanCommand.cs:24-35`：只有无 config 的 `--dir` 分支验证目录必须在 cwd 内。
- `src/Bukit-Core/Bukit.Engine/BuildPlanner.cs:83-108`：已有更完整的 output cleanup guard，但 `CleanCommand` 未复用。

复现/验证方式：

1. 构造临时站点，`site.yaml` 设置 `build.output: ".git"` 或 `build.output: "."`。
2. 创建对应目录/文件。
3. 调用 `CleanCommand.RunAsync(CreateCommand("clean", ["--config", siteYaml]))`。
4. 预期安全行为：拒绝 unsafe output 或缺 marker 的非空目录；当前行为：直接删除。

已有测试覆盖：不足。`tests/Bukit.Cli.Tests/CleanCommandTests.cs:42-58` 只覆盖正常 `dist` 和不存在目录；`:60-72` 只覆盖无 config 的 `--dir` 路径逃逸。

建议修复方向：

- 中央化 `OutputDirectorySafety`/`BuildOutputCleaner`，让 `BuildPlanner`、`CleanCommand`、doctor safety check 使用同一实现。
- `clean --config` 默认也要求 `.bukit-output-marker`，并拒绝 root/home/rootfs/`.git`。
- 增加 `CleanCommand` 配置分支的 unsafe/non-marker 回归测试。

### F-03 P2 - 输出目录内部既有符号链接可让写入/删除落到 output root 外

影响：Core 的常规 output path resolver 做字符串前缀检查，但不解析 output root 内部已经存在的符号链接。如果 outputDir 在不清理或增量构建场景下已包含 `assets -> /outside/path` 之类符号链接，后续 `FileWriter.WriteUtf8(outputDir, "assets/x")` 或 `SafeOutputFileSystem.DeleteFileAsync("assets/x")` 的字符串路径仍在 output root 下，实际文件系统操作会跟随符号链接写入/删除 output root 外部。该风险需要 outputDir 已被污染或由前序构建残留符号链接，故低于 P1。

证据：

- `src/Bukit-Core/Bukit.Engine/Output/SafePathResolver.cs:7-18`：只用 `Path.GetFullPath(Path.Combine(outputRoot, relativePath))` 和 `StartsWith(safeRoot)` 判定，没有解析 existing symlink segments。
- `src/Bukit-Core/Bukit.Engine/Output/SafeOutputFileSystem.cs:54-57`：写/删前调用 `SafePathResolver`。
- `src/Bukit-Core/Bukit.Engine/FileWriter.cs:16-31`：大量报告/渲染输出使用同一 resolver 后写文件。
- `src/Bukit-Core/Bukit.Shared/PathUtils.cs:5-20` 已有解析 symlink 的 helper，但 output resolver 未使用。
- `tests/Bukit.Engine.Tests/SafeOutputFileSystemTests.cs:11-67` 覆盖 `../`、absolute path、reserved device、正常写删；没有 output 内部 symlink 测试。

复现/验证方式：

1. 创建 outputDir 和外部目录。
2. 在 outputDir 内创建符号链接 `assets -> 外部目录`。
3. 调用 `new SafeOutputFileSystem(outputDir).WriteTextAsync("assets/pwn.txt", "x", ct)`。
4. 预期安全行为：拒绝经过 outputDir 内部 symlink 的写入；当前 resolver 会认为字符串路径在 outputDir 下。

已有测试覆盖：不足。`DirectoryCopy` 对源符号链接有跳过/realpath 限制，见 `src/Bukit-Core/Bukit.Engine/DirectoryCopy.cs:102-113`、`:125-135` 和 `tests/Bukit.Engine.Tests/DirectoryCopyFollowSymlinksTests.cs:47-109`；但 output 目的路径 resolver 没有同类测试。

建议修复方向：

- output path resolve 时逐段解析已存在路径，拒绝任何会把最终父目录解析到 output root 外的符号链接。
- 写入前确保父目录不存在符号链接，或清理/重建 outputDir 时移除符号链接。
- 增加 Linux/macOS symlink 回归测试；Windows 可按平台跳过。

### F-04 P2 - 媒体下载异常路径会把未脱敏 URL 写入日志和失败汇总

影响：媒体源 URL 可能包含 Notion signed URL、CMS 查询 token 或短期访问签名。常规 skip/失败路径大量使用 `UrlRedactor.Redact`，但异常 catch 分支打印原始 `source`，且失败汇总会再次原样输出 `MediaFailure.SourceUrl`，导致日志泄露签名 URL。

证据：

- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:92-110`、`:186-213`：多数日志使用 `UrlRedactor.Redact(source)`。
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:238-244`：异常分支写 `event=media.download_error source={source}`，未 redacted，并把 `ex.Message` 记录到 failure。
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:269-272`：`MediaFailure` 保存原始 `sourceUrl`。
- `src/Bukit-Core/Bukit.Engine/ContentProviderFactory.cs:100-104`：localize summary 中将 `f.SourceUrl` 原样输出。

复现/验证方式：

1. 配置 `content.media.downloadToLocal: true`，源 URL 使用 `https://example.test/img.jpg?token=secret`。
2. 使用会抛异常的 `HttpClient`/handler 或不可达地址触发 `media.download_error`。
3. 捕获 logger 输出，检查 `token=secret` 是否出现在日志。

已有测试覆盖：不足。当前测试覆盖私网阻断和部分 redaction 路径，例如 `tests/Bukit.Content.Tests/ImageAssetLocalizerTests.cs:442-514` 覆盖 SSRF/private ranges；未覆盖 exception catch 和 summary 的 URL redaction。

建议修复方向：

- 在异常分支使用 `UrlRedactor.Redact(source)`。
- `RecordFailure` 可保存 redacted URL 或把 `MediaFailure` 拆成 internal raw + report/log redacted。
- `ContentProviderFactory` summary 输出 redacted source/reason，并测试异常与 summary 双路径。

### F-05 P2 - GitHub Pages askpass 将 `GITHUB_TOKEN` 作为 shell/batch 源码落盘并直接插值

影响：deploy provider 当前创建临时 askpass 脚本，脚本正文包含 token。测试已经覆盖错误消息脱敏和清理，但 token 在部署期间以可读脚本内容落盘；同时 Unix 脚本使用 `echo "{token}"`，Windows 使用 `@echo {token}`，没有 shell/batch 安全转义。若 token 值包含引号、命令替换、换行或命令连接符，git 调用 askpass 时可能输出错误 token，甚至在 Unix shell/batch 解释器中执行非预期片段。虽然 token 通常来自受信环境变量，secret-handling 代码仍应避免这种脆弱模式。

证据：

- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs:29-33`：读取 `GITHUB_TOKEN`。
- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.cs:69-78`：创建 temp dir 和 askpass script。
- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Auth.cs:5-18`：Windows 写 `@echo {token}`；Unix 写 `#!/bin/sh\necho "{token}"`。
- `src/Bukit-Core/Bukit.Cli/Deploy/GitHubPagesDeployProvider.Git.cs:199-221`：通过 `GIT_ASKPASS` 交给 git。
- `tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs:295-345`：覆盖 askpass path/token 不出现在错误、失败/成功后删除。
- `tests/Bukit.Cli.Tests/GitHubPagesDeployProviderTests.cs:644-660`：测试明确断言 askpass 文件内容包含 `secret-token`。

复现/验证方式：

1. 调用 private `CreateAskpassScript(tempDir, "abc\"$(touch /tmp/bukit-pwn)\"")` 或含 `&`/换行的 Windows token。
2. 运行生成脚本，观察输出是否保持原 token，且无命令执行副作用。
3. 当前实现对 shell/batch metacharacters 没有转义。

已有测试覆盖：部分覆盖。已有测试覆盖清理和错误脱敏；没有覆盖 token metacharacter、脚本不含明文 token、或 helper 从环境安全读取 token。

建议修复方向：

- 避免把 token literal 写入脚本。可写固定 helper，从专用环境变量读取 token，或用小型可执行 helper/`dotnet` helper 输出 env var。
- 若必须写脚本，至少按平台做严格单引号/批处理转义，并限制文件权限。
- 增加 token 包含 `"`, `'`, `$()`, `` ` ``, `&`, `|`, newline 的回归测试。

### F-06 P2 - SSRF guard 未覆盖 IPv6 ULA/unspecified/multicast 等私有或保留地址

影响：媒体下载和 SEO external audit 使用 `SsrfGuard.SsrfSafeConnectAsync` 阻断私网连接；当前 IPv4 常见私网/metadata 地址覆盖较好，但 IPv6 仅阻断 link-local 和 site-local，未阻断 unique local `fc00::/7`、unspecified `::`、multicast `ff00::/8` 等保留/非公网地址。攻击者若能影响媒体 URL 或外链目标，并且运行环境 IPv6 可达，可能绕过 SSRF 预期控制。

证据：

- `src/Bukit-Core/Bukit.Shared/SsrfGuard.cs:14-18`：连接时选择第一个非 `IsPrivateAddress` 地址。
- `src/Bukit-Core/Bukit.Shared/SsrfGuard.cs:51-84`：IPv4 阻断 `0/8`、`10/8`、`127/8`、`169.254/16`、`172.16/12`、`192.168/16`；IPv6 只返回 `IsIPv6LinkLocal || IsIPv6SiteLocal`。
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:52-56`：媒体下载 handler 使用 SSRF guard。
- `src/Bukit-Core/Bukit.Cli/Commands/SeoExternalAuditor.cs:14`：SEO external audit 使用 SSRF guard。
- `tests/Bukit.Content.Tests/ImageAssetLocalizerTests.cs:500-514`：测试覆盖 IPv4 private、IPv4-mapped IPv6、`fe80::1`、公网 IPv6；没有覆盖 `fc00::1`、`fd00::1`、`::`、`ff00::1`。

复现/验证方式：

1. 直接断言 `SsrfGuard.IsPrivateAddress(IPAddress.Parse("fc00::1"))`、`fd00::1`、`::`、`ff00::1`。
2. 预期安全行为：返回 `true`；当前实现只基于 link-local/site-local，可能返回 `false`。

已有测试覆盖：不足。当前覆盖缺少 IPv6 unique local/reserved/multicast。

建议修复方向：

- 扩展 `IsPrivateAddress`：阻断 IPv6 unique local `fc00::/7`、unspecified、multicast、IPv4-compatible/IPv4-mapped 后的保留段、documentation/reserved ranges（按项目风险决定是否阻断）。
- 增加上述 IPv6 地址回归测试。

### F-07 P3 - dev server 可直接服务输出目录下 `.bukit` 报告

影响：Core 构建会把 `build-report.json`、`security-report.json`、`routes.json`、`assets.json`、`seo-report.json`、`publish-audit-report.json` 等写到 outputDir 下 `.bukit/`。`bukit dev` 请求处理器只校验 path stay within outputDir，不排除 `.bukit`。默认仅 `localhost`，风险较低；但用户使用 `--allow-lan`/`--public` 时，局域网访问者可读取构建报告，报告可能包含项目根路径、输出路径、路由、资产 hash、security gate 细节和诊断信息。

证据：

- `src/Bukit-Core/Bukit.Engine/BuildReporter.cs:14-24`：报告目录名是 `.bukit`。
- `src/Bukit-Core/Bukit.Engine/BuildReporter.cs:36-56`：写 security/build/routes/assets/incremental/release/artifact/digest reports。
- `src/Bukit-Core/Bukit.Engine/SeoAuditReportWriter.cs:78-79`、`:134`：写 `.bukit/seo-report.json`、publish/geo reports。
- `tests/Bukit.Engine.Tests/SiteEngineIntegrationTests.cs:316-319`：集成测试证明 `dist/.bukit/build-report.json` 和 `security-report.json` 存在。
- `src/Bukit-Core/Bukit.Cli/Commands/Dev/DevRequestHandler.cs:31-74`：只做 output root path guard，然后服务文件。
- `src/Bukit-Core/Bukit.Cli/Commands/DevCommand.cs:36-45`：非 loopback 需要显式 allow，但支持 LAN 暴露。

复现/验证方式：

1. 构建任意站点，确认 `dist/.bukit/security-report.json` 存在。
2. 运行 `bukit dev --no-watch`。
3. 请求 `http://localhost:35729/.bukit/security-report.json`。
4. 当前行为应返回 report JSON。

已有测试覆盖：不足。`tests/Bukit.Cli.Tests/DevCommandTests.cs` 覆盖静态输出服务和 LAN policy；未覆盖 `.bukit` deny policy。

建议修复方向：

- dev/preview server 默认拒绝 `/.bukit/`、`.bukit-build-state.json`、`.bukit-output-marker`，必要时提供显式 debug flag。
- 增加请求 `/.bukit/security-report.json` 返回 404/403 的测试。

### F-08 P3 - 媒体下载默认 50MB 上限内整文件进内存，存在大站点内存压力

影响：`ImageAssetLocalizer` 对单个媒体设置默认 50MB 上限，并用 `_inflight` 去重同一 URL；但读取实现会将每个成功响应完整写入 `MemoryStream` 后 `ToArray()`，并发下载多个大图片时会产生较高瞬时内存占用。该问题受 `maxFileSizeBytes`、下载并发和站点媒体规模影响，属于可扩展性风险。

证据：

- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:12`：默认最大文件大小 50MB。
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:38-40`、`:139-148`：有 cache/inflight 去重。
- `src/Bukit-Core/Bukit.Content/Media/ImageAssetLocalizer.cs:275-299`：`ReadWithLimitAsync` 使用 `MemoryStream` 累积完整响应并返回 `ms.ToArray()`。

复现/验证方式：

1. 构造多个不同 URL，每个响应接近默认 50MB。
2. 开启 media localization 并提高并发。
3. 观察峰值内存与并发数近似线性增长。

已有测试覆盖：不足。现有测试覆盖失败、SSRF、内容类型、大小限制等行为；没有压力/峰值内存回归。

建议修复方向：

- 下载时流式写临时文件，同时按字节计数限制大小，成功后原子 rename 到目标文件。
- 对全局 media 下载并发设置硬上限，并在 metrics 中输出下载峰值/失败计数。

## 4. 业务逻辑审计摘要

### 配置加载

- 配置加载和验证采用 strict YAML/defaults/schema 组合，`ConfigValidator` 对 required fields、枚举、deploy provider、theme path、build output traversal 等做集中检查。
- 风险集中在 `build.output` 对 `.`/`.git` 的允许与清理命令/恢复清理 guard 不一致，见 F-01/F-02。

### Markdown/Notion 内容源

- Markdown provider 使用 Markdig `.DisableHtml()`，降低 Markdown 直出 HTML 风险。
- Notion token 必须来自环境，缺 token 时 `ProviderValidators` 与 `ContentProviderFactory` 均拒绝。
- Notion API 有 maxRps、Retry-After、exponential retry 和 stats；适合普通站点，但超大数据库仍需要更明确的分页/缓存指标纳入报告输出。
- 媒体本地化有 SSRF guard、私网阻断、内容类型过滤、大小限制、重试和 URL 去重；但异常日志 redaction 与 IPv6 SSRF 私网覆盖存在缺口，见 F-04/F-06。

### 路由、list route、taxonomy

- `RouteSecurityValidator` 拒绝 absolute output path、drive-qualified path、`.`/`..`、control chars、reserved Windows device name、protocol-relative/absolute external internal URL，见 `src/Bukit-Core/Bukit.Routing/RouteSecurityValidator.cs:14-118`。
- Build reporter security gate 会重新验证 route URL、route output path、slug 和 plugin output path，见 `src/Bukit-Core/Bukit.Engine/BuildReporterSecurity.cs:182-245`。
- 当前未发现 list route/taxonomy 默认穿越输出目录的证据；风险主要转移到 output resolver 对既有 symlink 的处理，见 F-03。

### 主题 manifest 与静态资源

- `guide/dev/architecture.md:40-44` 明确 Core extension points 与 external process plugin protocol 分离。
- Core 架构测试拒绝 remote theme source tooling 和 site-level theme extends，见 `CoreBoundaryTests.cs:148-167`。
- `DirectoryCopy` 默认跳过 dotfile/sensitive dotfiles 和 symlink；`StaticFileService` 默认跳过 dot-prefixed segment，`publishDotFiles=true` 时仍拒绝 `.env`、`.git`、`.npmrc`、private key extension 等敏感文件，见 `DirectoryCopy.cs:19-30`、`StaticFileService.cs:11-18`、`:82-100`、`:123-148`。
- `tests/Bukit.Engine.Tests/StaticFileServiceTests.cs:93-126` 覆盖 `publishDotFiles=true` 时敏感 dotfile 仍拒绝。

### Scriban 渲染

- Renderer 有模板缓存、layout 深度限制和 parse error wrapping。
- 站点模板是受信作者控制面；Core 不应承诺对任意模板输出做 XSS sanitize。文档应继续明确“内容 Markdown 禁 HTML，模板/主题输出由站点维护者负责”的信任模型。

### SEO/GEO/publish audit

- `seo`、`geo`、`publish` 命令面已在 CLI registry 中注册，并由 architecture tests 固定。
- 报告写在 `.bukit/` 下并参与 build/report security gate。
- dev server 默认服务 `.bukit` 报告是本轮发现的低危暴露面，见 F-07。

### 增量构建与 deploy

- 增量 manifest 删除 stale 文件时通过 `SafeOutputFileSystem` 做相对路径校验，见 `src/Bukit-Core/Bukit.Engine/Incremental/BuildManifestTracker.cs:146-170`。
- GitHub Pages deploy 有 provider whitelist、branch/CNAME 归一校验、错误脱敏和临时目录清理测试；token askpass 落盘/插值是主要剩余风险，见 F-05。

## 5. 安全控制覆盖矩阵

| 控制面 | 当前状态 | 证据 |
|---|---|---|
| 路径穿越输出 | 有 `RouteSecurityValidator` + `SafeOutputFileSystem`，但 output 内部 symlink 未覆盖 | `RouteSecurityValidator.cs:90-118`、`SafePathResolver.cs:7-18`、F-03 |
| 输出目录清理 | `build.clean=true` 有 marker/unsafe guard；recovery 和 `clean --config` 绕过 | `BuildPlanner.cs:63-108`、`CleanCommand.cs:17-40`、F-01/F-02 |
| dev server 路径 | 有 output root guard 和 LAN opt-in；未 deny `.bukit` | `DevRequestHandler.cs:31-74`、`DevCommand.cs:36-45`、F-07 |
| SSRF/HTTP 下载 | 有 ConnectCallback + IPv4 private block；IPv6 private/reserved 覆盖不足 | `SsrfGuard.cs:51-84`、F-06 |
| Notion token | token 仅环境变量，不进 config | `EnvironmentHelper.cs:5-12`、`ProviderValidators.cs:106-112` |
| GitHub token | 错误脱敏与清理有测试；askpass literal 落盘 | `GitHubPagesDeployProvider.Auth.cs:5-18`、F-05 |
| 外部进程参数 | 插件使用 `ArgumentList`，不走 shell，清空环境 | `SystemProcessRunner.cs:83-116` |
| 插件权限 | network/filesystem/environment 子集校验，环境 wildcard 禁止 | `PluginPermissionEvaluator.cs:24-37` |
| secret masking | plugin execution report masking 有测试 | `PluginExecutionReporter.cs:69-88`、`PluginLockAndReportTests.cs:117-129` |
| dotfile/敏感文件复制 | 默认拒绝，`publishDotFiles` 也保留 denylist | `StaticFileService.cs:82-100`、`:123-148` |
| 模板/HTML 输出 | Markdown 禁 HTML；模板为受信作者输出面 | `BasicMarkdownToHtml.cs:10-19`、`ScribanTemplateRenderer.cs:98-110` |

## 6. 性能与可扩展性审计

已具备的性能控制：

- Scriban 模板按 mtime/length/content hash cache，见 `ScribanTemplateRenderer.cs:157-187`。
- Body cache 使用 `ConcurrentDictionary<string, Lazy<Task<ContentBody>>>` 和 LRU trimming，见 `src/Bukit-Core/Bukit.Content/BodyCacheDecorator.cs:12-31`、`:41-86`、`:88-113`。
- Notion API 有 maxRps throttle、Retry-After 和 retry stats，见 `NotionApiClient.cs:74-130`。
- Media localizer 使用 `_cache` 和 `_inflight` 去重相同 URL，见 `ImageAssetLocalizer.cs:38-40`、`:139-148`。
- 多语言构建有 `Parallel.ForEachAsync` 风格的 language job 并发，见 `SiteEngine.cs:210-221`。

主要扩展性风险：

- F-08：媒体下载整文件进内存，多个大文件并发时峰值内存较高。
- Markdown provider 递归枚举后 `ToArray()`，再逐文件读取，超大内容树会有启动延迟；当前有 `MaxItems`、include paths/globs，但没有 streaming load。
- `DoctorCommand`、SEO/publish diff、external audit 等诊断路径有多处同步 `File.ReadAllText`/`Directory.GetFiles`，对普通站点可接受；对超大站点应优先在 metrics 中暴露耗时阶段，再决定是否改 streaming。

## 7. 验证计划与结果

验证时间：2026-07-09，本地工作树 `1.0.8`。除依赖漏洞查询第一次被沙箱拦截外，其余命令均在当前工作树直接执行；依赖漏洞查询按权限规则提权后完成。

| 验证项 | 命令 | 当前结果 |
|---|---|---|
| 静态证据 | `git ls-files src/Bukit-Core tests/Bukit.Architecture.Tests scripts guide/dev docs/analysis` | 已执行；确认当前报告范围来自 git-tracked 主线文件。 |
| 架构与规范 | `dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release` | **失败**：55 tests 中 46 passed、9 failed。失败均来自现有测试引用当前主线不存在的文件：`scripts/checks/coverage.sh`、`scripts/checks/coverage-baseline-schema.sh`、`.github/workflows/ci.yml`。`git ls-files` 对这些路径无输出。 |
| Core 回归 | `bash scripts/checks/core-tests.sh Release` | **通过**：11 个 Core test project 全绿，合计 3281 tests passed。 |
| 安全回归 | `bash scripts/security/security-regression.sh Release` | **通过**：CLI/Content/Engine/PluginHost/Routing 安全回归共 277 tests passed。 |
| 主线快门禁 | `bash scripts/gates/ci-fast.sh Release` | **通过**：docs consistency、active workflow boundary、config docs contract、CLI docs sync、skills schema、README sync、Core CLI contract 均 OK。 |
| 主线全门禁 | `bash scripts/gates/ci-full.sh Release` | **通过**：复跑 fast gate，并通过 Core test projects。 |
| 依赖安全 | `dotnet list bukit-core.slnx package --vulnerable --include-transitive` | **通过**：使用 `https://api.nuget.org/v3/index.json`；12 个 Core project 均显示 no vulnerable packages。第一次沙箱执行因用户 NuGet http-cache 写权限被拒，提权后成功。 |
| 报告校验 | `git diff --check` + 报告内容复核 | **通过**：`git diff --check` 无输出；新增报告无 trailing whitespace，文件以 newline 结尾。旧目录名只在本报告“排除范围”中出现，未作为官方依据。 |

## 8. 后续修复顺序建议

1. 先修 F-01/F-02：把输出目录 cleanup guard 中央化，消除 destructive delete 的两条绕过路径。
2. 再修 F-03：让 output resolver 解析既有 symlink 或拒绝 output 内部 symlink 父目录。
3. 再修 F-04/F-06：补齐媒体/外链安全边界的 redaction 与 IPv6 SSRF 回归。
4. 再修 F-05：替换 GitHub Pages askpass secret handling。
5. 最后处理 F-07/F-08：dev server 隐藏报告路径、媒体下载改 streaming。

后续修复应按单个 finding 串行执行；每项修复后至少跑对应单元测试、`scripts/security/security-regression.sh Release` 中相关片段，以及最终 diff 审计。
