高优先级问题 / 潜在 Bug
P0-1：Linux / macOS 下输出路径安全判断使用 OrdinalIgnoreCase，存在大小写路径绕过风险

FileWriter.GetSafeFullPath 用于防止输出路径逃逸：

if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))

代码位置显示它把 outputRoot + relativePath 做 GetFullPath 后，再用 StartsWith(... OrdinalIgnoreCase) 判断是否仍在输出目录下。

SafeOutputFileSystem.GetSafeFullPath 也使用相同模式：

if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))

风险说明

在 Windows 上忽略大小写合理，但在 Linux/macOS 上文件系统通常大小写敏感。假设：

outputRoot = /tmp/out
relativePath = ../OUT/evil.html

Path.GetFullPath 后得到：

/tmp/OUT/evil.html

因为当前判断使用 OrdinalIgnoreCase，/tmp/OUT/ 会被认为以 /tmp/out/ 开头，从而误判为安全路径。

影响

这类问题可能影响：

插件输出文件追踪；
渲染输出；
增量构建删除 stale 文件；
静态资源同步；
任何调用 FileWriter 或 SafeOutputFileSystem 的地方。
修复建议

封装一个跨平台路径比较方法：

private static StringComparison PathComparison =>
    OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

并在 FileWriter、SafeOutputFileSystem、BuildPlanner.EnsureOutputDirectoryCanBeCleaned、DeleteEmptyDirectoriesUpToRoot 等所有路径安全比较处统一使用。

P0-2：静态资源/媒体复制未显式拒绝符号链接，可能泄露宿主机文件

DirectoryCopy.Sync 遍历源目录文件并复制到输出目录：

foreach (var file in Directory.GetFiles(sourceDir))
{
    ...
    SyncFile(file, destinationDir, options.HashMode, outputRoot);
}

最终 SyncFile 直接 File.Copy(sourceFile, destinationFile, overwrite: true)。

SyncFilesRecursive 也会对所有文件递归复制。

风险说明

如果用户主题 static/、assets/ 或 Notion 下载媒体缓存目录中存在符号链接：

static/leak.txt -> /etc/passwd
assets/private.key -> /home/user/.ssh/id_rsa

当前复制逻辑没有检测 FileAttributes.ReparsePoint，在 Linux/macOS 上可能会复制链接目标内容，从而把宿主机敏感文件发布进 dist。

影响范围

AssetPipeline 会同步：

theme.static 到输出根目录；
theme.assets 到 assets/；
content.media.downloadDir 到 assets/uploads/。
修复建议

在 DirectoryCopy 和 BuildManifestTracker 中统一跳过 symlink：

private static bool IsSymlink(string path)
{
    return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
}

对文件和目录都检测。默认拒绝；如确实需要支持软链接，应新增配置：

build:
  followSymlinks: false

默认必须为 false。

P0-3：外部 process 插件没有 sandbox，本质是任意代码执行能力

Bukit 已移除动态 Assembly 插件，改为 AOT 友好的外部协议插件。PluginRegistry 中内置插件源和 ExternalProtocolPluginSource 同时参与注册。

外部插件最终通过 ProcessPluginInvoker 启动：

FileName = plugin.Entry,
Arguments = arguments ?? string.Empty,
RedirectStandardInput = true,
RedirectStandardOutput = true,
RedirectStandardError = true,
UseShellExecute = false

它确实清空了环境变量，只允许 plugin.AllowEnvironment 中的变量透传，并设置 BUKIT_PROJECT_ROOT、BUKIT_OUTPUT_DIR 等上下文变量。

风险说明

当前 process 插件仍然拥有宿主机进程权限：

可以读取项目目录外文件；
可以访问网络；
可以执行任意子进程；
可以删除工作区文件；
可以读取 GitHub Actions 工作目录中的敏感文件；
如果用户错误 allow 环境变量，可能泄露 token。
这不是普通 bug，而是安全边界问题

外部 process 插件应被定义为 可信插件机制，不能宣传为安全 sandbox。当前 validator 只校验 runtime、entry、hooks、capabilities、timeout、stdout/stderr 限制等，没有提供真正的文件系统或网络隔离。

修复建议

短期：

site:
  externalPlugins:
    xxx:
      trusted: true

并在文档中明确：process 插件等同本地命令执行。

中期：

CI 默认禁用 external process plugin；
增加 --allow-external-plugins 显式开关；
插件 entry 必须位于项目目录内；
禁止绝对路径 entry，除非 allowAbsoluteEntry: true；
限制 AllowEnvironment 只允许白名单前缀。

长期：

Linux 使用 bubblewrap / firejail；
Windows 使用 Job Object + restricted token；
macOS 使用 sandbox-exec 或容器化方案；
或恢复 WASM/WASI 插件作为安全插件主路径。
3. 高优先级逻辑问题
P1-1：last-wins 派生页面冲突策略实现并不真正覆盖旧路由

PluginRunner.ApplyDeriveConflictPolicy 用 usedRouteUrls 和 usedOutputPaths 检测冲突。冲突时如果策略为 last-wins，它只是把当前 page 加入 acceptedPages：

if (deriveConflictPolicy == "last-wins")
{
    acceptedPages.Add(page);
    continue;
}

问题

它没有从已接受列表中移除旧 route，也没有从原始 routed 列表中覆盖已有页面。结果可能是：

渲染队列中存在两个相同 outputPath；
并行渲染时最后写入者不确定；
manifest 记录不稳定；
sitemap / search index / SEO index 可能重复。
修复建议

把 last-wins 明确定义为：

若冲突对象来自本次 derived 列表，则移除旧 derived page；
若冲突对象来自原始内容路由，则应该禁止覆盖，或显式允许覆盖并记录 warning；
对 url 和 outputPath 双索引都要同步更新；
加测试覆盖：
derived vs derived；
derived vs content；
same url different outputPath；
different url same outputPath。
P1-2：多语言构建实际被强制串行，Parallel.ForEachAsync 没有发挥作用

SiteEngine.BuildMultiLanguageAsync 使用了 Parallel.ForEachAsync，但 MaxDegreeOfParallelism = 1：

new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken }

问题

这会导致多语言站点构建时间线性增加：

zh → en → ms → ja → ...

如果 Bukit 目标是大规模内容站、SEO/GEO 多语言站，这会成为核心性能瓶颈。

可能原因

看起来作者可能为了避免：

共享 DirectoryHashCache 并发问题；
共享 bodyStore 并发读取问题；
日志输出混乱；
主题缓存线程安全问题。
修复建议

新增配置：

build:
  languageJobs: 1

默认 1 保守运行；CI 或大站点可设置为 CPU 数。然后逐步验证：

DirectoryHashCache 是否线程安全；
IContentBodyStore 是否只读线程安全；
ThemeBootstrapper 是否有全局缓存；
BuildManifest 每语言是否独立。
P1-3：AssetPipeline.TrackAssetOutputs(ctx.ParentAssetsDir, ctx.AssetsDir!, ...) 存在空值语义错误

AssetPipeline 判断条件是：

if (ctx.AssetsDir is not null && Directory.Exists(ctx.AssetsDir) 
    || (ctx.ParentAssetsDir is not null && Directory.Exists(ctx.ParentAssetsDir)))

如果只有 parent assets 存在、child assets 不存在，仍会进入分支。最后却调用：

BuildManifestTracker.TrackAssetOutputs(ctx.ParentAssetsDir, ctx.AssetsDir!, ...)

TrackAssetOutputs 的第二个参数声明为非空 string assetsDir，但内部又传给可空方法 AddAssetSourceOutputs。

问题

这在运行时可能不会立刻崩，因为 AddAssetSourceOutputs 接收 string?，但 API 语义已经错误：

非空签名不可信；
nullable warning 被 ! 掩盖；
未来维护者可能在 TrackAssetOutputs 中直接使用 assetsDir 导致 NRE；
parent-only theme 的 manifest 行为容易出错。
修复建议

修改签名：

internal static void TrackAssetOutputs(
    string? parentAssetsDir,
    string? assetsDir,
    ...
)

并去掉调用处 !。

P1-4：增量构建 fingerprint 默认使用 size + LastWriteTime，网络文件系统/CI 中可能误判

BuildManifestTracker.ComputeFileFingerprint 当前是：

return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";

虽然 README 建议 assetHashMode: sha256 用于 CI/network filesystems，默认仍是 size-time。

问题

size-time 在以下场景可能误判：

CI checkout 保留时间戳异常；
同大小文件内容变更；
网络文件系统时间精度不足；
Docker bind mount 时间戳同步延迟。
修复建议

对 manifest 的 HTML / static / asset / media 统一支持：

build:
  fingerprintMode: sha256

当前 assetHashMode 只影响 copy 检测，不等同于整个 build manifest 的 stale 判断。

4. 中优先级问题
P2-1：CLI 新旧解析路径并存，容易出现选项行为不一致

Program.cs 先尝试 BukitCliSpecs 规范解析。如果命令没有子命令，就走新 parser；否则回落到旧 ArgReader switch。

BuildCommand 同时有：

RunAsync(ArgReader reader)
RunAsync(CliBoundCommand command)

问题

这会出现：

新 parser 支持的 option，旧 parser 未同步；
help 展示和实际行为不一致；
子命令和一级命令处理风格不一致；
测试矩阵复杂化。
修复建议

逐步废弃旧 ArgReader 分支，让所有命令都走 BukitCliSpecs + CliParser。短期至少做一份命令参数一致性测试：

spec options == command reader consumed options
P2-2：BuildCommand.TryParsePositiveInt 对非法 --jobs 静默忽略

当前实现：

if (int.TryParse(text.Trim(), out var n) && n > 0)
{
    return n;
}

return null;

问题

用户执行：

bukit build --jobs abc
bukit build --jobs -1

不会报错，只会退回默认 Environment.ProcessorCount。这不利于 CI 和自动化 Agent 发现配置错误。

修复建议

非法值直接返回 CLI 参数错误，exit code 2：

--jobs must be a positive integer.
P2-3：BuildCommand 通过环境变量传递 AutoSummary，破坏构建上下文纯度

BuildCommand 会设置：

Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY", ...)
Environment.SetEnvironmentVariable("BUKIT_AUTO_SUMMARY_MAXLEN", ...)

问题

这是进程级全局状态：

单元测试之间可能互相污染；
同进程多站点构建时互相影响；
长期看不利于把 Bukit 嵌入 BukitJalil 或其他服务进程。
修复建议

把 AutoSummary 放入 BuildContext 或 RenderContext，不要通过环境变量传递。

P2-4：DirectoryCopy 默认 dotfile 防护不错，但 publishDotFiles=true 可能放开过大

默认 DirectoryCopyOptions.IgnoreDotPrefixedFiles = true，并且默认拒绝 .env、.git、.github、.npmrc、.pem、.key、.pfx 等敏感文件。

但 AssetPipeline.BuildCopyOptions 中如果 PublishDotFiles=true，就设置：

new DirectoryCopyOptions { IgnoreDotPrefixedFiles = false }

问题

一旦开启 publishDotFiles，默认 deny list 也被绕过，因为 Sync 中只有 IgnoreDotPrefixedFiles && ShouldSkipDotfile(...) 才跳过。

修复建议

拆分语义：

IgnoreDotPrefixedFiles
AlwaysDenySensitiveDotfiles

即使允许 dotfiles，也应始终拒绝：

.env
.env.*
.git
.github
*.pem
*.key
*.pfx
*.p12
.npmrc

允许 .well-known 即可，不应全量开放。

5. 架构层面建议
5.1 把 VariantBuildPipeline 拆成更小的 stage

现在 VariantBuildPipeline.ExecuteAsync 过长，承担：

theme bootstrap；
module 构建；
route pipeline；
renderer 创建；
plugin context；
taxonomy；
derive pages；
SEO；
render；
asset；
plugin after build；
report。

这一点从源码 159 到 325 行可以看出，单方法承载了过多职责。

建议拆成：

ThemeStage
DataModuleStage
RoutingStage
PluginDeriveStage
SeoStage
RenderStage
AssetStage
PluginAfterBuildStage
ReportStage

好处：

更容易单测；
Codex 更容易局部修复；
后续 BukitJalil 可视化构建进度更容易接入；
构建 metrics 可天然按 stage 输出。
5.2 插件系统需要明确“双轨制”

当前 Bukit 为 AOT 去掉 Assembly 插件和 WASM 插件，只剩 process 插件。源码中也明确注释：ExternalAssemblyPluginSource disabled，Wasmtime disabled。

建议定义双轨：

插件类型	定位	安全级别
Built-in Plugin	引擎内部能力	高
Process Plugin	本地可信扩展	低，无 sandbox
Future WASM Plugin	可分发社区插件	中高
Section Plugin	主题组件级能力	中

Bukit 未来如果要有主题市场和插件生态，process 插件不应成为社区插件默认形态。

5.3 输出安全应统一收敛为一个服务

现在同时存在：

FileWriter.GetSafeFullPath
SafeOutputFileSystem.GetSafeFullPath
RouteSecurityValidator.ValidateOutputPath
BuildPlanner.EnsureOutputDirectoryCanBeCleaned
DirectoryCopy.Sync(... outputRoot)

建议统一为：

IOutputPathPolicy
SafePathResolver
SafeOutputFileSystem

所有写入、复制、删除都必须经过同一个 resolver。

6. 建议优先修复清单
第一批：安全底座
修复 Linux/macOS 路径大小写绕过。
静态资源、assets、media 复制默认拒绝 symlink。
publishDotFiles=true 时仍强制拒绝敏感 dotfile。
external process plugin 增加 --allow-external-plugins 或 CI 默认禁用。
外部插件 entry 默认限制在项目目录内。
第二批：构建正确性
修复 deriveConflictPolicy=last-wins 的真实覆盖逻辑。
修复 TrackAssetOutputs nullability 签名。
manifest fingerprint 增加 sha256 模式。
对 --jobs 非法输入直接报错。
移除 BUKIT_AUTO_SUMMARY 全局环境变量传参。
第三批：性能与可维护性
多语言构建支持 languageJobs。
VariantBuildPipeline stage 化。
CLI 统一迁移到 spec parser。
增加构建阶段事件模型，为 BukitJalil 接入进度条和可视化日志做准备。