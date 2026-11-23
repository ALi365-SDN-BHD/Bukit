namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Single source of truth for image variant widths and file names.
/// </summary>
internal static class ImageVariantPlan
{
    private static readonly string[] RasterSourceExtensions = [".jpg", ".jpeg", ".png"];

    /// <summary>
    /// Whether a local image may get resized variants. WebP sources resize to
    /// WebP, so they are skipped when a JPEG/PNG with the same stem sits next to
    /// them (that file's WebP variants use the same names), and when the name
    /// already has the variant shape <c>-{width}w.webp</c>: a JPEG/PNG WebP
    /// variant whose ownership record is lost must not become a new source.
    /// </summary>
    internal static bool IsResizableSource(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (RasterSourceExtensions.Contains(extension))
        {
            return true;
        }
        if (extension != ".webp" || TryParseVariantFileName(path, out _))
        {
            return false;
        }

        var stem = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, Path.GetFileNameWithoutExtension(path));
        return !RasterSourceExtensions.Any(raster => File.Exists(stem + raster));
    }

    internal static bool IsWebpSource(string path)
        => Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase);

    internal static IReadOnlyList<int> GeneratedWidths(IReadOnlyList<int> sizes, int sourceWidth)
        => [.. sizes.Where(size => size < sourceWidth).Distinct().Order()];

    internal static IReadOnlyList<int> CandidateWidths(IReadOnlyList<int> sizes, int sourceWidth)
        => [.. sizes.Where(size => size < sourceWidth).Append(sourceWidth).Distinct().Order()];

    internal static string VariantFileName(string source, int width, string extension)
    {
        var directory = Path.GetDirectoryName(source);
        var fileName = $"{Path.GetFileNameWithoutExtension(source)}-{width}w{extension}";
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
    }

    // URL paths must keep '/' on every OS and must not let a query/fragment slash
    // become part of the file name.
    internal static string VariantUrl(string sourceUrl, int width)
    {
        var suffixAt = sourceUrl.IndexOfAny(['?', '#']);
        var suffix = suffixAt < 0 ? string.Empty : sourceUrl[suffixAt..];
        var path = suffixAt < 0 ? sourceUrl : sourceUrl[..suffixAt];
        var extensionAt = path.LastIndexOf('.');
        return $"{path[..extensionAt]}-{width}w{path[extensionAt..]}{suffix}";
    }

    internal static bool TryParseVariantFileName(string path, out int width)
    {
        width = 0;
        var stem = Path.GetFileNameWithoutExtension(path);
        var suffixStart = stem.LastIndexOf('-');
        if (suffixStart <= 0 || suffixStart >= stem.Length - 1)
        {
            return false;
        }

        var suffix = stem[(suffixStart + 1)..];
        return suffix.Length > 1 &&
               suffix[^1] == 'w' &&
               int.TryParse(suffix.AsSpan(0, suffix.Length - 1), out width) &&
               width > 0;
    }
}
