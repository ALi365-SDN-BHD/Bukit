using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed partial class AnalyticsPluginBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void AnalyticsPlugin_IsRegisteredExactlyOnceAsBuiltInWithInternalHtmlLifecycle()
    {
        var sourcePlugins = new BuiltInPluginSource().GetPlugins()
            .Where(plugin => string.Equals(plugin.Name, "analytics", StringComparison.Ordinal))
            .ToArray();
        var analytics = Assert.Single(sourcePlugins);

        Assert.Equal("1.0.0", analytics.Version);
        Type pluginType = analytics.GetType();
        Assert.Equal("Bukit.Engine.Plugins.BuiltIn.AnalyticsPlugin", pluginType.FullName);
        Assert.False(pluginType.IsPublic);

        string[] interfaceNames = pluginType.GetInterfaces()
            .Select(type => type.FullName ?? type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Contains(typeof(IBukitPlugin).FullName!, interfaceNames);
        Assert.Contains(typeof(IOrderedPlugin).FullName!, interfaceNames);
        Assert.Contains("Bukit.Engine.Plugins.IHookFilterPlugin", interfaceNames);
        Assert.Contains("Bukit.Engine.Plugins.IHtmlTransformPlugin", interfaceNames);

        var context = CreateBuildContext();
        var registered = PluginRegistry.GetAllPlugins(context)
            .Where(item => string.Equals(item.Plugin.Name, "analytics", StringComparison.Ordinal))
            .ToArray();
        var registeredAnalytics = Assert.Single(registered);
        Assert.Equal("built-in", registeredAnalytics.Source);
        Assert.Same(analytics.GetType(), registeredAnalytics.Plugin.GetType());
    }

    [Fact]
    public void HtmlTransformAndAnalyticsRuntimeTypes_RemainInternalToEngine()
    {
        Assembly engineAssembly = typeof(BuiltInPluginSource).Assembly;
        string[] internalEngineTypes =
        [
            "Bukit.Engine.IHtmlTransform",
            "Bukit.Engine.HtmlTransformContext",
            "Bukit.Engine.Plugins.IHtmlTransformPlugin",
            "Bukit.Engine.Plugins.HtmlTransformPluginContext",
            "Bukit.Engine.Analytics.IAnalyticsProvider",
            "Bukit.Engine.Analytics.AnalyticsProviderRegistry",
            "Bukit.Engine.Analytics.AnalyticsRenderContext",
            "Bukit.Engine.Analytics.GoogleAnalyticsProvider",
            "Bukit.Engine.Analytics.GoogleTagManagerProvider",
            "Bukit.Engine.Analytics.PlausibleProvider",
            "Bukit.Engine.Analytics.UmamiProvider",
            "Bukit.Engine.Analytics.AnalyticsBuildState",
            "Bukit.Engine.Analytics.AnalyticsReportWriter"
        ];

        foreach (string typeName in internalEngineTypes)
        {
            Type type = AssertEngineType(engineAssembly, typeName);
            Assert.False(type.IsPublic || type.IsNestedPublic, $"{typeName} must remain Engine-internal.");
        }

        Assembly[] externalSurfaceAssemblies =
        [
            typeof(IBukitPlugin).Assembly,
            typeof(Bukit.PluginHost.PluginConfigLoader).Assembly,
            typeof(Bukit.Plugin.Abstractions.PluginJsonSerializerContext).Assembly
        ];
        var forbiddenTypeNames = internalEngineTypes
            .Select(typeName => typeName.Split('.').Last())
            .ToHashSet(StringComparer.Ordinal);

        foreach (Assembly assembly in externalSurfaceAssemblies)
        {
            Type[] offenders = assembly.GetTypes()
                .Where(type =>
                    forbiddenTypeNames.Contains(type.Name) ||
                    (type.Namespace?.Contains("Analytics", StringComparison.Ordinal) ?? false))
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{assembly.GetName().Name} exposes Engine-owned Analytics/HTML runtime types: " +
                string.Join(", ", offenders.Select(type => type.FullName)));
        }
    }

    [Fact]
    public void ExternalPluginProtocol_DoesNotExposePageHtmlTransformOrAnalyticsCapability()
    {
        Assembly[] protocolAssemblies =
        [
            typeof(Bukit.PluginHost.PluginConfigLoader).Assembly,
            typeof(Bukit.Plugin.Abstractions.PluginJsonSerializerContext).Assembly
        ];
        string[] forbiddenMemberTokens =
        [
            "Analytics",
            "HtmlTransform",
            "PageHtml",
            "OutputFile",
            "OutputPath"
        ];

        foreach (Assembly assembly in protocolAssemblies)
        {
            string[] offenders = assembly.GetExportedTypes()
                .SelectMany(type => PublicSurfaceNames(type))
                .Where(name => forbiddenMemberTokens.Any(
                    token => name.Contains(token, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                offenders.Length == 0,
                $"{assembly.GetName().Name} exposes forbidden page/HTML/output-file capability: " +
                string.Join(", ", offenders));
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepoRoot, "docs", "schemas", "bukit-plugin-manifest.v1.schema.json")));
        string[] schemaPropertyNames = EnumerateSchemaPropertyNames(document.RootElement)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        string[] forbiddenSchemaProperties =
        [
            "analytics",
            "capabilities",
            "hooks",
            "html",
            "outputFile",
            "outputPath",
            "page",
            "pages",
            "providers",
            "transforms"
        ];

        Assert.Empty(schemaPropertyNames.Intersect(forbiddenSchemaProperties, StringComparer.OrdinalIgnoreCase));
        Assert.Equal(
            ["derive-pages", "emit-outputs"],
            PluginCapability.AllCapabilities.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void PluginRegistry_UsesOnlyTheStaticBuiltInSource()
    {
        string source = ReadMainlineSource("src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs");
        string[] pluginSources = PluginSourceConstructionRegex().Matches(source)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["BuiltInPluginSource"], pluginSources);
        AssertDoesNotUseDynamicDiscovery(source, "PluginRegistry.cs");
    }

    [Fact]
    public void AnalyticsProviderRegistry_IsStaticAndNativeAotFriendly()
    {
        string registrySource = ReadMainlineSource(
            "src/Bukit-Core/Bukit.Engine/Analytics/AnalyticsProviderRegistry.cs");
        string[] providerTypes = ProviderConstructionRegex().Matches(registrySource)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "GoogleAnalyticsProvider",
                "GoogleTagManagerProvider",
                "PlausibleProvider",
                "UmamiProvider"
            ],
            providerTypes);
        AssertDoesNotUseDynamicDiscovery(registrySource, "AnalyticsProviderRegistry.cs");

        string[] aotBoundaryFiles =
        [
            "src/Bukit-Core/Bukit.Engine/Plugins/PluginRegistry.cs",
            "src/Bukit-Core/Bukit.Engine/Plugins/IHtmlTransformPlugin.cs",
            "src/Bukit-Core/Bukit.Engine/Plugins/BuiltIn/AnalyticsPlugin.cs"
        ];
        foreach (string relativePath in aotBoundaryFiles)
        {
            AssertDoesNotUseDynamicDiscovery(ReadMainlineSource(relativePath), relativePath);
        }

        string analyticsDirectory = Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.Engine",
            "Analytics");
        foreach (string path in Directory.EnumerateFiles(analyticsDirectory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            AssertDoesNotUseDynamicDiscovery(File.ReadAllText(path), Path.GetRelativePath(RepoRoot, path));
        }
    }

    private static BuildContext CreateBuildContext()
        => new()
        {
            Config = new AppConfig
            {
                Site = new SiteConfig { Name = "architecture-test", Title = "Architecture Test" },
                Content = new ContentConfig()
            },
            RootDir = "/architecture-test",
            OutputDir = "/architecture-test/dist",
            BaseUrl = "/",
            LayoutsDir = "/architecture-test/theme/layouts",
            RoutedDocuments = [],
            Logger = new ConsoleLogger(LogLevel.Error)
        };

    private static Type AssertEngineType(Assembly engineAssembly, string typeName)
    {
        Type? type = engineAssembly.GetType(typeName, throwOnError: false, ignoreCase: false);
        Assert.NotNull(type);
        return type!;
    }

    private static IEnumerable<string> PublicSurfaceNames(Type type)
    {
        yield return type.FullName ?? type.Name;
        foreach (MemberInfo member in type.GetMembers(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public))
        {
            yield return $"{type.FullName}.{member.Name}";
        }
    }

    private static IEnumerable<string> EnumerateSchemaPropertyNames(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.NameEquals("properties") && property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty schemaProperty in property.Value.EnumerateObject())
                    {
                        yield return schemaProperty.Name;
                    }
                }

                foreach (string nested in EnumerateSchemaPropertyNames(property.Value))
                {
                    yield return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                foreach (string nested in EnumerateSchemaPropertyNames(item))
                {
                    yield return nested;
                }
            }
        }
    }

    private static void AssertDoesNotUseDynamicDiscovery(string source, string label)
    {
        string[] forbiddenPatterns =
        [
            @"\bAssembly\s*\.\s*Load(?:From|File)?\s*\(",
            @"\bAssemblyLoadContext\b",
            @"\bActivator\s*\.\s*CreateInstance\s*\(",
            @"\bGetExportedTypes\s*\(",
            @"\bGetTypes\s*\(",
            @"\bMakeGenericType\s*\(",
            @"\bType\s*\.\s*GetType\s*\("
        ];

        string[] offenders = forbiddenPatterns
            .Where(pattern => Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant))
            .ToArray();
        Assert.True(
            offenders.Length == 0,
            $"{label} must remain statically discoverable for Native AOT; found: {string.Join(", ", offenders)}");
    }

    private static string ReadMainlineSource(string relativePath)
        => File.ReadAllText(Path.Combine(RepoRoot, relativePath));

    [GeneratedRegex(@"\bnew\s+(?:BuiltIn\.)?([A-Za-z_][A-Za-z0-9_]*PluginSource)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex PluginSourceConstructionRegex();

    [GeneratedRegex(@"\bnew\s+([A-Za-z_][A-Za-z0-9_]*Provider)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex ProviderConstructionRegex();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
