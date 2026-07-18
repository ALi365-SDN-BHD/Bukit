using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal enum AssetOutputCategory
{
    Static,
    Assets,
    Media,
    Tokens
}

internal sealed record AssetOutputItem(
    string Source,
    string Destination,
    AssetOutputCategory Category,
    string? PhysicalSourceRoot = null,
    DirectoryCopyOptions? CopyOptions = null);

internal sealed class AssetOutputPlan
{
    private AssetOutputPlan(IReadOnlyList<AssetOutputItem> items)
    {
        Items = items;
    }

    internal IReadOnlyList<AssetOutputItem> Items { get; }

    internal static AssetOutputPlan Create(
        AssetPipelineContext context,
        DirectoryCopyOptions copyOptions,
        ThemeTokens? tokens,
        CancellationToken cancellationToken = default)
    {
        var comparer = PathComparer;
        var effectiveItems = new Dictionary<(AssetOutputCategory Category, string Destination), AssetOutputItem>(
            new ItemKeyComparer(comparer));

        AddDirectoryItems(effectiveItems, context.ParentStaticDir, string.Empty, AssetOutputCategory.Static, copyOptions, cancellationToken);
        AddDirectoryItems(effectiveItems, context.StaticDir, string.Empty, AssetOutputCategory.Static, copyOptions, cancellationToken);
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
                AssetOutputCategory.Tokens);
        }

        var items = effectiveItems.Values
            .OrderBy(item => item.Destination, comparer)
            .ThenBy(item => item.Category)
            .ThenBy(item => item.Source, StringComparer.Ordinal)
            .ToArray();
        Validate(items, comparer);
        return new AssetOutputPlan(items);
    }

    internal IReadOnlyList<AssetOutputItem> ForCategory(AssetOutputCategory category)
        => Items.Where(item => item.Category == category).ToArray();

    private static void AddDirectoryItems(
        Dictionary<(AssetOutputCategory Category, string Destination), AssetOutputItem> items,
        string? sourceDir,
        string destinationPrefix,
        AssetOutputCategory category,
        DirectoryCopyOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var candidate in DirectoryCopy.EnumerateFilesForSync(sourceDir, options, cancellationToken))
        {
            var destination = BuildPathUtils.NormalizeRelPath(Path.Combine(destinationPrefix, candidate.RelativePath));
            items[(category, destination)] = new AssetOutputItem(
                candidate.SourcePath,
                destination,
                category,
                candidate.PhysicalSourceRoot,
                options);
        }
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

    private static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;

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
