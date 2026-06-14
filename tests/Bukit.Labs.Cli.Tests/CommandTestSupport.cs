using System.Reflection;
using Bukit.Labs.Cli;

namespace Bukit.Labs.Cli.Tests;

internal static class CommandTestSupport
{
    internal sealed record CommandResult(int ExitCode, string StdOut, string StdErr);

    public static async Task<CommandResult> CaptureAsync(Func<Task<int>> action)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var originalOut = Console.Out;
        var originalErr = Console.Error;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            var exitCode = await action();
            return new CommandResult(exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    public static async Task<CommandResult> InvokeEntryPointAsync(params string[] args)
    {
        var entryPoint = typeof(LabsCliAssemblyMarker).Assembly.EntryPoint
            ?? throw new InvalidOperationException("Missing Bukit.Labs.Cli entry point.");

        return await CaptureAsync(async () =>
        {
            var result = entryPoint.Invoke(null, [args]);
            return result switch
            {
                Task<int> task => await task,
                Task task => await AwaitAndReturnZeroAsync(task),
                int code => code,
                _ => throw new InvalidOperationException($"Unsupported entry point return type: {result?.GetType().FullName ?? "null"}")
            };
        });
    }

    private static async Task<int> AwaitAndReturnZeroAsync(Task task)
    {
        await task;
        return 0;
    }

    public sealed class CurrentDirectoryScope : IDisposable
    {
        private readonly string _originalDirectory;

        public CurrentDirectoryScope(string directory)
        {
            _originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(directory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
        }
    }

    public sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _originalValue;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _originalValue = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _originalValue);
        }
    }

    public sealed class ConsoleInputScope : IDisposable
    {
        private readonly TextReader _originalIn;

        public ConsoleInputScope(string input)
        {
            _originalIn = Console.In;
            Console.SetIn(new StringReader(input));
        }

        public void Dispose()
        {
            Console.SetIn(_originalIn);
        }
    }
}
