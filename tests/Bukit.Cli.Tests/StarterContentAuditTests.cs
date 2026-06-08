using System.Globalization;
using System.Linq;
using Bukit.Config;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class StarterContentAuditTests
{
    [Fact]
    public void Starter_ContentFrontMatter_ShouldContainPublishAuditMinimumFields()
    {
        var repoRoot = FindRepositoryRoot();
        var contentDirs = new[]
        {
            Path.Combine(repoRoot, "examples", "starter", "content"),
            Path.Combine(repoRoot, "examples", "starter", "content-i18n"),
            Path.Combine(repoRoot, "examples", "starter", "content_extra")
        };

        foreach (var dir in contentDirs)
        {
            Assert.True(Directory.Exists(dir), $"Missing starter content directory: {dir}");

            foreach (var file in Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                var frontMatter = ParseFrontMatter(text);
                Assert.True(frontMatter.ContainsKey("author"), $"{Path.GetRelativePath(repoRoot, file)} missing author");
                Assert.True(frontMatter.ContainsKey("summary"), $"{Path.GetRelativePath(repoRoot, file)} missing summary");
                Assert.True(frontMatter.ContainsKey("updatedAt"), $"{Path.GetRelativePath(repoRoot, file)} missing updatedAt");

                Assert.False(string.IsNullOrWhiteSpace(frontMatter["author"]), $"{Path.GetRelativePath(repoRoot, file)} has empty author");
                Assert.False(string.IsNullOrWhiteSpace(frontMatter["summary"]), $"{Path.GetRelativePath(repoRoot, file)} has empty summary");
                Assert.True(
                    DateTimeOffset.TryParse(frontMatter["updatedAt"], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _),
                    $"{Path.GetRelativePath(repoRoot, file)} has invalid updatedAt");
            }
        }
    }

    [Fact]
    public void Starter_ContentModelSchema_ShouldDeclareMinimalPublishGuardrailsAndNoLegacyFieldSchemaHints()
    {
        var repoRoot = FindRepositoryRoot();
        var configPath = Path.Combine(repoRoot, "examples", "starter", "site.yaml");
        var config = ConfigLoader.Load(configPath);

        var schema = Assert.IsType<ContentModelSchemaConfig>(config.Content.ModelSchema);
        Assert.True(schema.RequireSummary, "Starter content schema should enforce summary as a required publish field.");
        Assert.True(schema.RequireAuthor, "Starter content schema should enforce author as a required publish field.");
        Assert.True(schema.RequireUpdatedAt, "Starter content schema should enforce updatedAt as a required publish field.");

        Assert.False(schema.RequireOrganization);
        Assert.False(schema.RequireProvenance);

        Assert.True(schema.CanonicalMappings is null or { Count: 0 });
        Assert.True(schema.CustomFields is null or { Count: 0 });
        Assert.True(schema.FieldScopes is null or { Count: 0 });
        Assert.True(schema.EntityMappings is null or { Count: 0 });
        Assert.True(schema.RelationMappings is null or { Count: 0 });

        if (schema.CanonicalMappings is not null)
        {
            var legacyCanonical = new[] { "seo_title", "cover", "cover_alt", "tableOfContents" };
            Assert.DoesNotContain(
                schema.CanonicalMappings,
                m => legacyCanonical.Contains(m.CanonicalField, StringComparer.OrdinalIgnoreCase));
        }
    }

    private static Dictionary<string, string> ParseFrontMatter(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        if (lines.Length < 3 || lines[0].Trim() != "---")
        {
            return [];
        }

        var end = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                end = i;
                break;
            }
        }

        if (end <= 0)
        {
            return [];
        }

        var frontMatterYaml = string.Join("\n", lines.Skip(1).Take(end - 1));
        var stream = new YamlStream();
        stream.Load(new StringReader(frontMatterYaml));
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            return [];
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in root.Children)
        {
            if (kv.Key is YamlScalarNode keyNode && kv.Value is YamlScalarNode valueNode)
            {
                result[keyNode.Value ?? string.Empty] = valueNode.Value ?? string.Empty;
            }
        }

        return result;
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "examples")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src")) &&
                Directory.Exists(Path.Combine(dir.FullName, "tests")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test runtime path.");
    }
}
