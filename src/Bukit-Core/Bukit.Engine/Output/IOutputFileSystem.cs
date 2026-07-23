namespace Bukit.Engine.Output;

internal interface IOutputFileSystem
{
    Task WriteTextAsync(string relativePath, string content, CancellationToken cancellationToken);
    Task CopyFileAsync(string sourcePath, string relativePath, CancellationToken cancellationToken);
    Task DeleteFileAsync(string relativePath, CancellationToken cancellationToken);
    string GetSafeFullPath(string relativePath);
}
