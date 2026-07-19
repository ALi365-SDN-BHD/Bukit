using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal enum AssetOutputCategory
{
    Static,
    Assets,
    Media,
    Tokens,
    Render
}

internal enum AssetOutputOperation
{
    Copy,
    Render,
    Generate
}

internal sealed record AssetOutputItem(
    string Source,
    string Destination,
    AssetOutputCategory Category,
    string? PhysicalSourceRoot = null,
    DirectoryCopyOptions? CopyOptions = null,
    AssetOutputOperation Operation = AssetOutputOperation.Copy);

internal sealed class AssetOutputPlan
{
    private AssetOutputPlan(IReadOnlyList<AssetOutputItem> items, StringComparer destinationComparer)
    {
        Items = items;
        DestinationComparer = destinationComparer;
    }

    internal IReadOnlyList<AssetOutputItem> Items { get; }
    internal StringComparer DestinationComparer { get; }

    internal static AssetOutputPlan Create(
        AssetPipelineContext context,
        DirectoryCopyOptions copyOptions,
        ThemeTokens? tokens,
        CancellationToken cancellationToken = default)
    {
        var comparer = OutputDestinationIdentityComparer.ForOutputRoot(context.OutputDir);
        var effectiveItems = new Dictionary<(AssetOutputCategory Category, string Destination), AssetOutputItem>(
            new ItemKeyComparer(comparer));
        var renderedStaticCopyDestinations = BuildRenderedStaticCopyDestinations(context, comparer);

        AddDirectoryItems(
            effectiveItems,
            context.ParentStaticDir,
            string.Empty,
            AssetOutputCategory.Static,
            copyOptions,
            cancellationToken,
            renderedStaticCopyDestinations);
        AddDirectoryItems(
            effectiveItems,
            context.StaticDir,
            string.Empty,
            AssetOutputCategory.Static,
            copyOptions,
            cancellationToken,
            renderedStaticCopyDestinations);
        AddDirectoryItems(effectiveItems, context.ParentAssetsDir, "assets", AssetOutputCategory.Assets, copyOptions, cancellationToken);
        AddDirectoryItems(effectiveItems, context.AssetsDir, "assets", AssetOutputCategory.Assets, copyOptions, cancellationToken);

        var mediaOptions = new DirectoryCopyOptions
        {
            IgnoreDotPrefixedFiles = true,
            FollowSymlinks = false
        };
        AddDirectoryItems(effectiveItems, context.MediaDownloadDir, "assets/uploads", AssetOutputCategory.Media, mediaOptions, cancellationToken);

        if (tokens is not null)
        {
            var destination = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", "css", "theme-tokens.css"));
            effectiveItems[(AssetOutputCategory.Tokens, destination)] = new AssetOutputItem(
                context.ThemeRoot ?? "generated theme tokens",
                destination,
                AssetOutputCategory.Tokens,
                Operation: AssetOutputOperation.Generate);
        }

        if (context.RenderEntries is not null)
        {
            foreach (var entry in context.RenderEntries)
            {
                var destination = BuildPathUtils.NormalizeRelPath(entry.Route.OutputPath);
                var category = entry.Kind == RenderEntryKind.Static
                    ? AssetOutputCategory.Static
                    : AssetOutputCategory.Render;
                effectiveItems[(category, destination)] = new AssetOutputItem(
                    entry.SourcePath ?? $"{entry.Kind.ToString().ToLowerInvariant()} route {entry.Route.Url}",
                    destination,
                    category,
                    Operation: AssetOutputOperation.Render);
            }
        }

        var items = effectiveItems.Values
            .OrderBy(item => item.Destination, comparer)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ToArray();
        Validate(items, comparer);
        return new AssetOutputPlan(items, comparer);
    }

    internal IReadOnlyList<AssetOutputItem> ForCategory(AssetOutputCategory category)
        => Items.Where(item => item.Category == category).ToArray();

    internal IReadOnlyList<AssetOutputItem> ForCopyCategory(AssetOutputCategory category)
        => Items.Where(item => item.Category == category && item.Operation == AssetOutputOperation.Copy).ToArray();

    private static void AddDirectoryItems(
        Dictionary<(AssetOutputCategory Category, string Destination), AssetOutputItem> items,
        string? sourceDir,
        string destinationPrefix,
        AssetOutputCategory category,
        DirectoryCopyOptions options,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? excludedDestinations = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var candidate in DirectoryCopy.EnumerateFilesForSync(sourceDir, options, cancellationToken))
        {
            var destination = BuildPathUtils.NormalizeRelPath(Path.Combine(destinationPrefix, candidate.RelativePath));
            if (excludedDestinations?.Contains(destination) == true)
            {
                continue;
            }

            items[(category, destination)] = new AssetOutputItem(
                candidate.SourcePath,
                destination,
                category,
                candidate.PhysicalSourceRoot,
                options);
        }
    }

    private static IReadOnlySet<string>? BuildRenderedStaticCopyDestinations(
        AssetPipelineContext context,
        StringComparer comparer)
    {
        if (context.RenderEntries is null || string.IsNullOrWhiteSpace(context.StaticDir))
        {
            return null;
        }

        var destinations = new HashSet<string>(comparer);
        foreach (var entry in context.RenderEntries.Where(entry =>
                     entry.Kind == RenderEntryKind.Static && !string.IsNullOrWhiteSpace(entry.SourcePath)))
        {
            var relativePath = BuildPathUtils.NormalizeRelPath(
                Path.GetRelativePath(context.StaticDir, entry.SourcePath!));
            if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith("../", StringComparison.Ordinal))
            {
                continue;
            }

            destinations.Add(relativePath);
        }

        return destinations;
    }

    private static void Validate(IReadOnlyList<AssetOutputItem> items, StringComparer comparer)
    {
        var exactCollision = items
            .GroupBy(item => item.Destination, comparer)
            .Select(group => group.ToArray())
            .FirstOrDefault(group => group.Length > 1);
        if (exactCollision is not null)
        {
            ThrowExactCollision(exactCollision);
        }

        var byDestination = items.ToDictionary(item => item.Destination, comparer);
        foreach (var descendant in items)
        {
            var separator = descendant.Destination.IndexOf('/');
            while (separator >= 0)
            {
                var ancestorPath = descendant.Destination[..separator];
                if (byDestination.TryGetValue(ancestorPath, out var ancestor))
                {
                    ThrowStructuralCollision(ancestor, descendant);
                }

                separator = descendant.Destination.IndexOf('/', separator + 1);
            }
        }
    }

    private static void ThrowExactCollision(IReadOnlyList<AssetOutputItem> collision)
    {
        var destination = collision[0].Destination;
        var owners = string.Join("; ", collision.Select(Describe));
        throw new BukitException(
            $"Asset output collision at '{destination}': {owners}.",
            DiagnosticCode.BuildAssetOutputCollision);
    }

    private static void ThrowStructuralCollision(AssetOutputItem ancestor, AssetOutputItem descendant)
    {
        throw new BukitException(
            $"Asset output collision between file '{ancestor.Destination}' ({Describe(ancestor)}) " +
            $"and descendant '{descendant.Destination}' ({Describe(descendant)}).",
            DiagnosticCode.BuildAssetOutputCollision);
    }

    private static string Describe(AssetOutputItem item)
        => $"category={item.Category.ToString().ToLowerInvariant()} source={item.Source}";

    private sealed class ItemKeyComparer(StringComparer pathComparer)
        : IEqualityComparer<(AssetOutputCategory Category, string Destination)>
    {
        public bool Equals(
            (AssetOutputCategory Category, string Destination) x,
            (AssetOutputCategory Category, string Destination) y)
            => x.Category == y.Category && pathComparer.Equals(x.Destination, y.Destination);

        public int GetHashCode((AssetOutputCategory Category, string Destination) obj)
            => HashCode.Combine(obj.Category, pathComparer.GetHashCode(obj.Destination));
    }
}
