深度问题清单
P0 / P1：正式版前建议优先修复
编号	严重度	问题	影响	建议
BKT-SEC-001	高	Notion 渲染中 URL 只做 HTML 编码，缺少 URL scheme allowlist	恶意内容可能注入 javascript:、危险 data:、不安全 iframe/embed URL	增加统一 SafeUrlSanitizer
BKT-SEC-002	高	外部 process 插件属于强信任执行边界	插件本质上可执行本地程序，不能视为沙箱	默认仅内置插件；外部插件必须显式 allow，并在文档中标注风险
BKT-ERR-001	中高	路由安全错误抛 InvalidOperationException，CLI 作为普通异常返回 1	用户配置错误会像内部崩溃	改为 ConfigException + DiagnosticCode，CLI 返回 2
BKT-REL-001	中	CLI version fallback 与全局版本存在漂移	发布版本信息可能不一致	统一版本来源
BKT-PERF-001	中	render jobs 没有上限钳制	用户设置过大可能打爆 CPU / IO	jobs 最大值限制到合理范围
BKT-FS-001	中	FollowSymlinks 可开启，输出路径是词法安全，不是 realpath 安全	复杂 symlink 情况下存在文件系统边界风险	默认禁用保持；开启时增加明确风险提示和 realpath 检查
4.1 BKT-SEC-001：Notion URL 缺少 scheme 级安全过滤
证据

Notion 富文本中，如果存在 href，会直接生成：

<a href="{HtmlEncode(href)}">...</a>

代码中是 HTML 编码，但不是 URL scheme allowlist。

图片 block 也是从 Notion file/external 提取 URL 后直接写入 <img src="...">。

ExtractFileUrl 对 Notion external/file URL 直接返回原始 URL。

风险

HTML attribute encoding 可以防止引号逃逸，但不能阻止危险协议。例如：

javascript:alert(1)
data:text/html,...
vbscript:
file:

在 <img> 中多数浏览器不会执行 javascript:，但在 <a>、iframe、embed、video、file、bookmark 这类 block 中风险更高。

建议修复

新增统一 URL 安全工具：

internal static class SafeUrl
{
    private static readonly HashSet<string> LinkSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel"
    };

    private static readonly HashSet<string> MediaSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https"
    };

    public static string? ForLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url.StartsWith('/') ? url : null;
        return LinkSchemes.Contains(uri.Scheme) ? url.Trim() : null;
    }

    public static string? ForMedia(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url.StartsWith('/') ? url : null;
        return MediaSchemes.Contains(uri.Scheme) ? url.Trim() : null;
    }
}

同时：

外链 <a> 默认增加 rel="noopener noreferrer"
iframe/embed/video/pdf 只允许 https
YouTube embed 只允许已识别的 YouTube host
对危险 URL 输出为空或纯文本，而不是生成链接
4.2 BKT-SEC-002：外部 process 插件必须被明确标注为强信任
证据

外部插件来自 site.externalPlugins，会被包装成 ExternalProtocolPlugin。

插件执行时使用 ProcessStartInfo.FileName = plugin.Entry，并把 JSON request 写入 stdin。

虽然环境变量默认清空，并只允许白名单透传，这是好的。 但进程插件本质上仍然是本地程序执行。

风险

如果用户从不可信主题、AI 生成项目、第三方 demo 迁移项目中带入 external plugin 配置，可能导致：

执行任意本地程序
读取项目目录
修改输出目录
网络访问
读取允许透传的环境变量
在 after-build 阶段篡改构建结果

当前 CI 下默认禁止 external plugins，除非显式 --allow-external-plugins。 这是正确的，但本地构建也应强化提示。

建议修复
本地构建时第一次检测到 external plugin，输出醒目的安全提示。
增加配置：
site:
  externalPluginPolicy: deny | prompt | allow
bukit build 默认：
local: prompt 或 warn
ci: deny
external plugin 建议支持 hash pin：
externalPlugins:
  my-plugin:
    runtime: process
    entry: tools/my-plugin
    sha256: "..."
插件上下文中的 data 建议命名空间隔离：
plugin.myPlugin.xxx

避免 plugin data 覆盖 source data。当前 MergeSiteData 中 pluginData 会覆盖 sourceData 同名 key。

4.3 BKT-ERR-001：路由安全异常类型不够友好
证据

RouteSecurityValidator.Fail 当前抛出的是 InvalidOperationException。

而 CLI 对普通异常只打印 message 和 inner message，返回 1。

风险

用户如果写错：

route:
  outputPath: "../index.html"

这本质是配置错误，应返回 CLI 参数/配置错误码 2，并带可读修复建议；现在会被视为普通异常。

建议修复

将：

throw new InvalidOperationException(...)

改为：

throw new ConfigException(..., DiagnosticCode.RouteOutputPathUnsafe);

并在 CLI 中区分：

catch (ConfigException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
catch (ContentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}
4.4 BKT-REL-001：版本信息存在漂移
证据

全局 Directory.Build.props 对 Bukit.* 项目设置版本为 1.0.7。

但 CLI csproj 中 BuildInfoVersionBase 的最终 fallback 是 1.0.6。

风险

多数情况下 $(Version) 会覆盖 fallback，因此不一定直接出错；但 fallback 已陈旧，后续发布脚本、手动 publish、特殊 CI 参数可能导致 bukit version 不一致。

建议

统一用一个来源：

<Version>1.0.7</Version>

CLI build info 不再写死 fallback，或者 fallback 直接读取 $(Version)，并增加测试：

bukit version == assembly informational version
4.5 BKT-PERF-001：渲染并发缺少上限保护
证据

SEO 阶段将 maxDegreeOfParallelism 设为 overrides.Jobs ?? Environment.ProcessorCount。

PageRenderDispatcher 中如果并发小于等于 0，则回落到 CPU 数；但如果用户传入很大的 jobs，没有看到进一步 cap。

风险

如果用户传：

bukit build --jobs 999

可能导致：

同时读取大量 Notion body / Markdown body
大量文件写入
GC 压力增加
磁盘 IO 放大
CI runner 不稳定
建议

统一限制：

var cpu = Environment.ProcessorCount;
var max = Math.Clamp(requestedJobs, 1, Math.Max(1, cpu * 2));

并允许高级用户通过环境变量解除：

BUKIT_MAX_JOBS
4.6 BKT-FS-001：Symlink 策略需要继续强化
证据

资源复制默认不跟随 symlink，除非 FollowSymlinks 开启。

DirectoryCopy 在 FollowSymlinks == false 时会跳过 symlink 文件和目录。

配置中 BuildConfig 也有 FollowSymlinks。

判断

默认行为是安全的。问题在于：一旦允许 follow symlink，当前主要靠输出路径词法校验，不等于 realpath 边界校验。

建议

保持默认禁用。若用户开启：

build:
  followSymlinks: true

应输出强警告，并增加：

源路径 realpath 检查
输出路径 realpath 检查
禁止 symlink 指向项目外敏感路径
CI 默认禁止 followSymlinks，除非显式 allow
5. Notion 数据源专项审计
5.1 做得好的地方

Notion provider 会强制要求 DatabaseId 和 Token。

配置校验也要求 NOTION_TOKEN 必须来自环境变量。

Notion API client：

设置 30 秒超时
支持 429 retry
支持 Retry-After
支持 maxRps 节流
不把 token 打入日志

Notion 内容加载还支持：

字段白名单
schema resolver
relation target cache
relation taxonomy
body cache
自动摘要

5.2 需要强化的地方
URL 安全

上面已经列为 P1。

CSS color fallback

NotionRichTextRenderer.NotionColorToCss 如果 palette 返回 inherit，会 fallback 到原始 notionColor。

这在正常 Notion 枚举下问题不大，但从安全防御角度，未知颜色不应进入 inline style。

建议改为：

return string.Equals(result, "inherit", StringComparison.Ordinal)
    ? "inherit"
    : result;
list item color class 编码不一致

NotionBlocksRenderer 中 list item color class 直接：

class="notion-{color}"

而 helper 中 GetBlockColorClass 是有 HtmlEncode 的。

建议统一使用 helper。

6. 路由与输出系统专项审计
6.1 路由 override 能力强，但要继续压住边界

RouteGenerator 支持 full route override，也支持 partial route override。

这对丝路商讯这类站点迁移很有价值，因为可以严格映射：

/insights/
/companies/
/china-companies/
/companies/{slug}/

但是 route override 是高风险能力，必须保持当前的强校验。

6.2 outputPathEncoding 默认是 none

SiteConfig.OutputPathEncoding 默认是 none。

RoutePathBuilder 支持：

none
urlencode
slug
sanitize

建议

正式版建议将默认策略改成更安全的：

site:
  outputPathEncoding: sanitize

或者至少在 doctor/lint 中警告：

outputPathEncoding=none may create platform-incompatible filenames.
7. 插件系统专项审计
7.1 当前设计方向正确

内置插件包括：

DataFiles
PagesIndex
Taxonomy
Sitemap
Feed
SearchIndex
Pagination
Archive
RelatedContent
Alias
LlmsTxt
Menu
ImageProcessing

外部插件走 protocol/process，且执行前检查 hook 和 capabilities。

7.2 仍需明确“不是沙箱”

外部 process 插件做了不少工程保护，但它不是沙箱。尤其 Bukit 的定位未来会被 AI Agent 驱动，AI 可能生成 external plugin 配置。这个风险要在产品层压住。

建议在 doctor 或 build 中输出：

External plugins execute local processes and should only be enabled for trusted projects.

并在 CI / Jalil / GUI 层默认禁止。

8. CLI 与开发体验问题
8.1 每次命令都向 stderr 打版本号

Program.cs 中，除 version 命令外，会向 stderr 输出：

bukit {CliBuildInfo.Version}

问题

很多自动化脚本会把 stderr 视为异常信号。
建议改为：

默认不输出
--verbose 输出到 stderr
bukit version 输出到 stdout
8.2 错误信息需要分层

当前 CLI 对普通异常只输出 message，没有 stack trace，也没有错误分类。

建议：

ConfigException     → exit 2
ContentException    → exit 2
PluginException     → exit 3
Internal exception  → exit 1

并支持：

bukit build --debug
BUKIT_DEBUG=1 bukit build

输出 stack trace。

9. 发布成熟度判断
可以公开测试吗？

可以进入公开测试。

原因：

架构拆分清晰
AOT 发布方向明确
输出目录清理保护已经具备
路由安全校验较强
Notion provider 已具备实际可用能力
插件体系已从动态 DLL 转向 AOT 友好的 process protocol
测试项目覆盖面从解决方案结构看较丰富
可以直接正式发布吗？

不建议直接正式版。

正式版前至少完成：

Notion / Markdown / embed / link 统一 URL sanitizer
路由安全异常改成 ConfigException
外部插件安全策略显式化
CLI stderr version 输出调整
版本号统一
jobs 并发上限
doctor 增加安全检查项
10. 建议 Codex 修复任务清单
P0：安全修复
# Task: Add SafeUrl sanitizer for all rendered external URLs

## Scope
- NotionRichTextRenderer
- ImageBlockRenderer
- VideoBlockRenderer
- EmbedBlockRenderer
- BookmarkBlockRenderer
- FileBlockRenderer
- PdfBlockRenderer
- LinkPreviewBlockRenderer

## Requirements
1. Add `SafeUrl` helper.
2. Links allow: http, https, mailto, tel, relative internal path.
3. Media allow: http, https, relative internal path.
4. Iframe/embed allow: https only, plus known provider allowlist when possible.
5. Block javascript:, data:, file:, vbscript:.
6. Add rel="noopener noreferrer" for external links.
7. Add tests for malicious URLs.
P1：CLI 错误分层
# Task: Convert routing validation failures to ConfigException

## Scope
- RouteSecurityValidator
- Program.cs
- CLI tests

## Requirements
1. Replace InvalidOperationException with ConfigException.
2. Add diagnostic codes for:
   - invalid internal URL
   - unsafe output path
   - reserved Windows path
   - encoded slash
3. Program.cs catches ConfigException and exits 2.
4. Add CLI tests for route traversal and absolute URL.
P1：外部插件安全策略
# Task: Harden external protocol plugin trust policy

## Scope
- Config model
- ConfigValidator
- BuildPlanner
- PluginRegistry
- docs

## Requirements
1. Add `site.externalPluginPolicy: deny|warn|allow`.
2. CI default deny.
3. Local default warn.
4. Add optional sha256 pin for plugin entry.
5. Doctor reports external plugin risks.
6. Docs clearly state process plugins are trusted code, not sandboxed.
P2：版本一致性
# Task: Unify Bukit CLI version source

## Scope
- Directory.Build.props
- Bukit.Cli.csproj
- VersionCommand tests

## Requirements
1. Remove stale 1.0.6 fallback.
2. Use one MSBuild version source.
3. Add test to assert `bukit version` matches assembly informational version.
11. 推荐新增测试矩阵
路由安全测试
输入	期望
/safe/path/	通过
//evil.com	拒绝
https://evil.com/a	拒绝
/a/%2F/b/	拒绝
/a/%5C/b/	拒绝
/../x/	拒绝
outputPath: ../x.html	拒绝
outputPath: C:\x.html	拒绝
outputPath: CON/index.html	拒绝
URL 渲染测试
URL	场景	期望
https://example.com	link/media	通过
/assets/a.png	internal media	通过
javascript:alert(1)	link	拒绝
data:text/html,...	link/embed	拒绝
file:///etc/passwd	media	拒绝
插件测试
场景	期望
CI + externalPlugins + no allow	拒绝
local + externalPlugins	警告
plugin stdout 超限	kill + failure
plugin timeout	kill process tree
plugin env 未白名单	不透传