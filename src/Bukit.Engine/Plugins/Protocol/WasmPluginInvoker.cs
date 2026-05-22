// DESKTOP-REMOVED: WasmPluginInvoker disabled (AOT-only, wasm runtime not supported).
// Only ProcessPluginInvoker is active for external protocol plugins.
#if false
using Bukit.Config;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

#if !AOT
using Wasmtime;
using WasmtimeEngine = Wasmtime.Engine;
#endif

namespace Bukit.Engine.Plugins.Protocol;

#if AOT
// In AOT builds we intentionally do not reference Wasmtime (see Bukit.Engine.csproj).
// This stub keeps the type available so the project compiles, while ensuring runtime
// behavior is explicit and actionable.
internal sealed class WasmPluginInvoker : IProtocolPluginInvoker
{
    public Task<ProtocolPluginInvocationResult> InvokeAsync(
        ExternalPluginConfig plugin,
        string requestJson,
        string? arguments,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        var entry = plugin.Entry ?? string.Empty;
        return Task.FromResult(new ProtocolPluginInvocationResult(
            -1,
            string.Empty,
            $"[plugin-policy] protocol plugin runtime 'wasm' is not supported in AOT build. Use 'process' runtime. entry={entry}",
            true,
            started.ElapsedMilliseconds));
    }
}
#else
internal sealed class WasmPluginInvoker : IProtocolPluginInvoker
{
    public async Task<ProtocolPluginInvocationResult> InvokeAsync(
        ExternalPluginConfig plugin,
        string requestJson,
        string? arguments,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.StartNew();
        var timeout = plugin.TimeoutMs > 0 ? plugin.TimeoutMs : 5000;

        try
        {
            var task = Task.Run(() => InvokeCore(plugin, requestJson, arguments), CancellationToken.None);
            var result = await task.WaitAsync(TimeSpan.FromMilliseconds(timeout), cancellationToken);
            return result;
        }
        catch (TimeoutException)
        {
            return new ProtocolPluginInvocationResult(
                -1,
                string.Empty,
                $"[plugin-timeout] protocol wasm plugin timed out after {timeout}ms. entry={plugin.Entry}",
                true,
                started.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new ProtocolPluginInvocationResult(
                -1,
                string.Empty,
                "[plugin-canceled] protocol wasm plugin invocation was canceled.",
                true,
                started.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            return new ProtocolPluginInvocationResult(
                -1,
                string.Empty,
                $"[plugin-runtime] {ex.Message}",
                false,
                started.ElapsedMilliseconds);
        }
    }

    private static ProtocolPluginInvocationResult InvokeCore(ExternalPluginConfig plugin, string requestJson, string? arguments)
    {
        var started = Stopwatch.StartNew();

        try
        {
            if (plugin.WasmAllowNetwork)
            {
                return new ProtocolPluginInvocationResult(
                    -1,
                    string.Empty,
                    $"[plugin-policy] wasm plugin '{plugin.Entry}' requested network access but wasmAllowNetwork=true is not allowed.",
                    false,
                    started.ElapsedMilliseconds);
            }

            var entryPath = ResolveEntryPath(plugin.Entry);
            var ioTempDir = CreateIoTempDir();
            var stdinPath = Path.Combine(ioTempDir, "stdin.json");
            var stdoutPath = Path.Combine(ioTempDir, "stdout.txt");
            var stderrPath = Path.Combine(ioTempDir, "stderr.txt");
            File.WriteAllText(stdinPath, requestJson, Encoding.UTF8);
            try
            {
                using (var engine = new WasmtimeEngine())
                using (var module = LoadModule(engine, entryPath))
                using (var linker = new Linker(engine))
                using (var store = new Store(engine))
                {
                    ApplyMemoryLimit(plugin, store);
                    linker.DefineWasi();

                    var wasiConfig = new WasiConfiguration()
                        .WithArgs(BuildArgs(entryPath, arguments))
                        .WithStandardInput(stdinPath)
                        .WithStandardOutput(stdoutPath)
                        .WithStandardError(stderrPath);
                    ApplyFilesystemPolicy(plugin, requestJson, wasiConfig);
                    store.SetWasiConfiguration(wasiConfig);

                    var instance = linker.Instantiate(store, module);
                    var run = instance.GetAction("_start") ?? instance.GetAction("run");
                    if (run is null)
                    {
                        return new ProtocolPluginInvocationResult(
                            -1,
                            string.Empty,
                            $"[plugin-init] wasm module '{entryPath}' must export '_start' or 'run'.",
                            false,
                            started.ElapsedMilliseconds);
                    }

                    run();
                }

                var stdoutText = ReadTextIfExists(stdoutPath);
                var stderrText = ReadTextIfExists(stderrPath);
                return new ProtocolPluginInvocationResult(0, stdoutText, stderrText, false, started.ElapsedMilliseconds);
            }
            finally
            {
                if (Directory.Exists(ioTempDir))
                {
                    Directory.Delete(ioTempDir, true);
                }
            }
        }
        catch (Exception ex)
        {
            return new ProtocolPluginInvocationResult(-1, string.Empty, $"[plugin-runtime] {ex.Message}", false, started.ElapsedMilliseconds);
        }
    }

    private static string CreateIoTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"bukit-wasm-io-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string ReadTextIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
    }

    private static void ApplyFilesystemPolicy(ExternalPluginConfig plugin, string requestJson, WasiConfiguration wasiConfig)
    {
        var fsMode = (plugin.WasmFsMode ?? "output-only").Trim().ToLowerInvariant();
        if (fsMode == "none")
        {
            return;
        }

        if (fsMode == "output-only")
        {
            var outputDir = ResolveOutputDir(requestJson);
            if (!string.IsNullOrWhiteSpace(outputDir) && Directory.Exists(outputDir))
            {
                wasiConfig.WithPreopenedDirectory(
                    outputDir,
                    "/out",
                    WasiDirectoryPermissions.Write,
                    WasiFilePermissions.Write);
            }
        }
    }

    private static string? ResolveOutputDir(string requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(requestJson);
        if (!document.RootElement.TryGetProperty("hook", out var hookNode))
        {
            return null;
        }

        var hook = hookNode.GetString();

        if (string.Equals(hook, "after-build", StringComparison.OrdinalIgnoreCase))
        {
            if (document.RootElement.TryGetProperty("afterBuild", out var afterBuild) &&
                afterBuild.TryGetProperty("outputDir", out var outputDirNode))
            {
                var outputDir = outputDirNode.GetString();
                return string.IsNullOrWhiteSpace(outputDir) ? null : Path.GetFullPath(outputDir);
            }

            return null;
        }

        return null;
    }

    private static void ApplyMemoryLimit(ExternalPluginConfig plugin, Store store)
    {
        var maxMemoryMb = plugin.MaxMemoryMb > 0 ? plugin.MaxMemoryMb : 64;
        var memorySizeBytes = checked((long)maxMemoryMb * 1024L * 1024L);
        store.SetLimits(memorySizeBytes, null, null, null, null);
    }

    private static string ResolveEntryPath(string entry)
    {
        if (string.IsNullOrWhiteSpace(entry))
        {
            throw new InvalidOperationException("WASM plugin entry is required.");
        }

        return Path.GetFullPath(entry);
    }

    private static Module LoadModule(WasmtimeEngine engine, string path)
    {
        var extension = Path.GetExtension(path);
        if (string.Equals(extension, ".wat", StringComparison.OrdinalIgnoreCase))
        {
            var text = File.ReadAllText(path);
            return Module.FromText(engine, Path.GetFileName(path), text);
        }

        return Module.FromFile(engine, path);
    }

    private static IReadOnlyList<string> BuildArgs(string entryPath, string? arguments)
    {
        var args = new List<string> { Path.GetFileName(entryPath) };
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return args;
        }

        foreach (var token in TokenizeArguments(arguments))
        {
            args.Add(token);
        }

        return args;
    }

    private static IReadOnlyList<string> TokenizeArguments(string value)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }
}
#endif
#endif
