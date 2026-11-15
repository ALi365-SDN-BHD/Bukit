using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Cli.Tests;

/// <summary>
/// The Bukit behaviour BukitJalil 0.1.0-beta.4 relies on (MIN_BUKIT_VERSION 2.2.3).
/// A change here is a consumer contract change: update BukitJalil's bukit-behavior.test.ts
/// and MIN_BUKIT_VERSION in the same release.
/// </summary>
[Collection("Console")]
public sealed class BukitJalilContractTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(),
        "bukit-bukitjalil-contract-" + Guid.NewGuid().ToString("N"));

    public BukitJalilContractTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Build_ParseFailure_ReturnsExitCodeThree()
    {
        var site = CreateSite("render");
        File.WriteAllText(Path.Combine(site, "layouts", "pages", "page.html"), "{{ 1 + }}\n");

        var result = await BuildAsync(site, "--log-format", "json");

        Assert.Equal(3, result.ExitCode);
        var schemaIndex = result.StdErr.LastIndexOf(
            "\"schema\": \"https://bukit.dev/schemas/cli-error",
            StringComparison.Ordinal);
        Assert.True(schemaIndex > 0, result.StdErr);
        var start = result.StdErr.LastIndexOf('{', schemaIndex);
        Assert.True(start >= 0, result.StdErr);

        using var error = JsonDocument.Parse(result.StdErr[start..]);
        Assert.Equal(3, error.RootElement.GetProperty("exitCode").GetInt32());
        var first = error.RootElement.GetProperty("errors")[0];
        var code = first.GetProperty("code").GetString();
        Assert.NotNull(code);
        Assert.StartsWith("BKT-03", code);
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Build_BaseUrl_NamedThemeNormalizesAndKeepsStaticAtRoot()
    {
        var site = CreateSite("base-url");
        var themeRoot = Path.Combine(site, "themes", "contract");
        Directory.CreateDirectory(themeRoot);
        Directory.Move(Path.Combine(site, "layouts"), Path.Combine(themeRoot, "layouts"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "static"));
        Directory.CreateDirectory(Path.Combine(themeRoot, "assets"));

        File.WriteAllText(Path.Combine(themeRoot, "theme.yaml"), """
            name: contract
            version: 1.0.0
            engine: bukit
            templates:
              home:
                template: pages/index.html
                required: true
              page:
                template: pages/page.html
                accepts:
                  collection: page
            """);
        const string robots = "User-agent: *\nAllow: /\n";
        File.WriteAllText(Path.Combine(themeRoot, "static", "robots.txt"), robots);
        File.WriteAllText(Path.Combine(themeRoot, "assets", "style.css"), "body {}\n");
        File.WriteAllText(Path.Combine(themeRoot, "layouts", "layouts", "base.html"), """
            {{ base_url = site.base_url }}
            {{ if base_url == "/" }}{{ base_url = "" }}{{ end }}
            <!DOCTYPE html>
            <html lang="{{ site.language }}">
            <head>
              <meta charset="utf-8" />
              <link rel="stylesheet" href="{{ base_url }}/assets/style.css" />
            </head>
            <body>
              <a href="{{ base_url }}/blog/">Blog</a>
              {{ content }}
            </body>
            </html>
            """);

        var siteYaml = Path.Combine(site, "site.yaml");
        var yaml = File.ReadAllText(siteYaml);
        Assert.Contains("theme:\n  layouts: layouts", yaml, StringComparison.Ordinal);
        File.WriteAllText(siteYaml, yaml.Replace(
            "theme:\n  layouts: layouts",
            "theme:\n  name: contract\n  layouts: layouts",
            StringComparison.Ordinal));

        var result = await BuildAsync(site, "--base-url", "/docs/");

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        var index = File.ReadAllText(Path.Combine(site, "out", "index.html"));
        Assert.Contains("href=\"/docs/assets/style.css\"", index, StringComparison.Ordinal);
        Assert.Contains("href=\"/docs/blog/\"", index, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/assets/", index, StringComparison.Ordinal);
        Assert.Equal(robots, File.ReadAllText(Path.Combine(site, "out", "robots.txt")));
        Assert.True(File.Exists(Path.Combine(site, "out", "assets", "style.css")));
    }

    [Fact]
    public async Task Build_JsonLog_EmitsContentSourceLoadedLinePerSource()
    {
        var site = CreateSite("json-log");

        var result = await BuildAsync(site, "--log-format", "json");

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        Assert.True(string.IsNullOrEmpty(result.StdOut), result.StdOut);

        var found = false;
        foreach (var line in result.StdErr.Split('\n'))
        {
            var text = line.Trim();
            if (!text.StartsWith('{'))
            {
                continue;
            }

            using var entry = JsonDocument.Parse(text);
            if (!entry.RootElement.TryGetProperty("msg", out var msg) ||
                msg.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var match = Regex.Match(
                msg.GetString()!,
                @"^event=content\.source\.loaded source=(.+?) count=(\d+)$");
            if (!match.Success)
            {
                continue;
            }

            Assert.Equal("pages", match.Groups[1].Value);
            Assert.Equal("1", match.Groups[2].Value);
            Assert.Equal("info", entry.RootElement.GetProperty("level").GetString()!.ToLowerInvariant());
            found = true;
            break;
        }

        Assert.True(found, result.StdErr);
    }

    [Fact]
    public async Task Build_SeoAndGeoReportsMatchConsumerSchema()
    {
        var site = CreateSite("seo");

        var result = await BuildAsync(site);

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        var reportDir = Path.Combine(site, "out", ".bukit");

        using var seo = JsonDocument.Parse(File.ReadAllText(Path.Combine(reportDir, "seo-report.json")));
        var root = seo.RootElement;
        if (root.TryGetProperty("siteUrl", out var siteUrl))
        {
            Assert.Equal(JsonValueKind.Null, siteUrl.ValueKind);
        }

        var issues = root.GetProperty("issues").EnumerateArray().ToArray();
        Assert.NotEmpty(issues);
        foreach (var issue in issues)
        {
            Assert.Equal(JsonValueKind.String, issue.GetProperty("severity").ValueKind);
            Assert.Equal(JsonValueKind.String, issue.GetProperty("code").ValueKind);
            Assert.Equal(JsonValueKind.String, issue.GetProperty("message").ValueKind);
            if (issue.TryGetProperty("route", out var route))
            {
                Assert.True(route.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            }
        }

        Assert.Contains(issues, issue =>
            issue.GetProperty("code").GetString() == "seo.canonical_not_absolute" &&
            issue.GetProperty("severity").GetString() == "warning");
        Assert.Contains(issues, issue =>
            issue.GetProperty("code").GetString() == "publish.sitemap_missing_route" &&
            issue.GetProperty("severity").GetString() == "warning");

        using var geo = JsonDocument.Parse(File.ReadAllText(Path.Combine(reportDir, "geo-report.json")));
        var geoRoot = geo.RootElement;
        var score = geoRoot.GetProperty("geoScore");
        Assert.Equal(JsonValueKind.Number, score.ValueKind);
        Assert.True(score.TryGetDouble(out var value) && double.IsFinite(value));
        if (geoRoot.TryGetProperty("llmsTxtGenerated", out var llmsTxt))
        {
            Assert.True(llmsTxt.ValueKind is JsonValueKind.True or JsonValueKind.False);
        }

        if (geoRoot.TryGetProperty("llmsFullTxtGenerated", out var llmsFullTxt))
        {
            Assert.True(llmsFullTxt.ValueKind is JsonValueKind.True or JsonValueKind.False);
        }

        if (geoRoot.TryGetProperty("geoEnhancedCount", out var enhanced))
        {
            Assert.Equal(JsonValueKind.Number, enhanced.ValueKind);
            Assert.True(enhanced.TryGetInt32(out var count) && count >= 0);
        }
    }

    [Fact]
    public async Task PublishAudit_RealBuild_ReportMatchesDeployConsumerShape()
    {
        var site = CreateSite("publish");

        var build = await BuildAsync(site);
        Assert.True(build.ExitCode == 0, build.StdOut + build.StdErr);

        var audit = await ProgramEntryPointTests.InvokeEntryPointAsync(
            ["publish", "audit", "--dir", Path.Combine(site, "out")]);

        Assert.True(audit.ExitCode == 0, audit.StdOut + audit.StdErr);
        using var report = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(site, "out", ".bukit", "publish-audit-report.json")));
        var root = report.RootElement;
        Assert.NotEmpty(root.GetProperty("documents").EnumerateArray());
        var issues = root.GetProperty("issues").EnumerateArray().ToArray();
        Assert.NotEmpty(issues);
        foreach (var issue in issues)
        {
            Assert.Equal(JsonValueKind.String, issue.GetProperty("severity").ValueKind);
            Assert.Equal(JsonValueKind.String, issue.GetProperty("code").ValueKind);
            Assert.Equal(JsonValueKind.String, issue.GetProperty("message").ValueKind);
            if (issue.TryGetProperty("route", out var route))
            {
                Assert.True(route.ValueKind is JsonValueKind.Null or JsonValueKind.String);
            }
        }
    }

    [Fact]
    public async Task ConfigSchema_OutputIsJsonAtTopLevel()
    {
        var output = Path.Combine(_tempDir, "schema.json");

        var result = await ProgramEntryPointTests.InvokeEntryPointAsync(
            ["config", "schema", "--output", output]);

        Assert.True(result.ExitCode == 0, result.StdOut + result.StdErr);
        using var schema = JsonDocument.Parse(File.ReadAllText(output));
        var root = schema.RootElement;
        Assert.Equal(JsonValueKind.String, root.GetProperty("$schema").ValueKind);
        var properties = root.GetProperty("properties");
        Assert.Equal(JsonValueKind.Object, properties.ValueKind);
        Assert.True(properties.TryGetProperty("site", out _));
        Assert.True(properties.TryGetProperty("content", out _));
    }

    private static Task<(int ExitCode, string StdOut, string StdErr)> BuildAsync(
        string site,
        params string[] extra)
        => ProgramEntryPointTests.InvokeEntryPointAsync(
            ["build", "--config", Path.Combine(site, "site.yaml"), "--output", "out", .. extra]);

    private string CreateSite(string name)
    {
        var source = Path.Combine(FindRepoRoot(), "tests", "fixtures", "basic-markdown-site");
        var target = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(target);
        File.Copy(Path.Combine(source, "site.yaml"), Path.Combine(target, "site.yaml"));
        CopyDirectory(Path.Combine(source, "content"), Path.Combine(target, "content"));
        CopyDirectory(Path.Combine(source, "layouts"), Path.Combine(target, "layouts"));
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
        }
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Bukit repository root was not found.");
    }
}
