using Microsoft.Win32.SafeHandles;

namespace Bukit.Engine.IO;

/// <summary>
/// A source file whose already-open handle has been verified: it was opened
/// without following reparse points and its resolved target is inside the
/// captured source root. Reads MUST go through <see cref="Stream"/>.
/// </summary>
internal sealed class VerifiedSourceFile : IDisposable
{
    public VerifiedSourceFile(SafeFileHandle handle, Stream stream, string verifiedPath)
    {
        Handle = handle;
        Stream = stream;
        VerifiedPath = verifiedPath;
    }

    /// <summary>Owned no-follow handle; the only handle that may open this file.</summary>
    public SafeFileHandle Handle { get; }

    /// <summary>Stream over the verified handle. Never re-open the path by name.</summary>
    public Stream Stream { get; }

    /// <summary>The resolved physical path of the already-open target.</summary>
    public string VerifiedPath { get; }

    /// <summary>Length read from the verified handle, never from the path.</summary>
    public long Length => RandomAccess.GetLength(Handle);

    /// <summary>Last-write time read from the verified handle, never from the path.</summary>
    public DateTime LastWriteTimeUtc => File.GetLastWriteTimeUtc(Handle);

    public void Dispose()
    {
        Stream.Dispose();
        Handle.Dispose();
    }
}
