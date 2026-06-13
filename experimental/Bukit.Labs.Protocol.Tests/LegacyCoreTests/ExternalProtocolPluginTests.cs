using System.Text.Json;
using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.Protocol;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using Bukit.Engine.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ExternalProtocolPluginTests
{
    [Fact]
    public void ProtocolRequest_SerializesExpectedShape()
    {
        var request = new ProtocolPluginInvocationRequest
        {
            SchemaVersion = "2",
            Hook = "after-build",
            Plugin = new ProtocolPluginIdentity
            {
                Name = "sample",
                Version = "0.1.0"
            },
            Site = new ProtocolSiteInfo
            {
                BaseUrl = "/",
                Language = "zh-CN",
                Title = "Test"
            },
            Config = new ProtocolPluginConfig
            {
                PluginOptions = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mode"] = "demo"
                }
            },
            AfterBuild = new AfterBuildRequestPayload
            {
                OutputDir = "dist",
                RoutedPages = Array.Empty<AfterBuildRoutedPage>()
            }
        };

        var json = JsonSerializer.Serialize(request);

        Assert.Contains("\"schemaVersion\":\"2\"", json, StringComparison.Ordinal);
        Assert.Contains("\"hook\":\"after-build\"", json, StringComparison.Ordinal);
        Assert.Contains("\"outputDir\":\"dist\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pluginOptions\":{\"mode\":\"demo\"}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ProtocolResponse_DeserializesExpectedShape()
    {
        var json = """
                   {
                     "ok": true,
                     "logs": [
                       { "level": "info", "message": "ok" }
                     ],
                     "outputs": [
                       { "path": "plugin-output.json", "contentType": "application/json", "text": "{\"ok\":true}" }
                     ]
                   }
                   """;

        var response = JsonSerializer.Deserialize<ProtocolPluginInvocationResponse>(json);

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Single(response.Logs);
        Assert.Single(response.Outputs);
        Assert.Equal("plugin-output.json", response.Outputs[0].Path);
    }

    [Fact]
    public async Task ProcessPluginInvoker_InvokesExecutableAndReadsJson()
    {
        var invoker = new ProcessPluginInvoker();
        var plugin = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = DotNetHostPath(),
            Hooks = new[] { "after-build" },
            TimeoutMs = 5000
        };

        var result = await invoker.InvokeAsync(
            plugin,
            "{}",
            BuildArguments("success"),
            CancellationToken.None);

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"ok\":true", result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessPluginInvoker_TimesOut_WhenProcessHangs()
    {
        var invoker = new ProcessPluginInvoker();
        var plugin = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = DotNetHostPath(),
            Hooks = new[] { "after-build" },
            TimeoutMs = 100
        };

        var result = await invoker.InvokeAsync(
            plugin,
            "{}",
            BuildArguments("sleep"),
            CancellationToken.None);

        Assert.True(result.TimedOut);
    }

    [Fact]
    public async Task ProcessPluginInvoker_Fails_WhenStdoutEmpty()
    {
        var invoker = new ProcessPluginInvoker();
        var plugin = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = DotNetHostPath(),
            Hooks = new[] { "after-build" },
            TimeoutMs = 5000
        };

        var result = await invoker.InvokeAsync(
            plugin,
            "{}",
            BuildArguments("empty"),
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(result.StdOut));
    }

    private static string BuildArguments(string mode, IReadOnlyList<string>? extraArgs = null)
    {
        var pluginDll = Path.Combine(AppContext.BaseDirectory, "ProtocolEchoPlugin.dll");
        var args = extraArgs is null || extraArgs.Count == 0
            ? string.Empty
            : " " + string.Join(" ", extraArgs.Select(x => $"\"{x}\""));
        return $"\"{pluginDll}\" {mode}{args}";
    }

    private static string DotNetHostPath()
        => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

    private static string JsonEncodedPath(string path)
        => JsonSerializer.Serialize(path).Trim('"');

    [Fact]
    public void PluginRegistry_IncludesExternalProtocolPlugins_WhenConfigured()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "warn", "success");

        var plugins = PluginRegistry.GetAllPlugins(context).ToList();

        Assert.Contains(plugins, x => x.Plugin.Name == "sample" && x.Source == "external-protocol");
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_WritesOutputFile()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "success");

        PluginRunner.RunAfterBuild(context);

        var outputPath = Path.Combine(context.OutputDir, "plugin-output.json");
        Assert.True(File.Exists(outputPath));
        Assert.Contains("\"ok\":true", File.ReadAllText(outputPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExternalProtocolPlugin_AfterBuildAsync_WritesOutputFile()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "success");

        await PluginRunner.RunAfterBuildAsync(context);

        var outputPath = Path.Combine(context.OutputDir, "plugin-output.json");
        Assert.True(File.Exists(outputPath));
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_UsesV2Negotiation_WhenSupported()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "handshake-v2");

        PluginRunner.RunAfterBuild(context);

        var outputPath = Path.Combine(context.OutputDir, "plugin-output.json");
        Assert.True(File.Exists(outputPath));
        Assert.Contains("\"version\":\"2\"", File.ReadAllText(outputPath), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("process", "handshake-v2", true)]
    public void ExternalProtocolPlugin_AfterBuild_ProtocolSchemaCompatibilityMatrix(
        string runtime,
        string mode,
        bool expectsV2)
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", mode, runtime: runtime);

        PluginRunner.RunAfterBuild(context);

        var outputPath = Path.Combine(context.OutputDir, "plugin-output.json");
        Assert.True(File.Exists(outputPath));
        var output = File.ReadAllText(outputPath);
        if (expectsV2)
        {
            Assert.Contains("\"version\":\"2\"", output, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.Contains("\"ok\":true", output, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_CachesHandshakePerBuildContext()
    {
        using var temp = new TempDir();
        var handshakeCounterPath = Path.Combine(temp.Path, "handshake-counter.txt");
        var context = CreateContext(temp.Path, "strict", "handshake-counter", extraPluginArgs: new[] { handshakeCounterPath });

        PluginRunner.RunAfterBuild(context);
        PluginRunner.RunAfterBuild(context);

        Assert.True(File.Exists(handshakeCounterPath));
        Assert.Equal("1", File.ReadAllText(handshakeCounterPath).Trim());
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_Default_DoesNotSendRoutedPages()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "handshake-routedpages");

        PluginRunner.RunAfterBuild(context);

        var outputPath = Path.Combine(context.OutputDir, "plugin-output.json");
        Assert.True(File.Exists(outputPath));
        Assert.Contains("\"routedPagesCount\":0", File.ReadAllText(outputPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_SendsRoutedPages_WhenConfigEnabled()
    {
        using var temp = new TempDir();
        var context = CreateContext(
            temp.Path,
            "strict",
            "handshake-routedpages",
            includeRoutedPages: true);

        PluginRunner.RunAfterBuild(context);

        var outputPath = Path.Combine(context.OutputDir, "plugin-output.json");
        Assert.True(File.Exists(outputPath));
        Assert.Contains("\"routedPagesCount\":1", File.ReadAllText(outputPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_Throws_WhenPluginDoesNotSupportV2()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "handshake-v1only");

        var ex = Assert.Throws<ConfigException>(() => PluginRunner.RunAfterBuild(context));

        Assert.Contains("requires protocol schema version 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_Throws_WhenHandshakeReturnsInvalidJson()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "handshake-invalid");

        var ex = Assert.Throws<ConfigException>(() => PluginRunner.RunAfterBuild(context));

        Assert.Contains("handshake returned invalid JSON", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_DefaultEnvironmentDoesNotExposeHostSecrets()
    {
        using var temp = new TempDir();
        var oldOpenAi = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var oldGithub = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", "secret-openai");
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "secret-github");
            var context = CreateContext(temp.Path, "strict", "env");

            PluginRunner.RunAfterBuild(context);

            var output = File.ReadAllText(Path.Combine(context.OutputDir, "plugin-output.json"));
            using var doc = JsonDocument.Parse(output);
            Assert.Equal("", doc.RootElement.GetProperty("openAi").GetString());
            Assert.Equal("", doc.RootElement.GetProperty("github").GetString());
            Assert.Equal("sample", doc.RootElement.GetProperty("pluginName").GetString());
            Assert.Equal("after-build", doc.RootElement.GetProperty("pluginHook").GetString());
            Assert.Equal(temp.Path, doc.RootElement.GetProperty("projectRoot").GetString());
            Assert.Equal(context.OutputDir, doc.RootElement.GetProperty("outputDir").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAI_API_KEY", oldOpenAi);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", oldGithub);
        }
    }

    [Fact]
    public async Task ProcessPluginInvoker_FailsWhenStdoutExceedsLimit()
    {
        var invoker = new ProcessPluginInvoker();
        var plugin = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = DotNetHostPath(),
            Hooks = new[] { "after-build" },
            TimeoutMs = 5000,
            MaxStdoutBytes = 128
        };

        var result = await invoker.InvokeAsync(plugin, "{}", BuildArguments("large-stdout"), CancellationToken.None);

        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("stdout", result.StdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.StdOut.Length <= 128);
    }

    [Fact]
    public async Task ProcessPluginInvoker_FailsWhenStderrExceedsLimit()
    {
        var invoker = new ProcessPluginInvoker();
        var plugin = new ExternalPluginConfig
        {
            Runtime = "process",
            Entry = DotNetHostPath(),
            Hooks = new[] { "after-build" },
            TimeoutMs = 5000,
            MaxStderrBytes = 128
        };

        var result = await invoker.InvokeAsync(plugin, "{}", BuildArguments("large-stderr"), CancellationToken.None);

        Assert.Equal(-1, result.ExitCode);
        Assert.Contains("stderr", result.StdErr, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.StdErr.Length < 1024);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_TimesOutAndFailsFast()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "sleep", timeoutMs: 50);
        var started = DateTimeOffset.UtcNow;

        var ex = Assert.ThrowsAny<Exception>(() => PluginRunner.RunAfterBuild(context));
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_RespectsWarnMode()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "warn", "invalid");

        var ex = Record.Exception(() => PluginRunner.RunAfterBuild(context));

        Assert.Null(ex);
        Assert.Contains(context.PluginExecutions, x => x.Name == "sample" && !x.Success);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_ThrowsInStrictMode()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "invalid");

        Assert.ThrowsAny<Exception>(() => PluginRunner.RunAfterBuild(context));
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_RejectsPathTraversal()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "traversal");

        Assert.ThrowsAny<Exception>(() => PluginRunner.RunAfterBuild(context));
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_Throws_WhenPluginReturnsOkFalse()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "error");

        var ex = Assert.ThrowsAny<Exception>(() => PluginRunner.RunAfterBuild(context));
        Assert.Contains("plugin failed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_ReturnsDerivedPage_WhenHookConfigured()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-success", hooks: new[] { "derive-pages" });

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x =>
            x.Document.Id == "derived-1" &&
            x.Route.Url == "/derived/derived-1/" &&
            string.Equals(
                x.Route.OutputPath.Replace('\\', '/'),
                "derived/derived-1/index.html",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_UsesV2Negotiation_WhenSupported()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-success", hooks: new[] { "derive-pages" });

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x => x.Document.Id == "derived-1");
        Assert.Contains(context.PluginExecutions, x => x.Name == "sample" && x.Hook == "derive-pages" && x.Success);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_Throws_WhenPluginDoesNotSupportV2()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "handshake-v1only", hooks: new[] { "derive-pages" });

        var ex = Assert.Throws<ConfigException>(() => PluginRunner.RunDerivePages(context));

        Assert.Contains("requires protocol schema version 2", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_CachesHandshakePerBuildContext()
    {
        using var temp = new TempDir();
        var handshakeCounterPath = Path.Combine(temp.Path, "derive-handshake-counter.txt");
        var context = CreateContext(
            temp.Path,
            "strict",
            "handshake-counter",
            hooks: new[] { "derive-pages" },
            extraPluginArgs: new[] { handshakeCounterPath });

        PluginRunner.RunDerivePages(context);
        PluginRunner.RunDerivePages(context);

        Assert.True(File.Exists(handshakeCounterPath));
        Assert.Equal("1", File.ReadAllText(handshakeCounterPath).Trim());
    }

    [Theory]
    [InlineData("process")]
    public void ExternalProtocolPlugin_DerivePages_CompatibilityMatrix_ByRuntime(string runtime)
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-success", runtime: runtime, hooks: new[] { "derive-pages" });

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x => x.Document.Id == "derived-1");
    }

    [Fact]
    public async Task ExternalProtocolPlugin_DerivePagesAsync_ReturnsDerivedPage_WhenHookConfigured()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-success", hooks: new[] { "derive-pages" });

        var derived = await PluginRunner.RunDerivePagesAsync(context);

        Assert.Contains(derived, x => x.Document.Id == "derived-1");
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_NotExecuted_WhenHookNotConfigured()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-success", hooks: new[] { "after-build" });

        var derived = PluginRunner.RunDerivePages(context);

        Assert.DoesNotContain(derived, x => x.Document.Id == "derived-1");
        Assert.DoesNotContain(context.PluginExecutions, x => x.Name == "sample" && x.Hook == "derive-pages");
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_ThrowsOnRouteConflict_InStrictMode()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-conflict", hooks: new[] { "derive-pages" });

        var ex = Assert.ThrowsAny<Exception>(() => PluginRunner.RunDerivePages(context));
        Assert.Contains("conflict", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_WarnMode_SkipsConflictingDerivedPages()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "warn", "derive-conflict", hooks: new[] { "derive-pages" });

        var derived = PluginRunner.RunDerivePages(context);

        Assert.DoesNotContain(derived, x => x.Document.Id == "derived-conflict");
        Assert.Contains(context.PluginExecutions, x => x.Name == "sample" && x.Hook == "derive-pages" && !x.Success);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_LastWinsPolicy_AllowsDerivedPages()
    {
        using var temp = new TempDir();
        var context = CreateContext(
            temp.Path,
            "strict",
            "derive-lastwins",
            hooks: new[] { "derive-pages" },
            deriveConflictPolicy: "last-wins");

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x => x.Document.Id == "derived-conflict");
        Assert.Contains(context.PluginExecutions, x => x.Name == "sample" && x.Hook == "derive-pages" && x.Success);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_LastWins_ReplacesEarlierPluginDerivedPage()
    {
        using var temp = new TempDir();
        var outputDir = Path.Combine(temp.Path, "dist");
        Directory.CreateDirectory(outputDir);
        var context = new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test",
                    PluginFailMode = "strict",
                    DeriveConflictPolicy = "last-wins",
                    ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sample-a"] = new()
                        {
                            Runtime = "process",
                            Entry = DotNetHostPath(),
                            Hooks = new[] { "derive-pages" },
                            Capabilities = new[] { "derive-pages" },
                            TimeoutMs = 5000,
                            Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["processArgs"] = BuildProcessArgsOptions("derive-plugin-a")
                            }
                        },
                        ["sample-b"] = new()
                        {
                            Runtime = "process",
                            Entry = DotNetHostPath(),
                            Hooks = new[] { "derive-pages" },
                            Capabilities = new[] { "derive-pages" },
                            TimeoutMs = 5000,
                            Options = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["processArgs"] = BuildProcessArgsOptions("derive-plugin-b")
                            }
                        }
                    }
                },
                Content = TestContent.Markdown()
            },
            RootDir = temp.Path,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(temp.Path, "layouts"),
            RoutedDocuments = new List<(ContentDocument, RouteInfo)>
            {
                (ContentDocument.Create("post-1", "Post 1", "post-1", DateTimeOffset.UtcNow, "<p>Body</p>",
                    ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "post" })),
                    new RouteInfo("/blog/post-1/", Path.Combine("blog", "post-1", "index.html"), "pages/post.html"))
            }.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x => x.Document.Id == "plugin-b");
        Assert.DoesNotContain(derived, x => x.Document.Id == "plugin-a");
        Assert.Contains(context.PluginExecutions, x => x.Name == "sample-b" && x.Hook == "derive-pages" && x.Success);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_WarnConflictPolicy_SkipsWithoutFailingPluginExecution()
    {
        using var temp = new TempDir();
        var context = CreateContext(
            temp.Path,
            "strict",
            "derive-conflict",
            hooks: new[] { "derive-pages" },
            deriveConflictPolicy: "warn");

        var derived = PluginRunner.RunDerivePages(context);

        Assert.DoesNotContain(derived, x => x.Document.Id == "derived-conflict");
        Assert.Contains(context.PluginExecutions, x => x.Name == "sample" && x.Hook == "derive-pages" && x.Success);
    }

    [Fact]
    public void ExternalProtocolPlugin_AfterBuild_InvalidJsonError_ContainsStandardKeyword()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "invalid");

        var ex = Assert.ThrowsAny<Exception>(() => PluginRunner.RunAfterBuild(context));
        Assert.Contains("[plugin-protocol][after-build]", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExternalProtocolPlugin_DerivePages_GeneratedPageContent_CanBeRendered()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-success", hooks: new[] { "derive-pages" });

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x => x.Document.Id == "derived-1");
        var derivePage = derived.First(x => x.Document.Id == "derived-1");
        Assert.Equal("Derived 1", derivePage.Document.Title);
    }

    private static BuildContext CreateContext(
        string rootDir,
        string failMode,
        string pluginMode,
        string runtime = "process",
        IReadOnlyList<string>? hooks = null,
        string? deriveConflictPolicy = null,
        IReadOnlyList<string>? extraPluginArgs = null,
        bool includeRoutedPages = false,
        int timeoutMs = 5000,
        IReadOnlyList<string>? allowEnvironment = null)
    {
        var outputDir = Path.Combine(rootDir, "dist");
        Directory.CreateDirectory(outputDir);
        var isWasm = string.Equals(runtime, "wasm", StringComparison.OrdinalIgnoreCase);

        var effectiveHooks = hooks ?? new[] { "after-build" };
        var effectiveCapabilities = effectiveHooks
            .Select(h => h.Trim().ToLowerInvariant() switch
            {
                "derive-pages" => "derive-pages",
                "after-build" => "emit-outputs",
                _ => null
            })
            .Where(c => c is not null)
            .Cast<string>()
            .ToList();

        return new BuildContext
        {
            Config = new AppConfig
            {
                Site = new SiteConfig
                {
                    Name = "test",
                    Title = "Test",
                    PluginFailMode = failMode,
                    DeriveConflictPolicy = deriveConflictPolicy ?? "fail",
                    ExternalProtocolIncludeRoutedPages = includeRoutedPages,
                    ExternalPlugins = new Dictionary<string, ExternalPluginConfig>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["sample"] = new()
                        {
                            Runtime = runtime,
                            Entry = isWasm ? CreateWasmModuleForMode(rootDir, pluginMode) : DotNetHostPath(),
                            Hooks = effectiveHooks,
                            Capabilities = effectiveCapabilities,
                            TimeoutMs = timeoutMs,
                            AllowEnvironment = allowEnvironment,
                            Options = isWasm
                                ? null
                                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["processArgs"] = BuildProcessArgsOptions(pluginMode, extraPluginArgs)
                                }
                        }
                    }
                },
                Content = TestContent.Markdown()
            },
            RootDir = rootDir,
            OutputDir = outputDir,
            BaseUrl = "/",
            LayoutsDir = Path.Combine(rootDir, "layouts"),
            RoutedDocuments = new List<(ContentDocument Item, RouteInfo Route)>
            {
                (
                    ContentDocument.Create("post-1", "Post 1", "post-1", DateTimeOffset.UtcNow, "<p>Body</p>",
                        ContentFieldReader.ToFieldMap(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["type"] = "post"
                        })),
                    new RouteInfo("/blog/post-1/", Path.Combine("blog", "post-1", "index.html"), "pages/post.html")
                )
            }.ToRoutedDocuments(),
            Logger = new ConsoleLogger(LogLevel.Error)
        };
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bukit-external-plugin-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }

    private static IReadOnlyDictionary<string, object> BuildProcessArgsOptions(string mode, IReadOnlyList<string>? extraArgs = null)
    {
        var positionals = new List<object> { Path.Combine(AppContext.BaseDirectory, "ProtocolEchoPlugin.dll"), mode };
        if (extraArgs is not null)
        {
            foreach (var arg in extraArgs)
            {
                positionals.Add(arg);
            }
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["positionals"] = positionals
        };
    }

    private static string CreateWasmModuleForMode(string rootDir, string mode)
    {
        var json = mode switch
        {
            "empty" => "{}",
            "success" => "{\"ok\":true,\"outputs\":[{\"path\":\"plugin-output.json\",\"contentType\":\"text/plain\",\"text\":\"{\\\"ok\\\":true}\"}]}",
            "derive-success" => "{\"ok\":true,\"derivedPages\":[{\"id\":\"derived-1\",\"title\":\"Derived 1\",\"slug\":\"derived-1\",\"url\":\"/derived/derived-1/\",\"outputPath\":\"derived/derived-1/index.html\",\"template\":\"pages/page.html\",\"fields\":{\"type\":\"page\"}}]}",
            "handshake-v2" => "{\"ok\":true,\"outputs\":[{\"path\":\"plugin-output.json\",\"contentType\":\"text/plain\",\"text\":\"{\\\"version\\\":\\\"2\\\"}\"}]}",
            "handshake-v1only" => "{\"ok\":true,\"outputs\":[{\"path\":\"plugin-output.json\",\"contentType\":\"text/plain\",\"text\":\"{\\\"ok\\\":true}\"}]}",
            _ => "{\"ok\":true}"
        };

        var wasmDir = Path.Combine(rootDir, "plugins");
        Directory.CreateDirectory(wasmDir);
        var watPath = Path.Combine(wasmDir, $"protocol-{mode}.wat");
        File.WriteAllText(watPath, mode == "memory-overlimit" ? BuildWasiMemoryOverLimitModule() : BuildWasiWatResponseModule(json));
        return watPath;
    }

    private static string BuildWasiWatResponseModule(string json)
    {
        var bytes = Encoding.UTF8.GetByteCount(json);
        var escaped = EscapeWatString(json);
        return $"""
(module
  (import "wasi_snapshot_preview1" "fd_write" (func $fd_write (param i32 i32 i32 i32) (result i32)))
  (memory (export "memory") 1)
  (data (i32.const 8) "{escaped}")
  (func (export "_start")
    (i32.store (i32.const 0) (i32.const 8))
    (i32.store (i32.const 4) (i32.const {bytes}))
    (drop (call $fd_write (i32.const 1) (i32.const 0) (i32.const 1) (i32.const 20)))
  )
)
""";
    }

    private static string EscapeWatString(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\0a", StringComparison.Ordinal)
            .Replace("\r", "\\0d", StringComparison.Ordinal);

    private static string BuildWasiMemoryOverLimitModule()
    {
        return """
(module
  (memory 20)
  (func (export "_start"))
)
""";
    }

    [Fact]
    public void ProtocolPlugin_Environment_RetainsPathAndHome()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "warn", "env-allowlist");

        PluginRunner.RunAfterBuild(context);

        var reportPath = Path.Combine(context.OutputDir, "env-report.json");
        Assert.True(File.Exists(reportPath), "env-report.json not created");
        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.True(doc.RootElement.GetProperty("pathNotEmpty").GetBoolean(), "PATH should not be empty");
        Assert.True(doc.RootElement.GetProperty("homeNotEmpty").GetBoolean(), "HOME should not be empty");
    }

    [Fact]
    public void ProtocolPlugin_Environment_BlocksSensitiveVariables()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "warn", "env-allowlist");

        PluginRunner.RunAfterBuild(context);

        var reportPath = Path.Combine(context.OutputDir, "env-report.json");
        Assert.True(File.Exists(reportPath), "env-report.json not created");
        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.False(doc.RootElement.GetProperty("notionNotEmpty").GetBoolean(), "NOTION_TOKEN should be empty");
        Assert.False(doc.RootElement.GetProperty("openAiNotEmpty").GetBoolean(), "OPENAI_API_KEY should be empty");
    }

    [Fact]
    public void ProtocolPlugin_Environment_InjectBukitContext()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "warn", "env-allowlist");

        PluginRunner.RunAfterBuild(context);

        var reportPath = Path.Combine(context.OutputDir, "env-report.json");
        Assert.True(File.Exists(reportPath), "env-report.json not created");
        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal("sample", doc.RootElement.GetProperty("pluginName").GetString());
        Assert.Equal("after-build", doc.RootElement.GetProperty("pluginHook").GetString());
    }

    [Fact]
    public void ProtocolPlugin_AllowEnvironment_RetainsCustomVariable()
    {
        var testVarName = "BUKIT_TEST_ALLOW_ENV";
        try
        {
            Environment.SetEnvironmentVariable(testVarName, "custom-value");

            using var temp = new TempDir();
            var context = CreateContext(temp.Path, "warn", "env-allowlist",
                allowEnvironment: new[] { testVarName });

            PluginRunner.RunAfterBuild(context);

            var reportPath = Path.Combine(context.OutputDir, "env-report.json");
            Assert.True(File.Exists(reportPath), "env-report.json not created");
            using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
            Assert.Equal("custom-value", doc.RootElement.GetProperty("customEnv").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [Fact]
    public void ProtocolPlugin_DeriveConflict_InvokesSuccessfully()
    {
        using var temp = new TempDir();
        var context = CreateContext(temp.Path, "strict", "derive-lastwins",
            hooks: new[] { "derive-pages" }, deriveConflictPolicy: "last-wins");

        var derived = PluginRunner.RunDerivePages(context);

        Assert.Contains(derived, x => x.Document.Id == "derived-conflict");
    }
}
