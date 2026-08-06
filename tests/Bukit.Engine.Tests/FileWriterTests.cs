using System.Reflection;
using Bukit.Engine.Output;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class FileWriterTests : IDisposable
{
    private readonly string _tempDir;

    public FileWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"bukit_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void WriteUtf8_CreatesFileInOutputDir()
    {
        var content = "<html><body>hello</body></html>";

        FileWriter.WriteUtf8(_tempDir, "index.html", content);

        var filePath = Path.Combine(_tempDir, "index.html");
        Assert.True(File.Exists(filePath));
        var written = File.ReadAllText(filePath);
        Assert.Equal("<html><body>hello</body></html>", written);
    }

    [Fact]
    public void WriteUtf8_CreatesParentDirectories()
    {
        var content = "<p>nested</p>";

        FileWriter.WriteUtf8(_tempDir, "deep/nested/page.html", content);

        var filePath = Path.Combine(_tempDir, "deep", "nested", "page.html");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void WriteUtf8_HandlesDeepPath()
    {
        var content = "data";

        FileWriter.WriteUtf8(_tempDir, "a/b/c/d/e/f/g/file.txt", content);

        var filePath = Path.Combine(_tempDir, "a", "b", "c", "d", "e", "f", "g", "file.txt");
        Assert.True(File.Exists(filePath));
        Assert.Equal("data", File.ReadAllText(filePath));
    }

    [Fact]
    public void WriteUtf8_OverwritesExistingFile()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "old content");

        FileWriter.WriteUtf8(_tempDir, "test.txt", "new content");

        Assert.Equal("new content", File.ReadAllText(filePath));
    }

    [Fact]
    public void DefaultPolicy_ConcurrentFirstReads_PublishSingleInstance()
    {
        const int workerCount = 64;
        const int rounds = 200;
        var field = typeof(FileWriter).GetField(
            "s_defaultPolicy",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var original = FileWriter.DefaultPolicy;
        var observed = new IOutputPathPolicy?[workerCount];
        var observedMultipleInstances = false;
        using var barrier = new Barrier(workerCount + 1);
        var workers = Enumerable.Range(0, workerCount)
            .Select(index => new Thread(() =>
            {
                for (var round = 0; round < rounds; round++)
                {
                    barrier.SignalAndWait();
                    observed[index] = FileWriter.DefaultPolicy;
                    barrier.SignalAndWait();
                }
            })
            {
                IsBackground = true
            })
            .ToArray();

        try
        {
            foreach (var worker in workers)
            {
                worker.Start();
            }

            for (var round = 0; round < rounds; round++)
            {
                field.SetValue(null, null);
                Array.Clear(observed);
                barrier.SignalAndWait();
                barrier.SignalAndWait();
                observedMultipleInstances |= observed
                    .Distinct(ReferenceEqualityComparer.Instance)
                    .Count() > 1;
            }
        }
        finally
        {
            foreach (var worker in workers)
            {
                worker.Join();
            }

            field.SetValue(null, original);
        }

        Assert.False(
            observedMultipleInstances,
            "Concurrent first reads published more than one default policy instance.");
    }
}
