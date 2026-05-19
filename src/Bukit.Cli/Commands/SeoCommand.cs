using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

public static partial class SeoCommand
{
    private const string ExpectedSchema = "https://bukit.dev/schemas/seo-report.v1.json";
    private const string ExpectedSchemaVersion = "1.0";

    public static async Task<int> RunAsync(ArgReader reader)
    {
        var subcommand = reader.GetArg(1);
        if (string.Equals(subcommand, "audit", StringComparison.OrdinalIgnoreCase))
        {
            var reportPath = reader.GetOption("--report");
            var dir = reader.GetOption("--dir") ?? "dist";
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                reportPath = Path.Combine(dir, "seo-report.json");
            }

            return await AuditAsync(reportPath, dir, strict: reader.HasFlag("--strict"), external: reader.HasFlag("--external"));
        }

        if (string.Equals(subcommand, "diff", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var baseline = reader.GetOption("--baseline") ?? reader.GetArg(2);
                var current = reader.GetOption("--current") ?? reader.GetArg(3);
                return Diff(
                    baseline,
                    current,
                    maxNewErrors: ReadOptionalInt(reader.GetOption("--max-new-errors")),
                    maxNewWarnings: ReadOptionalInt(reader.GetOption("--max-new-warnings")),
                    maxNewIssues: ReadOptionalInt(reader.GetOption("--max-new-issues")),
                    failOnNewCodes: SplitCsv(reader.GetOption("--fail-on-new-code")),
                    failOnRouteRemoved: reader.HasFlag("--fail-on-route-removed"),
                    failOnIndexableDrop: reader.HasFlag("--fail-on-indexable-drop"));
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"Invalid SEO diff option: {ex.Message}");
                return 2;
            }
        }

        Console.Error.WriteLine("Usage: bukit seo audit [--dir dist] [--report seo-report.json] [--strict] [--external]");
        Console.Error.WriteLine("       bukit seo diff --baseline old-report.json --current new-report.json [--max-new-errors n] [--max-new-warnings n] [--max-new-issues n] [--fail-on-new-code code1,code2] [--fail-on-route-removed] [--fail-on-indexable-drop]");
        return 2;
    }

    internal static int Audit(string reportPath, bool strict)
        => AuditAsync(reportPath, Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? ".", strict, external: false).GetAwaiter().GetResult();

    internal static async Task<int> AuditAsync(string reportPath, string outputDir, bool strict, bool external)
    {
        var fullPath = Path.GetFullPath(reportPath);
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"SEO report not found: {fullPath}");
            return 2;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
            ValidateReportContract(doc.RootElement);

            var summary = doc.RootElement.GetProperty("summary");
            var errorCount = ReadRequiredInt(summary, "summary", "errorCount");
            var warningCount = ReadRequiredInt(summary, "summary", "warningCount");
            var routeCount = ReadRequiredInt(summary, "summary", "routeCount");

            Console.WriteLine($"SEO audit: routes={routeCount} errors={errorCount} warnings={warningCount}");
            if (doc.RootElement.TryGetProperty("issues", out var issues))
            {
                foreach (var issue in issues.EnumerateArray())
                {
                    var severity = ReadString(issue, "severity");
                    var code = ReadString(issue, "code");
                    var route = ReadString(issue, "route") ?? "-";
                    var message = ReadString(issue, "message");
                    Console.WriteLine($"{severity} {code} {route} {message}");
                }
            }

            if (external)
            {
                var externalResult = await RunExternalAuditAsync(doc.RootElement, outputDir);
                errorCount += externalResult.Errors;
                warningCount += externalResult.Warnings;
            }

            return errorCount > 0 || (strict && warningCount > 0) ? 1 : 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report JSON: {ex.Message}");
            return 2;
        }
        catch (KeyNotFoundException)
        {
            Console.Error.WriteLine("Invalid SEO report: missing summary.");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report: {ex.Message}");
            return 2;
        }
    }

    internal static int Diff(
        string? baselinePath,
        string? currentPath,
        int? maxNewErrors,
        int? maxNewWarnings,
        int? maxNewIssues,
        IReadOnlySet<string> failOnNewCodes,
        bool failOnRouteRemoved,
        bool failOnIndexableDrop)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || string.IsNullOrWhiteSpace(currentPath))
        {
            Console.Error.WriteLine("Usage: bukit seo diff --baseline old-report.json --current new-report.json");
            return 2;
        }

        try
        {
            using var baselineDoc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(baselinePath)));
            using var currentDoc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(currentPath)));
            ValidateReportContract(baselineDoc.RootElement);
            ValidateReportContract(currentDoc.RootElement);

            var baseline = SeoReportSnapshot.From(baselineDoc.RootElement);
            var current = SeoReportSnapshot.From(currentDoc.RootElement);

            var baselineIssues = baseline.Issues.ToHashSet();
            var currentIssues = current.Issues.ToHashSet();
            var newIssues = currentIssues.Except(baselineIssues).OrderBy(x => x.SortKey, StringComparer.Ordinal).ToArray();
            var resolvedIssues = baselineIssues.Except(currentIssues).OrderBy(x => x.SortKey, StringComparer.Ordinal).ToArray();
            var newErrors = newIssues.Count(x => string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase));
            var newWarnings = newIssues.Count(x => string.Equals(x.Severity, "warning", StringComparison.OrdinalIgnoreCase));

            var addedRoutes = current.Routes.Keys.Except(baseline.Routes.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var removedRoutes = baseline.Routes.Keys.Except(current.Routes.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var indexableDrops = baseline.Routes
                .Where(x => x.Value.Indexable &&
                            current.Routes.TryGetValue(x.Key, out var route) &&
                            !route.Indexable)
                .Select(x => x.Key)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Console.WriteLine($"SEO diff: newIssues={newIssues.Length} newErrors={newErrors} newWarnings={newWarnings} resolvedIssues={resolvedIssues.Length} addedRoutes={addedRoutes.Length} removedRoutes={removedRoutes.Length} indexableDrops={indexableDrops.Length}");
            foreach (var issue in newIssues)
            {
                Console.WriteLine($"+ {issue.Severity} {issue.Code} {issue.Route ?? "-"} {issue.Message}");
            }

            foreach (var route in removedRoutes)
            {
                Console.WriteLine($"- route {route}");
            }

            foreach (var route in indexableDrops)
            {
                Console.WriteLine($"! indexable-drop {route}");
            }

            var failed = false;
            if (maxNewErrors is not null && newErrors > maxNewErrors.Value)
            {
                Console.Error.WriteLine($"SEO diff budget exceeded: new errors {newErrors} > {maxNewErrors.Value}.");
                failed = true;
            }

            if (maxNewWarnings is not null && newWarnings > maxNewWarnings.Value)
            {
                Console.Error.WriteLine($"SEO diff budget exceeded: new warnings {newWarnings} > {maxNewWarnings.Value}.");
                failed = true;
            }

            if (maxNewIssues is not null && newIssues.Length > maxNewIssues.Value)
            {
                Console.Error.WriteLine($"SEO diff budget exceeded: new issues {newIssues.Length} > {maxNewIssues.Value}.");
                failed = true;
            }

            foreach (var code in failOnNewCodes)
            {
                if (newIssues.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.Error.WriteLine($"SEO diff budget exceeded: new issue code {code}.");
                    failed = true;
                }
            }

            if (failOnRouteRemoved && removedRoutes.Length > 0)
            {
                Console.Error.WriteLine("SEO diff budget exceeded: routes were removed.");
                failed = true;
            }

            if (failOnIndexableDrop && indexableDrops.Length > 0)
            {
                Console.Error.WriteLine("SEO diff budget exceeded: indexable routes became non-indexable.");
                failed = true;
            }

            return failed ? 1 : 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report JSON: {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Failed to read SEO report: {ex.Message}");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report: {ex.Message}");
            return 2;
        }
    }

    private static void ValidateReportContract(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("root must be a JSON object.");
        }

        EnsureAllowedProperties(root, "$", "schema", "schemaVersion", "generatedAt", "siteName", "siteUrl", "baseUrl", "routes", "issues", "summary");

        var schema = ReadRequiredString(root, "$", "schema");
        if (!string.Equals(schema, ExpectedSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{ExpectedSchema}'.");
        }

        var schemaVersion = ReadRequiredString(root, "$", "schemaVersion");
        if (!string.Equals(schemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schemaVersion '{schemaVersion}'. Expected '{ExpectedSchemaVersion}'.");
        }

        var routes = ReadRequiredArray(root, "$", "routes");
        var issues = ReadRequiredArray(root, "$", "issues");
        var summary = ReadRequiredObject(root, "$", "summary");

        ReadRequiredString(root, "$", "generatedAt");
        ReadRequiredString(root, "$", "siteName");
        ReadRequiredString(root, "$", "baseUrl");
        ReadOptionalString(root, "$", "siteUrl");
        EnsureAllowedProperties(summary, "summary", "routeCount", "indexableCount", "nonIndexableCount", "errorCount", "warningCount", "llmsTxtGenerated", "llmsFullTxtGenerated", "geoEnhancedCount", "geoScore");
        ReadRequiredInt(summary, "summary", "routeCount");
        ReadRequiredInt(summary, "summary", "indexableCount");
        ReadRequiredInt(summary, "summary", "nonIndexableCount");
        ReadRequiredInt(summary, "summary", "errorCount");
        ReadRequiredInt(summary, "summary", "warningCount");
        ReadOptionalBool(summary, "summary", "llmsTxtGenerated");
        ReadOptionalBool(summary, "summary", "llmsFullTxtGenerated");
        ReadOptionalInt(summary, "summary", "geoEnhancedCount");
        ReadOptionalInt(summary, "summary", "geoScore");

        var routeIndex = 0;
        foreach (var route in routes.EnumerateArray())
        {
            var path = $"routes[{routeIndex}]";
            EnsureObject(route, path);
            EnsureAllowedProperties(route, path, "url", "outputPath", "title", "description", "canonical", "robots", "indexable", "lastModified", "contentType", "sourceItemId", "sitemapIncluded", "searchIncluded", "rssIncluded", "alternates", "schemaTypes");
            ReadRequiredString(route, path, "url");
            ReadRequiredString(route, path, "outputPath");
            ReadOptionalString(route, path, "title");
            ReadOptionalString(route, path, "description");
            ReadRequiredString(route, path, "canonical");
            ReadOptionalString(route, path, "robots");
            ReadRequiredBool(route, path, "indexable");
            ReadRequiredString(route, path, "lastModified");
            ReadOptionalString(route, path, "contentType");
            ReadOptionalString(route, path, "sourceItemId");
            ReadRequiredBool(route, path, "sitemapIncluded");
            ReadRequiredBool(route, path, "searchIncluded");
            ReadRequiredBool(route, path, "rssIncluded");
            var alternates = ReadRequiredArray(route, path, "alternates");
            var altIndex = 0;
            foreach (var alternate in alternates.EnumerateArray())
            {
                var altPath = $"{path}.alternates[{altIndex}]";
                EnsureObject(alternate, altPath);
                EnsureAllowedProperties(alternate, altPath, "hreflang", "href");
                ReadRequiredString(alternate, altPath, "hreflang");
                ReadRequiredString(alternate, altPath, "href");
                altIndex++;
            }

            var schemaTypes = ReadRequiredArray(route, path, "schemaTypes");
            var schemaTypeIndex = 0;
            foreach (var schemaType in schemaTypes.EnumerateArray())
            {
                if (schemaType.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"{path}.schemaTypes[{schemaTypeIndex}] must be a string.");
                }

                schemaTypeIndex++;
            }

            routeIndex++;
        }

        var issueIndex = 0;
        foreach (var issue in issues.EnumerateArray())
        {
            var path = $"issues[{issueIndex}]";
            EnsureObject(issue, path);
            EnsureAllowedProperties(issue, path, "severity", "code", "route", "message");
            var severity = ReadRequiredString(issue, path, "severity");
            if (!string.Equals(severity, "error", StringComparison.Ordinal) &&
                !string.Equals(severity, "warning", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{path}.severity must be 'error' or 'warning'.");
            }

            ReadRequiredString(issue, path, "code");
            ReadRequiredString(issue, path, "message");
            if (issue.TryGetProperty("route", out var route) &&
                route.ValueKind != JsonValueKind.Null &&
                route.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{path}.route must be a string or null.");
            }

            issueIndex++;
        }
    }

    private static async Task<(int Errors, int Warnings)> RunExternalAuditAsync(JsonElement report, string outputDir)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("bukit-seo-audit/1.0");
        var errors = 0;
        var warnings = 0;
        var checkedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var route in report.GetProperty("routes").EnumerateArray())
        {
            var routeUrl = ReadRequiredString(route, "route", "url");
            var canonical = ReadRequiredString(route, "route", "canonical");
            if (await CheckUrlAsync(http, canonical, $"canonical {routeUrl}", checkedUrls, severity: "error"))
            {
                errors++;
            }

            var outputPath = Path.Combine(outputDir, ReadRequiredString(route, "route", "outputPath"));
            if (!File.Exists(outputPath))
            {
                continue;
            }

            var html = File.ReadAllText(outputPath);
            foreach (var image in ExtractImageUrls(html))
            {
                var result = await CheckUrlAsync(http, image, $"image {routeUrl}", checkedUrls, requireImage: true);
                if (result)
                {
                    warnings++;
                }
            }

            foreach (var link in ExtractLinks(html, canonical))
            {
                var result = await CheckUrlAsync(http, link, $"link {routeUrl}", checkedUrls);
                if (result)
                {
                    warnings++;
                }
            }
        }

        return (errors, warnings);
    }

    private static async Task<bool> CheckUrlAsync(HttpClient http, string url, string label, HashSet<string> checkedUrls, bool requireImage = false, string severity = "warning")
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not "http" and not "https" ||
            !checkedUrls.Add((requireImage ? "image:" : "url:") + url))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, uri);
                using var getResponse = await http.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead);
                return AnalyzeExternalResponse(getResponse, url, label, requireImage, severity);
            }

            return AnalyzeExternalResponse(response, url, label, requireImage, severity);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"external {severity} seo.external_fetch_failed - {label} {url} error={ex.GetType().Name}");
            return true;
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"external {severity} seo.external_fetch_timeout - {label} {url}");
            return true;
        }
    }

    private static bool AnalyzeExternalResponse(HttpResponseMessage response, string url, string label, bool requireImage, string severity)
    {
        if ((int)response.StatusCode >= 400)
        {
            Console.WriteLine($"external {severity} seo.external_http_status - {label} {url} status={(int)response.StatusCode}");
            return true;
        }

        if (requireImage && response.Content.Headers.ContentType?.MediaType is { } mediaType &&
            !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"external {severity} seo.external_image_mime - {label} {url} contentType={mediaType}");
            return true;
        }

        Console.WriteLine($"external ok {label} {url} status={(int)response.StatusCode}");
        return false;
    }

    private static IReadOnlyList<string> ExtractImageUrls(string html)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in SocialImageRegex().Matches(html))
        {
            values.Add(WebUtility.HtmlDecode(match.Groups[1].Value));
        }

        foreach (Match match in ImageSourceRegex().Matches(html))
        {
            values.Add(WebUtility.HtmlDecode(match.Groups[1].Value));
        }

        return values.Where(IsHttpUrl).ToArray();
    }

    private static IReadOnlyList<string> ExtractLinks(string html, string canonical)
    {
        if (!Uri.TryCreate(canonical, UriKind.Absolute, out var baseUri))
        {
            return Array.Empty<string>();
        }

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AnchorHrefRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(match.Groups[1].Value);
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(baseUri, href, out var absolute) ||
                absolute.Scheme is not "http" and not "https")
            {
                continue;
            }

            values.Add(absolute.ToString());
        }

        return values.ToArray();
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static JsonElement ReadRequiredObject(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path}.{property} must be an object.");
        }

        return value;
    }

    private static JsonElement ReadRequiredArray(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{path}.{property} must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static void ReadOptionalString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{path}.{property} must be a string or null.");
        }
    }

    private static int ReadRequiredInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }

        return result;
    }

    private static void ReadOptionalInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }
    }

    private static bool ReadRequiredBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static void ReadOptionalBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }
    }

    private static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} must be an object.");
        }
    }

    private static void EnsureAllowedProperties(JsonElement element, string path, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!set.Contains(property.Name))
            {
                throw new InvalidDataException($"{path}.{property.Name} is not allowed by the SEO report schema.");
            }
        }
    }

    private static int? ReadOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new InvalidDataException($"Expected a non-negative integer, got '{value}'.");
        }

        return result;
    }

    private static IReadOnlySet<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    private sealed record SeoReportSnapshot(
        IReadOnlyDictionary<string, SeoRouteSnapshot> Routes,
        IReadOnlyList<SeoIssueSnapshot> Issues)
    {
        public static SeoReportSnapshot From(JsonElement root)
        {
            var routes = new Dictionary<string, SeoRouteSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var route in root.GetProperty("routes").EnumerateArray())
            {
                var url = ReadRequiredString(route, "route", "url");
                routes[url] = new SeoRouteSnapshot(url, ReadRequiredBool(route, "route", "indexable"));
            }

            var issues = root.GetProperty("issues").EnumerateArray()
                .Select(x => new SeoIssueSnapshot(
                    ReadRequiredString(x, "issue", "severity"),
                    ReadRequiredString(x, "issue", "code"),
                    ReadString(x, "route"),
                    ReadRequiredString(x, "issue", "message")))
                .ToArray();

            return new SeoReportSnapshot(routes, issues);
        }
    }

    private sealed record SeoRouteSnapshot(string Url, bool Indexable);

    private sealed record SeoIssueSnapshot(string Severity, string Code, string? Route, string Message)
    {
        public string SortKey => $"{Severity}\u001f{Route}\u001f{Code}\u001f{Message}";
    }

    [GeneratedRegex(@"<meta\b(?=[^>]*(?:property|name)\s*=\s*[""'](?:og:image|twitter:image)[""'])(?=[^>]*content\s*=\s*[""']([^""']+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SocialImageRegex();

    [GeneratedRegex(@"<img\b(?=[^>]*src\s*=\s*[""']([^""']+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImageSourceRegex();

    [GeneratedRegex(@"<a\b(?=[^>]*href\s*=\s*[""']([^""'#]+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AnchorHrefRegex();
}
