using System.Reflection;
using Bukit.Cli.Commands;
using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class ProgramHelpersTests
{
    private static readonly Type s_programType = typeof(VersionCommand).Assembly.EntryPoint?.DeclaringType
        ?? throw new InvalidOperationException("Missing Bukit.Cli entry point type.");

    private static readonly MethodInfo s_readGlobalLogFormat = FindProgramLocalFunction("ReadGlobalLogFormat");

    private static readonly MethodInfo s_printDiagnostics = FindProgramLocalFunction("PrintDiagnostics");

    private static readonly MethodInfo s_printError = FindProgramLocalFunction("PrintError");

    [Fact]
    public void ReadGlobalLogFormat_InlineValue_WithKeep_PreservesToken()
    {
        var outputArgs = new List<string>();

        var detected = (string?)s_readGlobalLogFormat.Invoke(null, [new[] { "--log-format=json", "build" }, outputArgs, true]);

        Assert.Equal("json", detected);
        Assert.Equal(["--log-format=json", "build"], outputArgs);
    }

    [Fact]
    public void ReadGlobalLogFormat_SplitValue_WithKeep_PreservesTokens()
    {
        var outputArgs = new List<string>();

        var detected = (string?)s_readGlobalLogFormat.Invoke(null, [new[] { "--log-format", "json", "build" }, outputArgs, true]);

        Assert.Equal("json", detected);
        Assert.Equal(["--log-format", "json", "build"], outputArgs);
    }

    [Fact]
    public void ReadGlobalLogFormat_MissingValue_TreatsOptionAsRegularArgument()
    {
        var outputArgs = new List<string>();

        var detected = (string?)s_readGlobalLogFormat.Invoke(null, [new[] { "--log-format", "-h", "build" }, outputArgs, false]);

        Assert.Null(detected);
        Assert.Equal(["--log-format", "-h", "build"], outputArgs);
    }

    [Fact]
    public void PrintDiagnostics_NoDiagnostics_PrintsGenericError()
    {
        var (exitCode, _, stdErr) = CaptureConsole(() =>
            (int)s_printDiagnostics.Invoke(null, ["build", 2, Array.Empty<CliDiagnostic>(), "Usage: bukit build", false])!);

        Assert.Equal(2, exitCode);
        Assert.Contains("Error.", stdErr, StringComparison.Ordinal);
        Assert.DoesNotContain("Usage:", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintError_WithoutDiagnosticCode_UsesFallbackCliErrorCode()
    {
        var (_, _, stdErr) = CaptureConsole(() =>
        {
            s_printError.Invoke(null, ["build", 2, new InvalidOperationException("boom"), true, null]);
            return 0;
        });

        Assert.Contains("\"code\": \"cli-error\"", stdErr, StringComparison.Ordinal);
        Assert.Contains("\"message\": \"boom\"", stdErr, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintError_WithExplicitDiagnosticCode_UsesProvidedCode()
    {
        var (_, _, stdErr) = CaptureConsole(() =>
        {
            s_printError.Invoke(null, ["doctor", 3, new RenderException("render failed"), true, DiagnosticCodeFormatter.Format(DiagnosticCode.RenderFailed)]);
            return 0;
        });

        Assert.Contains("\"code\": \"BKT-0399\"", stdErr, StringComparison.Ordinal);
        Assert.Contains("\"command\": \"doctor\"", stdErr, StringComparison.Ordinal);
    }

    private static (int Result, string StdOut, string StdErr) CaptureConsole(Func<int> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var result = action();
            return (result, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    private static MethodInfo FindProgramLocalFunction(string suffix)
    {
        return s_programType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .FirstOrDefault(method => method.Name.Contains(suffix, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Program local function '{suffix}' not found.");
    }
}
