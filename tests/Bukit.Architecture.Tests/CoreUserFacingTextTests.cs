using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class CoreUserFacingTextTests
{
    private static readonly (string Term, StringComparison Comparison)[] ForbiddenHmrTerms =
    [
        ("HMR", StringComparison.OrdinalIgnoreCase),
        ("Hot Module Replacement", StringComparison.OrdinalIgnoreCase)
    ];

    private static readonly (string Term, StringComparison Comparison)[] ForbiddenNonCoreCommandTerms =
    [
        ("bukit theme manifest", StringComparison.OrdinalIgnoreCase),
        ("bukit theme wizard", StringComparison.OrdinalIgnoreCase),
        ("bukit import", StringComparison.OrdinalIgnoreCase),
        ("bukit clone", StringComparison.OrdinalIgnoreCase),
        ("bukit webhook", StringComparison.OrdinalIgnoreCase),
        ("bukit plugin", StringComparison.OrdinalIgnoreCase),
        ("--allow-external-plugins", StringComparison.Ordinal)
    ];

    private static readonly HashSet<string> AllowedTextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".css",
        ".editorconfig",
        ".html",
        ".md",
        ".mdc",
        ".py",
        ".txt",
        ".csproj",
        ".props",
        ".targets",
        ".slnx",
        ".sln",
        ".sh",
        ".ps1",
        ".cmd",
        ".json",
        ".yaml",
        ".yml",
        ".xml",
        ".config",
        ".toml",
        ".ini",
        ".gitignore",
        ".dockerfile"
    };

    private static readonly string[] ScanRoots =
    [
        "src/Bukit.Cli",
        "src/Bukit.Config",
        "guide/user",
        "guide/dev",
        "guide/skills"
    ];

    [Fact]
    public void CoreUserFacingText_DoesNotReferenceNonCoreCommands()
    {
        var files = FindForbiddenMatches(ForbiddenNonCoreCommandTerms);

        Assert.Empty(files);
    }

    [Fact]
    public void CoreUserFacingText_UsesLiveReloadNotHmr()
    {
        var files = FindForbiddenMatches(ForbiddenHmrTerms);

        Assert.Empty(files);
    }

    private static string[] FindForbiddenMatches((string Term, StringComparison Comparison)[] forbiddenTerms)
    {
        var repoRoot = FindRepoRoot();
        return ScanRoots
            .Select(root => Path.Combine(repoRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => EnumerateTextFiles(root, repoRoot))
            .Concat(Directory.EnumerateFiles(repoRoot, "README*.md", SearchOption.TopDirectoryOnly))
            .Select(path => Path.GetFullPath(path))
            .SelectMany(path => FindForbiddenMatches(path, repoRoot, forbiddenTerms))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateTextFiles(string root, string repoRoot)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var directory in Directory.EnumerateDirectories(current))
            {
                if (!IsExcludedByRelativePath(directory, repoRoot) &&
                    !IsInBuildOutputDirectory(directory))
                {
                    pending.Push(directory);
                }
            }

            foreach (var file in Directory.EnumerateFiles(current))
            {
                if (!IsExcludedByRelativePath(file, repoRoot) &&
                    !IsInBuildOutputDirectory(file) &&
                    IsProbablyTextFile(file))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> FindForbiddenMatches(
        string filePath,
        string repoRoot,
        (string Term, StringComparison Comparison)[] forbiddenTerms)
    {
        string[] lines;
        try
        {
            if (IsBinary(filePath))
            {
                yield break;
            }

            lines = File.ReadAllLines(filePath);
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        var lineNumber = 1;
        foreach (var line in lines)
        {
            foreach (var (term, comparison) in forbiddenTerms)
            {
                if (line.Contains(term, comparison))
                {
                    yield return
                        $"{Path.GetRelativePath(repoRoot, filePath)}:{lineNumber}: {line.Trim()} ({term})";
                }
            }

            lineNumber++;
        }
    }

    private static bool IsExcludedByRelativePath(string path, string repoRoot)
    {
        var relativePath = Path.GetRelativePath(repoRoot, path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .ToLowerInvariant();

        return relativePath.StartsWith("guide/labs/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("guide/archive/", StringComparison.OrdinalIgnoreCase)
            || relativePath.StartsWith("tests/fixtures/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProbablyTextFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
        {
            var fileName = Path.GetFileName(path);
            if (string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "Makefile", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        if (!AllowedTextFileExtensions.Contains(extension))
        {
            return false;
        }

        return true;
    }

    private static bool IsInBuildOutputDirectory(string path)
    {
        var normalized = Path.GetFullPath(path)
            .Replace(Path.DirectorySeparatorChar, '/')
            .TrimEnd('/');
        return normalized.EndsWith("/bin", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/obj", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("/.git", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBinary(string path)
    {
        Span<byte> buffer = stackalloc byte[4096];
        using var stream = File.OpenRead(path);
        int read = stream.Read(buffer);
        return buffer.Slice(0, read).IndexOf((byte)0) >= 0;
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
