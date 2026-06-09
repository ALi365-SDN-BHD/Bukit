using Bukit.Config;

namespace Bukit.Cli.Commands;

internal static class ContentSourceInspector
{
    internal static IReadOnlyList<string> GetMarkdownDirs(ContentConfig content)
    {
        var dirs = new List<string>();
        if (content.Sources is { Count: > 0 })
        {
            foreach (var source in content.Sources)
            {
                if (source.Markdown is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(source.Markdown.Dir))
                {
                    dirs.Add(source.Markdown.Dir);
                }
            }
        }

        return dirs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
