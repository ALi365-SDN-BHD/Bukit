namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Single source of truth for image variant widths and file names.
/// </summary>
internal static class ImageVariantPlan
{
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
