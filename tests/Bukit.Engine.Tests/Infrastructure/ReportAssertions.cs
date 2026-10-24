using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Engine.Tests.Infrastructure;

internal static class ReportAssertions
{
    private static readonly string[] AuditReports = ["seo-report.json", "publish-audit-report.json", "seo-route-map.json", "geo-report.json"];
    private static readonly string[] RootReports = ["build-report.json", "routes.json", "assets.json", "incremental-manifest.json",
        "security-report.json", "release-bundle-checksums.json", "artifact-manifest.json", "build-manifest-digest.json", "publish-url-snapshot.json"];

    internal static void Schema(string fileName, JsonElement report)
    {
        using var inventory = Read(Path.Combine(SiteSandbox.RepositoryRoot, "docs/testing/core-report-inventory.json"));
        var record = Assert.Single(inventory.RootElement.GetProperty("records").EnumerateArray(), record =>
            record.GetProperty("localSchema").ValueKind == JsonValueKind.String &&
            record.GetProperty("artifactPaths").EnumerateArray().Any(path => Path.GetFileName(path.GetString()) == fileName));
        using var schema = Read(Path.Combine(SiteSandbox.RepositoryRoot, record.GetProperty("localSchema").GetString()!));
        Assert.Equal(record.GetProperty("declaredSchemaId").GetString(), report.GetProperty("schema").GetString());
        LocalJsonSchema.AssertValid(report, schema.RootElement);
    }

    public static void Generated(SiteBuilder site, BuildResult result, DateTimeOffset wallStart, DateTimeOffset wallEnd)
    {
        var errors = new List<string>();
        void Check(string name, Action check)
        {
            var error = Record.Exception(check);
            if (error is not null) errors.Add(name + ": " + error);
        }
        var languages = site.Config.Site.Languages?.ToArray() ?? [];
        var all = OutputSnapshot.Managed(("output", site.Output)).Entries
            .Where(pair => pair.Value.Kind == "file").ToDictionary(pair => pair.Key[7..], pair => pair.Value.Bytes, StringComparer.Ordinal);
        var expectedReports = RootReports.Concat(AuditReports).Select(name => ".bukit/" + name).ToList();
        foreach (var prefix in languages.Length == 0 ? new[] { "" } : languages.Select(language => language + "/"))
        {
            expectedReports.Add(prefix + ".bukit/analytics-report.json");
            if (prefix.Length > 0) expectedReports.AddRange(AuditReports.Select(name => prefix + ".bukit/" + name));
        }
        var actualReports = all.Keys.Where(path => path.EndsWith(".json", StringComparison.Ordinal) &&
            (path.StartsWith(".bukit/", StringComparison.Ordinal) || languages.Any(language => path.StartsWith(language + "/.bukit/", StringComparison.Ordinal))));
        Assert.Equal(expectedReports.Order(StringComparer.Ordinal), actualReports.Order(StringComparer.Ordinal));
        foreach (var path in expectedReports)
        {
            using var document = JsonDocument.Parse(all[path]);
            Check(path, () => Schema(Path.GetFileName(path), document.RootElement));
            if (document.RootElement.TryGetProperty("generatedAt", out var time))
                Check(path + " timestamp", () => Assert.InRange(time.GetDateTimeOffset(), wallStart, wallEnd));
        }

        JsonDocument Report(string name) => JsonDocument.Parse(all[".bukit/" + name + ".json"]);
        Check("build/cache/routes semantics", () =>
        {
            using var build = Report("build-report");
            var root = build.RootElement;
            Assert.Equal(SiteBuilder.BuildTime, root.GetProperty("startedAt").GetDateTimeOffset());
            Assert.Equal(result.EndedAt, root.GetProperty("endedAt").GetDateTimeOffset());
            Assert.InRange(result.EndedAt, wallStart, wallEnd);
            Assert.Equal(result.DurationMs, root.GetProperty("durationMs").GetInt64());
            Assert.True(result.DurationMs >= 0);
            Assert.Equal(site.Root, root.GetProperty("project").GetProperty("root").GetString());
            Assert.Equal("dist", root.GetProperty("project").GetProperty("output").GetString());
            var summary = root.GetProperty("summary");
            Assert.Equal(result.Summary.PageCount, summary.GetProperty("pageCount").GetInt32());
            Assert.Equal(result.Summary.RouteCount, summary.GetProperty("routeCount").GetInt32());
            Assert.Equal(result.Variants.Sum(variant => variant.RenderedCount + variant.SkippedCount), result.Summary.PageCount);
            Assert.Equal(result.Variants.Sum(variant => variant.RouteCount), result.Summary.RouteCount);
            Assert.Equal(all.Keys.Count(path => path.StartsWith("assets/", StringComparison.Ordinal)), summary.GetProperty("assetCount").GetInt32());
            Assert.Equal(all.Keys.Count(path => path.StartsWith("assets/uploads/", StringComparison.Ordinal)), summary.GetProperty("mediaCount").GetInt32());
            Assert.Equal(result.Summary.PluginCount, summary.GetProperty("pluginCount").GetInt32());
            Assert.Equal(result.Summary.WarningCount, summary.GetProperty("warningCount").GetInt32());
            Assert.Equal(result.Summary.ErrorCount, summary.GetProperty("errorCount").GetInt32());
            Assert.Equal(result.Summary.SchemaErrorCount, summary.GetProperty("schemaErrorCount").GetInt32());
            var publicFiles = OutputSnapshot.Public(site.Output, languages).Entries.Keys.ToArray();
            Assert.Equal(publicFiles, Strings(root.GetProperty("generatedFiles")).Order(StringComparer.Ordinal));
            Assert.Equal(publicFiles, result.GeneratedFiles.Order(StringComparer.Ordinal));

            using var incremental = Report("incremental-manifest");
            foreach (var counters in new[] { root.GetProperty("incremental"), incremental.RootElement })
            {
                Assert.False(counters.GetProperty("enabled").GetBoolean());
                Assert.Equal(0, counters.GetProperty("cacheHitCount").GetInt32());
                Assert.Equal(result.Summary.PageCount, counters.GetProperty("cacheMissCount").GetInt32());
            }
            var variants = incremental.RootElement.GetProperty("variants").EnumerateArray().ToArray();
            Assert.Equal(result.Variants.Count, variants.Length);
            foreach (var variant in variants)
            {
                var expected = Assert.Single(result.Variants, item => item.Language == variant.GetProperty("language").GetString());
                Assert.Equal(expected.OutputDir, variant.GetProperty("outputDir").GetString());
                Assert.Equal(expected.RenderedCount, variant.GetProperty("renderedCount").GetInt32());
                Assert.Equal(0, variant.GetProperty("skippedCount").GetInt32());
            }
            Assert.Equal(result.Summary.PageCount, incremental.RootElement.GetProperty("renderReasons").EnumerateObject().Sum(pair => pair.Value.GetInt32()));
            Assert.True(File.Exists(Path.Combine(site.Cache, "build-manifest.json")));
            using var cache = Read(Path.Combine(site.Cache, "build-manifest.json"));
            Assert.Equal(3, cache.RootElement.GetProperty("version").GetInt32());
            Assert.Equal("bukit-output\n", File.ReadAllText(Path.Combine(site.Output, ".bukit-output-marker")));
            using var state = Read(Path.Combine(site.Output, ".bukit-build-state.json"));
            Assert.Equal("completed", state.RootElement.GetProperty("status").GetString());
            Assert.InRange(state.RootElement.GetProperty("ts").GetDateTimeOffset(), wallStart, wallEnd);

            using var routes = Report("routes");
            var routeRows = routes.RootElement.GetProperty("routes").EnumerateArray().ToArray();
            Assert.Equal(result.Summary.RouteCount, routeRows.Count(row => row.GetProperty("kind").GetString() != "plugin"));
            foreach (var route in routeRows)
            {
                var prefix = languages.Length == 0 ? "" : route.GetProperty("language").GetString() + "/";
                Assert.True(all.ContainsKey(prefix + route.GetProperty("outputPath").GetString()), "Missing reported route output.");
            }
        });
        foreach (var prefix in new[] { "" }.Concat(languages.Select(language => language + "/")))
            Check(prefix + "audit semantics", () => AuditBundle(all, prefix));
        foreach (var variant in result.Variants)
        {
            Check(variant.Language + " analytics", () =>
            {
                var prefix = languages.Length == 0 ? "" : variant.Language + "/";
                using var analytics = JsonDocument.Parse(all[prefix + ".bukit/analytics-report.json"]);
                var analyticsRoot = analytics.RootElement;
                Assert.Equal(site.Config.Site.Analytics.Enabled, analyticsRoot.GetProperty("analyticsEnabled").GetBoolean());
                Assert.Empty(analyticsRoot.GetProperty("providerTypes").EnumerateArray());
                Assert.Equal(0, analyticsRoot.GetProperty("injectedHtml").GetInt32());
                Assert.Equal(variant.RenderedCount, analyticsRoot.GetProperty("processedHtml").GetInt32());
                Assert.Equal(analyticsRoot.GetProperty("processedHtml").GetInt32(),
                    analyticsRoot.GetProperty("skippedByReason").EnumerateObject().Sum(pair => pair.Value.GetInt32()));
            });
        }
        using var snapshot = Report("publish-url-snapshot");
        using var seo = Report("seo-report");
        using var publish = Report("publish-audit-report");
        Check("publish-url-snapshot", () => PublishSnapshot(snapshot.RootElement, seo.RootElement, publish.RootElement, all, site.Config.Site.Url!));
        using var security = Report("security-report");
        Check("security outcome", () =>
        {
            Assert.Equal("passed", security.RootElement.GetProperty("status").GetString());
            Assert.Empty(security.RootElement.GetProperty("warnings").EnumerateArray());
            Assert.Empty(security.RootElement.GetProperty("errors").EnumerateArray());
        });

        using var assets = Report("assets");
        var assetPaths = all.Keys.Where(path => !path.StartsWith(".bukit/", StringComparison.Ordinal) && path is not ".bukit-output-marker" and not ".bukit-build-state.json");
        Check("assets", () => Files(assets.RootElement, "assets", assetPaths, path => all[path]));
        using var release = Report("release-bundle-checksums");
        // Clean builds write the marker after reports; recovery state is internal metadata.
        Check("release-bundle-checksums", () => Files(release.RootElement, "files", all.Keys.Where(path => !path.StartsWith(".bukit/", StringComparison.Ordinal) && path is not ".bukit-output-marker" and not ".bukit-build-state.json"),
            path => all[path], "fileCount", "bundleHash"));
        using var artifacts = Report("artifact-manifest");
        var artifactPaths = expectedReports.Where(path => path.StartsWith(".bukit/", StringComparison.Ordinal))
            .Select(path => path[7..]).Where(path => path is not "artifact-manifest.json" and not "build-manifest-digest.json")
            .Concat(languages.Select(language => language + "/.bukit/analytics-report.json"));
        Check("artifact-manifest", () => Files(artifacts.RootElement, "artifacts", artifactPaths, path => all[path.Contains('/') ? path : ".bukit/" + path], "artifactCount", "artifactSetHash"));
        using var digest = Report("build-manifest-digest");
        var digestPaths = expectedReports.Where(path => path.StartsWith(".bukit/", StringComparison.Ordinal))
            .Select(path => path[7..]).Where(path => path != "build-manifest-digest.json");
        Check("build-manifest-digest", () => Files(digest.RootElement, "reports", digestPaths, path => all[".bukit/" + path], "reportCount", "reportSetHash"));
        foreach (var (property, file) in new[] { ("artifactManifestHash", "artifact-manifest"), ("releaseBundleHash", "release-bundle-checksums"), ("securityReportHash", "security-report") })
            Check(property, () => Assert.Equal(Hash(all[".bukit/" + file + ".json"]), digest.RootElement.GetProperty(property).GetString()));
        Assert.True(errors.Count == 0, string.Join("\n\n", errors));
    }

    internal static void AuditBundle(IReadOnlyDictionary<string, byte[]> files, string prefix)
    {
        using var seo = JsonDocument.Parse(files[prefix + ".bukit/seo-report.json"]);
        var routes = seo.RootElement.GetProperty("routes").EnumerateArray().ToArray();
        var issues = seo.RootElement.GetProperty("issues").EnumerateArray().ToArray();
        Counts(seo.RootElement, routes, "routeCount", issues);
        foreach (var route in routes) Assert.True(files.ContainsKey(prefix + route.GetProperty("outputPath").GetString()!.Replace('\\', '/')), "Missing SEO output.");
        using var publish = JsonDocument.Parse(files[prefix + ".bukit/publish-audit-report.json"]);
        var documents = publish.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        // Publish totals intentionally cover all SEO issues, while its own issue list is a subset.
        Counts(publish.RootElement, documents, "documentCount", issues);
        foreach (var count in new[] { "publishIssueCount", "machineReadabilityIssueCount", "trustIssueCount", "representationGapCount" })
            Assert.Equal(seo.RootElement.GetProperty("summary").GetProperty(count).GetInt32(),
                publish.RootElement.GetProperty("summary").GetProperty(count).GetInt32());
        Assert.Equal(issues.Count(issue => issue.GetProperty("code").GetString()!.StartsWith("publish.", StringComparison.OrdinalIgnoreCase)),
            publish.RootElement.GetProperty("summary").GetProperty("publishIssueCount").GetInt32());
        foreach (var issue in publish.RootElement.GetProperty("issues").EnumerateArray())
            Assert.Contains(issues, candidate => new[] { "severity", "code", "route", "message" }.All(field =>
                candidate.GetProperty(field).GetRawText() == issue.GetProperty(field).GetRawText()));
        Assert.Equal(routes.Select(row => row.GetProperty("url").GetString()).Order(StringComparer.Ordinal),
            documents.Select(row => row.GetProperty("routeUrl").GetString()).Order(StringComparer.Ordinal));
        foreach (var document in documents)
        {
            Assert.True(files.ContainsKey(prefix + document.GetProperty("outputPath").GetString()!.Replace('\\', '/')), "Missing publish output.");
            foreach (var representation in document.GetProperty("representations").EnumerateArray())
                if (representation.GetProperty("generated").GetBoolean())
                    Assert.True(files.ContainsKey(prefix + representation.GetProperty("path").GetString()), "Missing generated representation.");
        }
        using var map = JsonDocument.Parse(files[prefix + ".bukit/seo-route-map.json"]);
        Assert.Equal(routes.Select(row => row.GetProperty("url").GetString()).Order(StringComparer.Ordinal),
            map.RootElement.GetProperty("routes").EnumerateArray().Select(row => row.GetProperty("route").GetString()).Order(StringComparer.Ordinal));
        using var geo = JsonDocument.Parse(files[prefix + ".bukit/geo-report.json"]);
        Assert.Equal(geo.RootElement.GetProperty("geoEnhancedRoutes").GetArrayLength(), geo.RootElement.GetProperty("geoEnhancedCount").GetInt32());
        foreach (var field in new[] { "geoEnhancedCount", "geoScore", "llmsTxtGenerated", "llmsFullTxtGenerated" })
            Assert.Equal(seo.RootElement.GetProperty("summary").GetProperty(field).GetRawText(), geo.RootElement.GetProperty(field).GetRawText());
    }

    private static void Counts(JsonElement report, JsonElement[] rows, string count, JsonElement[] issues)
    {
        var summary = report.GetProperty("summary");
        Assert.Equal(rows.Length, summary.GetProperty(count).GetInt32());
        var indexable = rows.Count(row => row.GetProperty("indexable").GetBoolean());
        Assert.Equal(indexable, summary.GetProperty("indexableCount").GetInt32());
        Assert.Equal(rows.Length - indexable, summary.GetProperty("nonIndexableCount").GetInt32());
        foreach (var severity in new[] { "error", "warning" })
            Assert.Equal(issues.Count(issue => string.Equals(issue.GetProperty("severity").GetString(), severity, StringComparison.OrdinalIgnoreCase)),
                summary.GetProperty(severity + "Count").GetInt32());
    }

    internal static void PublishSnapshot(JsonElement snapshot, JsonElement seo, JsonElement publish,
        IReadOnlyDictionary<string, byte[]> files, string siteUrl)
    {
        Assert.Equal(new Uri(siteUrl).AbsoluteUri, snapshot.GetProperty("siteUrl").GetString());
        var routes = seo.GetProperty("routes").EnumerateArray().Where(row => row.GetProperty("indexable").GetBoolean())
            .OrderBy(row => row.GetProperty("canonical").GetString(), StringComparer.Ordinal).ToArray();
        var actual = snapshot.GetProperty("routes").EnumerateArray().ToArray();
        Assert.Equal(routes.Select(row => row.GetProperty("canonical").GetString()), actual.Select(row => row.GetProperty("url").GetString()));
        foreach (var (expected, row) in routes.Zip(actual))
        {
            Assert.True(row.GetProperty("indexable").GetBoolean());
            var document = Assert.Single(publish.GetProperty("documents").EnumerateArray(), item =>
                item.GetProperty("routeUrl").GetString() == expected.GetProperty("url").GetString());
            var projection = document.GetProperty("representations").EnumerateArray().SingleOrDefault(item =>
                item.GetProperty("kind").GetString() == "json" && item.GetProperty("generated").GetBoolean());
            var body = "";
            if (projection.ValueKind != JsonValueKind.Undefined)
            {
                using var content = JsonDocument.Parse(files[projection.GetProperty("path").GetString()!]);
                body = content.RootElement.GetProperty("body").GetString() ?? "";
            }
            // shortcut: author-free, stable phase 0 fixtures; extend the independent projection when fixture author/privacy fields are added.
            Assert.True(string.IsNullOrEmpty(expected.GetProperty("author").GetString()));
            var html = Encoding.UTF8.GetString(files[expected.GetProperty("outputPath").GetString()!.Replace('\\', '/')]);
            var jsonLd = Regex.Matches(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline)
                .Select(match =>
                {
                    using var json = JsonDocument.Parse(match.Groups[1].Value);
                    return JsonSerializer.Serialize(CanonicalJson(json.RootElement));
                }).Order(StringComparer.Ordinal).ToArray();
            var payload = new
            {
                body = StableText(body), title = StableText(expected.GetProperty("title").GetString()),
                description = StableText(expected.GetProperty("description").GetString()),
                canonical = new Uri(expected.GetProperty("canonical").GetString()!).AbsoluteUri,
                robots = StableText(expected.GetProperty("robots").GetString()),
                author = new { name = "", type = "", url = "", sameAs = Array.Empty<string>() }, jsonLd
            };
            Assert.Equal(Hash(JsonSerializer.SerializeToUtf8Bytes(payload)), row.GetProperty("semanticHash").GetString());
        }
    }

    private static string StableText(string? text)
    {
        text ??= "";
        Assert.DoesNotMatch(@"(?:file://|/(?:Users|private|var|tmp|home)/|[A-Za-z]:\\|\b[0-9a-fA-F]{32}\b|\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b)", text);
        return text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();
    }

    private static object CanonicalJson(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(property => property.Name, property => CanonicalJson(property.Value)),
        JsonValueKind.Array => value.EnumerateArray().Select(CanonicalJson).ToArray(),
        _ => value.Clone()
    };

    internal static void Files(JsonElement report, string array, IEnumerable<string> expectedPaths, Func<string, byte[]> read,
        string? count = null, string? aggregateHash = null)
    {
        var entries = report.GetProperty(array).EnumerateArray().ToArray();
        Assert.Equal(expectedPaths.Order(StringComparer.OrdinalIgnoreCase), entries.Select(entry => entry.GetProperty("path").GetString()));
        if (count is not null) Assert.Equal(entries.Length, report.GetProperty(count).GetInt32());
        var aggregate = new StringBuilder();
        var errors = new List<Exception>();
        foreach (var entry in entries)
        {
            var path = entry.GetProperty("path").GetString()!;
            var bytes = read(path);
            var hash = Hash(bytes);
            var error = Record.Exception(() =>
            {
                Assert.True(hash == entry.GetProperty("hash").GetString(), $"{array}: stale hash for {path}; recorded={entry.GetProperty("hash").GetString()}; actual={hash}");
                Assert.Equal(bytes.LongLength, entry.GetProperty("size").GetInt64());
                if (entry.TryGetProperty("source", out var source)) Assert.Equal(path, source.GetString());
            });
            if (error is not null) errors.Add(error);
            aggregate.Append(path).Append('|').Append(hash).Append('|').Append(bytes.LongLength.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        if (aggregateHash is not null)
        {
            var error = Record.Exception(() => Assert.Equal(Hash(Encoding.UTF8.GetBytes(aggregate.ToString())), report.GetProperty(aggregateHash).GetString()));
            if (error is not null) errors.Add(error);
        }
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    internal static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static IEnumerable<string> Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString()!);
    private static JsonDocument Read(string path) => JsonDocument.Parse(File.ReadAllText(path));
}
