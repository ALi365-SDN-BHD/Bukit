using System.IO.Compression;
using System.Text;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Labs.Cli.Commands;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneWorkflowTests : IDisposable
{
    private readonly string _rootDir;

    public CloneWorkflowTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "bukit-labs-clone-workflows-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_rootDir, recursive: true);
    }

    [Fact]
    public async Task CloneCommand_RunAsync_WithMinimalTokens_CreatesTheme()
    {
        var tokensPath = Path.Combine(_rootDir, "tokens.json");
        File.WriteAllText(tokensPath, """
{
  "tokens": {
    "primary": "#123456",
    "accent": "#abcdef"
  }
}
""");

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            CloneCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>
                {
                    ["--tokens"] = tokensPath,
                    ["--theme"] = "sample-clone"
                },
                [])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Theme cloned: sample-clone", result.StdOut, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "sample-clone", "theme.yaml")));
    }

    [Fact]
    public void CloneFidelityGenerator_Generate_WritesThemeFilesAndCopiesAssets()
    {
        var htmlDir = Path.Combine(_rootDir, "html");
        Directory.CreateDirectory(Path.Combine(htmlDir, "assets"));

        File.WriteAllText(Path.Combine(htmlDir, "index.html"), """
<!DOCTYPE html>
<html>
  <body>
    <header><h1>Home</h1></header>
    <nav><a href="/about.html">About</a></nav>
    <main><p>Welcome</p><img src="/assets/logo.png" /></main>
    <footer><a href="/contact.html">Contact</a></footer>
  </body>
</html>
""");
        File.WriteAllText(Path.Combine(htmlDir, "about.html"), """
<!DOCTYPE html>
<html>
  <body>
    <header><h1>About</h1></header>
    <main><p>About page</p></main>
    <footer><a href="/index.html">Home</a></footer>
  </body>
</html>
""");
        File.WriteAllText(Path.Combine(htmlDir, "assets", "app.js"), "console.log('hello');");
        WriteMinimalPng(Path.Combine(htmlDir, "assets", "logo.png"), 1, 1, new byte[] { 255, 0, 0, 255 });

        var summary = CloneFidelityGenerator.Generate(_rootDir, htmlDir, "fidelity-theme");

        Assert.True(summary.TemplateCount >= 5);
        Assert.True(summary.AssetCount >= 1);
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "fidelity-theme", "layouts", "pages", "index.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "fidelity-theme", "layouts", "pages", "about.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "fidelity-theme", "layouts", "layouts", "base.html")));
        Assert.True(File.Exists(Path.Combine(_rootDir, "themes", "fidelity-theme", "assets", "assets", "logo.png")));
    }

    [Fact]
    public async Task CloneFidelityRunner_RunAsync_WithUse_UpdatesSiteConfig()
    {
        var htmlDir = Path.Combine(_rootDir, "fidelity-html");
        Directory.CreateDirectory(htmlDir);
        File.WriteAllText(Path.Combine(htmlDir, "index.html"), "<html><body><main>Home</main></body></html>");
        File.WriteAllText(Path.Combine(_rootDir, "site.yaml"), """
site:
  name: demo
theme:
  name: starter
""");

        using var scope = new CommandTestSupport.CurrentDirectoryScope(_rootDir);

        var result = await CommandTestSupport.CaptureAsync(() =>
            CloneFidelityRunner.RunAsync(
                _rootDir,
                "fidelity-runner",
                htmlDir,
                force: false,
                use: true,
                new CliBoundCommand(new Dictionary<string, string?>(), [])));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Theme cloned (fidelity mode): fidelity-runner", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("Theme set: fidelity-runner", result.StdOut, StringComparison.Ordinal);
        Assert.Contains("name: fidelity-runner", File.ReadAllText(Path.Combine(_rootDir, "site.yaml")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloneScreenshotComparer_CanDiffAndInferAffectedSections()
    {
        var targetDir = Path.Combine(_rootDir, "target");
        var localDir = Path.Combine(_rootDir, "local");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(localDir);

        WriteMinimalPng(Path.Combine(targetDir, "target-1440.png"), 1, 1, new byte[] { 255, 0, 0, 255 });
        WriteMinimalPng(Path.Combine(localDir, "local-1440.png"), 1, 1, new byte[] { 0, 0, 255, 255 });

        var comparisons = CloneScreenshotComparer.CompareScreenshotFiles(targetDir, localDir).ToList();
        var comparison = Assert.Single(comparisons);
        Assert.Equal("target-1440.png", comparison.Name);
        Assert.True(comparison.DiffRatio > 0);
        Assert.True(comparison.HasMismatchBounds);

        var missing = CloneScreenshotComparer.FindMissingScreenshotPairs(targetDir, localDir).ToList();
        Assert.Contains(missing, pair => pair.Viewport == "768");
        Assert.Contains(missing, pair => pair.Viewport == "390");

        var sections = new[]
        {
            new CloneSectionInfo
            {
                Id = "hero",
                Heading = "Hero",
                Bounds = new CloneBox { Y = 0, Height = 10 }
            }
        };

        var affected = CloneScreenshotComparer.FindAffectedSections(comparisons, sections, visualThreshold: 0.01d).ToList();
        var section = Assert.Single(affected);
        Assert.Equal("hero", section.SectionId);

        var report = new StringBuilder();
        CloneScreenshotComparer.AppendAffectedSections(report, affected, hasSections: true);
        Assert.Contains("Likely Affected Sections", report.ToString(), StringComparison.Ordinal);

        CloneVerifier.WriteBehaviorVerifyScript(_rootDir);
        Assert.True(File.Exists(Path.Combine(_rootDir, "docs", "research", "BEHAVIORS_VERIFY.js")));
    }

    private static void WriteMinimalPng(string path, int width, int height, byte[] rgba)
    {
        using var fs = File.Create(path);
        fs.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var ihdr = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(fs, "IHDR", ihdr);

        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            for (var x = 0; x < width; x++)
            {
                raw.Write(rgba);
            }
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.Write(raw.ToArray());
        }

        WriteChunk(fs, "IDAT", compressed.ToArray());
        WriteChunk(fs, "IEND", []);
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> len = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        stream.Write(len);
        stream.Write(Encoding.ASCII.GetBytes(type));
        stream.Write(data);
        stream.Write([0, 0, 0, 0]);
    }
}
