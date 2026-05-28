using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Cli.Parsing;

namespace Bukit.Cli.Commands.DocsCheck;

public static class ExampleParserChecker
{
    public static IReadOnlyList<DocsIssue> Check(IReadOnlyList<DocFile> docFiles, CliCommandRegistry registry)
    {
        var issues = new List<DocsIssue>();

        foreach (var docFile in docFiles)
        {
            if (docFile.Category != DocCategory.Readme)
            {
                continue;
            }

            var lines = File.ReadAllLines(docFile.Path);
            var inBashBlock = false;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var lineNumber = i + 1;
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("```bash", StringComparison.Ordinal))
                {
                    inBashBlock = true;
                    continue;
                }

                if (inBashBlock && trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    inBashBlock = false;
                    continue;
                }

                if (!inBashBlock)
                {
                    continue;
                }

                var content = line.Trim();

                if (string.IsNullOrEmpty(content) || content.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string command;
                IReadOnlyList<string> args;

                if (content.StartsWith("bukit ", StringComparison.Ordinal))
                {
                    var parts = SplitArgs(content.Substring("bukit ".Length));
                    if (parts.Count == 0)
                    {
                        continue;
                    }

                    command = parts[0];
                    args = parts.Skip(1).ToArray();
                }
                else if (content.Contains("dotnet run --project src/Bukit.Cli", StringComparison.Ordinal))
                {
                    var separatorIndex = content.LastIndexOf(" -- ", StringComparison.Ordinal);
                    if (separatorIndex < 0)
                    {
                        continue;
                    }

                    var afterSeparator = content.Substring(separatorIndex + 4).TrimStart();
                    var parts = SplitArgs(afterSeparator);
                    if (parts.Count == 0)
                    {
                        continue;
                    }

                    command = parts[0];
                    args = parts.Skip(1).ToArray();
                }
                else
                {
                    continue;
                }

                var spec = CommandPathExtractor.ResolveSpec(command, registry);
                if (spec is null)
                {
                    issues.Add(new DocsIssue(
                        docFile.Path,
                        lineNumber,
                        Severity.Error,
                        CheckType.Examples,
                        $"Unknown command in example: '{command}'"));
                    continue;
                }

                var result = CliParser.Parse(spec, args);
                if (!result.IsSuccess)
                {
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        issues.Add(new DocsIssue(
                            docFile.Path,
                            lineNumber,
                            Severity.Error,
                            CheckType.Examples,
                            diagnostic.Message));
                    }
                }
            }
        }

        return issues;
    }

    private static List<string> SplitArgs(string input)
    {
        var args = new List<string>();
        var span = input.AsSpan().Trim();

        while (span.Length > 0)
        {
            if (span[0] == '"')
            {
                var end = span.Slice(1).IndexOf('"');
                if (end >= 0)
                {
                    args.Add(span.Slice(1, end).ToString());
                    span = span.Slice(end + 2).TrimStart();
                }
                else
                {
                    args.Add(span.Slice(1).ToString());
                    break;
                }
            }
            else
            {
                var spaceIndex = span.IndexOf(' ');
                if (spaceIndex >= 0)
                {
                    args.Add(span.Slice(0, spaceIndex).ToString());
                    span = span.Slice(spaceIndex + 1).TrimStart();
                }
                else
                {
                    args.Add(span.ToString());
                    break;
                }
            }
        }

        return args;
    }
}
