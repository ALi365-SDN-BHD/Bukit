using Bukit.Cli.Commands;
using Bukit.Cli.Commands.Dev;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Config;
using Bukit.Shared;
using System.Net;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DevCommandTests
{
    [Fact]
    public async Task WaitForShutdownOrAcceptLoopAsync_AcceptLoopFaults_PropagatesBeforeCancellation()
    {
        using var cts = new CancellationTokenSource();
        var expected = new InvalidOperationException("accept loop failed");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevCommand.WaitForShutdownOrAcceptLoopAsync(
                Task.FromException(expected),
                cts.Token));

        Assert.Same(expected, actual);
        Assert.False(cts.IsCancellationRequested);
    }

    [Fact]
    public void DevPathGuard_OnNonWindows_RejectsCaseDifferentSiblingEscape()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-path-" + Guid.NewGuid().ToString("N"), "site");
        Directory.CreateDirectory(root);
        try
        {
            var result = DevPathGuard.TryResolveWithinRoot(root, "../SITE/index.html");
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
        }
    }

    [Fact]
    public void PathUtils_IsSubPathOf_DoesNotMatchPrefixSibling()
    {
        var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo", "themes", "foo"));
        var sibling = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "repo", "themes", "foo-extra", "layout.html"));

        Assert.False(PathUtils.IsSubPathOf(sibling, parent));
    }

    [Fact]
    public void ResolveWatchDirs_DoesNotTreatPrefixSiblingAsThemeChild()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-watch-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "themes", "foo"));
            Directory.CreateDirectory(Path.Combine(root, "themes", "foo-extra"));

            var config = MinimalConfig() with
            {
                Theme = new ThemeConfig
                {
                    Name = "foo",
                    Layouts = Path.Combine("themes", "foo-extra"),
                    Assets = "missing-assets",
                    Static = "missing-static"
                }
            };

            var dirs = DevCommand.ResolveWatchDirs(root, config);

            Assert.Contains(Path.GetFullPath(Path.Combine(root, "themes", "foo")), dirs);
            Assert.Contains(Path.GetFullPath(Path.Combine(root, "themes", "foo-extra")), dirs);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void DevFileWatcher_ShouldIgnore_DynamicOutputCacheAndCommonGeneratedDirs()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-ignore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var output = Path.Combine(root, "public");
            var cache = Path.Combine(root, ".cache");
            var excluded = DevCommand.ResolveExcludedWatchDirs(root, output, cache);
            using var watcher = new DevFileWatcher(
                Array.Empty<string>(),
                root,
                new TestLogger(),
                static (_, _) => Task.CompletedTask,
                excluded);

            Assert.True(watcher.ShouldIgnore(Path.Combine(output, "index.html"), "index.html"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(cache, "manifest.json"), "manifest.json"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, ".git", "HEAD"), "HEAD"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "node_modules", "pkg", "index.js"), "index.js"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, ".bukit", "state.json"), "state.json"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "bin", "Debug", "file.dll"), "file.dll"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "obj", "project.assets.json"), "project.assets.json"));
            Assert.True(watcher.ShouldIgnore(Path.Combine(root, "content", ".draft.md"), ".draft.md"));

            Assert.False(watcher.ShouldIgnore(Path.Combine(root, "content", "page.md"), "page.md"));
            Assert.False(watcher.ShouldIgnore(Path.Combine(root, "dist", "index.html"), "index.html"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-rebuild-error-" + Guid.NewGuid().ToString("N"));
        var watchDir = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchDir);
        File.WriteAllText(Path.Combine(watchDir, "page.md"), "initial");

        try
        {
            var watchedFile = Path.Combine(watchDir, "page.md");
            var rebuildAttempts = 0;
            using var logger = new BufferingLogger();
            using var watcher = new DevFileWatcher(
                new[] { watchDir },
                root,
                logger,
                (_, _) =>
                {
                    var attempt = Interlocked.Increment(ref rebuildAttempts);
                    if (attempt == 1)
                    {
                        return Task.FromException(new InvalidOperationException("simulated rebuild failure"));
                    }

                    return Task.CompletedTask;
                },
                debounceMs: 25);

            watcher.Start(CancellationToken.None);

            await InvokeScheduleRebuildAsync(watcher, watchedFile);
            await InvokeScheduleRebuildAsync(watcher, watchedFile);

            Assert.Equal(2, rebuildAttempts);
            Assert.Contains(logger.Errors, error => error.Contains("dev.rebuild.error", StringComparison.Ordinal));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DevFileWatcher_RebuildFailure_AllowsNextSuccessfulRebuild()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-rebuild-recovery-" + Guid.NewGuid().ToString("N"));
        var watchDir = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchDir);
        var watchedFile = Path.Combine(watchDir, "page.md");

        try
        {
            var rebuildAttempts = 0;
            var successfulRebuilds = 0;
            using var watcher = new DevFileWatcher(
                new[] { watchDir },
                root,
                new BufferingLogger(),
                (_, _) =>
                {
                    var attempt = Interlocked.Increment(ref rebuildAttempts);
                    if (attempt == 1)
                    {
                        return Task.FromException(new InvalidOperationException("broken template"));
                    }

                    Interlocked.Increment(ref successfulRebuilds);
                    return Task.CompletedTask;
                },
                debounceMs: 25);

            watcher.Start(CancellationToken.None);

            await InvokeScheduleRebuildAsync(watcher, watchedFile);
            await InvokeScheduleRebuildAsync(watcher, watchedFile);

            Assert.Equal(2, rebuildAttempts);
            Assert.Equal(1, successfulRebuilds);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DevFileWatcher_RebuildFailure_DoesNotBroadcastReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-rebuild-no-reload-" + Guid.NewGuid().ToString("N"));
        var watchDir = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchDir);
        var watchedFile = Path.Combine(watchDir, "page.md");

        try
        {
            var reloads = 0;
            var rebuildAttempts = 0;
            using var watcher = new DevFileWatcher(
                new[] { watchDir },
                root,
                new BufferingLogger(),
                (_, _) =>
                {
                    var attempt = Interlocked.Increment(ref rebuildAttempts);
                    if (attempt == 1)
                    {
                        return Task.FromException(new InvalidOperationException("broken template"));
                    }

                    Interlocked.Increment(ref reloads);
                    return Task.CompletedTask;
                },
                debounceMs: 25);

            watcher.Start(CancellationToken.None);

            await InvokeScheduleRebuildAsync(watcher, watchedFile);
            Assert.Equal(0, reloads);

            await InvokeScheduleRebuildAsync(watcher, watchedFile);
            Assert.Equal(1, reloads);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DevFileWatcher_RapidChanges_DebouncedToSingleRebuild()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-rebuild-debounce-" + Guid.NewGuid().ToString("N"));
        var watchDir = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchDir);
        var watchedFile = Path.Combine(watchDir, "page.md");

        try
        {
            var rebuildCount = 0;
            using var watcher = new DevFileWatcher(
                new[] { watchDir },
                root,
                new BufferingLogger(),
                (_, _) =>
                {
                    Interlocked.Increment(ref rebuildCount);
                    return Task.CompletedTask;
                },
                debounceMs: 25);

            watcher.Start(CancellationToken.None);

            var tasks = Enumerable
                .Range(0, 12)
                .Select(_ => InvokeScheduleRebuildAsync(watcher, watchedFile))
                .ToArray();

            await Task.WhenAll(tasks);
            Assert.Equal(1, rebuildCount);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DevFileWatcher_MultipleEventsWithinDebounceWindow_OnlyOneBuild()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-rebuild-window-" + Guid.NewGuid().ToString("N"));
        var watchDir = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchDir);
        var watchedFile = Path.Combine(watchDir, "page.md");

        try
        {
            var rebuildCount = 0;
            using var watcher = new DevFileWatcher(
                new[] { watchDir },
                root,
                new BufferingLogger(),
                (_, _) =>
                {
                    Interlocked.Increment(ref rebuildCount);
                    return Task.CompletedTask;
                },
                debounceMs: 50);

            watcher.Start(CancellationToken.None);

            await Task.WhenAll(
                InvokeScheduleRebuildAsync(watcher, watchedFile),
                InvokeScheduleRebuildAsync(watcher, watchedFile),
                InvokeScheduleRebuildAsync(watcher, watchedFile));

            Assert.Equal(1, rebuildCount);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task DevFileWatcher_EventsAfterDebounce_TriggerNewBuild()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-rebuild-after-window-" + Guid.NewGuid().ToString("N"));
        var watchDir = Path.Combine(root, "watch");
        Directory.CreateDirectory(watchDir);
        var watchedFile = Path.Combine(watchDir, "page.md");

        try
        {
            var rebuildCount = 0;
            using var watcher = new DevFileWatcher(
                new[] { watchDir },
                root,
                new BufferingLogger(),
                (_, _) =>
                {
                    Interlocked.Increment(ref rebuildCount);
                    return Task.CompletedTask;
                },
                debounceMs: 25);

            watcher.Start(CancellationToken.None);

            await InvokeScheduleRebuildAsync(watcher, watchedFile);
            await InvokeScheduleRebuildAsync(watcher, watchedFile);

            Assert.Equal(2, rebuildCount);
        }
        finally
        {
            TestCleanup.DeleteDirectory(root);
        }
    }

    [Fact]
    public void WebSocketPolicy_RequiresHostAndOriginSameOrigin()
    {
        var policy = new DevWebSocketAccessPolicy("localhost", 35729, allowLan: false);

        Assert.True(policy.IsAllowed("localhost:35729", "http://localhost:35729", out _));
        Assert.False(policy.IsAllowed("localhost:35729", null, out var missingOrigin));
        Assert.Contains("Origin", missingOrigin, StringComparison.Ordinal);
        Assert.False(policy.IsAllowed("localhost:35729", "http://example.com:35729", out var crossOrigin));
        Assert.Contains("Origin host", crossOrigin, StringComparison.Ordinal);
        Assert.False(policy.IsAllowed("localhost:3000", "http://localhost:3000", out var wrongPort));
        Assert.Contains("Host port", wrongPort, StringComparison.Ordinal);
    }

    [Fact]
    public void WebSocketPolicy_RejectsLanHostUnlessAllowLan()
    {
        var loopbackOnly = new DevWebSocketAccessPolicy("0.0.0.0", 35729, allowLan: false);
        Assert.False(loopbackOnly.IsAllowed("192.168.1.10:35729", "http://192.168.1.10:35729", out var reason));
        Assert.Contains("loopback", reason, StringComparison.Ordinal);

        var allowLan = new DevWebSocketAccessPolicy("0.0.0.0", 35729, allowLan: true);
        Assert.True(allowLan.IsAllowed("192.168.1.10:35729", "http://192.168.1.10:35729", out _));
        Assert.False(allowLan.IsAllowed("192.168.1.10:35729", "http://192.168.1.11:35729", out _));
    }

    [Fact]
    public void ExtractOptions_RecognizesAllowLanAndPublicAliases()
    {
        var allowLan = DevCommand.ExtractOptions(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--allow-lan"] = "true"
            },
            Array.Empty<string>()));

        var publicAlias = DevCommand.ExtractOptions(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--public"] = "true"
            },
            Array.Empty<string>()));

        Assert.True(allowLan.allowLan);
        Assert.True(publicAlias.allowLan);
    }

    [Fact]
    public void DevCommand_NonLoopbackHostRequiresAllowLan()
    {
        Assert.False(DevCommand.ShouldRefuseLanExposure("localhost", allowLan: false));
        Assert.False(DevCommand.ShouldRefuseLanExposure("127.0.0.1", allowLan: false));
        Assert.True(DevCommand.ShouldRefuseLanExposure("0.0.0.0", allowLan: false));
        Assert.True(DevCommand.ShouldRefuseLanExposure("192.168.1.10", allowLan: false));
        Assert.False(DevCommand.ShouldRefuseLanExposure("0.0.0.0", allowLan: true));
    }

    [Fact]
    public void DevCommand_PublicAliasEnablesLanAccess()
    {
        var publicAlias = DevCommand.ExtractOptions(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--public"] = "true"
            },
            Array.Empty<string>()));

        Assert.True(publicAlias.allowLan);
        Assert.False(DevCommand.ShouldRefuseLanExposure("0.0.0.0", publicAlias.allowLan));
    }

    [Fact]
    public void DevCommand_NoWatch_DoesNotStartWatcher()
    {
        Assert.False(DevCommand.ShouldStartWatcher(noWatch: true, watchedDirsCount: 1));
        Assert.False(DevCommand.ShouldStartWatcher(noWatch: false, watchedDirsCount: 0));
        Assert.True(DevCommand.ShouldStartWatcher(noWatch: false, watchedDirsCount: 1));
    }

    [Fact]
    public void CreateBuildOverrides_UsesDevelopmentExecutionMode()
    {
        var overrides = DevCommand.CreateBuildOverrides(clean: true, outputOverride: null, cacheDir: ".cache");

        Assert.Equal(BuildExecutionMode.Development, overrides.ExecutionMode);
    }

    [Fact]
    public async Task DevCommand_NoWatch_ServesStaticOutput()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-nowatch-static-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), "<html><body>static</body></html>");

        try
        {
            Assert.False(DevCommand.ShouldStartWatcher(noWatch: true, watchedDirsCount: 1));

            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                "/",
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("static", response.Body, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public void DevCommand_NoWatch_DoesNotPrintLiveReloadWatchingMessage()
    {
        Assert.False(DevCommand.ShouldPrintWatchStatus(noWatch: true));
        Assert.True(DevCommand.ShouldPrintWatchStatus(noWatch: false));
    }

    [Fact]
    public void CliRegistry_DevCommandIncludesLanExposureFlags()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var dev = registry.Commands.Single(c => c.Name == "dev");
        var optionNames = (dev.Options ?? Array.Empty<Cli.Shared.Cli.Metadata.CliOptionSpec>())
            .Select(o => o.Name)
            .ToArray();

        Assert.Contains("--allow-lan", optionNames);
        Assert.Contains("--public", optionNames);
    }

    [Fact]
    public void InjectLivereload_UsesLocationProtocolAndHostnameForWebSocketUrl()
    {
        var html = DevRequestHandler.InjectLivereload("<html><head></head><body></body></html>");

        Assert.Contains("location.protocol === 'https:' ? 'wss://' : 'ws://'", html, StringComparison.Ordinal);
        Assert.Contains("location.hostname", html, StringComparison.Ordinal);
        Assert.Contains("const port = location.port ? ':' + location.port : '';", html, StringComparison.Ordinal);
        Assert.DoesNotContain(".split(':')", html, StringComparison.Ordinal);
        Assert.DoesNotContain("'ws://'+", html, StringComparison.Ordinal);
    }

    [Fact]
    public void DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket()
    {
        var html = DevRequestHandler.InjectLivereload("<html><head></head><body></body></html>");

        Assert.Contains("const protocol = location.protocol === 'https:' ? 'wss://' : 'ws://';", html, StringComparison.Ordinal);
        Assert.Contains("const host = location.hostname || 'localhost';", html, StringComparison.Ordinal);
        Assert.Contains("const socketHost = host.indexOf(':') >= 0 ? '[' + host + ']' : host;", html, StringComparison.Ordinal);
        Assert.Contains("const port = location.port ? ':' + location.port : '';", html, StringComparison.Ordinal);
        Assert.Contains("var s=new WebSocket(protocol+socketHost+port+'/__ws__');", html, StringComparison.Ordinal);
        Assert.DoesNotContain("ws://localhost", html, StringComparison.Ordinal);
        Assert.DoesNotContain("'ws://' +", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".ico", "image/x-icon")]
    [InlineData(".avif", "image/avif")]
    [InlineData(".webmanifest", "application/manifest+json; charset=utf-8")]
    [InlineData(".woff", "font/woff")]
    [InlineData(".woff2", "font/woff2")]
    [InlineData(".map", "application/json; charset=utf-8")]
    [InlineData(".pdf", "application/pdf")]
    public void ResolveMimeType_CoversCommonDevAssets(string extension, string expected)
    {
        Assert.Equal(expected, ResolveMimeType(extension));
    }

    [Fact]
    public async Task DevRequestHandler_HandleAsync_ServesHtmlWithLivereload()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), """
            <html><head>
              <script async src="https://www.googletagmanager.com/gtag/js?id=G-123"></script>
              <script>gtag('config', 'G-123');</script>
            </head><body>ok</body></html>
            """);

        try
        {
            var original = """
                <html><head>
                  <!-- bukit:analytics:google-analytics:G-12345:head:start -->
                  <script>managed analytics</script>
                  <!-- bukit:analytics:google-analytics:G-12345:head:end -->
                </head><body>ok</body></html>
                """;
            File.WriteAllText(Path.Combine(outputDir, "index.html"), original);
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: true, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                "/",
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ok", response.Body, StringComparison.Ordinal);
            Assert.Contains("new WebSocket", response.Body, StringComparison.Ordinal);
            Assert.DoesNotContain("managed analytics", response.Body, StringComparison.Ordinal);
            Assert.Equal(original, File.ReadAllText(Path.Combine(outputDir, "index.html")));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DevRequestHandler_HandleAsync_PreservesUtf8BomWhenInjectingLiveReload()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-bom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        var original = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(System.Text.Encoding.UTF8.GetBytes("<html><head></head><body>ok</body></html>"))
            .ToArray();
        File.WriteAllBytes(Path.Combine(outputDir, "index.html"), original);

        try
        {
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRawRequestAsync(
                "/",
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, response.Body[..3]);
            Assert.Contains("new WebSocket", System.Text.Encoding.UTF8.GetString(response.Body), StringComparison.Ordinal);
            Assert.Equal(response.Body.Length, response.ContentLength);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DevRequestHandler_HandleAsync_RejectsInvalidUtf8InsteadOfReplacingBytes()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-invalid-utf8-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        var payload = "<html><body>caf"u8.ToArray()
            .Concat(new byte[] { 0xE9 })
            .Concat("</body></html>"u8.ToArray())
            .ToArray();
        File.WriteAllBytes(Path.Combine(outputDir, "index.html"), payload);

        try
        {
            var logger = new BufferingLogger();
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, logger);
            var response = await ProcessSingleRawRequestAsync(
                "/",
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Empty(response.Body);
            Assert.Contains(logger.Warnings, warning => warning.Contains("valid UTF-8", StringComparison.Ordinal));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DevRequestHandler_HandleAsync_ReturnsNotFoundForMissingFile()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                "/missing",
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("/.bukit/security-report.json", ".bukit", "security-report.json")]
    [InlineData("/.bukit-build-state.json", null, ".bukit-build-state.json")]
    [InlineData("/.bukit-output-marker", null, ".bukit-output-marker")]
    public async Task DevRequestHandler_HandleAsync_DoesNotServeBukitInternalFiles(string requestPath, string? subdir, string fileName)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-internal-" + Guid.NewGuid().ToString("N"));
        var fileDir = subdir is null ? outputDir : Path.Combine(outputDir, subdir);
        Directory.CreateDirectory(fileDir);
        File.WriteAllText(Path.Combine(fileDir, fileName), """{"secret":"internal"}""");

        try
        {
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                requestPath,
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("internal", response.Body, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("/%252e%252e/")]
    [InlineData("/%5c..%5csecret")]
    [InlineData("/%00")]
    public async Task DevRequestHandler_RejectsEncodedDotDotPath(string path)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-traversal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "index.html"), "<html><body>ok</body></html>");

        try
        {
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                path,
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain(outputDir, response.Body, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DevRequestHandler_RejectsBackslashTraversal()
    {
        await AssertDevTraversalRejectedAsync("/%5c..%5csecret");
    }

    [Fact]
    public async Task DevRequestHandler_RejectsNullByteEncodedPath()
    {
        await AssertDevTraversalRejectedAsync("/%00");
    }

    [Fact]
    public async Task DevRequestHandler_HandlesVeryLongPath()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-long-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            var longPath = "/" + new string('a', 1024);
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                longPath,
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(outputDir, response.Body, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task DevRequestHandler_DoesNotInjectLiveReloadIntoNonHtml()
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-css-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(Path.Combine(outputDir, "site.css"), "body{color:red;}");

        try
        {
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                "/site.css",
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("body{color:red;}", response.Body);
            Assert.DoesNotContain("WebSocket", response.Body, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("/%2e%2e/")]
    [InlineData("/%252e%252e/")]
    [InlineData("/%5c..%5csecret")]
    [InlineData("/%EF%BC%8E%EF%BC%8E/secret")]
    public void DevPathGuard_RejectsUnicodeAndEncodedTraversal(string path)
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-dev-path-guard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Assert.Null(DevPathGuard.TryResolveWithinRoot(root, path));
        }
        finally
        {
            TestCleanup.DeleteDirectory(root, recursive: true);
        }
    }

    [Fact]
    public async Task DevServerHost_RunAcceptLoopAsync_DispatchesIncomingRequest()
    {
        using var logger = new BufferingLogger();
        using var host = DevServerHost.Start("localhost", 0, logger);
        using var cts = new CancellationTokenSource();
        var requestHandled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var loopTask = host.RunAcceptLoopAsync(context =>
        {
            context.Response.StatusCode = 204;
            context.Response.Close();
            requestHandled.TrySetResult(true);
            return Task.CompletedTask;
        }, cts.Token);

        using var client = new HttpClient();
        using var response = await client.GetAsync(host.Prefix);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await requestHandled.Task;

        cts.Cancel();
        await loopTask;
    }

    [Fact]
    public async Task DevServerHost_RunAcceptLoopAsync_CancellationWaitsForActiveDispatch()
    {
        using var logger = new BufferingLogger();
        using var host = DevServerHost.Start("localhost", 0, logger);
        using var cts = new CancellationTokenSource();
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var loopTask = host.RunAcceptLoopAsync(async context =>
        {
            entered.TrySetResult(true);
            await release.Task;
            context.Response.StatusCode = 204;
            context.Response.Close();
        }, cts.Token);

        using var client = new HttpClient();
        var responseTask = client.GetAsync(host.Prefix);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cts.Cancel();
        Assert.NotSame(loopTask, await Task.WhenAny(loopTask, Task.Delay(100)));

        release.TrySetResult(true);
        await loopTask.WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DevServerHost_Dispose_WaitsForDispatchBeforeDisposingRequestGate()
    {
        using var logger = new BufferingLogger();
        var host = DevServerHost.Start("localhost", 0, logger);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var loopTask = host.RunAcceptLoopAsync(async context =>
        {
            entered.TrySetResult(true);
            await release.Task;
            context.Response.StatusCode = 204;
            context.Response.Close();
        }, CancellationToken.None);

        using var client = new HttpClient();
        var responseTask = client.GetAsync(host.Prefix);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var disposeTask = Task.Run(host.Dispose);
        Assert.NotSame(disposeTask, await Task.WhenAny(disposeTask, Task.Delay(100)));

        release.TrySetResult(true);
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(5));
        await loopTask.WaitAsync(TimeSpan.FromSeconds(5));
        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task DevWebSocketHub_HandleUpgradeAsync_RejectsWhenConnectionLimitReached()
    {
        using var listener = StartListener(out var prefix, out var port);
        using var logger = new BufferingLogger();
        var hub = new DevWebSocketHub(logger, DevWebSocketAccessPolicy.Loopback(port), maxConnections: 0);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var contextTask = listener.GetContextAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, prefix);
        request.Headers.TryAddWithoutValidation("Origin", prefix.TrimEnd('/'));
        var responseTask = client.SendAsync(request);

        var context = await contextTask.WaitAsync(TimeSpan.FromSeconds(5));
        await hub.HandleUpgradeAsync(context, CancellationToken.None);

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal((HttpStatusCode)429, response.StatusCode);
        Assert.Contains(logger.Warnings, warning => warning.Contains("too many dev WebSocket clients", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DevWebSocketHub_HandleUpgradeAsync_RejectsMissingOrigin()
    {
        using var listener = StartListener(out var prefix, out var port);
        using var logger = new BufferingLogger();
        var hub = new DevWebSocketHub(logger, DevWebSocketAccessPolicy.Loopback(port), maxConnections: 1);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var contextTask = listener.GetContextAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, prefix);
        var responseTask = client.SendAsync(request);

        var context = await contextTask.WaitAsync(TimeSpan.FromSeconds(5));
        await hub.HandleUpgradeAsync(context, CancellationToken.None);

        using var response = await responseTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(logger.Warnings, warning => warning.Contains("missing or invalid Origin header", StringComparison.Ordinal));
    }

    private static AppConfig MinimalConfig()
        => new()
        {
            Site = new SiteConfig { Name = "test", Title = "Test" },
            Content = new ContentConfig
            {
                Sources =
                [
                    new ContentSourceConfig
                    {
                        Type = "markdown",
                        Name = "page",
                        Collection = "page",
                        Markdown = new MarkdownConfig()
                    }
                ]
            }
        };

    private static string ResolveMimeType(string extension)
    {
        var method = typeof(DevRequestHandler).GetMethod(
            "ResolveMimeType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [extension])!;
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> ProcessSingleRequestAsync(
        string path,
        Func<HttpListenerContext, CancellationToken, Task> handleAsync)
    {
        var response = await ProcessSingleRawRequestAsync(path, handleAsync);
        return (response.StatusCode, System.Text.Encoding.UTF8.GetString(response.Body));
    }

    private static async Task<(HttpStatusCode StatusCode, byte[] Body, long? ContentLength)> ProcessSingleRawRequestAsync(
        string path,
        Func<HttpListenerContext, CancellationToken, Task> handleAsync)
    {
        using var listener = StartListener(out var prefix, out _);
        using var client = new HttpClient();
        var contextTask = listener.GetContextAsync();
        var responseTask = client.GetAsync(new Uri(new Uri(prefix), path));

        var context = await contextTask;
        await handleAsync(context, CancellationToken.None);

        using var response = await responseTask;
        var body = await response.Content.ReadAsByteArrayAsync();
        return (response.StatusCode, body, response.Content.Headers.ContentLength);
    }

    private static Task InvokeScheduleRebuildAsync(DevFileWatcher watcher, string file)
    {
        var method = typeof(DevFileWatcher).GetMethod(
            "ScheduleRebuildAsync",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        var task = (Task)method!.Invoke(watcher, [file])!;
        return task;
    }

    private static async Task AssertDevTraversalRejectedAsync(string path)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "bukit-dev-handler-reject-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDir);

        try
        {
            var handler = new DevRequestHandler(outputDir, removeManagedAnalytics: false, new TestLogger());
            var response = await ProcessSingleRequestAsync(
                path,
                (context, ct) => handler.HandleAsync(context, ct));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain(outputDir, response.Body, StringComparison.Ordinal);
        }
        finally
        {
            TestCleanup.DeleteDirectory(outputDir, recursive: true);
        }
    }

    private static HttpListener StartListener(out string prefix, out int port)
    {
        using var tcp = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        tcp.Start();
        port = ((System.Net.IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();

        prefix = $"http://localhost:{port}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();
        return listener;
    }

    private class TestLogger : ILogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }

    private sealed class BufferingLogger : ILogger, IDisposable
    {
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];

        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) => Errors.Add(message);

        public void Dispose()
        {
        }
    }
}
