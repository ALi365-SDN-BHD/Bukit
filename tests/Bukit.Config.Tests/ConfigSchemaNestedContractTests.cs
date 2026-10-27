using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigSchemaNestedContractTests
{
    private const string ValidatorRelativePath = "src/Bukit-Core/Bukit.Config/ConfigStrictFieldValidator.cs";

    private static readonly IReadOnlySet<string> TopLevelPaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "root",
        "site",
        "content",
        "build",
        "theme",
        "taxonomy"
    };

    private static readonly IReadOnlyDictionary<string, string[]> PathParameterSeeds = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["ValidateNotion.path"] = ["content/sources[]/notion"],
        ["ValidateFieldDefinitions.path"] = ["content/modelSchema/customFields", "content/modelSchema/fieldScopes/*"],
        ["ValidateReference.path"] =
        [
            "content/modelSchema/customFields[]",
            "content/modelSchema/fieldScopes/*[]",
            "content/modelSchema/entityMappings[]",
            "content/modelSchema/relationMappings[]"
        ],
        ["ValidateMenuItems.path"] = ["site/menus/*"]
    };

    private static readonly Regex MethodDeclarationRegex = new(
        @"^[ \t]*(?:private|internal|public)\s+static\s+(?:void|[A-Za-z_][A-Za-z0-9_<>,?\[\] ]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex StaticSetRegex = new(
        @"private static readonly HashSet<string> (?<name>[A-Za-z_][A-Za-z0-9_]*) = Set\((?<fields>.*?)\);",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex LocalVariableRegex = new(
        @"\bvar\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<expression>[^;]+);",
        RegexOptions.Compiled);

    private static readonly Regex StringLiteralRegex = new(
        "\"((?:[^\"\\\\]|\\\\.)*)\"",
        RegexOptions.Compiled);

    private static readonly Regex ProviderVariantRegex = new(
        "\"(?<type>[a-z0-9-]+)\"\\s*=>\\s*Set\\((?<fields>[^)]*)\\)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly SchemaCase[] NestedCases =
    [
        new("site/seo", "enabled", "renderMode", "diagnostics", "homeTitleTemplate", "pageTitleTemplate", "titleSeparator", "defaultImage", "twitterSite", "organization", "robotsTxt", "schema", "geo"),
        new("site/seo/organization", "type", "name", "url", "logo", "sameAs"),
        new("site/seo/robotsTxt", "enabled"),
        new("site/seo/schema", "webPage", "collectionPage", "searchAction"),
        new("site/seo/geo", "enabled", "llmsTxt", "llmsFullTxt", "llmsTxtMaxArticles", "aiBotMode", "aiBotAllowList", "aiBotBlockList", "llmsTxtOptionalLinks"),
        new("site/seo/geo/llmsTxtOptionalLinks[]", "title", "url", "description"),
        new("site/analytics", "enabled", "productionOnly", "consent", "csp", "providers"),
        new("site/analytics/csp", "mode"),
        new("site/analytics/consent", "google"),
        new("site/analytics/consent/google", "mode", "defaults", "waitForUpdateMs"),
        new("site/analytics/consent/google/defaults", "adStorage", "analyticsStorage", "adUserData", "adPersonalization"),
        new("site/analytics/providers[]", "type", "measurementId", "containerId", "domain", "snippetMode", "websiteId", "scriptUrl"),
        new("site/feed", "mode", "formats", "limit", "path"),
        new("site/search", "mode", "route", "ui", "uiTheme", "placeholderText", "maxContentLength"),
        new("site/related", "enabled", "threshold", "limit", "indices"),
        new("site/related/indices[]", "name", "weight"),
        new("site/sitemapDetail", "defaultPriority", "defaultChangefreq", "imageEnabled", "videoEnabled"),
        new("site/pagination", "enabled", "pageSize"),
        new("site/collections/*", "permalink", "template", "listRoute", "listTitle", "listDescription", "listTemplate", "schemaFailMode", "noindexWhenEmpty", "indexPolicy", "pagination", "output", "filteredLists"),
        new("site/collections/*/indexPolicy", "minimumItems", "belowMinimum"),
        new("site/collections/*/pagination", "enabled", "pageSize", "urlPattern", "firstPageUsesListRoute"),
        new("site/collections/*/output", "rss", "sitemap", "archive", "feedPath", "feedTitle", "feedDescription", "archiveDetail"),
        new("site/collections/*/output/archiveDetail", "depth", "template", "routePrefix"),
        new("site/collections/*/filteredLists[]", "field", "operator", "value", "values", "listRoute", "title", "description", "listTemplate", "pageSize", "urlPattern", "emptyBehavior"),
        new("site/menus/*[]", "identifier", "name", "url", "weight", "children"),
        new("site/plugins/*", "enabled", "options"),
        new("content/sources[]", "type", "name", "mode", "collection", "addToCollections", "markdown", "notion", "dataIndex"),
        new("content/sources[]/markdown", "dir", "defaultType", "maxItems", "includePaths", "includeGlobs"),
        new("content/sources[]/notion", "databaseId", "pageSize", "maxItems", "renderContent", "renderConcurrency", "maxRps", "maxRetries", "fieldPolicy", "filterProperty", "filterType", "filterValue", "sortProperty", "sortDirection", "includeSlugs", "includeSlugProperty", "cacheMode", "cacheDir", "propertyMap"),
        new("content/sources[]/notion/fieldPolicy", "mode", "allowed"),
        new("content/sources[]/notion/propertyMap", "Title", "Slug", "Type", "PublishAt", "Language", "I18nKey", "Summary", "Collection", "SeoTitle", "SeoDescription", "SeoImage", "Canonical", "OriginalUrl", "References", "EntitiesJson", "Cover", "CoverAlt", "CoverCaption"),
        new("content/sources[]/dataIndex", "scopeField", "keyField", "valueField", "valueTypeField", "requiredKeys"),
        new("content/media", "downloadToLocal", "downloadDir", "urlBase", "defaultImageUrl", "fieldKeys", "maxConcurrency", "maxRetries", "timeoutMs", "maxFileSizeBytes", "blockPrivateNetworks", "retryBaseDelayMs"),
        new("content/modelSchema", "contentTypes", "statuses", "reviewStatuses", "syncStatuses", "canonicalMappings", "customFields", "fieldScopes", "entityMappings", "relationMappings", "media", "rejectUnknownRawKeys", "requireSummary", "requireAuthor", "requireOrganization", "requireUpdatedAt", "requireProvenance", "requireReviewedAt", "requireMediaAlt", "requireMediaDescription", "requireMediaLicense", "requireEntityIds", "requireRelationTargets"),
        new("content/modelSchema/canonicalMappings[]", "canonicalField", "rawKey", "semanticType", "required"),
        new("content/modelSchema/customFields[]", "name", "fieldType", "required", "semanticType", "label", "format", "enum", "min", "max", "default", "sourcePolicy", "reference"),
        new("content/modelSchema/customFields[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/fieldScopes/*[]", "name", "fieldType", "required", "semanticType", "label", "format", "enum", "min", "max", "default", "sourcePolicy", "reference"),
        new("content/modelSchema/fieldScopes/*[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/entityMappings[]", "rawKey", "entityType", "idField", "nameField", "descriptionField", "urlField", "sameAsField", "required", "reference"),
        new("content/modelSchema/entityMappings[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/relationMappings[]", "rawKey", "relationType", "targetType", "targetField", "targetIdField", "required", "reference"),
        new("content/modelSchema/relationMappings[]/reference", "targetType", "idField", "labelField", "urlField", "required"),
        new("content/modelSchema/media", "requireAlt", "requireDescription", "requireLicense", "allowedKinds"),
        new("content/routeMetadata", "source", "routeField", "titleField", "summaryField", "seoTitleField", "seoDescriptionField", "requiredRoutes"),
        new("build/report", "enabled", "securityFailMode"),
        new("theme/scss", "enabled", "entryPoint", "outputDir"),
        new("theme/images", "enabled", "formats", "sizes", "quality"),
        new("theme/components/*", "template", "props"),
        new("taxonomy/kinds[]", "key", "kind", "title", "description", "singularTitlePrefix", "template", "indexTemplate", "termTemplate", "indexEnabled", "hierarchical", "routePrefix"),
        new("logging", "level"),
        new("deploy", "provider", "branch", "message", "cname", "keepHistory")
    ];

    private static readonly string[] DynamicMapPaths =
    [
        "site/permalinks",
        "theme/params",
        "theme/shortcodes",
        "theme/components/*/props",
        "taxonomy/pinFieldBySource",
        "taxonomy/pinOrderFieldBySource"
    ];

    [Fact]
    public void Generate_NestedSections_MatchNestedCaseSnapshot()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var failures = CompareCasesToSchema(NestedCases, schema.RootElement);

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void StrictValidatorSource_NestedWhitelistsMatchNestedCases()
    {
        var source = ReadValidatorSource();
        var parsed = ParseWhitelistCalls(source);
        Assert.Equal(
            ["ValidateAnalyticsProviderFields"],
            parsed.DynamicCalls.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            TopLevelPaths.Order(StringComparer.Ordinal),
            parsed.Calls
                .Where(call => TopLevelPaths.Contains(call.Path))
                .Select(call => call.Path)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
        Assert.Contains("RequireOnly(map, allowedKeys, itemPath)", source, StringComparison.Ordinal);

        var providerCall = Assert.Single(
            parsed.Calls,
            call => call.Path == "site/analytics/providers[]" && call.Method == "ValidateSequenceMappings");
        Assert.Equal("ValidateAnalyticsProviderFields", providerCall.AfterCallback);

        var failures = CompareSourceToCases(parsed.Calls, NestedCases);
        failures.AddRange(CompareParameterSeedsToCallSites(source, parsed));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void StrictValidatorSource_ComparisonDetectsOneSidedWhitelistChanges()
    {
        var source = ReadValidatorSource();

        var addedField = source.Replace(
            "\"defaultImage\", \"twitterSite\"",
            "\"defaultImage\", \"auditProbe\", \"twitterSite\"",
            StringComparison.Ordinal);
        Assert.NotEqual(source, addedField);
        var fieldFailures = CompareSourceToCases(ParseWhitelistCalls(addedField).Calls, NestedCases);
        Assert.Contains(
            fieldFailures,
            failure => failure.Contains("site/seo", StringComparison.Ordinal) &&
                       failure.Contains("auditProbe", StringComparison.Ordinal));

        var addedPath = source.Replace(
            "if (Map(seo, \"organization\")",
            "RequireOnly(seo, Set(\"auditProbe\"), \"site.auditProbe\");\n        if (Map(seo, \"organization\")",
            StringComparison.Ordinal);
        Assert.NotEqual(source, addedPath);
        var pathFailures = CompareSourceToCases(ParseWhitelistCalls(addedPath).Calls, NestedCases);
        Assert.Contains(
            pathFailures,
            failure => failure.Contains("site/auditProbe", StringComparison.Ordinal));

        var renamedCallSite = source.Replace(
            "ValidateNotion(notion, $\"{path}.notion\")",
            "ValidateNotion(notion, $\"{path}.notionRenamed\")",
            StringComparison.Ordinal);
        Assert.NotEqual(source, renamedCallSite);
        var renamedParsed = ParseWhitelistCalls(renamedCallSite);
        var seedFailures = CompareParameterSeedsToCallSites(renamedCallSite, renamedParsed);
        Assert.Contains(
            seedFailures,
            failure => failure.Contains("ValidateNotion", StringComparison.Ordinal) &&
                       failure.Contains("notionRenamed", StringComparison.Ordinal));

        var changedCase = NestedCases
            .Select(item => item.Path == "site/seo"
                ? item with { Fields = [.. item.Fields, "auditProbe"] }
                : item)
            .ToArray();
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var schemaFailures = CompareCasesToSchema(changedCase, schema.RootElement);
        Assert.Contains(
            schemaFailures,
            failure => failure.Contains("site/seo", StringComparison.Ordinal) &&
                       failure.Contains("auditProbe", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_AnalyticsProviderVariants_MatchStrictTypeSpecificFields()
    {
        var expected = ParseAnalyticsProviderVariants(ReadValidatorSource());
        Assert.Equal(4, expected.Count);

        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var providers = Resolve(schema.RootElement, "site/analytics/providers[]");
        var variants = providers.GetProperty("oneOf").EnumerateArray().ToDictionary(
            item => item.GetProperty("properties").GetProperty("type").GetProperty("const").GetString()!,
            item => item.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal),
            StringComparer.Ordinal);

        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            variants.Keys.Order(StringComparer.Ordinal));
        foreach (var (type, fields) in expected)
        {
            Assert.Equal(fields.Order(StringComparer.Ordinal), variants[type].Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public void Generate_DynamicMapLocations_RemainOpenObjects()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());

        foreach (var path in DynamicMapPaths)
        {
            var node = Resolve(schema.RootElement, path);
            Assert.True(
                node.TryGetProperty("additionalProperties", out _),
                $"{path} must remain an explicitly open dynamic map.");
        }
    }

    [Fact]
    public void Generate_PluginToggle_MappingVariantMatchesStrictFields()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var toggle = Resolve(schema.RootElement, "site/plugins/*");
        var variants = toggle.GetProperty("oneOf").EnumerateArray().ToArray();
        var mapping = Assert.Single(variants, item => item.TryGetProperty("properties", out _));

        Assert.Equal(
            ["enabled", "options"],
            mapping.GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.Ordinal));
        Assert.Contains(variants, item => item.GetProperty("type").GetString() == "boolean");
    }

    [Fact]
    public void Generate_PluginOptions_RemainOpenDynamicMap()
    {
        using var schema = JsonDocument.Parse(ConfigJsonSchemaGenerator.Generate());
        var toggle = Resolve(schema.RootElement, "site/plugins/*");
        var mapping = toggle.GetProperty("oneOf").EnumerateArray()
            .Single(item => item.TryGetProperty("properties", out _));
        var options = mapping.GetProperty("properties").GetProperty("options");

        Assert.True(options.TryGetProperty("additionalProperties", out _));
    }

    private static List<string> CompareSourceToCases(
        IReadOnlyList<WhitelistCall> calls,
        IReadOnlyList<SchemaCase> cases)
    {
        var failures = new List<string>();
        var caseMap = cases.ToDictionary(item => item.Path, StringComparer.Ordinal);
        var covered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var call in calls)
        {
            if (TopLevelPaths.Contains(call.Path))
            {
                continue;
            }

            if (!caseMap.TryGetValue(call.Path, out var nestedCase))
            {
                failures.Add(
                    $"{call.EnclosingMethod} ({call.Method}) whitelist for '{call.Path}' has no nested schema case.");
                continue;
            }

            covered.Add(call.Path);
            var expected = nestedCase.Fields.ToHashSet(StringComparer.Ordinal);
            var actual = call.Fields.ToHashSet(StringComparer.Ordinal);
            if (!expected.SetEquals(actual))
            {
                failures.Add(
                    $"{call.EnclosingMethod} ({call.Method}) path '{call.Path}': validator [{JoinFields(actual)}] != nested case [{JoinFields(expected)}].");
            }
        }

        foreach (var path in caseMap.Keys.Except(covered, StringComparer.Ordinal))
        {
            failures.Add($"Nested case '{path}' has no matching strict-validator whitelist.");
        }

        return failures;
    }

    private static List<string> CompareParameterSeedsToCallSites(
        string source,
        ValidatorParseResult parsed)
    {
        var failures = new List<string>();
        var declarations = MethodDeclarations(source);

        foreach (var method in new[] { "ValidateNotion", "ValidateFieldDefinitions", "ValidateMenuItems" })
        {
            var actual = FindCallSitePaths(source, declarations, method);
            var expected = PathParameterSeeds[$"{method}.path"];
            if (!actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            {
                failures.Add(
                    $"{method} call-site paths [{JoinFields(actual)}] != path seeds [{JoinFields(expected)}]");
            }
        }

        var referenceActual = parsed.Calls
            .Where(call => call.Method == "ValidateSequenceMappings" && call.AfterCallback == "ValidateReference")
            .Select(call => call.Path)
            .ToHashSet(StringComparer.Ordinal);
        var referenceExpected = PathParameterSeeds["ValidateReference.path"].ToHashSet(StringComparer.Ordinal);
        if (!referenceActual.SetEquals(referenceExpected))
        {
            failures.Add(
                $"ValidateReference callback paths [{JoinFields(referenceActual)}] != path seeds [{JoinFields(referenceExpected)}]");
        }

        return failures;
    }

    private static List<string> FindCallSitePaths(
        string source,
        IReadOnlyList<MethodDeclaration> declarations,
        string method)
    {
        var result = new List<string>();
        var searchIndex = 0;
        var callIndex = source.IndexOf(method + "(", searchIndex, StringComparison.Ordinal);
        while (callIndex >= 0)
        {
            searchIndex = callIndex + method.Length;
            var nextIndex = source.IndexOf(method + "(", searchIndex, StringComparison.Ordinal);
            var enclosingMethod = EnclosingMethod(declarations, callIndex);
            if (enclosingMethod != method)
            {
                var openIndex = callIndex + method.Length;
                var closeIndex = FindClosingParenthesis(source, openIndex);
                var arguments = SplitArguments(source[(openIndex + 1)..closeIndex]);
                if (arguments.Count >= 2)
                {
                    result.AddRange(ResolvePaths(enclosingMethod, arguments[1], source, declarations));
                }
            }

            callIndex = nextIndex;
        }

        return result;
    }

    private static List<string> CompareCasesToSchema(
        IReadOnlyList<SchemaCase> cases,
        JsonElement schemaRoot)
    {
        var failures = new List<string>();
        foreach (var item in cases)
        {
            try
            {
                var actual = ResolveProperties(schemaRoot, item.Path);
                var expected = item.Fields.ToHashSet(StringComparer.Ordinal);
                if (!expected.SetEquals(actual))
                {
                    failures.Add(
                        $"{item.Path}: nested case [{JoinFields(expected)}] != schema [{JoinFields(actual)}]");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{item.Path}: {exception.Message}");
            }
        }

        return failures;
    }

    private static ValidatorParseResult ParseWhitelistCalls(string source)
    {
        var declarations = MethodDeclarations(source);
        var staticSets = ParseStaticSets(source);
        var calls = new List<WhitelistCall>();
        var dynamicCalls = new List<string>();

        foreach (var (methodName, isSequence) in new[] { ("RequireOnly", false), ("ValidateSequenceMappings", true) })
        {
            var searchIndex = 0;
            var callIndex = source.IndexOf(methodName + "(", searchIndex, StringComparison.Ordinal);
            while (callIndex >= 0)
            {
                searchIndex = callIndex + methodName.Length;
                var nextIndex = source.IndexOf(methodName + "(", searchIndex, StringComparison.Ordinal);
                var enclosingMethod = EnclosingMethod(declarations, callIndex);
                if (enclosingMethod != methodName && enclosingMethod != "ValidateSequenceMappings")
                {
                    var openIndex = callIndex + methodName.Length;
                    var closeIndex = FindClosingParenthesis(source, openIndex);
                    var arguments = SplitArguments(source[(openIndex + 1)..closeIndex]);
                    if (arguments.Count < 3)
                    {
                        throw new InvalidOperationException(
                            $"{enclosingMethod}: expected three arguments for {methodName}.");
                    }

                    var fields = ResolveFields(arguments[1], staticSets);
                    if (fields is null)
                    {
                        dynamicCalls.Add(enclosingMethod);
                    }
                    else
                    {
                        var afterCallback = arguments.Count > 3 ? arguments[3].Trim() : null;
                        foreach (var path in ResolvePaths(enclosingMethod, arguments[2], source, declarations))
                        {
                            var normalized = isSequence ? AppendSequencePath(path) : path;
                            calls.Add(new WhitelistCall(methodName, normalized, fields, enclosingMethod, afterCallback));
                        }
                    }
                }

                callIndex = nextIndex;
            }
        }

        return new ValidatorParseResult(calls, dynamicCalls);
    }

    private static Dictionary<string, string[]> ParseStaticSets(string source)
        => StaticSetRegex.Matches(source).ToDictionary(
            match => match.Groups["name"].Value,
            match => StringLiterals(match.Groups["fields"].Value),
            StringComparer.Ordinal);

    private static Dictionary<string, string[]> ParseAnalyticsProviderVariants(string source)
    {
        var declarations = MethodDeclarations(source);
        var body = MethodBody(source, "ValidateAnalyticsProviderFields", declarations);
        return ProviderVariantRegex.Matches(body).ToDictionary(
            match => match.Groups["type"].Value,
            match => StringLiterals(match.Groups["fields"].Value),
            StringComparer.Ordinal);
    }

    private static string[]? ResolveFields(
        string rawArgument,
        IReadOnlyDictionary<string, string[]> staticSets)
    {
        var argument = rawArgument.Trim();
        if (argument == "allowedFields")
        {
            return null;
        }

        if (argument.StartsWith("Set(", StringComparison.Ordinal))
        {
            return StringLiterals(argument[(argument.IndexOf('(') + 1)..argument.LastIndexOf(')')]);
        }

        if (staticSets.TryGetValue(argument, out var fields))
        {
            return fields;
        }

        throw new InvalidOperationException($"Cannot resolve strict-validator whitelist argument '{argument}'.");
    }

    private static IReadOnlyList<string> ResolvePaths(
        string method,
        string rawPath,
        string source,
        IReadOnlyList<MethodDeclaration> declarations)
    {
        var path = rawPath.Trim();
        if (path.StartsWith("$\"", StringComparison.Ordinal))
        {
            return ResolveInterpolatedPath(method, path, source, declarations);
        }

        if (path.StartsWith('"'))
        {
            return [NormalizePath(path[1..^1])];
        }

        if (IsIdentifier(path))
        {
            return ResolveVariable(method, path, source, declarations);
        }

        throw new InvalidOperationException($"Cannot resolve strict-validator path expression '{path}'.");
    }

    private static IReadOnlyList<string> ResolveInterpolatedPath(
        string method,
        string rawPath,
        string source,
        IReadOnlyList<MethodDeclaration> declarations)
    {
        var results = new List<string> { rawPath[2..^1] };
        foreach (var variable in new[] { "itemPath", "path" })
        {
            var token = "{" + variable + "}";
            if (results.All(result => !result.Contains(token, StringComparison.Ordinal)))
            {
                continue;
            }

            var values = ResolveVariable(method, variable, source, declarations);
            results = results
                .SelectMany(result => result.Contains(token, StringComparison.Ordinal)
                    ? values.Select(value => result.Replace(token, value, StringComparison.Ordinal))
                    : [result])
                .ToList();
        }

        return results
            .Select(result => result
                .Replace("{name}", "*", StringComparison.Ordinal)
                .Replace("{scope}", "*", StringComparison.Ordinal)
                .Replace("{index}", "", StringComparison.Ordinal))
            .Select(NormalizePath)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveVariable(
        string method,
        string variable,
        string source,
        IReadOnlyList<MethodDeclaration> declarations)
    {
        var body = MethodBody(source, method, declarations);
        var local = LocalVariableRegex.Matches(body)
            .Where(match => match.Groups["name"].Value == variable)
            .Select(match => match.Groups["expression"].Value.Trim())
            .FirstOrDefault();
        if (local is not null)
        {
            return ResolvePaths(method, local, source, declarations);
        }

        if (PathParameterSeeds.TryGetValue($"{method}.{variable}", out var seeds))
        {
            return seeds;
        }

        throw new InvalidOperationException($"Cannot resolve path variable '{variable}' in {method}.");
    }

    private static string MethodBody(
        string source,
        string method,
        IReadOnlyList<MethodDeclaration> declarations)
    {
        var index = -1;
        for (var candidate = 0; candidate < declarations.Count; candidate++)
        {
            if (declarations[candidate].Name == method)
            {
                index = candidate;
                break;
            }
        }

        if (index < 0)
        {
            throw new InvalidOperationException($"Cannot find method '{method}' in validator source.");
        }

        var start = declarations[index].Index;
        var end = index + 1 < declarations.Count ? declarations[index + 1].Index : source.Length;
        return source[start..end];
    }

    private static List<MethodDeclaration> MethodDeclarations(string source)
        => MethodDeclarationRegex.Matches(source)
            .Select(match => new MethodDeclaration(match.Groups["name"].Value, match.Index))
            .ToList();

    private static string EnclosingMethod(IReadOnlyList<MethodDeclaration> declarations, int index)
    {
        for (var candidate = declarations.Count - 1; candidate >= 0; candidate--)
        {
            if (declarations[candidate].Index < index)
            {
                return declarations[candidate].Name;
            }
        }

        throw new InvalidOperationException("Cannot determine the enclosing method in validator source.");
    }

    private static int FindClosingParenthesis(string source, int openIndex)
    {
        var depth = 0;
        for (var index = openIndex; index < source.Length; index++)
        {
            var current = source[index];
            if (current == '"')
            {
                index = SkipQuoted(source, index);
                continue;
            }

            if (current == '\'')
            {
                index = SkipQuoted(source, index);
                continue;
            }

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')' && --depth == 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Unclosed parenthesis in validator source.");
    }

    private static List<string> SplitArguments(string arguments)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        for (var index = 0; index < arguments.Length; index++)
        {
            var current = arguments[index];
            if (current == '"')
            {
                index = SkipQuoted(arguments, index);
                continue;
            }

            if (current == '\'')
            {
                index = SkipQuoted(arguments, index);
                continue;
            }

            switch (current)
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(arguments[start..index].Trim());
                    start = index + 1;
                    break;
            }
        }

        result.Add(arguments[start..].Trim());
        return result;
    }

    private static int SkipQuoted(string text, int quoteIndex)
    {
        for (var index = quoteIndex + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
                continue;
            }

            if (text[index] == text[quoteIndex])
            {
                return index;
            }
        }

        throw new InvalidOperationException("Unclosed quoted literal in validator source.");
    }

    private static string[] StringLiterals(string text)
        => StringLiteralRegex.Matches(text)
            .Select(match => Regex.Unescape(match.Groups[1].Value))
            .ToArray();

    private static bool IsIdentifier(string value)
        => value.Length > 0 &&
           (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
           value.Skip(1).All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static string NormalizePath(string value)
        => value.Replace('.', '/');

    private static string AppendSequencePath(string path)
        => path.EndsWith("[]", StringComparison.Ordinal) ? path : path + "[]";

    private static string JoinFields(IEnumerable<string> fields)
        => string.Join(", ", fields.Order(StringComparer.Ordinal));

    private static IReadOnlySet<string> ResolveProperties(JsonElement root, string path)
        => PropertyNames(Resolve(root, path)).ToHashSet(StringComparer.Ordinal);

    private static IEnumerable<string> PropertyNames(JsonElement node)
    {
        if (node.TryGetProperty("properties", out var properties))
        {
            return properties.EnumerateObject().Select(property => property.Name);
        }

        if (node.TryGetProperty("oneOf", out var variants))
        {
            return variants.EnumerateArray().SelectMany(PropertyNames);
        }

        return [];
    }

    private static JsonElement Resolve(JsonElement root, string path)
    {
        var current = root;
        var currentPath = "$";
        foreach (var rawSegment in path.Split('/'))
        {
            var segment = rawSegment;
            var isArray = segment.EndsWith("[]", StringComparison.Ordinal);
            if (isArray)
            {
                segment = segment[..^2];
            }

            currentPath += "/" + rawSegment;
            if (segment == "*")
            {
                if (!current.TryGetProperty("additionalProperties", out current))
                {
                    throw new InvalidOperationException($"{currentPath} has no additionalProperties.");
                }
            }
            else
            {
                if (!current.TryGetProperty("properties", out var properties) ||
                    !properties.TryGetProperty(segment, out current))
                {
                    throw new InvalidOperationException($"{currentPath} has no schema property '{segment}'.");
                }
            }

            if (isArray)
            {
                if (!current.TryGetProperty("items", out current))
                {
                    throw new InvalidOperationException($"{currentPath} has no items schema.");
                }
            }
        }

        return current;
    }

    private static string ReadValidatorSource()
        => File.ReadAllText(Path.Combine(RepoRoot(), ValidatorRelativePath));

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed record SchemaCase(string Path, params string[] Fields);

    private sealed record WhitelistCall(
        string Method,
        string Path,
        string[] Fields,
        string EnclosingMethod,
        string? AfterCallback);

    private sealed record ValidatorParseResult(
        IReadOnlyList<WhitelistCall> Calls,
        IReadOnlyList<string> DynamicCalls);

    private sealed record MethodDeclaration(string Name, int Index);
}
