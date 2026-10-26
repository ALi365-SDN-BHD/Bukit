using System.Text.Json;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Rendering;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal static class I18nWorktreeProtocol
{
    internal const string ContentSchema = "i18n-content-snapshot.v1";
    internal const string RequestSchema = "i18n-worker-request.v1";
    internal const string ResultSchema = "i18n-variant-result.v1";

    internal static IReadOnlyList<string> ResolveThemeInputPaths(
        string rootDir,
        ThemeConfig theme,
        ILogger logger)
    {
        var resolved = ThemePathResolver.Resolve(rootDir, theme, logger);
        var paths = new List<string>(6);
        if (!string.IsNullOrWhiteSpace(theme.Name))
        {
            paths.Add(resolved.ThemeRoot);
        }

        AddIfPresent(resolved.ParentThemeRoot);
        AddIfPresent(resolved.LayoutsDir);
        AddIfPresent(resolved.AssetsDir);
        AddIfPresent(resolved.StaticDir);
        AddIfPresent(resolved.UserLayoutsDir);
        return paths;

        void AddIfPresent(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }
    }

    internal static string ComputeConfigHash(AppConfig config, ConfigOverrides overrides)
    {
        var identity = new I18nConfigIdentity(
            config,
            overrides.ExecutionMode,
            overrides.IsCI,
            overrides.Incremental,
            overrides.Jobs);
        return HashUtil.Sha256Hex(JsonSerializer.SerializeToUtf8Bytes(
            identity,
            I18nWorktreeJsonContext.Default.I18nConfigIdentity));
    }

    internal static async Task<I18nContentSnapshot> CaptureContentAsync(
        string head,
        DateTimeOffset buildStartedAt,
        ContentPipelineResult content,
        string sourceRoot,
        string identityRoot,
        CancellationToken cancellationToken)
    {
        var bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal);
        foreach (var document in content.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.Body.Html is null && document.Body.BodyKey is { Length: > 0 } key)
            {
                bodies[MapPathIdentity(key, sourceRoot, identityRoot)] =
                    await content.BodyStore.GetAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }

        var documents = content.Documents
            .Select(document => CanonicalizeDocumentPaths(document, sourceRoot, identityRoot))
            .ToArray();

        return new I18nContentSnapshot(
            ContentSchema,
            head,
            buildStartedAt,
            documents,
            bodies,
            new Dictionary<string, I18nBodyFileSnapshot>(StringComparer.Ordinal),
            content.SchemaErrors,
            content.BodyCacheMetrics);
    }

    private static ContentDocument CanonicalizeDocumentPaths(
        ContentDocument document,
        string sourceRoot,
        string identityRoot)
    {
        IReadOnlyDictionary<string, ContentField>? fields = document.CustomFields;
        if (fields is not null && fields.TryGetValue("sourcePath", out var sourcePathField) &&
            sourcePathField.Value is string sourcePath)
        {
            var mapped = MapPathIdentity(sourcePath, sourceRoot, identityRoot);
            if (!string.Equals(mapped, sourcePath, StringComparison.Ordinal))
            {
                var updated = new Dictionary<string, ContentField>(fields, StringComparer.OrdinalIgnoreCase)
                {
                    ["sourcePath"] = sourcePathField with { Value = mapped }
                };
                fields = updated;
            }
        }

        return document with
        {
            Body = document.Body with
            {
                BodyKey = document.Body.BodyKey is { Length: > 0 } key
                    ? MapPathIdentity(key, sourceRoot, identityRoot)
                    : document.Body.BodyKey
            },
            Source = document.Source with
            {
                SourcePath = document.Source.SourcePath is { Length: > 0 } path
                    ? MapPathIdentity(path, sourceRoot, identityRoot)
                    : document.Source.SourcePath
            },
            CustomFields = fields
        };
    }

    private static string MapPathIdentity(string path, string sourceRoot, string identityRoot)
    {
        if (!Path.IsPathRooted(path))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        return PathUtils.IsSameOrSubPathOf(fullPath, sourceRoot)
            ? Path.GetFullPath(Path.Combine(identityRoot, Path.GetRelativePath(sourceRoot, fullPath)))
            : path;
    }

    internal static async Task<I18nVariantSnapshot> CaptureVariantAsync(
        string head,
        string configHash,
        string contentHash,
        BuildVariantResult result,
        int warningCount,
        int errorCount,
        CancellationToken cancellationToken)
    {
        var bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal);
        var documents = result.RoutedDocuments.Select(x => x.Document)
            .Concat(result.DerivedDocuments.Select(x => x.Document))
            .Concat(result.ContentGraph?.Documents ?? [])
            .DistinctBy(x => (x.Id, x.Body.BodyKey));
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.Body.Html is null && document.Body.BodyKey is { Length: > 0 } key)
            {
                bodies[key] = await result.BodyStore.GetAsync(document, cancellationToken).ConfigureAwait(false);
            }
        }

        var pending = result.PendingManifest
            ?? throw new InvalidOperationException("Worker variant did not produce an incremental manifest.");
        return new I18nVariantSnapshot(
            ResultSchema,
            head,
            configHash,
            contentHash,
            result.Language,
            Completed: true,
            result.OutputDir,
            result.BaseUrl,
            result.SearchSnippetsEnabled,
            bodies,
            [.. result.DerivedRoutes.Select(x => new I18nDerivedRouteSnapshot(x.Route, x.LastModified))],
            result.SeoIndex,
            result.SeoModels,
            result.PluginExecutions,
            result.RenderedCount,
            result.SkippedCount,
            result.RenderReasons,
            result.StageMetrics,
            result.RoutedDocuments,
            result.ListRouteGraph?.Routes ?? [],
            result.DerivedDocuments,
            result.ProjectionResults,
            result.StaticRoutes,
            result.PluginOutputs,
            FromManifest(pending.Manifest),
            pending.TemplateHash,
            pending.IncrementalEnabled,
            [.. result.PlannedOutputs.Select(x => new I18nAssetOutputSnapshot(x.Source, x.Destination, x.Category, x.Operation))],
            warningCount,
            errorCount);
    }

    internal static BuildVariantResult RestoreVariant(
        I18nVariantSnapshot snapshot,
        CanonicalContentGraph contentGraph,
        string expectedHead,
        string expectedConfigHash,
        string expectedContentHash,
        string expectedLanguage,
        string expectedOutputDir,
        string manifestPath)
    {
        if (snapshot.Schema != ResultSchema || !snapshot.Completed ||
            !string.Equals(snapshot.Head, expectedHead, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ConfigHash, expectedConfigHash, StringComparison.Ordinal) ||
            !string.Equals(snapshot.ContentHash, expectedContentHash, StringComparison.Ordinal) ||
            !string.Equals(snapshot.Language, expectedLanguage, StringComparison.Ordinal) ||
            !PathEquals(snapshot.OutputDir, expectedOutputDir))
        {
            throw new InvalidDataException("Worker result identity or protocol version is invalid.");
        }

        ValidateOutputPaths(expectedOutputDir, snapshot);
        var bodyStore = new DictionaryContentBodyStore(snapshot.Bodies);
        return new BuildVariantResult(
            snapshot.Language,
            Path.GetFullPath(expectedOutputDir),
            snapshot.BaseUrl,
            snapshot.SearchSnippetsEnabled,
            bodyStore,
            [.. snapshot.DerivedRoutes.Select(x => (x.Route, x.LastModified))],
            snapshot.SeoIndex,
            snapshot.SeoModels,
            snapshot.PluginExecutions,
            snapshot.RenderedCount,
            snapshot.SkippedCount,
            snapshot.RenderReasons,
            snapshot.StageMetrics,
            snapshot.RoutedDocuments,
            contentGraph,
            ListRouteGraph.Create(snapshot.ListRoutes),
            snapshot.DerivedDocuments,
            snapshot.ProjectionResults,
            snapshot.StaticRoutes,
            snapshot.PluginOutputs)
        {
            PendingManifest = new ManifestSetupResult(
                ToManifest(snapshot.Manifest),
                snapshot.TemplateHash,
                manifestPath,
                ManifestEntries: null,
                snapshot.IncrementalEnabled),
            PlannedOutputs = [.. snapshot.PlannedOutputs.Select(x => new AssetOutputItem(
                x.Source,
                x.Destination,
                x.Category,
                Operation: x.Operation))]
        };
    }

    private static I18nBuildManifestSnapshot FromManifest(BuildManifest manifest)
        => new(
            manifest.Version,
            manifest.OutputRoot,
            [.. manifest.OwnedOutputs.Order(StringComparer.Ordinal)],
            manifest.TemplateHash,
            manifest.Entries,
            manifest.Media,
            manifest.Assets,
            manifest.Static,
            manifest.PluginOutputs);

    private static BuildManifest ToManifest(I18nBuildManifestSnapshot snapshot)
        => new()
        {
            Version = snapshot.Version,
            OutputRoot = snapshot.OutputRoot,
            OwnedOutputs = snapshot.OwnedOutputs.ToHashSet(StringComparer.Ordinal),
            TemplateHash = snapshot.TemplateHash,
            Entries = new Dictionary<string, BuildManifestEntry>(snapshot.Entries, StringComparer.Ordinal),
            Media = new Dictionary<string, string>(snapshot.Media, StringComparer.Ordinal),
            Assets = new Dictionary<string, string>(snapshot.Assets, StringComparer.Ordinal),
            Static = new Dictionary<string, string>(snapshot.Static, StringComparer.Ordinal),
            PluginOutputs = new Dictionary<string, PluginOutputManifestEntry>(snapshot.PluginOutputs, StringComparer.Ordinal)
        };

    private static void ValidateOutputPaths(string outputDir, I18nVariantSnapshot snapshot)
    {
        foreach (var path in snapshot.PlannedOutputs.Select(x => x.Destination)
                     .Concat(snapshot.PluginOutputs.Select(x => x.Path))
                     .Concat(snapshot.RoutedDocuments.Select(x => x.Route.OutputPath))
                     .Concat(snapshot.ListRoutes.Select(x => x.OutputPath))
                     .Concat(snapshot.DerivedDocuments.Select(x => x.Route.OutputPath))
                     .Concat(snapshot.ProjectionResults.SelectMany(x => x.Outputs).Select(x => x.Path))
                     .Concat(snapshot.StaticRoutes.Select(x => x.OutputPath))
                     .Concat(snapshot.Manifest.OwnedOutputs)
                     .Concat(snapshot.Manifest.Entries.Values.Select(x => x.OutputPath))
                     .Concat(snapshot.Manifest.Media.Keys)
                     .Concat(snapshot.Manifest.Assets.Keys)
                     .Concat(snapshot.Manifest.Static.Keys)
                     .Concat(snapshot.Manifest.PluginOutputs.Values.Select(x => x.Path)))
        {
            _ = FileWriter.GetSafeFullPath(outputDir, path);
        }
    }

    private static bool PathEquals(string left, string right)
        => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), Shared.PlatformPathHelper.PathComparison);
}
