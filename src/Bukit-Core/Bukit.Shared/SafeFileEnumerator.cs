namespace Bukit.Shared;

internal static class SafeFileEnumerator
{
    internal static IEnumerable<string> EnumerateFiles(string root, string pattern = "*", bool recurse = true)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recurse,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            ReturnSpecialDirectories = false
        };

        return Directory.EnumerateFiles(root, pattern, options);
    }
}
