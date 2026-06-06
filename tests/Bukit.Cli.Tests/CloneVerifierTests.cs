using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneVerifierTests : IDisposable
{
    private readonly string _testDir;

    public CloneVerifierTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-verifier-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    private static byte[] MinimalPng()
    {
        return
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53,
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41,
            0x54, 0x08, 0xD7, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x08, 0x00, 0x01, 0x35, 0xEC, 0x68, 0xB3,
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        ];
    }

    [Fact]
    public void CompareScreenshotFiles_MatchingPairs_FindsPairs()
    {
        var targetDir = Path.Combine(_testDir, "target-screenshots");
        var localDir = Path.Combine(_testDir, "local-screenshots");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(localDir);

        var png = MinimalPng();
        File.WriteAllBytes(Path.Combine(targetDir, "target-1440.png"), png);
        File.WriteAllBytes(Path.Combine(localDir, "local-1440.png"), png);

        var method = typeof(CloneVerifier).GetMethod("CompareScreenshotFiles",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = ((System.Collections.IEnumerable)method.Invoke(null,
            [targetDir, localDir])!).Cast<object>().ToList();

        Assert.Single(result);
    }

    [Fact]
    public void CompareScreenshotFiles_OnlyTargetNoLocal_ReturnsEmpty()
    {
        var targetDir = Path.Combine(_testDir, "target-only");
        var localDir = Path.Combine(_testDir, "local-empty");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(localDir);

        File.WriteAllBytes(Path.Combine(targetDir, "target-1440.png"), MinimalPng());

        var method = typeof(CloneVerifier).GetMethod("CompareScreenshotFiles",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = ((System.Collections.IEnumerable)method.Invoke(null,
            [targetDir, localDir])!).Cast<object>().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void CompareScreenshotFiles_NonexistentDirectories_ReturnsEmpty()
    {
        var targetDir = Path.Combine(_testDir, "nonexistent-target");
        var localDir = Path.Combine(_testDir, "nonexistent-local");

        var method = typeof(CloneVerifier).GetMethod("CompareScreenshotFiles",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = ((System.Collections.IEnumerable)method.Invoke(null,
            [targetDir, localDir])!).Cast<object>().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void FindMissingScreenshotPairs_OnlyTarget_FindsMissing()
    {
        var targetDir = Path.Combine(_testDir, "target-missing-test");
        var localDir = Path.Combine(_testDir, "local-missing-test");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(localDir);

        var png = MinimalPng();
        File.WriteAllBytes(Path.Combine(targetDir, "target-1440.png"), png);
        File.WriteAllBytes(Path.Combine(targetDir, "target-768.png"), png);

        var method = typeof(CloneVerifier).GetMethod("FindMissingScreenshotPairs",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = ((System.Collections.IEnumerable)method.Invoke(null,
            [targetDir, localDir])!).Cast<object>().ToList();

        Assert.True(result.Count >= 2);
    }

    [Fact]
    public void FindMissingScreenshotPairs_AllPresent_ReturnsEmpty()
    {
        var targetDir = Path.Combine(_testDir, "target-complete");
        var localDir = Path.Combine(_testDir, "local-complete");
        Directory.CreateDirectory(targetDir);
        Directory.CreateDirectory(localDir);

        var png = MinimalPng();
        foreach (var vp in new[] { "1440", "768", "390" })
        {
            File.WriteAllBytes(Path.Combine(targetDir, $"target-{vp}.png"), png);
            File.WriteAllBytes(Path.Combine(localDir, $"local-{vp}.png"), png);
        }

        var method = typeof(CloneVerifier).GetMethod("FindMissingScreenshotPairs",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var result = ((System.Collections.IEnumerable)method.Invoke(null,
            [targetDir, localDir])!).Cast<object>().ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractViewportName_TargetPrefix_StripsPrefix()
    {
        var method = typeof(CloneVerifier).GetMethod("ExtractViewportName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("1440", (string)method.Invoke(null, ["target-1440.png"])!);
        Assert.Equal("768", (string)method.Invoke(null, ["target-768.png"])!);
        Assert.Equal("390", (string)method.Invoke(null, ["target-390.png"])!);
    }

    [Fact]
    public void ExtractViewportName_LocalPrefix_StripsPrefix()
    {
        var method = typeof(CloneVerifier).GetMethod("ExtractViewportName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("1440", (string)method.Invoke(null, ["local-1440.png"])!);
        Assert.Equal("768", (string)method.Invoke(null, ["local-768.png"])!);
    }

    [Fact]
    public void ExtractViewportName_NoPrefix_ReturnsFileNameWithoutExtension()
    {
        var method = typeof(CloneVerifier).GetMethod("ExtractViewportName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("custom", (string)method.Invoke(null, ["custom.png"])!);
    }

    [Fact]
    public void SectionLabel_Fallback_Id()
    {
        var section = new CloneSectionInfo { Id = "hero-section", Heading = "Welcome", Title = "Hero" };

        var method = typeof(CloneVerifier).GetMethod("SectionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("hero-section", (string)method.Invoke(null, [section])!);
    }

    [Fact]
    public void SectionLabel_Fallback_Heading()
    {
        var section = new CloneSectionInfo { Heading = "About Us", Title = "About" };

        var method = typeof(CloneVerifier).GetMethod("SectionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("About Us", (string)method.Invoke(null, [section])!);
    }

    [Fact]
    public void SectionLabel_Fallback_Title()
    {
        var section = new CloneSectionInfo { Title = "Features", Type = "features" };

        var method = typeof(CloneVerifier).GetMethod("SectionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("Features", (string)method.Invoke(null, [section])!);
    }

    [Fact]
    public void SectionLabel_Fallback_Type()
    {
        var section = new CloneSectionInfo { Type = "hero" };

        var method = typeof(CloneVerifier).GetMethod("SectionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("hero", (string)method.Invoke(null, [section])!);
    }

    [Fact]
    public void SectionLabel_Fallback_Semantic()
    {
        var section = new CloneSectionInfo { Semantic = "navigation" };

        var method = typeof(CloneVerifier).GetMethod("SectionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("navigation", (string)method.Invoke(null, [section])!);
    }

    [Fact]
    public void SectionLabel_Fallback_Default()
    {
        var section = new CloneSectionInfo();

        var method = typeof(CloneVerifier).GetMethod("SectionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal("section", (string)method.Invoke(null, [section])!);
    }
}
