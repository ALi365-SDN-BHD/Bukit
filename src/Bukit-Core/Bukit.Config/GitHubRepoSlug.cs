namespace Bukit.Config;

// GitHub "owner/name" rules for deploy.repo and the config schema pattern.
// Bukit.Cli keeps its own copy for deploy --repo (it may not link Config sources);
// Bukit.Cli.Tests DeployRepoSlugParityTests checks that copy against Pattern.
internal static class GitHubRepoSlug
{
    // JSON Schema (ECMA-262) form of IsValid. Keep the two in sync; GitHubRepoSlugTests checks parity.
    // owner: 1-39 of [A-Za-z0-9-], alphanumeric first.
    // name: 1-100 of [A-Za-z0-9._-], not '.' or '..', not ending in '.git' (any case).
    internal const string Pattern =
        "^[A-Za-z0-9][A-Za-z0-9-]{0,38}/(?!\\.{1,2}$)(?!.*\\.[Gg][Ii][Tt]$)[A-Za-z0-9._-]{1,100}$";

    internal static bool IsValid(string value)
    {
        var parts = value.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        var owner = parts[0];
        var name = parts[1];
        if (owner.Length is < 1 or > 39 || !IsAsciiAlphanumeric(owner[0]))
        {
            return false;
        }

        foreach (var c in owner)
        {
            if (!(IsAsciiAlphanumeric(c) || c == '-'))
            {
                return false;
            }
        }

        if (name is "." or ".." || name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) || name.Length is < 1 or > 100)
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!(IsAsciiAlphanumeric(c) || c == '.' || c == '_' || c == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAsciiAlphanumeric(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
}
