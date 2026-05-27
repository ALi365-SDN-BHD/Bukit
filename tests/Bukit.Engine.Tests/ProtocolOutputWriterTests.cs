using Bukit.Engine.Plugins.Protocol;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ProtocolOutputWriterTests
{
    [Fact]
    public void WriteOutputs_Base64_WritesDecodedBytes()
    {
        using var temp = new TempDir();
        var outputDir = Path.Combine(temp.Path, "dist");
        Directory.CreateDirectory(outputDir);
        var content = "hello";
        var outputs = new[]
        {
            new AfterBuildOutputFile
            {
                Path = "bin/data.txt",
                ContentType = "text/plain",
                Base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content))
            }
        };

        ProtocolOutputWriter.WriteOutputs(outputDir, outputs);

        var bytes = File.ReadAllBytes(Path.Combine(outputDir, "bin", "data.txt"));
        Assert.Equal(content, System.Text.Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void WriteOutputs_Base64Invalid_Throws()
    {
        using var temp = new TempDir();
        var outputDir = Path.Combine(temp.Path, "dist");
        Directory.CreateDirectory(outputDir);
        var outputs = new[]
        {
            new AfterBuildOutputFile
            {
                Path = "bin/data.txt",
                ContentType = "text/plain",
                Base64 = "not-base64!"
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ProtocolOutputWriter.WriteOutputs(outputDir, outputs));
        Assert.Contains("base64", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteOutputs_PathTraversal_WithBase64_Throws()
    {
        using var temp = new TempDir();
        var outputDir = Path.Combine(temp.Path, "dist");
        Directory.CreateDirectory(outputDir);
        var outputs = new[]
        {
            new AfterBuildOutputFile
            {
                Path = "../escape.bin",
                ContentType = "application/octet-stream",
                Base64 = "aGVsbG8="
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ProtocolOutputWriter.WriteOutputs(outputDir, outputs));
        Assert.Contains("escapes", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WriteOutputs_TextAndBase64Together_Throws()
    {
        using var temp = new TempDir();
        var outputDir = Path.Combine(temp.Path, "dist");
        Directory.CreateDirectory(outputDir);
        var outputs = new[]
        {
            new AfterBuildOutputFile
            {
                Path = "mixed.txt",
                ContentType = "text/plain",
                Text = "hello",
                Base64 = "aGVsbG8="
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => ProtocolOutputWriter.WriteOutputs(outputDir, outputs));
        Assert.Contains("either", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
