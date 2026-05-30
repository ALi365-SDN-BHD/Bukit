仍然存在的测试稳定性残留
P1：Task.WhenAny 仍可能留下后台构建任务

虽然 CWD 修复已经到位，但 BuildCommandTests 中仍有多处：

await Task.WhenAny(BuildCommand.RunAsync(command), Task.Delay(Timeout.Infinite, cts.Token));

例如 RunAsync_WithConfigOption_ResolvesAndStartsBuild 仍是直接 Task.WhenAny，没有确认 build task 是否完成，也没有 await build task 的结果。

RunAsync_WithSiteOption_ResolvesAndStartsBuild 也一样，并且它在 Task.WhenAny 后删除临时目录。如果 delay 先完成，构建任务可能还在后台运行，此时 CWD scope 释放、目录被删除，仍可能造成竞态。

RunAsync_JobsFour_RunsSuccessfully 也仍然没有断言 build task 完成。

建议修复

把这类代码统一改成：

var buildTask = BuildCommand.RunAsync(command);
var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

var completed = await Task.WhenAny(buildTask, timeoutTask);

Assert.Same(buildTask, completed);
await buildTask;

如果构建本来可能失败，就不要叫 RunsSuccessfully，应明确断言允许的异常类型。

4. 安全修复复核
4.1 SafeUrl 已加入，但存在新漏洞

现在已有 Bukit.Shared.SafeUrl，并支持：

ForLink
ForMedia
ForEmbed
IsExternal

链接允许 http / https / mailto / tel，媒体允许 http / https，embed 只允许 https。

这是好方向。

但当前实现有一个关键问题：

if (trimmed.StartsWith('/')) return trimmed;

这会把 //evil.com/x.js 这类 protocol-relative URL 当作内部路径放行。

风险

//evil.com 在浏览器中不是站内路径，而是协议相对外链：

<a href="//evil.com">...</a>

浏览器会按当前协议解析成：

https://evil.com

这与 RouteSecurityValidator 的规则不一致，因为路由校验已经明确拒绝 // 开头的 protocol-relative URL。

建议修复

SafeUrl 应改为：

if (trimmed.StartsWith("//", StringComparison.Ordinal))
    return null;

if (trimmed.StartsWith('/'))
    return trimmed;

并增加测试：

SafeUrl.ForLink("//evil.com") == null
SafeUrl.ForMedia("//evil.com/a.png") == null
SafeUrl.ForEmbed("//evil.com/embed") == null


P1：Audio block 仍未接入 SafeUrl

AudioBlockRenderer 当前仍然直接使用 ExtractFileUrl(audio) 后进行 HTML 编码，并输出到 <audio src=""> 和 <a href="">，没有经过 SafeUrl.ForMedia 或 SafeUrl.ForLink。

建议修复
var url = ExtractFileUrl(audio);
var safeUrl = SafeUrl.ForMedia(url);
if (string.IsNullOrWhiteSpace(safeUrl))
{
    return Task.FromResult<string?>(null);
}

var encodedUrl = WebUtility.HtmlEncode(safeUrl);

如果 fallback <a> 保留，可以共用 safeUrl，并为外链加 rel="noopener noreferrer"。


需修复的问题清单
P0：修复 SafeUrl protocol-relative 放行

当前：

if (trimmed.StartsWith('/')) return trimmed;

会放行 //evil.com。

修复建议：

if (trimmed.StartsWith("//", StringComparison.Ordinal))
{
    return null;
}

if (trimmed.StartsWith('/'))
{
    return trimmed;
}

同时加测试。

P1：Audio block 接入 SafeUrl

AudioBlockRenderer 当前仍直接输出原始 URL。

修复建议：

<audio src> 使用 SafeUrl.ForMedia
fallback <a href> 使用同一个 safe URL
外链加 rel="noopener noreferrer"
增加 javascript:、data:、//evil.com 测试
P1：清理 Task.WhenAny 假绿测试

仍有多个测试没有确认 build task 完成。

修复建议：

不要只 await Task.WhenAny，必须：

Assert.Same(buildTask, completed);
await buildTask;

否则测试可能在构建仍运行时结束。

P2：externalPluginPolicy 非法值不应静默变成 Warn

ConfigLoader.ReadExternalPluginPolicy 当前对未知值默认返回 Warn。

这不利于配置质量。比如：

site:
  externalPluginPolicy: alow

会被静默当作 warn，用户可能以为已经 allow。

建议改为：

_ => throw new ConfigException("site.externalPluginPolicy must be deny|warn|allow.")
P2：ConfigPathResolver --site 路径穿越仍抛 InvalidOperationException

ConfigPathResolver 对 --site ../../../etc/passwd 仍抛 InvalidOperationException。

这类错误本质是用户输入/配置错误，更适合改成 ConfigException 并返回 exit code 2。