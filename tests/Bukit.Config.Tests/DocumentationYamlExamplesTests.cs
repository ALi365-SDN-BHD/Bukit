using Xunit;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config.Tests;

/// <summary>
/// Every <c>```yaml</c> example in the user guide, the Core skills and the READMEs must be valid
/// YAML, so a reader who copies one into <c>site.yaml</c> does not get a parse error. Examples may be
/// fragments (an indented excerpt of a larger file); they only have to parse.
/// </summary>
public sealed class DocumentationYamlExamplesTests
{
    [Fact]
    public void YamlExamples_InGuidesSkillsAndReadmes_Parse()
    {
        var root = RepoRoot();
        var failures = new List<string>();
        var examples = 0;

        foreach (var file in DocumentationFiles(root))
        {
            foreach (var (line, yaml) in YamlBlocks(File.ReadAllLines(file)))
            {
                examples++;
                try
                {
                    new YamlStream().Load(new StringReader(yaml));
                }
                catch (YamlException ex)
                {
                    failures.Add($"{Path.GetRelativePath(root, file)}:{line}: {ex.Message}");
                }
            }
        }

        // Guards against a scan that silently finds nothing (a moved guide or a changed fence style).
        Assert.True(examples > 50, $"Expected more than 50 YAML examples, found {examples}.");
        Assert.True(failures.Count == 0, "YAML examples that do not parse:\n" + string.Join('\n', failures));
    }

    [Fact]
    public void YamlBlocks_StripsFenceIndentAndKeepsStartLine()
    {
        string[] lines =
        [
            "Intro",
            "1. Step",
            "   ```yaml",
            "   a:",
            "     b: 1",
            "   ```",
            "```yml",
            "c: 2",
            "```",
            "```markdown",
            "d: [",
            "```"
        ];

        var blocks = YamlBlocks(lines).ToList();

        Assert.Equal([(3, "a:\n  b: 1"), (7, "c: 2")], blocks);
    }

    [Fact]
    public void YamlStream_RejectsBracketsInsideFlowMapping()
    {
        // The guide 25 example this check was added for: [] are flow indicators inside { }.
        Assert.ThrowsAny<YamlException>(() =>
            new YamlStream().Load(new StringReader("fields: { title: string, stops: object[] }")));
    }

    /// <summary>Fenced <c>yaml</c>/<c>yml</c> blocks with their 1-based fence line; a fence's indent is removed from its body.</summary>
    internal static IEnumerable<(int Line, string Yaml)> YamlBlocks(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed is not ("```yaml" or "```yml"))
            {
                continue;
            }

            var indent = lines[index].Length - trimmed.Length;
            var start = index + 1;
            var body = new List<string>();
            for (index++; index < lines.Count && lines[index].Trim() != "```"; index++)
            {
                var line = lines[index];
                body.Add(line.Length >= indent && string.IsNullOrWhiteSpace(line[..indent]) ? line[indent..] : line.TrimStart());
            }

            yield return (start, string.Join('\n', body));
        }
    }

    private static IEnumerable<string> DocumentationFiles(string root)
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "guide", "user"), "*.md"))
        {
            yield return file;
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "guide", "skills"), "SKILL.md", SearchOption.AllDirectories))
        {
            yield return file;
        }

        foreach (var file in Directory.EnumerateFiles(root, "README*.md"))
        {
            yield return file;
        }
    }

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit-core.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
