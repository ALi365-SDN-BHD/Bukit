namespace Bukit.Plugin.Import;

public static class ImportPluginPathGuard
{
    public static string NormalizeRoot(string rootDir)
    {
        if (string.IsNullOrWhiteSpace(rootDir))
            throw new ImportPluginOptionsException("plugin.import.invalidRoot", "Plugin context rootDir is required.");

        return Path.GetFullPath(rootDir);
    }

    public static string NormalizeWorkingDir(string rootDir, string workingDir)
    {
        var value = string.IsNullOrWhiteSpace(workingDir) ? rootDir : workingDir;
        return EnsureUnderRoot(rootDir, Path.GetFullPath(value), "workingDir");
    }

    public static string? ResolveOptionalPath(string rootDir, string baseDir, string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var fullPath = Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(baseDir, value));
        return EnsureUnderRoot(rootDir, fullPath, name);
    }

    public static string ResolveRequiredPath(string rootDir, string baseDir, string value, string name)
        => ResolveOptionalPath(rootDir, baseDir, value, name)
           ?? throw new ImportPluginOptionsException("plugin.import.missingArgument", $"Missing required argument: <{name}>");

    public static string? PreserveSafeRelativeOrRootedPath(string rootDir, string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Path.IsPathRooted(value))
            return EnsureUnderRoot(rootDir, Path.GetFullPath(value), name);

        var normalized = value.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part is "." or ".."))
            throw new ImportPluginOptionsException("plugin.import.pathDenied", $"{name} must not escape the project root.");

        return value;
    }

    private static string EnsureUnderRoot(string rootDir, string fullPath, string name)
    {
        var normalizedRoot = Path.GetFullPath(rootDir);
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        if (!fullPath.Equals(normalizedRoot, PathComparison) &&
            !fullPath.StartsWith(rootWithSeparator, PathComparison))
        {
            throw new ImportPluginOptionsException("plugin.import.pathDenied", $"{name} must stay under the project root.");
        }

        return fullPath;
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}

public sealed class ImportPluginOptionsException : Exception
{
    public ImportPluginOptionsException(string code, string message, int exitCode = 2)
        : base(message)
    {
        Code = code;
        ExitCode = exitCode;
    }

    public string Code { get; }
    public int ExitCode { get; }
}
