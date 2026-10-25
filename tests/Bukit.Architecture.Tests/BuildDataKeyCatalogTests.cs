using System.Reflection;
using System.Text.RegularExpressions;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Plugins;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class BuildDataKeyCatalogTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void BuildDataKeys_CatalogNamesAreUniqueIgnoringCase()
    {
        var catalog = typeof(SiteEngine).Assembly.GetType("Bukit.Engine.BuildDataKeys", throwOnError: true)!;
        var names = catalog
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.FieldType.IsGenericType &&
                            field.FieldType.GetGenericTypeDefinition() == typeof(BuildDataKey<>))
            .Select(field => (string)field.FieldType.GetProperty(nameof(BuildDataKey<object>.Name))!.GetValue(field.GetValue(null))!)
            .ToArray();

        Assert.NotEmpty(names);
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void BuildContext_LegacyUntypedDataView_IsObsolete()
    {
        var property = typeof(BuildContext).GetProperty("Data", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property);
        Assert.NotNull(property!.GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void CoreSource_DoesNotAccessBuildContextDataByString()
    {
        var coreRoot = Path.Combine(RepoRoot, "src", "Bukit-Core");
        var forbidden = new Regex(
            @"\.Data(\[|\.(TryGetValue|ContainsKey|Remove|Add)\()|BuildContextDataKeys?\b|new BuildDataKey<",
            RegexOptions.CultureInvariant);
        var catalogPath = Path.Combine(coreRoot, "Bukit.Engine", "BuildDataKeys.cs");
        var violations = Directory
            .EnumerateFiles(coreRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path) && !string.Equals(path, catalogPath, StringComparison.Ordinal))
            .Where(path => !path.EndsWith(".Generated.cs", StringComparison.Ordinal))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => (path, line, index))
                .Where(item => forbidden.IsMatch(item.line) && !IsAllowedNonBuildContextAccess(item.line)))
            .Select(item => $"{Path.GetRelativePath(RepoRoot, item.path)}:{item.index + 1}: {item.line.Trim()}")
            .ToArray();

        Assert.Empty(violations);
    }

    private static bool IsAllowedNonBuildContextAccess(string line)
        => line.Contains("model.Data", StringComparison.Ordinal) ||
           line.Contains("def.Data", StringComparison.Ordinal) ||
           line.Contains("themeSectionDef.Data", StringComparison.Ordinal);

    private static bool IsBuildOutput(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
           path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "bukit-core.slnx")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
