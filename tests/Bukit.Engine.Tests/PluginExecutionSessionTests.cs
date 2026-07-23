using System.Collections;
using System.Reflection;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Analytics;
using Bukit.Engine.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class PluginExecutionSessionTests
{
    [Fact]
    public void SameSession_ReusesRegistrations_AndDifferentConfigSessionsAreIsolated()
    {
        var context = CreateContext();
        var configA = CreateConfig("a");
        var configB = CreateConfig("b");
        var sessionA = PluginExecutionSession.Create(configA, BuildExecutionMode.Production);
        var sessionB = PluginExecutionSession.Create(configB, BuildExecutionMode.Production);

        var first = PluginRegistry.GetAllPlugins(context, sessionA).ToArray();
        var second = PluginRegistry.GetAllPlugins(context, sessionA).ToArray();
        var other = PluginRegistry.GetAllPlugins(context, sessionB).ToArray();

        Assert.Equal(10, first.Length);
        Assert.All(first.Zip(second), pair => Assert.Same(pair.First.Plugin, pair.Second.Plugin));
        Assert.All(first.Zip(other), pair => Assert.NotSame(pair.First.Plugin, pair.Second.Plugin));
        Assert.Equal(
            [
                "analytics",
                "data-files",
                "pages-index",
                "taxonomy",
                "pagination",
                "archive",
                "related-content",
                "alias",
                "menu",
                "image-processing"
            ],
            first.Select(item => item.Plugin.Name));
        Assert.All(first, item => Assert.Equal("built-in", item.Source));
        Assert.DoesNotContain(first, item => item.Plugin.Name is
            "feed" or "llms-txt" or "search-index" or "sitemap");
    }

    [Fact]
    public async Task ProductionSession_DoesNotSmuggleConfigThroughBuildContextData()
    {
        var outputDir = Path.Combine(
            Path.GetTempPath(),
            "bukit_plugin_session_" + Guid.NewGuid().ToString("N"));
        try
        {
            var context = CreateContext(outputDir);
            var config = CreateConfig("production") with
            {
                Site = CreateConfig("production").Site with
                {
                    Menus = new Dictionary<string, IReadOnlyList<MenuConfig>>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        ["Main"] =
                        [
                            new MenuConfig
                            {
                                Identifier = "parent",
                                Name = "Parent",
                                Url = "/parent/",
                                Weight = 9,
                                Children =
                                [
                                    new MenuConfig
                                    {
                                        Identifier = "child",
                                        Name = "Child",
                                        Url = "/child/",
                                        Weight = 3
                                    }
                                ]
                            }
                        ]
                    }
                }
            };
            var session = PluginExecutionSession.Create(config, BuildExecutionMode.Production);

            _ = PluginRegistry.GetAllPlugins(context, session).ToArray();
            _ = await PluginRunner.RunDerivePagesAsync(context, session);
            var transforms = PluginRunner.CollectHtmlTransforms(
                context,
                session,
                BuildExecutionMode.Production);
            var analytics = Assert.Single(transforms, transform => transform.Name == "analytics");
            _ = analytics.Transform(
                new HtmlTransformContext(
                    "/post/",
                    "post/index.html",
                    HtmlDocumentKind.Content,
                    BuildExecutionMode.Production,
                    context.Logger),
                "<html><head></head><body></body></html>");
            await PluginRunner.RunAfterBuildAsync(context, session);

            var menus = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(
                context.Data["menus"]);
            var main = Assert.IsAssignableFrom<IReadOnlyList<object>>(menus["Main"]);
            var parent = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(Assert.Single(main));
            Assert.Equal("parent", parent["identifier"]);
            Assert.Equal("Parent", parent["name"]);
            Assert.Equal("/parent/", parent["url"]);
            Assert.Equal(9, parent["weight"]);
            var children = Assert.IsAssignableFrom<IReadOnlyList<object>>(parent["children"]);
            var child = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object>>(Assert.Single(children));
            Assert.Equal("child", child["identifier"]);
            Assert.Equal(3, child["weight"]);

            Assert.DoesNotContain(session.AnalyticsBuildState, context.Data.Values);
            Assert.False(context.Data.ContainsKey("__plugin_registry_cache"));
            Assert.False(context.Data.ContainsKey("__analytics_build_state"));
            Assert.False(context.Data.ContainsKey("__taxonomy_index_cache"));
            AssertDataGraphDoesNotReachConfig(context.Data);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void AnalyticsPlugin_UsesTheSessionStateWithoutAttachingItToData()
    {
        var context = CreateContext();
        var session = PluginExecutionSession.Create(
            CreateConfig("analytics"),
            BuildExecutionMode.Production);

        var transforms = PluginRunner.CollectHtmlTransforms(
            context,
            session,
            BuildExecutionMode.Production);
        var analytics = Assert.Single(transforms, transform => transform.Name == "analytics");
        _ = analytics.Transform(
            new HtmlTransformContext(
                "/post/",
                "post/index.html",
                HtmlDocumentKind.Content,
                BuildExecutionMode.Production,
                context.Logger),
            "<html><head></head><body></body></html>");

        Assert.Equal(1, session.AnalyticsBuildState.Snapshot().ProcessedHtml);
        Assert.DoesNotContain(session.AnalyticsBuildState, context.Data.Values);
    }

    [Fact]
    public void ProductionSession_UsesEffectivePolicyInsteadOfCompatibilityDefaults()
    {
        var context = CreateContext();
        var config = CreateConfig("disabled") with
        {
            Site = CreateConfig("disabled").Site with
            {
                Plugins = new Dictionary<string, PluginToggleConfig>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["analytics"] = new() { Enabled = false }
                }
            }
        };
        var productionSession = PluginExecutionSession.Create(
            config,
            BuildExecutionMode.Production);

        var productionTransforms = PluginRunner.CollectHtmlTransforms(
            context,
            productionSession,
            BuildExecutionMode.Production);
        var compatibilityTransforms = PluginRunner.CollectHtmlTransforms(
            CreateContext(),
            BuildExecutionMode.Production);

        Assert.DoesNotContain(productionTransforms, transform => transform.Name == "analytics");
        Assert.Contains(compatibilityTransforms, transform => transform.Name == "analytics");
        Assert.False(productionSession.AnalyticsBuildState.Snapshot().PluginEnabled);
    }

    [Fact]
    public void TaxonomyCache_IsReusedWithinSessionAndIsolatedAcrossSessions()
    {
        var outputDir = Path.Combine(
            Path.GetTempPath(),
            "bukit_taxonomy_session_" + Guid.NewGuid().ToString("N"));
        try
        {
            var context = CreateContext(outputDir);
            var config = CreateConfig("taxonomy");
            var firstSession = PluginExecutionSession.Create(
                config,
                BuildExecutionMode.Production);
            var secondSession = PluginExecutionSession.Create(
                config,
                BuildExecutionMode.Production);
            var firstTaxonomy = Assert.IsType<Bukit.Engine.Plugins.BuiltIn.TaxonomyPlugin>(
                firstSession.Registrations.Single(item => item.Plugin.Name == "taxonomy").Plugin);
            var secondTaxonomy = Assert.IsType<Bukit.Engine.Plugins.BuiltIn.TaxonomyPlugin>(
                secondSession.Registrations.Single(item => item.Plugin.Name == "taxonomy").Plugin);
            Bukit.Engine.Plugins.BuiltIn.TaxonomyPlugin.ResetBuildIndexCountForTests();

            _ = firstTaxonomy.GetTemplateRequirementKinds(context);
            _ = firstTaxonomy.DerivePages(context);
            firstTaxonomy.AfterBuild(context);
            Assert.Equal(2, Bukit.Engine.Plugins.BuiltIn.TaxonomyPlugin.BuildIndexCountForTests);

            _ = secondTaxonomy.DerivePages(context);
            Assert.Equal(4, Bukit.Engine.Plugins.BuiltIn.TaxonomyPlugin.BuildIndexCountForTests);
            Assert.False(context.Data.ContainsKey("__taxonomy_index_cache"));
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ConfigGraphCheck_DetectsConfigInDictionaryBasePrivateField()
    {
        var malicious = new HiddenConfigDictionary(CreateConfig("hidden"))
        {
            ["visible"] = "ordinary"
        };
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["malicious"] = malicious
        };

        var exception = Record.Exception(() => AssertDataGraphDoesNotReachConfig(data));

        Assert.NotNull(exception);
    }

    [Fact]
    public void ConfigGraphCheck_HandlesDictionaryCycles()
    {
        var cycle = new Dictionary<string, object>(StringComparer.Ordinal);
        cycle["self"] = cycle;
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["cycle"] = cycle
        };

        AssertDataGraphDoesNotReachConfig(data);
    }

    private static void AssertDataGraphDoesNotReachConfig(
        IReadOnlyDictionary<string, object> data)
    {
        var configAssembly = typeof(AppConfig).Assembly;
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>(data.Values.Where(value => value is not null)!);

        while (pending.TryPop(out var value))
        {
            var type = value.GetType();
            Assert.NotSame(configAssembly, type.Assembly);
            if (type.IsPrimitive ||
                type.IsEnum ||
                value is string or decimal or DateTime or DateTimeOffset or TimeSpan or Guid or Type or Delegate ||
                !visited.Add(value))
            {
                continue;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not null)
                    {
                        pending.Push(entry.Key);
                    }

                    if (entry.Value is not null)
                    {
                        pending.Push(entry.Value);
                    }
                }
            }
            else if (value is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item is not null)
                    {
                        pending.Push(item);
                    }
                }
            }

            for (var currentType = type;
                 currentType is not null;
                 currentType = currentType.BaseType)
            {
                foreach (var field in currentType.GetFields(
                             BindingFlags.Instance |
                             BindingFlags.Public |
                             BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    if (field.FieldType.IsPointer || field.GetValue(value) is not { } fieldValue)
                    {
                        continue;
                    }

                    pending.Push(fieldValue);
                }
            }
        }
    }

    private static AppConfig CreateConfig(string name)
        => new()
        {
            Site = new SiteConfig
            {
                Name = name,
                Title = name,
                Analytics = new AnalyticsConfig
                {
                    ProductionOnly = false,
                    Providers =
                    [
                        new AnalyticsProviderConfig
                        {
                            Type = "google-analytics",
                            MeasurementId = "G-SESSION"
                        }
                    ]
                }
            },
            Content = TestContent.Markdown()
        };

    private static BuildContext CreateContext(string outputDir = "/test/dist")
        => new()
        {
            RootDir = "/test",
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = "/test/layouts",
            RoutedDocuments = [],
            BodyStore = NullContentBodyStore.Instance,
            TemplateResolver = kind => $"pages/{kind}.html",
            Logger = new ConsoleLogger(LogLevel.Error)
        };

    private abstract class ConfigHoldingDictionaryBase : Dictionary<object, object>
    {
        private readonly AppConfig _hiddenConfig;

        protected ConfigHoldingDictionaryBase(AppConfig hiddenConfig)
        {
            _hiddenConfig = hiddenConfig;
        }

        public override string ToString() => _hiddenConfig.Site.Name;
    }

    private sealed class HiddenConfigDictionary : ConfigHoldingDictionaryBase
    {
        internal HiddenConfigDictionary(AppConfig hiddenConfig)
            : base(hiddenConfig)
        {
        }
    }
}
