using System.Text;

namespace Bukit.Labs.Cli.Commands;

internal sealed record ScreenshotComparison(string Name, string Status, int ComparedPixels, int MismatchedPixels, double DiffRatio, int TargetWidth, int TargetHeight, int LocalWidth, int LocalHeight, int? MismatchMinX, int? MismatchMinY, int? MismatchMaxX, int? MismatchMaxY)
{
    public bool HasMismatchBounds => MismatchMinX is not null && MismatchMinY is not null && MismatchMaxX is not null && MismatchMaxY is not null;
}

internal sealed record MissingScreenshotPair(string Viewport, string TargetPath, string LocalPath, bool TargetExists, bool LocalExists);

internal sealed record AffectedSection(
    string Screenshot,
    string Viewport,
    int SectionIndex,
    string SectionKey,
    string? SectionId,
    string? SectionType,
    int? SectionOrder,
    string SectionLabel,
    string DataPath,
    string SpecPath,
    double SectionY,
    double SectionHeight,
    int MismatchMinY,
    int MismatchMaxY);

internal sealed record VisualVerifyResult(int Comparisons, int FailedComparisons, int MissingScreenshots)
{
    public bool HasFailures => FailedComparisons > 0;
}

internal static class CloneScreenshotComparer
{
    internal static IEnumerable<ScreenshotComparison> CompareScreenshotFiles(string targetDir, string localDir)
    {
        if (!Directory.Exists(targetDir) || !Directory.Exists(localDir))
            yield break;

        foreach (var target in Directory.EnumerateFiles(targetDir, "*.png"))
        {
            var targetName = Path.GetFileName(target);
            var localName = targetName.StartsWith("target-", StringComparison.OrdinalIgnoreCase)
                ? "local-" + targetName["target-".Length..]
                : targetName;
            var local = Path.Combine(localDir, localName);
            if (!File.Exists(local))
                continue;

            yield return ComparePngScreenshots(targetName, target, local);
        }
    }

    internal static IEnumerable<MissingScreenshotPair> FindMissingScreenshotPairs(string targetDir, string localDir)
    {
        foreach (var viewport in new[] { "1440", "768", "390" })
        {
            var target = Path.Combine(targetDir, $"target-{viewport}.png");
            var local = Path.Combine(localDir, $"local-{viewport}.png");
            var targetExists = File.Exists(target);
            var localExists = File.Exists(local);
            if (!targetExists || !localExists)
                yield return new MissingScreenshotPair(viewport, target, local, targetExists, localExists);
        }
    }

    internal static ScreenshotComparison ComparePngScreenshots(string name, string targetPath, string localPath)
    {
        try
        {
            var target = PngImage.Read(targetPath);
            var local = PngImage.Read(localPath);
            var width = Math.Min(target.Width, local.Width);
            var height = Math.Min(target.Height, local.Height);
            var compared = width * height;
            var mismatched = 0;
            int? minX = null;
            int? minY = null;
            int? maxX = null;
            int? maxY = null;

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var ti = ((y * target.Width) + x) * 4;
                    var li = ((y * local.Width) + x) * 4;
                    if (target.Pixels[ti] != local.Pixels[li] ||
                        target.Pixels[ti + 1] != local.Pixels[li + 1] ||
                        target.Pixels[ti + 2] != local.Pixels[li + 2] ||
                        target.Pixels[ti + 3] != local.Pixels[li + 3])
                    {
                        mismatched++;
                        minX = minX is null ? x : Math.Min(minX.Value, x);
                        minY = minY is null ? y : Math.Min(minY.Value, y);
                        maxX = maxX is null ? x : Math.Max(maxX.Value, x);
                        maxY = maxY is null ? y : Math.Max(maxY.Value, y);
                    }
                }
            }

            if (target.Width != local.Width || target.Height != local.Height)
            {
                mismatched += Math.Abs((target.Width * target.Height) - (local.Width * local.Height));
                minX ??= 0;
                minY ??= 0;
                maxX = Math.Max(target.Width, local.Width) - 1;
                maxY = Math.Max(target.Height, local.Height) - 1;
            }

            var total = Math.Max(target.Width * target.Height, local.Width * local.Height);
            var ratio = total == 0 ? 0 : (double)mismatched / total;
            var status = mismatched == 0 ? "identical" : "pixel-different";
            return new ScreenshotComparison(name, status, compared, mismatched, ratio, target.Width, target.Height, local.Width, local.Height, minX, minY, maxX, maxY);
        }
        catch (Exception ex)
        {
            var targetBytes = new FileInfo(targetPath).Length;
            var localBytes = new FileInfo(localPath).Length;
            var same = File.ReadAllBytes(targetPath).SequenceEqual(File.ReadAllBytes(localPath));
            return new ScreenshotComparison(name, same ? "identical-bytes" : $"unsupported-png: {ex.Message}", 0, same ? 0 : 1, same ? 0 : 1, (int)targetBytes, 0, (int)localBytes, 0, null, null, null, null);
        }
    }

    internal static IEnumerable<AffectedSection> FindAffectedSections(IReadOnlyList<ScreenshotComparison> comparisons, IReadOnlyList<CloneSectionInfo> sections, double visualThreshold)
    {
        foreach (var comparison in comparisons.Where(c => c.DiffRatio > visualThreshold && c.HasMismatchBounds))
        {
            var viewport = ExtractViewportName(comparison.Name);
            foreach (var item in sections.Select((section, index) => new { Section = section, Index = index, Bounds = ResolveSectionBounds(section, viewport) }))
            {
                var bounds = item.Bounds;
                if (bounds is null)
                    continue;
                var y = bounds.Y ?? 0;
                var height = bounds.Height ?? 0;
                if (!RangesOverlap(y, y + height, comparison.MismatchMinY!.Value, comparison.MismatchMaxY!.Value))
                    continue;

                yield return new AffectedSection(
                    Screenshot: comparison.Name,
                    Viewport: viewport,
                    SectionIndex: item.Index + 1,
                    SectionKey: CloneContentWriter.SectionDataKey(item.Section, item.Index),
                    SectionId: item.Section.Id,
                    SectionType: item.Section.Type ?? item.Section.Semantic,
                    SectionOrder: item.Section.Order,
                    SectionLabel: SectionLabel(item.Section),
                    DataPath: $"data/{CloneContentWriter.SectionDataKey(item.Section, item.Index)}.md",
                    SpecPath: $"docs/research/components/{CloneContentWriter.SectionSpecFileName(item.Section, item.Index)}",
                    SectionY: y,
                    SectionHeight: height,
                    MismatchMinY: comparison.MismatchMinY.Value,
                    MismatchMaxY: comparison.MismatchMaxY.Value);
            }
        }
    }

    internal static void AppendAffectedSections(StringBuilder sb, IReadOnlyList<AffectedSection> affectedSections, bool hasSections)
    {
        sb.AppendLine();
        sb.AppendLine("## Likely Affected Sections");
        if (!hasSections)
        {
            sb.AppendLine("- No sections metadata available. Pass `--sections sections.json` to map visual diffs back to extracted sections.");
            return;
        }

        if (affectedSections.Count == 0)
        {
            sb.AppendLine("- none inferred");
            return;
        }

        foreach (var group in affectedSections.GroupBy(a => a.Screenshot))
        {
            sb.AppendLine($"- {group.Key}: overlaps:");
            foreach (var item in group)
            {
                sb.AppendLine($"  - section {item.SectionIndex}: `{item.SectionLabel}` id=`{item.SectionId ?? ""}` type=`{item.SectionType ?? ""}` order=`{item.SectionOrder?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? ""}` y={item.SectionY:0} height={item.SectionHeight:0}");
                sb.AppendLine($"    data: `{item.DataPath}`");
                sb.AppendLine($"    spec: `{item.SpecPath}`");
            }
        }
    }

    internal static CloneBox? ResolveSectionBounds(CloneSectionInfo section, string viewport)
    {
        if (section.Responsive?.Viewports is { Count: > 0 })
        {
            if (section.Responsive.Viewports.TryGetValue(viewport, out var exact) && exact.Bounds is not null)
                return exact.Bounds;
            var alias = viewport switch
            {
                "1440" => "desktop",
                "768" => "tablet",
                "390" => "mobile",
                _ => null
            };
            if (alias is not null && section.Responsive.Viewports.TryGetValue(alias, out var named) && named.Bounds is not null)
                return named.Bounds;
        }

        return section.Bounds;
    }

    internal static string ExtractViewportName(string screenshotName)
    {
        var file = Path.GetFileNameWithoutExtension(screenshotName);
        if (file.StartsWith("target-", StringComparison.OrdinalIgnoreCase))
            return file["target-".Length..];
        if (file.StartsWith("local-", StringComparison.OrdinalIgnoreCase))
            return file["local-".Length..];
        return file;
    }

    internal static bool RangesOverlap(double aStart, double aEnd, double bStart, double bEnd)
        => aEnd >= bStart && bEnd >= aStart;

    internal static string SectionLabel(CloneSectionInfo section)
        => section.Id ?? section.Heading ?? section.Title ?? section.Type ?? section.Semantic ?? "section";
}
