using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.PluginHost;

public sealed partial class PluginPathValidator : IPluginPathValidator
{
    public PluginPathValidationResult ValidatePluginSource(string projectRoot, string source)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return PluginPathValidationResult.Invalid("Project root is required.");
        }

        if (!TryNormalizeRelativePath(source, out string normalized, out string error))
        {
            return PluginPathValidationResult.Invalid(error);
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !StringComparer.Ordinal.Equals(parts[0], "plugins") || string.IsNullOrWhiteSpace(parts[1]))
        {
            return PluginPathValidationResult.Invalid("Plugin source must be plugins/<id>.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, normalized));
        string pluginsRoot = Path.GetFullPath(Path.Combine(projectRoot, "plugins"));
        if (!IsUnderDirectory(fullPath, pluginsRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin source must stay under plugins/.");
        }

        if (!PathUtils.IsSubPathOf(fullPath, pluginsRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin source real path must stay under plugins/.");
        }

        return PluginPathValidationResult.Valid(fullPath, normalized);
    }

    public PluginPathValidationResult ValidatePluginEntry(string projectRoot, string pluginRoot, string entry)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            return PluginPathValidationResult.Invalid("Project root is required.");
        }

        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin root is required.");
        }

        if (!TryNormalizeRelativePath(entry, out string normalized, out string error))
        {
            return PluginPathValidationResult.Invalid(error);
        }

        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => StringComparer.Ordinal.Equals(part, ".bukit")))
        {
            return PluginPathValidationResult.Invalid("Plugin entry must not point into .bukit/.");
        }

        string fullPath = Path.GetFullPath(Path.Combine(pluginRoot, normalized));
        string fullPluginRoot = Path.GetFullPath(pluginRoot);
        string fullBukitRoot = Path.GetFullPath(Path.Combine(projectRoot, ".bukit"));
        if (!IsUnderDirectory(fullPath, fullPluginRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin entry must stay inside the plugin directory.");
        }

        if (!PathUtils.IsSubPathOf(fullPath, fullPluginRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin entry real path must stay inside the plugin directory.");
        }

        if (IsUnderDirectory(fullPath, fullBukitRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin entry must not be inside .bukit/.");
        }

        if (PathUtils.IsSameOrSubPathOf(fullPath, fullBukitRoot))
        {
            return PluginPathValidationResult.Invalid("Plugin entry real path must not be inside .bukit/.");
        }

        return PluginPathValidationResult.Valid(fullPath, normalized);
    }

    private static bool TryNormalizeRelativePath(string path, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Path is required.";
            return false;
        }

        if (WindowsAbsolutePathRegex().IsMatch(path) || Path.IsPathFullyQualified(path))
        {
            error = "Path must be relative.";
            return false;
        }

        normalized = path.Replace('\\', '/').Trim('/');
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            error = "Path is required.";
            return false;
        }

        if (parts.Any(part => part is "." or ".."))
        {
            error = "Path must not contain traversal segments.";
            return false;
        }

        return true;
    }

    private static bool IsUnderDirectory(string candidate, string directory)
    {
        string normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        string normalizedCandidate = candidate.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return normalizedCandidate.StartsWith(normalizedDirectory, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^(?:[A-Za-z]:[\\/]|\\\\)")]
    private static partial Regex WindowsAbsolutePathRegex();
}
