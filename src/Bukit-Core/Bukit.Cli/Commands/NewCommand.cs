using System.Text;
using System.Text.RegularExpressions;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class NewCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var targetArg = command.GetArgument(0)!;
        if (string.IsNullOrWhiteSpace(targetArg))
        {
            throw new CommandArgumentException("new requires a target directory.");
        }

        var target = Path.GetFullPath(targetArg);
        var titleOption = command.GetString("--title");
        var defaultTitle = Path.GetFileName(Path.TrimEndingDirectorySeparator(target));
        var title = titleOption is null ? defaultTitle : titleOption.Trim();
        if (titleOption is not null && title.Length == 0)
        {
            throw new CommandArgumentException("--title must not be blank.");
        }

        if (title.Length == 0)
        {
            title = "My Site";
        }

        if (title.Length is < 1 or > 200 || title.Any(char.IsControl))
        {
            throw new CommandArgumentException(
                "--title must be 1-200 characters and contain no control characters.");
        }

        var language = (command.GetString("--language") ?? "en-US").Trim();
        if (!IsSafeLanguageTag(language))
        {
            throw new CommandArgumentException(
                "--language must use safe alphanumeric subtags separated by hyphens.");
        }

        var root = StarterSiteScaffold.Create(target, title, Slug(title), language).Root;
        Console.WriteLine($"Created: {root}");
        Console.WriteLine("Next:");
        Console.WriteLine($"  cd \"{root}\"");
        Console.WriteLine("  bukit config check");
        Console.WriteLine("  bukit doctor");
        Console.WriteLine("  bukit build");
        Console.WriteLine("  bukit preview --dir dist");
        return Task.FromResult(0);
    }

    private static string Slug(string title)
    {
        var slug = title.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        slug = Regex.Replace(slug, "[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "site" : slug[..Math.Min(slug.Length, 64)].TrimEnd('-');
    }

    // Same rule as SiteConfigValidator; that internal helper cannot be exposed
    // to the CLI assembly because the dependency matrix restricts InternalsVisibleTo.
    private static bool IsSafeLanguageTag(string language)
        => Regex.IsMatch(language, "^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$", RegexOptions.CultureInvariant);
}
