using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Output;

public sealed class SafeOutputFileSystem : IOutputFileSystem
{
    private readonly string _outputRoot;
    private readonly IOutputPathPolicy _pathPolicy;

    public SafeOutputFileSystem(string outputRoot, IOutputPathPolicy? pathPolicy = null)
    {
        _outputRoot = Path.GetFullPath(outputRoot);
        _pathPolicy = pathPolicy ?? new SafePathResolver();
    }

    public async Task WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        var fullPath = GetSafeFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    public async Task CopyFileAsync(string sourcePath, string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = GetSafeFullPath(relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(fullPath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    public Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = GetSafeFullPath(relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetSafeFullPath(string relativePath)
    {
        RouteSecurityValidator.ValidateOutputPath(relativePath, "output path");
        return _pathPolicy.ResolveSafePath(_outputRoot, relativePath);
    }
}
