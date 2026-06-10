namespace Bukit.Cli.Commands.DocsCheck;

public static class ConfigFieldChecker
{
    public static IReadOnlyList<DocsIssue> Check(IReadOnlyList<DocFile> docFiles)
    {
        var issues = new List<DocsIssue>();

        var canonicalPaths = new HashSet<string>(ConfigFieldExtractor.ExtractAllConfigPaths(), StringComparer.OrdinalIgnoreCase);

        var coveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var docFile in docFiles)
        {
            if (!File.Exists(docFile.Path))
            {
                continue;
            }

            var text = File.ReadAllText(docFile.Path);

            var references = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

            foreach (var reference in references)
            {
                coveredPaths.Add(reference);

                if (!canonicalPaths.Contains(reference))
                {
                    if (ConfigFieldExtractor.IsDynamicMapChild(reference) ||
                        ConfigFieldExtractor.IsKnownTemplateVariable(reference))
                    {
                        continue;
                    }

                    issues.Add(new DocsIssue(
                        docFile.Path,
                        0,
                        Severity.Error,
                        CheckType.ConfigFields,
                        $"Referenced config field '{reference}' does not exist in site.yaml schema."));
                }
            }
        }

        foreach (var canonicalPath in canonicalPaths)
        {
            if (!coveredPaths.Contains(canonicalPath))
            {
                issues.Add(new DocsIssue(
                    ".",
                    0,
                    Severity.Warn,
                    CheckType.ConfigFields,
                    $"Config field '{canonicalPath}' has no documentation coverage."));
            }
        }

        return issues;
    }
}
