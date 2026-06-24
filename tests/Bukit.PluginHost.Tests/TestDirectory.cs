namespace Bukit.PluginHost.Tests;

internal sealed class TestDirectory : IDisposable
{
    private TestDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TestDirectory Create()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bukit-pluginhost-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new TestDirectory(path);
    }

    public string Write(string relativePath, string text)
    {
        string fullPath = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        string? directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, text);
        return fullPath;
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
