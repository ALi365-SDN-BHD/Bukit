// DESKTOP-REMOVED: PathReportPlugin is being converted to a process protocol plugin.
#if false
using System.Net;
using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Plugins;
using Bukit.Plugins.PathReportPlugin;
using Bukit.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PathReportPluginTests
{
    [Fact]
    public void AfterBuild_WritesReportWithPathsAndFiles()
    {
        var root = CreateTempRoot();

        var cacheFile = Path.Combine(root, ".cache", "a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        File.WriteAllText(cacheFile, "a");

        var distDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(distDir);
        var distFile = Path.Combine(distDir, "b.txt");
        File.WriteAllText(distFile, "b");

        var themeRoot = Path.Combine(root, "themes", "alt");
        var themeAssetsDir = Path.Combine(themeRoot, "assets");
        Directory.CreateDirectory(themeAssetsDir);
        var themeAssetFile = Path.Combine(themeAssetsDir, "c.css");
        File.WriteAllText(themeAssetFile, "c");

        var context = CreateContext(root, distDir, themeName: "alt");

        var plugin = new PathReportPlugin();
        plugin.AfterBuild(context);

        var reportPath = Path.Combine(distDir, "_debug", "paths-report.json");
        Assert.True(File.Exists(reportPath));

        var report = JsonSerializer.Deserialize(File.ReadAllText(reportPath), PathReportJsonContext.Default.PathReport);
        Assert.NotNull(report);

        Assert.Equal(Path.Combine(root, ".cache"), report!.CacheDir);
        Assert.Equal(distDir, report.DistDir);
        Assert.Equal(themeRoot, report.ThemeRoot);
        Assert.Equal(themeAssetsDir, report.AssetsDir);

        Assert.Contains("a.txt", report.Files.Cache);
        Assert.Contains("b.txt", report.Files.Dist);
        Assert.Contains("assets/c.css", report.Files.Theme);
        Assert.Contains("c.css", report.Files.Assets);
        Assert.Null(report.WechatMaterialUpload);
    }

    [Fact]
    public void AfterBuild_WechatMaterialUpload_WritesMediaIdAndUrl()
    {
        var root = CreateTempRoot();
        var distDir = Path.Combine(root, "dist");
        Directory.CreateDirectory(distDir);
        var imgPath = Path.Combine(distDir, "assets", "imgs", "default.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imgPath)!);
        File.WriteAllBytes(imgPath, "png"u8.ToArray());

        var appIdEnv = $"WECHAT_APP_ID_{Guid.NewGuid():N}";
        var appSecretEnv = $"WECHAT_APP_SECRET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(appIdEnv, "app");
        Environment.SetEnvironmentVariable(appSecretEnv, "secret");
        try
        {
            var handler = new FakeWechatHttpHandler();
            var http = new HttpClient(handler);
            var uploader = new WechatMaterialUploader(http);
            var plugin = new PathReportPlugin(uploader);

            var pluginOptions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["wechatMaterialUpload"] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["enabled"] = true,
                    ["appIdEnv"] = appIdEnv,
                    ["appSecretEnv"] = appSecretEnv,
                    ["file"] = "assets/imgs/default.png",
                    ["type"] = "image"
                }
            };

            var context = CreateContext(root, distDir, themeName: "alt", pluginOptions);
            plugin.AfterBuild(context);

            var reportPath = Path.Combine(distDir, "_debug", "paths-report.json");
            var report = JsonSerializer.Deserialize(File.ReadAllText(reportPath), PathReportJsonContext.Default.PathReport);
            Assert.NotNull(report);
            Assert.NotNull(report!.WechatMaterialUpload);
            Assert.Equal("MID", report.WechatMaterialUpload!.MediaId);
            Assert.Equal("https://mmbiz.qpic.cn/x.png", report.WechatMaterialUpload.Url);
            Assert.NotNull(handler.LastAddMaterialRequestBytes);
            var bodyText = System.Text.Encoding.UTF8.GetString(handler.LastAddMaterialRequestBytes!);
            Assert.Contains("name=\"media\"", bodyText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("filename=\"default.png\"", bodyText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(appIdEnv, null);
            Environment.SetEnvironmentVariable(appSecretEnv, null);
        }
    }

    private static BuildContext CreateContext(string rootDir, string distDir, string themeName, Dictionary<string, object>? pluginOptions = null)
    {
        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "demo",
                    Title = "demo",
                    Url = "https://example.com",
                    Plugins = new Dictionary<string, PluginToggleConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["path-report"] = new PluginToggleConfig { Enabled = true, Options = pluginOptions }
                    }
                },
                Content = new ContentConfig { Provider = "markdown" },
                Theme = new ThemeConfig { Name = themeName }
            },
            RootDir = rootDir,
            OutputDir = distDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(rootDir, "themes", themeName, "layouts"),
            Routed = new List<(ContentItem Item, RouteInfo Route)>(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FakeWechatHttpHandler : HttpMessageHandler
    {
        public byte[]? LastAddMaterialRequestBytes { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith("/cgi-bin/token", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"TOKEN\",\"expires_in\":7200}")
                });
            }

            if (path.EndsWith("/cgi-bin/material/add_material", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Content is not null)
                {
                    LastAddMaterialRequestBytes = request.Content.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult();
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"media_id\":\"MID\",\"url\":\"https://mmbiz.qpic.cn/x.png\"}")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"errcode\":404,\"errmsg\":\"not found\"}")
            });
        }
    }
}
#endif
