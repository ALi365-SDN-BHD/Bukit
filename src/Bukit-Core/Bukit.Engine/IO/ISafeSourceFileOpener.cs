namespace Bukit.Engine.IO;

/// <summary>
/// Opens a source file with a no-follow handle so the path validated during
/// enumeration cannot be swapped for a symlink before it is opened.
/// </summary>
internal interface ISafeSourceFileOpener
{
    /// <summary>
    /// Opens <paramref name="path"/> without following reparse points and verifies
    /// the already-open target still resolves inside <paramref name="sourceRoot"/>.
    /// </summary>
    /// <exception cref="IOException">When the path is a reparse point, its target
    /// escapes the source root, or the platform lacks a safe primitive.</exception>
    VerifiedSourceFile Open(string path, string sourceRoot);
}
