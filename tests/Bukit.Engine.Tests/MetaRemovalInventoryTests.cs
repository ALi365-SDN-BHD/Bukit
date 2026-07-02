using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MetaRemovalInventoryTests
{
    private static readonly Regex ForbiddenRuntimePattern = new(
        @"(\.Meta\b|\bMetaHelpers\b|page\.meta)",
        RegexOptions.Compiled);

    [Fact]
    public void RuntimeSources_ShouldNotUseLegacyMetaSurface_WhenVNextRemovalIsComplete()
    {
        var root = FindRepositoryRoot();
        var hits = EnumerateTrackedTextFiles(root)
            .SelectMany(path => FindForbiddenHits(root, path))
            .Where(hit => !IsAllowedHit(hit.RelativePath))
            .ToList();

        Assert.True(
            hits.Count == 0,
            "vNext Meta removal inventory found forbidden legacy Meta usages:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, hits.Take(80).Select(h => $"{h.RelativePath}:{h.LineNumber}: {h.Line.Trim()}")) +
            (hits.Count > 80 ? $"{Environment.NewLine}... and {hits.Count - 80} more" : string.Empty));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static IEnumerable<string> EnumerateTrackedTextFiles(string root)
    {
        var roots = new[] { "src", "tests", "guide" };
        foreach (var relativeRoot in roots)
        {
            var absoluteRoot = Path.Combine(root, relativeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                if (ShouldRead(path))
                {
                    yield return path;
                }
            }
        }
    }

    private static bool ShouldRead(string path)
    {
        var extension = Path.GetExtension(path);
        return extension is ".cs" or ".md" or ".scriban" or ".html" or ".json" or ".yaml" or ".yml";
    }

    private static IEnumerable<InventoryHit> FindForbiddenHits(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;
            if (ForbiddenRuntimePattern.IsMatch(line))
            {
                yield return new InventoryHit(relativePath, lineNumber, line);
            }
        }
    }

    private static bool IsAllowedHit(string relativePath)
    {
        if (relativePath == "tests/Bukit.Engine.Tests/MetaRemovalInventoryTests.cs")
        {
            return true;
        }

        if (relativePath.EndsWith(".csproj.lscache", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (relativePath.StartsWith("src/Bukit-Core/Bukit.Content/", StringComparison.Ordinal))
        {
            return true;
        }

        if (relativePath.StartsWith("src/Bukit-Core/Bukit.Engine/Normalization/", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private sealed record InventoryHit(string RelativePath, int LineNumber, string Line);
}
