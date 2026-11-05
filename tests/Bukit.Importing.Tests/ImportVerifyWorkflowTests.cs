using Xunit;

namespace Bukit.Importing.Tests;

[Collection("ImportingConsole")]
public sealed class ImportVerifyWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public ImportVerifyWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-importing-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
            Directory.Delete(_rootDir, recursive: true);
    }

    [Fact]
    public async Task VerifyAsync_UsesSitePathFallbackAndMapsMissingConfigToExitCodeOne()
    {
        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(
                new ImportResult
                {
                    ThemePath = Path.Combine(_rootDir, "themes", "demo")
                },
                _rootDir,
                "demo"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(Path.Combine(_rootDir, "sites", "demo", "site.yaml"), result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_ConfigValidationFailureReturnsExitCodeOne()
    {
        var siteDir = Path.Combine(_rootDir, "custom-site");
        Directory.CreateDirectory(siteDir);
        File.WriteAllText(Path.Combine(siteDir, "site.yaml"), """
site:
  name: broken
content:
  sources:
    - type: markdown
      markdown:
        dir: content
""");

        var result = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(
                new ImportResult
                {
                    ThemePath = Path.Combine(_rootDir, "themes", "demo"),
                    SitePath = siteDir
                },
                _rootDir,
                "demo"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("title is required.", result.StdErr, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_ImportedSiteBuildsAndEachPageCarriesOnlyItsOwnAssets()
    {
        var demoDir = Path.Combine(_rootDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title><style>.home-only{color:red}</style></head>" +
            "<body><main><h1>Home</h1><p>Welcome.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title><style>.about-only{color:blue}</style></head>" +
            "<body><main><h1>About</h1><p>About us.</p></main><script>window.aboutOnly=1;</script></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "pricing.html"),
            "<html><head><title>Pricing</title><style>.pricing-only{color:green}</style></head>" +
            "<body><main><h1>Pricing</h1><p>Plans.</p></main></body></html>");
        var routeMap = Path.Combine(demoDir, "routes.yaml");
        File.WriteAllText(routeMap, """
pages:
  - source: about.html
    route: /company/about/
    type: Page
    template: about-us
""");

        var import = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "e2e",
            RootDir = _rootDir,
            Force = true,
            RouteMapPath = routeMap
        });

        var verify = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(import, _rootDir, "e2e"));
        Assert.True(verify.ExitCode == 0, verify.StdErr);

        var distDir = Path.Combine(_rootDir, "dist");
        string Output(string relative)
        {
            var path = Path.Combine(distDir, relative);
            Assert.True(File.Exists(path), $"Missing build output '{relative}'.");
            return File.ReadAllText(path);
        }

        var home = Output("index.html");
        Assert.Contains(".home-only", home, StringComparison.Ordinal);
        Assert.DoesNotContain(".about-only", home, StringComparison.Ordinal);

        var about = Output(Path.Combine("company", "about", "index.html"));
        Assert.Contains(".about-only", about, StringComparison.Ordinal);
        Assert.Contains("window.aboutOnly", about, StringComparison.Ordinal);
        Assert.DoesNotContain(".home-only", about, StringComparison.Ordinal);
        Assert.DoesNotContain(".pricing-only", about, StringComparison.Ordinal);

        var pricing = Output(Path.Combine("pricing", "index.html"));
        Assert.Contains(".pricing-only", pricing, StringComparison.Ordinal);
        Assert.DoesNotContain(".about-only", pricing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_CorporateDemoUnderSubPath_BuildsWithWorkingLinksAndSeparateCollections()
    {
        var demoDir = Path.Combine(_rootDir, "corp-demo");
        Directory.CreateDirectory(Path.Combine(demoDir, "news"));
        Directory.CreateDirectory(Path.Combine(demoDir, "css"));
        Directory.CreateDirectory(Path.Combine(demoDir, "img"));
        File.WriteAllText(Path.Combine(demoDir, "css", "style.css"), "body{margin:0}");
        File.WriteAllText(Path.Combine(demoDir, "img", "logo.png"), "png");

        string Page(string prefix, string title, string main, string head = "") =>
            $"<html><head><title>{title}</title><link rel=\"stylesheet\" href=\"{prefix}css/style.css\">{head}</head><body>\n" +
            $"<header><a href=\"{prefix}index.html\"><img src=\"{prefix}img/logo.png\" alt=\"logo\"></a><nav><ul>" +
            $"<li><a href=\"{prefix}index.html\">Home</a></li><li><a href=\"{prefix}about.html\">About</a></li>" +
            $"<li><a href=\"{prefix}services.html\">Services</a></li><li><a href=\"{prefix}news.html\">News</a></li></ul></nav></header>\n" +
            $"<main>{main}</main>\n<footer><p>Footer text</p><a href=\"{prefix}about.html\">About us</a></footer></body></html>";

        File.WriteAllText(Path.Combine(demoDir, "index.html"), Page("", "Home",
            "<section class=\"hero\"><h1>Precision parts</h1><p>Since 2006.</p><a href=\"about.html\">Learn more</a></section>"));
        File.WriteAllText(Path.Combine(demoDir, "about.html"), Page("", "About",
            "<h1>Company story</h1><p>Founded in 2006. See our <a href=\"services.html\">services</a>.</p>",
            "<style>.about-only{color:blue}</style>"));
        File.WriteAllText(Path.Combine(demoDir, "services.html"), Page("", "Services",
            "<h1>Our services</h1><div class=\"service-card\"><h3>Machining</h3><p>CNC work.</p></div>" +
            "<div class=\"service-card\"><h3>Moulds</h3><p>Mould design.</p></div>"));
        File.WriteAllText(Path.Combine(demoDir, "news.html"), Page("", "News",
            "<h1>Newsroom</h1>" +
            "<article class=\"article-card\"><h3><a href=\"news/expo.html\">Expo 2026</a></h3><p>We exhibit.</p></article>" +
            "<article class=\"article-card\"><h3><a href=\"news/plant.html\">New plant</a></h3><p>Now open.</p></article>"));
        File.WriteAllText(Path.Combine(demoDir, "news", "expo.html"), Page("../", "Expo 2026",
            "<article><h1>Expo 2026</h1><p>Booth A-101. Back to <a href=\"../news.html\">news</a>.</p></article>"));
        File.WriteAllText(Path.Combine(demoDir, "news", "plant.html"), Page("../", "New plant",
            "<article><h1>New plant</h1><p>Twenty thousand square metres.</p></article>"));

        var import = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "corp",
            RootDir = _rootDir,
            Force = true,
            BaseUrl = "/corp/"
        });

        var verify = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(import, _rootDir, "corp"));
        Assert.True(verify.ExitCode == 0, verify.StdErr);

        var dist = Path.Combine(_rootDir, "dist");
        string Built(string url) => File.ReadAllText(Path.Combine(dist, url.Trim('/'), "index.html"));

        // Each collection publishes only its own records, at its own routes.
        var newsList = Built("/insights/");
        Assert.Contains("Newsroom", newsList, StringComparison.Ordinal);
        Assert.Contains("href=\"/corp/insights/expo/\"", newsList, StringComparison.Ordinal);
        Assert.Contains("href=\"/corp/insights/plant/\"", newsList, StringComparison.Ordinal);
        Assert.DoesNotContain("Company story", newsList, StringComparison.Ordinal);
        Assert.Contains("Machining", Built("/services/"), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(dist, "index")), "The home page must not be published twice.");
        Assert.False(Directory.Exists(Path.Combine(dist, "insights", "about")));

        // Detail pages keep their wrapper markup and only their own styles.
        Assert.Contains("</article>", Built("/insights/expo/"), StringComparison.Ordinal);
        Assert.Contains(".about-only", Built("/about/"), StringComparison.Ordinal);
        Assert.DoesNotContain(".about-only", Built("/insights/expo/"), StringComparison.Ordinal);

        // Every internal link and asset resolves to a built file under the sub-path.
        var broken = new List<string>();
        foreach (var file in Directory.GetFiles(dist, "*.html", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}content{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                 !f.Contains($"{Path.DirectorySeparatorChar}.bukit", StringComparison.Ordinal)))
        {
            foreach (System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(
                         File.ReadAllText(file), "(?:href|src)=\"(?<url>[^\"]+)\""))
            {
                var url = link.Groups["url"].Value;
                if (url.StartsWith("http", StringComparison.Ordinal) || url.StartsWith('#'))
                    continue;
                var path = Uri.UnescapeDataString(url.Split('#')[0].Split('?')[0]);
                if (!path.StartsWith("/corp/", StringComparison.Ordinal))
                {
                    broken.Add($"{Path.GetRelativePath(dist, file)} -> {url} (no base path)");
                    continue;
                }

                var target = Path.Combine(dist, path["/corp/".Length..]);
                if (path.EndsWith('/'))
                    target = Path.Combine(target, "index.html");
                if (!File.Exists(target))
                    broken.Add($"{Path.GetRelativePath(dist, file)} -> {url}");
            }
        }

        Assert.True(broken.Count == 0, string.Join(Environment.NewLine, broken));
    }

    [Fact]
    public async Task VerifyAsync_FolderStyleChineseDemo_KeepsEveryPageItsTextAndEncoding()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var demoDir = Path.Combine(_rootDir, "folder-demo");
        foreach (var dir in new[] { "about", "contact", Path.Combine("dist", "css") })
            Directory.CreateDirectory(Path.Combine(demoDir, dir));
        // Built CSS in dist/ is an ordinary asset, not a secret.
        File.WriteAllText(Path.Combine(demoDir, "dist", "css", "app.css"), "body{margin:0}");

        string Page(string title, string main, string charset = "utf-8", string head = "") =>
            $"<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"{charset}\"><title>{title} - 华南精密</title>" +
            $"<link rel=\"stylesheet\" href=\"/dist/css/app.css\">{head}</head><body>\n" +
            "<header><nav><ul><li><a href=\"/\">首页</a></li><li><a href=\"/about/\">关于我们</a></li>" +
            "<li><a href=\"/contact/\">联系我们</a></li></ul></nav></header>\n" +
            $"<main>{main}</main>\n<footer><p>版权所有</p></footer></body></html>";

        File.WriteAllText(Path.Combine(demoDir, "index.html"), Page("首页",
            "<h1>精密制造</h1><p>欢迎访问华南精密。</p><div id=\"app\">{{ message }} {% raw %}</div>",
            head: "<link rel=\"canonical\" href=\"https://www.example.cn/\">"));
        File.WriteAllText(Path.Combine(demoDir, "about", "index.html"), Page("关于我们",
            "<h1>关于我们</h1><p>公司成立于2006年。</p>",
            head: "<meta name=\"description\" content=\"华南精密的发展历程\">"));
        File.WriteAllBytes(Path.Combine(demoDir, "contact", "index.html"),
            System.Text.Encoding.GetEncoding("GB18030").GetBytes(Page("联系我们", "<h1>联系我们</h1><p>联系电话 0755-1234。</p>", "gbk")));

        var import = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "folder",
            RootDir = _rootDir,
            Force = true
        });

        var verify = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(import, _rootDir, "folder"));
        Assert.True(verify.ExitCode == 0, verify.StdErr);

        var dist = Path.Combine(_rootDir, "dist");
        string Built(string url) => File.ReadAllText(Path.Combine(dist, url.Trim('/'), "index.html"));

        // Folder pages are their folder's page, not more home pages.
        var home = Built("/");
        Assert.Contains("欢迎访问华南精密。", home, StringComparison.Ordinal);
        Assert.Contains("href=\"/about/\"", home, StringComparison.Ordinal);
        Assert.Contains("href=\"/contact/\"", home, StringComparison.Ordinal);
        // Front-end template syntax in the demo prints as written.
        Assert.Contains("{{ message }} {% raw %}", home, StringComparison.Ordinal);
        Assert.Contains("公司成立于2006年。", Built("/about/"), StringComparison.Ordinal);
        Assert.Contains("联系电话 0755-1234。", Built("/contact/"), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(dist, "dist", "css", "app.css")), "dist/ assets must be copied.");

        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_rootDir, "sites", "folder", "site.yaml"));
        Assert.Equal("华南精密", config.Site.Name);
        Assert.Equal("https://www.example.cn", config.Site.Url);
        // The home page renders from pages/index.html; no seed record may publish it again.
        Assert.DoesNotContain("pages/index.html",
            File.ReadAllText(Path.Combine(_rootDir, "sites", "folder", "notion-seed", "pages.json")), StringComparison.Ordinal);
        Assert.Contains("seo_desc: 华南精密的发展历程",
            File.ReadAllText(Path.Combine(_rootDir, "sites", "folder", "content", "pages", "about.md")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_ReimportWithForce_UpdatesUneditedFilesKeepsEditedOnesAndBuilds()
    {
        var demoDir = Path.Combine(_rootDir, "reimport-demo");
        Directory.CreateDirectory(demoDir);
        void WritePage(string file, string title, string text) =>
            File.WriteAllText(Path.Combine(demoDir, file),
                $"<html><head><title>{title} | Acme</title></head><body><main><h1>{title}</h1><p>{text}</p></main></body></html>");
        WritePage("index.html", "Home", "Welcome.");
        WritePage("about.html", "About", "Founded in 2006.");
        WritePage("team.html", "Team", "Ten people.");
        WritePage("contact.html", "Contact", "Call us.");

        var options = new HtmlDemoImportOptions { InputPath = demoDir, ThemeName = "again", RootDir = _rootDir, Force = true };
        HtmlDemoImporter.Import(options);
        var pagesDir = Path.Combine(_rootDir, "sites", "again", "content", "pages");
        File.AppendAllText(Path.Combine(pagesDir, "team.md"), "\nEdited by hand.\n");

        // The demo changes: about's text, team's text, and contact is gone.
        WritePage("about.html", "About", "Founded in 2008.");
        WritePage("team.html", "Team", "Twelve people.");
        File.Delete(Path.Combine(demoDir, "contact.html"));
        var import = HtmlDemoImporter.Import(options);

        Assert.Contains("Founded in 2008.", File.ReadAllText(Path.Combine(pagesDir, "about.md")), StringComparison.Ordinal);
        var team = File.ReadAllText(Path.Combine(pagesDir, "team.md"));
        Assert.Contains("Edited by hand.", team, StringComparison.Ordinal);
        Assert.Contains("Ten people.", team, StringComparison.Ordinal);
        Assert.Contains(import.Warnings, w => w.Contains("Kept 'content/pages/team.md'", StringComparison.Ordinal));
        Assert.False(File.Exists(Path.Combine(pagesDir, "contact.md")), "A draft for a deleted page must be removed.");

        var verify = await ImportingCommandTestSupport.CaptureAsync(() =>
            ImportVerifyWorkflow.VerifyAsync(import, _rootDir, "again"));
        Assert.True(verify.ExitCode == 0, verify.StdErr);
        Assert.False(Directory.Exists(Path.Combine(_rootDir, "dist", "contact")));

        // --overwrite replaces the hand-edited draft.
        HtmlDemoImporter.Import(options with { Overwrite = true });
        Assert.Contains("Twelve people.", File.ReadAllText(Path.Combine(pagesDir, "team.md")), StringComparison.Ordinal);

        // An import that writes no drafts leaves the existing ones alone.
        HtmlDemoImporter.Import(options with { ExtractContent = false });
        Assert.True(File.Exists(Path.Combine(pagesDir, "about.md")), "Drafts must survive --no-extract-content.");
        Assert.True(File.Exists(Path.Combine(pagesDir, "team.md")));
    }
}
