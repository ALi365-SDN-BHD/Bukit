# mediaCopy Skip-Unchanged Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `mediaCopy` skip unchanged files in `assets/uploads` using file length plus `LastWriteTimeUtc`, while preserving current top-level-only behavior and dotfile filtering.

**Architecture:** Add a focused top-level file sync helper to `DirectoryCopy` so `mediaCopy` can reuse the same metadata-based skip rule already used by `assetsSync`. Cover the helper with targeted xUnit tests first, then switch `SiteEngine` from inline overwrite logic to the new helper without changing stage metrics or recursive behavior.

**Tech Stack:** C# 14, .NET 10, xUnit

---

## File Map

- Modify: `src/Bukit.Engine/DirectoryCopy.cs`
  - Add a top-level file sync helper that supports optional dotfile filtering and reuses the existing skip-unchanged rule.
- Modify: `src/Bukit.Engine/SiteEngine.cs`
  - Replace inline `mediaCopy` file iteration with the new helper call.
- Create: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
  - Add focused regression coverage for missing source, copy, skip, overwrite, timestamp preservation, dotfile filtering, and non-recursive behavior.

### Task 1: Add focused failing tests for `DirectoryCopy`

**Files:**
- Create: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`
- Modify: none
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`

- [ ] **Step 1: Write the failing test file**

```csharp
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DirectoryCopyTests
{
    [Fact]
    public void SyncFiles_NoOps_WhenSourceDirectoryDoesNotExist()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "missing");
        var destinationDir = Path.Combine(root, "output");

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.False(Directory.Exists(destinationDir));
    }

    [Fact]
    public void SyncFiles_CopiesNewFile_AndPreservesTimestamp()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        File.WriteAllText(sourceFile, "v1");
        var sourceTimestamp = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, sourceTimestamp);

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        var destinationFile = Path.Combine(destinationDir, "photo.jpg");
        Assert.True(File.Exists(destinationFile));
        Assert.Equal("v1", File.ReadAllText(destinationFile));
        Assert.Equal(sourceTimestamp, File.GetLastWriteTimeUtc(destinationFile));
    }

    [Fact]
    public void SyncFiles_SkipsUnchangedDestinationFile()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);

        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        var destinationFile = Path.Combine(destinationDir, "photo.jpg");
        File.WriteAllText(sourceFile, "same-content");
        var sharedTimestamp = new DateTime(2024, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, sharedTimestamp);
        File.WriteAllText(destinationFile, "same-content");
        File.SetLastWriteTimeUtc(destinationFile, sharedTimestamp);

        var beforeSync = File.GetLastWriteTimeUtc(destinationFile);
        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);
        var afterSync = File.GetLastWriteTimeUtc(destinationFile);

        Assert.Equal("same-content", File.ReadAllText(destinationFile));
        Assert.Equal(beforeSync, afterSync);
    }

    [Fact]
    public void SyncFiles_OverwritesDestination_WhenLengthOrTimestampDiffers()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destinationDir);

        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        var destinationFile = Path.Combine(destinationDir, "photo.jpg");
        File.WriteAllText(sourceFile, "new-content");
        var sourceTimestamp = new DateTime(2024, 05, 06, 07, 08, 09, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(sourceFile, sourceTimestamp);
        File.WriteAllText(destinationFile, "old");
        File.SetLastWriteTimeUtc(destinationFile, sourceTimestamp.AddMinutes(-10));

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.Equal("new-content", File.ReadAllText(destinationFile));
        Assert.Equal(sourceTimestamp, File.GetLastWriteTimeUtc(destinationFile));
    }

    [Fact]
    public void SyncFiles_IgnoresDotPrefixedFiles_WhenOptionEnabled()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(sourceDir);

        File.WriteAllText(Path.Combine(sourceDir, ".hidden.jpg"), "skip-me");

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.False(File.Exists(Path.Combine(destinationDir, ".hidden.jpg")));
    }

    [Fact]
    public void SyncFiles_DoesNotCopySubdirectoryFiles()
    {
        var root = CreateTempRoot();
        var sourceDir = Path.Combine(root, "source");
        var nestedDir = Path.Combine(sourceDir, "nested");
        var destinationDir = Path.Combine(root, "output");
        Directory.CreateDirectory(nestedDir);

        File.WriteAllText(Path.Combine(nestedDir, "nested.jpg"), "nested");

        DirectoryCopy.SyncFiles(sourceDir, destinationDir, ignoreDotPrefixedFiles: true);

        Assert.False(File.Exists(Path.Combine(destinationDir, "nested.jpg")));
        Assert.False(Directory.Exists(Path.Combine(destinationDir, "nested")));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
```

- [ ] **Step 2: Run the new test file and verify it fails**

Run:

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter DirectoryCopyTests
```

Expected:

```text
FAIL
error CS0117: 'DirectoryCopy' does not contain a definition for 'SyncFiles'
```

- [ ] **Step 3: Commit the failing test scaffold**

```bash
git add tests/Bukit.Engine.Tests/DirectoryCopyTests.cs
git commit -m "test: add directory copy sync file coverage"
```

### Task 2: Implement `DirectoryCopy.SyncFiles`

**Files:**
- Modify: `src/Bukit.Engine/DirectoryCopy.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`

- [ ] **Step 1: Add the minimal helper implementation**

Update `src/Bukit.Engine/DirectoryCopy.cs` to:

```csharp
namespace Bukit.Engine;

public static class DirectoryCopy
{
    public static void Copy(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destinationDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Copy(dir, dest);
        }
    }

    public static void Sync(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            SyncFile(file, destinationDir);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Sync(dir, dest);
        }
    }

    public static void SyncFiles(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ignoreDotPrefixedFiles && name.StartsWith('.'))
            {
                continue;
            }

            SyncFile(file, destinationDir);
        }
    }

    private static void SyncFile(string sourceFile, string destinationDir)
    {
        var name = Path.GetFileName(sourceFile);
        var destinationFile = Path.Combine(destinationDir, name);

        var sourceInfo = new FileInfo(sourceFile);
        var destinationInfo = new FileInfo(destinationFile);
        if (destinationInfo.Exists &&
            destinationInfo.Length == sourceInfo.Length &&
            destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
        {
            return;
        }

        File.Copy(sourceFile, destinationFile, overwrite: true);
        File.SetLastWriteTimeUtc(destinationFile, sourceInfo.LastWriteTimeUtc);
    }
}
```

- [ ] **Step 2: Run the targeted tests and verify they pass**

Run:

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter DirectoryCopyTests
```

Expected:

```text
PASS
```

- [ ] **Step 3: Commit the helper implementation**

```bash
git add src/Bukit.Engine/DirectoryCopy.cs tests/Bukit.Engine.Tests/DirectoryCopyTests.cs
git commit -m "feat: add top-level sync helper for media copy"
```

### Task 3: Switch `mediaCopy` to the new helper

**Files:**
- Modify: `src/Bukit.Engine/SiteEngine.cs`
- Modify: `src/Bukit.Engine/DirectoryCopy.cs`
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`

- [ ] **Step 1: Replace inline overwrite logic in `SiteEngine`**

Update the `mediaCopy` block in `src/Bukit.Engine/SiteEngine.cs` to:

```csharp
        if (Directory.Exists(ctx.MediaDownloadDir))
        {
            var mediaCopyStopwatch = Stopwatch.StartNew();
            var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
            DirectoryCopy.SyncFiles(ctx.MediaDownloadDir, mediaOutputDir, ignoreDotPrefixedFiles: true);
            mediaCopyStopwatch.Stop();
            variantStageMetrics.AddDuration("mediaCopy", mediaCopyStopwatch.ElapsedMilliseconds);
        }
```

- [ ] **Step 2: Run the focused engine test suite**

Run:

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "DirectoryCopyTests|BuildManifestTests|MetricsWriterTests"
```

Expected:

```text
PASS
```

- [ ] **Step 3: Run diagnostics on edited files**

Check diagnostics for:

```text
src/Bukit.Engine/DirectoryCopy.cs
src/Bukit.Engine/SiteEngine.cs
tests/Bukit.Engine.Tests/DirectoryCopyTests.cs
```

Expected:

```text
No errors
```

- [ ] **Step 4: Commit the `SiteEngine` integration**

```bash
git add src/Bukit.Engine/SiteEngine.cs src/Bukit.Engine/DirectoryCopy.cs tests/Bukit.Engine.Tests/DirectoryCopyTests.cs
git commit -m "perf: skip unchanged media copy files"
```

### Task 4: Final verification and handoff

**Files:**
- Modify: none
- Test: `tests/Bukit.Engine.Tests/DirectoryCopyTests.cs`

- [ ] **Step 1: Run the final targeted verification command**

Run:

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj --filter "DirectoryCopyTests|BuildManifestTests|MetricsWriterTests|DirectoryHashCacheTests"
```

Expected:

```text
PASS
```

- [ ] **Step 2: Inspect git diff for scope control**

Run:

```bash
git diff --stat HEAD~2..HEAD
```

Expected:

```text
Only DirectoryCopy, SiteEngine, and DirectoryCopyTests changes for this feature
```

- [ ] **Step 3: Summarize behavior for handoff**

Record the outcome in the final handoff:

```text
- mediaCopy now skips unchanged top-level files in assets/uploads
- dot-prefixed files remain ignored
- recursive media directory copying was not introduced
- helper behavior is covered by focused xUnit tests
```

## Self-Review

- Spec coverage checked:
  - skip unchanged using metadata: covered by Task 1 and Task 2
  - preserve top-level-only behavior: covered by Task 1 and Task 3
  - preserve dotfile filtering: covered by Task 1 and Task 3
  - keep `mediaCopy` metric unchanged: covered by Task 3
- Placeholder scan checked:
  - no `TODO`, `TBD`, or vague “write tests” steps remain
- Type consistency checked:
  - plan consistently uses `DirectoryCopy.SyncFiles(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false)`
