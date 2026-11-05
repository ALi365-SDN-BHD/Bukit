using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ImportManifestTests : IDisposable
{
    private readonly string _site = Path.Combine(Path.GetTempPath(), "bukit-manifest-" + Guid.NewGuid().ToString("N"));

    public ImportManifestTests() => Directory.CreateDirectory(Path.Combine(_site, "content"));

    public void Dispose()
    {
        if (Directory.Exists(_site))
            Directory.Delete(_site, recursive: true);
    }

    private string Write(string relative, string text)
    {
        var path = Path.Combine(_site, relative);
        File.WriteAllText(path, text);
        return path;
    }

    [Fact]
    public void Save_RemovesUneditedStaleFilesOnlyInProducedAreas()
    {
        var first = ImportManifest.Load(_site, overwrite: false);
        first.Record(Write("content/old.md", "old"));
        first.Record(Write("site.yaml", "site"));
        first.Save();

        // The next import regenerates content but not site.yaml.
        var second = ImportManifest.Load(_site, overwrite: false);
        second.Produced(Path.Combine(_site, "content"));
        second.Save();

        Assert.False(File.Exists(Path.Combine(_site, "content", "old.md")));
        Assert.True(File.Exists(Path.Combine(_site, "site.yaml")));

        // site.yaml is still tracked, so a later import may update it.
        Assert.True(ImportManifest.Load(_site, overwrite: false).CanWrite(Path.Combine(_site, "site.yaml")));
    }

    [Fact]
    public void Save_WithoutCleanUp_RecordsWrittenFilesAndRemovesNothing()
    {
        var first = ImportManifest.Load(_site, overwrite: false);
        first.Record(Write("content/kept.md", "kept"));
        first.Save();

        // A failed import that wrote one new file.
        var failed = ImportManifest.Load(_site, overwrite: false);
        failed.Produced(Path.Combine(_site, "content"));
        failed.Record(Write("content/new.md", "new"));
        failed.Save(cleanUp: false);

        Assert.True(File.Exists(Path.Combine(_site, "content", "kept.md")));
        var next = ImportManifest.Load(_site, overwrite: false);
        Assert.True(next.CanWrite(Path.Combine(_site, "content", "new.md")));
        Assert.True(next.CanWrite(Path.Combine(_site, "content", "kept.md")));
    }

    [Fact]
    public void CanWrite_KeepsEditedAndForeignFiles()
    {
        var first = ImportManifest.Load(_site, overwrite: false);
        var edited = Write("content/edited.md", "generated");
        first.Record(edited);
        first.Save();
        File.AppendAllText(edited, "hand edit");
        var foreign = Write("content/mine.md", "mine");

        var next = ImportManifest.Load(_site, overwrite: false);

        Assert.False(next.CanWrite(edited));
        Assert.False(next.CanWrite(foreign));
        Assert.True(ImportManifest.Load(_site, overwrite: true).CanWrite(edited));
    }
}
