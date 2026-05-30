namespace Bukit.Cli.Tests;

public sealed class CurrentDirectoryScope : IDisposable
{
    private readonly string _original;

    public CurrentDirectoryScope(string path)
    {
        _original = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(path);
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_original);
    }
}
