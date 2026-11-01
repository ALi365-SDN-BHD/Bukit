using System.Reflection;
using Bukit.Cli.Deploy;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DeployWindowsRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-deploy-regression-" + Guid.NewGuid().ToString("N"));

    [WindowsFact]
    public void ResolveGit_Windows_PrefersNativeGitOverEarlierBatchWrapper()
    {
        var wrapper = Path.Combine(_root, "batch & wrapper");
        var native = Path.Combine(_root, "native 测试");
        Directory.CreateDirectory(wrapper);
        Directory.CreateDirectory(native);
        File.WriteAllText(Path.Combine(wrapper, "git.cmd"), "@exit /b 99");
        var executable = Path.Combine(native, "git.exe");
        File.WriteAllText(executable, "resolver existence probe; not executed");
        var original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", wrapper + Path.PathSeparator + native);
            Assert.Equal(executable, Invoke<string?>("ResolveGit", [null]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", original);
        }
    }

    [WindowsFact]
    public void ResolveGit_Windows_RejectsBatchOnlyPath()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "git.cmd"), "@exit /b 99");
        var original = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", _root);
            Assert.Null(Invoke<string?>("ResolveGit", [null]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", original);
        }
    }

    [Fact]
    public void CleanupDeploymentDirectory_RemovesReadOnlyGitObjects()
    {
        var owned = Path.Combine(_root, "owned");
        var objects = Path.Combine(owned, ".git", "objects", "ab");
        Directory.CreateDirectory(objects);
        var file = Path.Combine(objects, "object");
        File.WriteAllText(file, "owned Git object");
        File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.ReadOnly);

        Invoke<object?>("CleanupDeploymentDirectory", [owned]);

        Assert.False(Directory.Exists(owned));
    }

    [Fact]
    public void CleanupDeploymentDirectory_DoesNotFollowFileOrDirectoryLinks()
    {
        var owned = Path.Combine(_root, "owned");
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(owned);
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "keep.txt");
        File.WriteAllText(sentinel, "outside sentinel");
        File.SetAttributes(sentinel, File.GetAttributes(sentinel) | FileAttributes.ReadOnly);
        var attributes = File.GetAttributes(sentinel);
        File.CreateSymbolicLink(Path.Combine(owned, "file-link"), sentinel);
        Directory.CreateSymbolicLink(Path.Combine(owned, "directory-link"), outside);
        File.CreateSymbolicLink(Path.Combine(owned, "dangling-link"), Path.Combine(outside, "missing"));

        Invoke<object?>("CleanupDeploymentDirectory", [owned]);

        Assert.False(Directory.Exists(owned));
        Assert.Equal("outside sentinel", File.ReadAllText(sentinel));
        Assert.Equal(attributes, File.GetAttributes(sentinel));
    }

    [Fact]
    public void CleanupDeploymentDirectory_RootLinkDoesNotChangeTarget()
    {
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "keep.txt");
        File.WriteAllText(sentinel, "keep");
        var link = Path.Combine(_root, "owned-link");
        Directory.CreateSymbolicLink(link, outside);

        Invoke<object?>("CleanupDeploymentDirectory", [link]);

        Assert.False(Directory.Exists(link));
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    [WindowsFact]
    public void CleanupDeploymentDirectory_LockedFileReportsWarningWithoutLeakingPath()
    {
        var owned = Path.Combine(_root, "TOKEN-must-not-be-logged");
        Directory.CreateDirectory(owned);
        var file = Path.Combine(owned, "locked.txt");
        using var locked = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var error = new StringWriter();
        var original = Console.Error;
        try
        {
            Console.SetError(error);
            Invoke<object?>("CleanupDeploymentDirectory", [owned]);
        }
        finally
        {
            Console.SetError(original);
        }

        Assert.Contains("Deploy: failed to clean up temp dir:", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("TOKEN-must-not-be-logged", error.ToString(), StringComparison.Ordinal);
        Assert.True(Directory.Exists(owned));
    }

    [Fact]
    public void CleanupDeploymentDirectory_MissingDirectoryIsHarmless()
        => Invoke<object?>("CleanupDeploymentDirectory", [Path.Combine(_root, "missing")]);

    private static T Invoke<T>(string name, object?[] arguments)
    {
        var method = typeof(GitHubPagesDeployProvider).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return (T)method.Invoke(null, arguments)!;
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var name in new[] { "owned/.git/objects/ab/object", "outside/keep.txt" })
        {
            var file = Path.Combine(_root, name);
            if (File.Exists(file)) File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(_root, recursive: true);
    }
}
