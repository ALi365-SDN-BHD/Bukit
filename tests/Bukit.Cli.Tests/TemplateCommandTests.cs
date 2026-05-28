using System.Reflection;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class TemplateCommandTests : IDisposable
{
    private readonly string _layoutsDir;

    private static readonly MethodInfo s_resolveTemplatePath = typeof(TemplateCommand)
        .GetMethod("ResolveTemplatePath", BindingFlags.NonPublic | BindingFlags.Static)!;

    public TemplateCommandTests()
    {
        _layoutsDir = Path.Combine(Path.GetTempPath(), "bukit-tpl-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_layoutsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_layoutsDir))
        {
            Directory.Delete(_layoutsDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveTemplatePath_SimpleName_ReturnsFullPath()
    {
        var result = InvokeResolveTemplatePath("index.html");

        Assert.Equal(Path.Combine(_layoutsDir, "index.html"), result);
    }

    [Fact]
    public void ResolveTemplatePath_NestedName_ReturnsFullPath()
    {
        var result = InvokeResolveTemplatePath("pages/post.html");

        Assert.Equal(Path.Combine(_layoutsDir, "pages", "post.html"), result);
    }

    [Fact]
    public void ResolveTemplatePath_Null_ThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeResolveTemplatePath(null!));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void ResolveTemplatePath_Empty_ThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeResolveTemplatePath(""));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void ResolveTemplatePath_Whitespace_ThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeResolveTemplatePath("   "));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void ResolveTemplatePath_Traversal_ThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeResolveTemplatePath("../etc/passwd"));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void ResolveTemplatePath_AbsolutePath_ThrowsArgumentException()
    {
        var ex = Assert.Throws<TargetInvocationException>(() => InvokeResolveTemplatePath("/etc/passwd"));
        Assert.IsType<ArgumentException>(ex.InnerException);
    }

    [Fact]
    public void ResolveTemplatePath_DotSegment_Resolves()
    {
        var subDir = Path.Combine(_layoutsDir, "pages");
        Directory.CreateDirectory(subDir);

        var result = InvokeResolveTemplatePath("pages/../index.html");

        Assert.Equal(Path.Combine(_layoutsDir, "index.html"), result);
    }

    [Fact]
    public async Task RunAsync_NoSubcommand_ReturnsTwo()
    {
        var reader = new ArgReader(new[] { "template" });

        var exitCode = await TemplateCommand.RunAsync(reader);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_UnknownSubcommand_ReturnsTwo()
    {
        var reader = new ArgReader(new[] { "template", "unknown" });

        var exitCode = await TemplateCommand.RunAsync(reader);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public async Task RunAsync_Snippets_ReturnsZero()
    {
        var reader = new ArgReader(new[] { "template", "snippets" });

        var exitCode = await TemplateCommand.RunAsync(reader);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_Snippets_KnownSnippet_ReturnsZero()
    {
        var reader = new ArgReader(new[] { "template", "snippets", "post-card" });

        var exitCode = await TemplateCommand.RunAsync(reader);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_Snippets_UnknownSnippet_ReturnsZero()
    {
        var reader = new ArgReader(new[] { "template", "snippets", "nonexistent-snippet" });

        var exitCode = await TemplateCommand.RunAsync(reader);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task RunAsync_Hints_ReturnsZero()
    {
        var reader = new ArgReader(new[] { "template", "hints" });

        var exitCode = await TemplateCommand.RunAsync(reader);

        Assert.Equal(0, exitCode);
    }

    private string InvokeResolveTemplatePath(string name)
    {
        return (string)s_resolveTemplatePath.Invoke(null, new object[] { _layoutsDir, name })!;
    }
}
