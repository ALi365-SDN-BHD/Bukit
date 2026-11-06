using System.Text.RegularExpressions;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ContentFieldReservationsTests
{
    [Fact]
    public void StrictSchema_AllowsEngineManagedFieldsWithoutDeclaringThem()
    {
        var schema = new ContentModelSchema(RejectUnknownRawKeys: true);
        var raw = RawDocument(new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["title"] = new("text", "Page"),
            ["slug"] = new("text", "page"),
            ["type"] = new("text", "page"),
            ["collection"] = new("text", "page"),
            ["source"] = new("text", "markdown"),
            ["sourcePath"] = new("text", "content/page.md"),
            ["sourceKey"] = new("text", "pages"),
            ["sourceMode"] = new("text", "content"),
            ["sourceId"] = new("text", "page"),
            ["bodyFingerprint"] = new("text", "abc"),
            ["tableOfContents"] = new("list", Array.Empty<object>()),
            ["publishAt"] = new("date", DateTimeOffset.UnixEpoch)
        });

        var document = ContentDocumentNormalizer.ToDocument(raw, schema);

        Assert.DoesNotContain(document.Diagnostics, diagnostic =>
            string.Equals(diagnostic.Code, "content.unknown_raw_key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StrictSchema_StillRejectsUndeclaredUserFields()
    {
        var schema = new ContentModelSchema(RejectUnknownRawKeys: true);
        var raw = RawDocument(new Dictionary<string, RawContentValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = new("text", "page"),
            ["collection"] = new("text", "page"),
            ["sourceKey"] = new("text", "pages"),
            ["articleOnly"] = new("text", "not declared")
        });

        var exception = Assert.Throws<ConfigException>(() => ContentDocumentNormalizer.ToDocument(raw, schema));

        Assert.Contains("articleOnly", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EngineInjectedAndCanonicalFieldGroupsDoNotOverlap()
    {
        Assert.Empty(ContentFieldReservations.EngineInjected.Intersect(
            ContentFieldReservations.CanonicalFields,
            StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryLiteralFieldKeyTheEngineReadsIsReserved()
    {
        // Drift guard: a new front matter key read by Core must be added to
        // ContentFieldReservations, or strict schema would reject valid content.
        var srcRoot = Path.Combine(FindRepoRoot(), "src", "Bukit-Core");
        var reads = new Regex(
            @"(?:ContentFieldReader\.(?:Get(?!ContentType)\w*|TryGet\w*|Has\w*)|FirstTextField)\(\s*[\w.?]+\s*,\s*""(?<key>[A-Za-z0-9_]+)""",
            RegexOptions.CultureInvariant);
        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in reads.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups["key"].Value;
                if (!ContentFieldReservations.IsEngineManaged(key))
                {
                    missing.Add($"{key} ({Path.GetRelativePath(srcRoot, file)})");
                }
            }
        }

        Assert.True(missing.Count == 0, "Add these keys to ContentFieldReservations: " + string.Join(", ", missing));
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private static RawContentDocument RawDocument(IReadOnlyDictionary<string, RawContentValue> properties)
        => new(
            SourceId: "page",
            SourceKind: "markdown",
            Title: "Page",
            Slug: "page",
            PublishedAt: DateTimeOffset.UnixEpoch,
            Body: new RawBody(InlineHtml: "<p>Page</p>"),
            Properties: properties,
            Source: new ContentSourceInfo("markdown", SourcePath: "content/page.md"));
}
