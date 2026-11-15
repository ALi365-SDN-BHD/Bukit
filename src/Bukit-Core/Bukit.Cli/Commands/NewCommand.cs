using System.Text;
using Bukit.Cli.Commands.New;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class NewCommand
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
        var source = title.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        var builder = new StringBuilder(source.Length);
        var pendingDash = false;
        foreach (var c in source)
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(c);
                pendingDash = false;
            }
            else if (builder.Length > 0 && !pendingDash)
            {
                builder.Append('-');
                pendingDash = true;
            }
        }

        var slug = builder.ToString().TrimEnd('-');
        if (slug.Length > 64)
        {
            slug = slug[..64].TrimEnd('-');
        }

        return slug.Length == 0 ? "site" : slug;
    }

    // Same rule as SiteConfigValidator; that internal helper cannot be exposed
    // to the CLI assembly because the dependency matrix restricts InternalsVisibleTo.
    private static bool IsSafeLanguageTag(string language)
    {
        if (language.Length == 0)
        {
            return false;
        }

        var segmentLength = 0;
        foreach (var c in language)
        {
            if (c == '-')
            {
                if (segmentLength == 0)
                {
                    return false;
                }

                segmentLength = 0;
                continue;
            }

            if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                segmentLength++;
                continue;
            }

            return false;
        }

        return segmentLength > 0;
    }
}
