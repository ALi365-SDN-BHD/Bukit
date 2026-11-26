using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Config;

/// <summary>
/// <c>${env:NAME}</c> references in a config value that is read when the build fetches content
/// (today only <c>cloudbase.filter.value</c>). They resolve when the content source is created, so
/// <c>config check</c> works without the variable, and an unset or empty variable fails the build
/// instead of querying with an empty value.
/// </summary>
internal static partial class ConfigEnvValue
{
    /// <returns>Null when every <c>${</c> starts a valid <c>${env:NAME}</c> reference, else the problem.</returns>
    internal static string? FindSyntaxError(string value)
    {
        var rest = ReferenceRegex().Replace(value, string.Empty);
        return rest.Contains("${", StringComparison.Ordinal)
            ? "uses '${' outside a ${env:NAME} reference (NAME is letters, digits and '_', not starting with a digit)"
            : null;
    }

    /// <summary>Replaces every reference; throws when a variable is unset or empty.</summary>
    internal static string Resolve(string value, string path, Func<string, string?>? lookup = null)
    {
        lookup ??= Environment.GetEnvironmentVariable;
        return ReferenceRegex().Replace(value, match =>
        {
            var name = match.Groups["name"].Value;
            var resolved = lookup(name);
            return string.IsNullOrWhiteSpace(resolved)
                ? throw new ConfigException(
                    $"{path} reads environment variable {name}, which is not set or is empty.",
                    DiagnosticCode.ConfigRequiredFieldMissing)
                : resolved.Trim();
        });
    }

    [GeneratedRegex(@"\$\{env:(?<name>[A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex ReferenceRegex();
}
