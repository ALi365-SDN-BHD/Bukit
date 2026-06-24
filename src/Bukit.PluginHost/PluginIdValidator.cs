using Bukit.Shared;

namespace Bukit.PluginHost;

public static class PluginIdValidator
{
    public static void Validate(string id)
    {
        if (!IsValid(id))
        {
            throw new ConfigException(
                $"Plugin id must use lowercase letters, digits, and hyphen: {id}",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static bool IsValid(string id)
    {
        if (string.IsNullOrWhiteSpace(id)
            || id.Length > 64
            || id is "." or ".."
            || id[0] == '-'
            || id[^1] == '-'
            || id.Contains("--", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char ch in id)
        {
            bool valid = ch is >= 'a' and <= 'z'
                || ch is >= '0' and <= '9'
                || ch == '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}
