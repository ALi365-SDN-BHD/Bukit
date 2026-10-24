namespace Bukit.Engine.Tests.Infrastructure;

internal sealed class SiteSandbox : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "bukit-phase0-" + Guid.NewGuid().ToString("N"));

    public SiteSandbox() => Directory.CreateDirectory(Root);

    public string PathFor(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(Root, relative));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!path.StartsWith(Root + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("Path must be inside the sandbox.", nameof(relative));
        return path;
    }

    public string Write(string relative, string text)
    {
        var path = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public string Link(string relative, string target, bool directory)
    {
        var path = PathFor(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (directory) Directory.CreateSymbolicLink(path, target);
        else File.CreateSymbolicLink(path, target);
        return path;
    }

    public void Dispose() => TestCleanup.DeleteDirectory(Root);

    internal static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
        }
    }
}
