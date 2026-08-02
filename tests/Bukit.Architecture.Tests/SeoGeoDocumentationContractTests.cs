using System.Text.Json;
using System.Xml.Linq;
using Bukit.Config;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class SeoGeoDocumentationContractTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ActiveSchemas_MatchSeoGeoPublicContracts()
    {
        using var configSchema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var site = configSchema.RootElement.GetProperty("properties").GetProperty("site");
        var organization = site
            .GetProperty("properties")
            .GetProperty("seo")
            .GetProperty("properties")
            .GetProperty("organization")
            .GetProperty("properties");

        Assert.Equal(
            ["Organization", "NewsMediaOrganization"],
            organization.GetProperty("type").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("array", organization.GetProperty("sameAs").GetProperty("type").GetString());

        var collection = site
            .GetProperty("properties")
            .GetProperty("collections")
            .GetProperty("additionalProperties")
            .GetProperty("properties");
        Assert.Equal("boolean", collection.GetProperty("noindexWhenEmpty").GetProperty("type").GetString());

        using var snapshotSchema = ReadJson("docs", "schemas", "publish-url-snapshot.v1.schema.json");
        Assert.Equal(
            "https://bukit.dev/schemas/publish-url-snapshot.v1.json",
            snapshotSchema.RootElement.GetProperty("$id").GetString());
        Assert.Equal(
            ["schema", "siteUrl", "routes"],
            snapshotSchema.RootElement.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var siteUrl = snapshotSchema.RootElement.GetProperty("properties").GetProperty("siteUrl");
        Assert.Equal(string.Empty, siteUrl.GetProperty("oneOf")[0].GetProperty("const").GetString());
        Assert.Equal("^https?://", siteUrl.GetProperty("oneOf")[1].GetProperty("pattern").GetString());
        var route = snapshotSchema.RootElement
            .GetProperty("properties")
            .GetProperty("routes")
            .GetProperty("items");
        Assert.Equal(
            ["url", "indexable", "semanticHash"],
            route.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^sha256:[0-9a-f]{64}$",
            route.GetProperty("properties").GetProperty("semanticHash").GetProperty("pattern").GetString());
        Assert.Equal(
            "^https?://",
            route.GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());

        using var changeSetSchema = ReadJson("docs", "schemas", "publish-url-change-set.v1.schema.json");
        var change = changeSetSchema.RootElement
            .GetProperty("properties")
            .GetProperty("changes")
            .GetProperty("items");
        Assert.Equal(
            ["added", "updated", "deleted"],
            change.GetProperty("properties").GetProperty("type").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["type", "url", "semanticHash"], change.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^https?://",
            change.GetProperty("properties").GetProperty("url").GetProperty("pattern").GetString());

        using var routeMapSchema = ReadJson("docs", "schemas", "seo-route-map.v1.schema.json");
        var routeMapRoot = routeMapSchema.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", routeMapRoot.GetProperty("$schema").GetString());
        Assert.Equal("https://bukit.dev/schemas/seo-route-map.v1.json", routeMapRoot.GetProperty("$id").GetString());
        Assert.False(routeMapRoot.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            ["schema", "schemaVersion", "generatedAt", "siteUrl", "baseUrl", "routes"],
            routeMapRoot.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        var routeMapSiteUrl = routeMapRoot.GetProperty("properties").GetProperty("siteUrl");
        Assert.Equal(string.Empty, routeMapSiteUrl.GetProperty("oneOf")[0].GetProperty("const").GetString());
        Assert.Equal("^https?://", routeMapSiteUrl.GetProperty("oneOf")[1].GetProperty("pattern").GetString());

        var routeMapRoutes = routeMapRoot.GetProperty("properties").GetProperty("routes");
        Assert.False(routeMapRoutes.TryGetProperty("uniqueItems", out _));
        var routeMapEntry = routeMapRoutes.GetProperty("items");
        Assert.False(routeMapEntry.GetProperty("additionalProperties").GetBoolean());
        Assert.DoesNotContain(
            "contentKey",
            routeMapEntry.GetProperty("required").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(
            "^route:sha256:[0-9a-f]{64}$",
            routeMapEntry.GetProperty("properties").GetProperty("routeKey").GetProperty("pattern").GetString());
        Assert.Equal("^/", routeMapEntry.GetProperty("properties").GetProperty("route").GetProperty("pattern").GetString());
        var canonical = routeMapEntry.GetProperty("properties").GetProperty("canonical");
        Assert.Equal("^https?://", canonical.GetProperty("oneOf")[0].GetProperty("pattern").GetString());
        Assert.Equal("^/", canonical.GetProperty("oneOf")[1].GetProperty("pattern").GetString());
        var contentKey = routeMapEntry.GetProperty("properties").GetProperty("contentKey").GetProperty("oneOf");
        Assert.Equal("^content:sha256:[0-9a-f]{64}$", contentKey[0].GetProperty("pattern").GetString());
        Assert.Equal("null", contentKey[1].GetProperty("type").GetString());
    }

    [Fact]
    public void ActiveGuide_DocumentsSeoGeoAndIndexNowContracts()
    {
        var siteConfig = ReadText("guide", "user", "04-site-yaml-config.md");
        var seo = ReadText("guide", "user", "11-i18n-seo.md");
        var parameters = ReadText("guide", "user", "16-parameter-cheatsheet.md");
        var pluginGuide = ReadText("guide", "dev", "plugins.md");

        Assert.Contains("organization.type", siteConfig, StringComparison.Ordinal);
        Assert.Contains("organization.sameAs", siteConfig, StringComparison.Ordinal);
        Assert.Contains("noindexWhenEmpty", siteConfig, StringComparison.Ordinal);
        Assert.Contains("NewsMediaOrganization", seo, StringComparison.Ordinal);
        Assert.Contains("noindexWhenEmpty", parameters, StringComparison.Ordinal);

        Assert.Contains("indexnow submit", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("INDEXNOW_KEY", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("publish-url-snapshot.v1", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("publish-url-change-set.v1", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("osx-arm64", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("SHA-256", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("internal", pluginGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phase 1", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("Deploy the generated output to GitHub Pages", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("rerun the same command", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("HTTP 200", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("response body exactly equals `INDEXNOW_KEY`", pluginGuide, StringComparison.Ordinal);
        Assert.Contains("only then", pluginGuide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProtectedReferenceTrees_AreNotActiveInputs()
    {
        var protectedNames = new[]
        {
            ".github/workflows-0.1",
            "workflows-0.1",
            "scripts-0.1",
            "scripts-0.2",
            "guide-0.1",
            "guide-0.2"
        };
        var activeRoots = new[] { ".github/workflows", "scripts", "src", "guide" };
        var violations = new List<string>();

        foreach (var relativeRoot in activeRoots)
        {
            var root = Path.Combine(RepoRoot, relativeRoot);
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                        !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                        Path.GetFileName(path) is not "active-workflow-boundary.sh" and not "active-workflow-boundary-self-test.sh"))
            {
                var relativePath = Path.GetRelativePath(RepoRoot, path).Replace(Path.DirectorySeparatorChar, '/');
                var lineNumber = 0;
                foreach (var line in File.ReadLines(path))
                {
                    lineNumber++;
                    if (protectedNames.Any(name => line.Contains(name, StringComparison.Ordinal)) &&
                        !IsAllowedProtectedReference(relativePath, line))
                    {
                        violations.Add($"{relativePath}:{lineNumber}");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    private static bool IsAllowedProtectedReference(string relativePath, string line)
        => relativePath switch
        {
            "guide/README.md" =>
                line.Contains("`guide-0.2` snapshot informed its information architecture", StringComparison.Ordinal) ||
                line == "If present, `guide-0.1`, `guide-0.2`, `scripts-0.1`, and `scripts-0.2` are",
            "guide/dev/agent-task-workflow.md" =>
                line == "- Do not create, synchronize, or modify `guide-0.1/`, `guide-0.2/`," ||
                line == "  `scripts-0.1/`, or `scripts-0.2/` by default; their absence is valid. Touch",
            _ => false
        };

    [Fact]
    public void IndexNow_IsInSolutionsAndInternalInstallManifest()
    {
        var pluginProjects = ReadSolutionProjectPaths("bukit-plugins.slnx");
        Assert.Contains("src/Bukit-Plugins/Bukit.IndexNow/Bukit.IndexNow.csproj", pluginProjects);
        Assert.Contains("src/Bukit-Plugins/Bukit.Plugin.IndexNow/Bukit.Plugin.IndexNow.csproj", pluginProjects);

        var testProjects = ReadSolutionProjectPaths("bukit-test.slnx");
        Assert.Contains("tests/Bukit.Plugin.IndexNow.Tests/Bukit.Plugin.IndexNow.Tests.csproj", testProjects);

        using var install = ReadJson("docs", "internal", "seo-geo-wp1-osx-arm64.install.json");
        Assert.Equal(
            "d186278302caa3b757d1a233468c4fe7e89766b2",
            install.RootElement.GetProperty("sourceCommit").GetString());
        var core = install.RootElement.GetProperty("core");
        Assert.Equal("2.0.0", core.GetProperty("version").GetString());
        Assert.Equal("osx-arm64", core.GetProperty("rid").GetString());
        Assert.Matches("^[0-9a-f]{64}$", core.GetProperty("archiveSha256").GetString());
        Assert.False(string.IsNullOrWhiteSpace(core.GetProperty("installTarget").GetString()));

        var plugin = install.RootElement.GetProperty("plugin");
        Assert.Equal("indexnow", plugin.GetProperty("id").GetString());
        Assert.Equal("0.1.0", plugin.GetProperty("version").GetString());
        Assert.Equal("bukit-plugin-v1", plugin.GetProperty("protocol").GetString());
        Assert.Equal("osx-arm64", plugin.GetProperty("rid").GetString());
        Assert.Matches("^[0-9a-f]{64}$", plugin.GetProperty("entrySha256").GetString());
        Assert.Matches("^[0-9a-f]{64}$", plugin.GetProperty("packageSha256").GetString());
        Assert.False(string.IsNullOrWhiteSpace(plugin.GetProperty("installTarget").GetString()));
        Assert.Equal(
            "stage/plugins/indexnow/bin/osx-arm64/bukit-plugin-indexnow",
            plugin.GetProperty("stagedEntry").GetString());
        Assert.Equal(
            "plugins/indexnow/bin/osx-arm64/bukit-plugin-indexnow",
            plugin.GetProperty("packageEntry").GetString());
        Assert.NotEqual(plugin.GetProperty("stagedEntry").GetString(), plugin.GetProperty("packageEntry").GetString());
        Assert.False(plugin.TryGetProperty("entry", out _));

        var manifest = ReadText(
            "src", "Bukit-Plugins", "Bukit.Plugin.IndexNow", "examples", "minimal", "plugins", "indexnow", "plugin.yaml");
        Assert.Contains($"sha256: {plugin.GetProperty("entrySha256").GetString()}", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("0000000000000000000000000000000000000000000000000000000000000000", manifest, StringComparison.Ordinal);

        var serialized = install.RootElement.GetRawText();
        Assert.DoesNotContain("INDEXNOW_KEY", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"state\"", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"report\"", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonDocument ReadJson(params string[] segments)
        => JsonDocument.Parse(ReadText(segments));

    private static string ReadText(params string[] segments)
        => File.ReadAllText(Path.Combine([RepoRoot, .. segments]));

    private static string[] ReadSolutionProjectPaths(string relativePath)
        => XDocument.Load(Path.Combine(RepoRoot, relativePath))
            .Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .OfType<string>()
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
