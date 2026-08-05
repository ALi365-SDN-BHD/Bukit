using Bukit.Cli;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Bukit.Cli.Shared.Cli.Rendering;
using Bukit.Cli.Commands;
using Bukit.Shared;

var command = args.Length > 0 ? args[0] : null;
var commandArgsRaw = args.Skip(1).ToArray();
var isJsonErrorMode = string.Equals(ReadGlobalLogFormat(commandArgsRaw, new List<string>(commandArgsRaw.Length), keepLogFormat: false), "json", StringComparison.OrdinalIgnoreCase);

if (command is null || command is "help" or "--help" or "-h")
{
    HelpPrinter.Print();
    return 0;
}

try
{
    var coreDescriptors = BukitCliDescriptors.CreateDescriptors();
    var descriptor = BukitCliDescriptors.ResolveDescriptor(coreDescriptors, command);
    if (descriptor is null)
    {
        var pluginCli = await PluginCliLoader.CreateDefault().LoadAsync(
            Directory.GetCurrentDirectory(),
            CancellationToken.None,
            toleratePluginFailures: string.Equals(command, "plugin", StringComparison.Ordinal));
        var descriptors = BukitCliComposer.Compose(coreDescriptors, pluginCli.Descriptors);
        descriptor = BukitCliDescriptors.ResolveDescriptor(descriptors, command);
    }

    var commandArgsInfo = NormalizeGlobalLogFormat(commandArgsRaw, descriptor);
    var commandArgs = commandArgsInfo.Args;
    isJsonErrorMode = commandArgsInfo.LogFormat == "json";

    if (descriptor is not null)
    {
        if (commandArgs.Any(x => x is "--help" or "-h"))
        {
            var help = ResolveHelpTarget(descriptor.Spec, commandArgs);
            var usage = CliHelpRenderer.Render(help.Spec, help.CommandPath);
            Console.WriteLine(usage);
            return 0;
        }

        var parsed = CliParser.Parse(descriptor.Spec, commandArgs);
        if (!parsed.IsSuccess)
        {
            var usage = CliHelpRenderer.Render(descriptor.Spec, $"bukit {descriptor.Spec.Name}");
            return PrintDiagnostics(command, 2, parsed.Diagnostics, usage, isJsonErrorMode);
        }

        return await descriptor.DispatchAsync(parsed);
    }

    return UnknownCommand(command, isJsonErrorMode);
}
catch (CommandArgumentException ex)
{
    PrintError(command, 2, ex, isJsonErrorMode);
    return 2;
}
catch (ConfigException ex)
{
    PrintError(command, 2, ex, isJsonErrorMode, ex.Code.HasValue ? ex.Code.Value.ToString() : null);
    return 2;
}
catch (ContentException ex)
{
    PrintError(command, 2, ex, isJsonErrorMode, ex.Code.HasValue ? ex.Code.Value.ToString() : null);
    return 2;
}
catch (RenderException ex)
{
    PrintError(command, 3, ex, isJsonErrorMode, ex.Code.HasValue ? ex.Code.Value.ToString() : null);
    return 3;
}
catch (Exception ex)
{
    return PrintUnhandledError(command, ex, isJsonErrorMode);
}

static (CliCommandSpec Spec, string CommandPath) ResolveHelpTarget(
    CliCommandSpec root,
    IReadOnlyList<string> args)
{
    var current = root;
    var commandPath = $"bukit {root.Name}";
    foreach (var token in args)
    {
        if (token is "--help" or "-h" || token.StartsWith("-", StringComparison.Ordinal))
        {
            break;
        }

        var child = current.Subcommands?.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, token, StringComparison.OrdinalIgnoreCase) ||
            candidate.Aliases is not null && candidate.Aliases.Any(alias =>
                string.Equals(alias, token, StringComparison.OrdinalIgnoreCase)));
        if (child is null)
        {
            break;
        }

        current = child;
        commandPath += $" {child.Name}";
    }

    return (current, commandPath);
}

static int UnknownCommand(string command, bool isJsonErrorMode)
{
    var usage = "Run 'bukit --help' to view supported commands.";
    return PrintDiagnostics(
        command,
        2,
        new[]
        {
            new CliDiagnostic("unknown-command", $"Unknown command: {command}", ShowUsage: true)
        },
        usage,
        isJsonErrorMode);
}

static (string? LogFormat, string[] Args) NormalizeGlobalLogFormat(string[] args, CommandDescriptor? descriptor)
{
    var output = new List<string>(args.Length);
    var keepLogFormat = descriptor?.Spec.Options is not null && descriptor.Spec.Options.Any(o => o.Name == "--log-format");
    var logFormat = ReadGlobalLogFormat(args, output, keepLogFormat);
    return (logFormat, output.ToArray());
}

static string? ReadGlobalLogFormat(string[] args, List<string> outputArgs, bool keepLogFormat)
{
    string? detected = null;

    for (var i = 0; i < args.Length; i++)
    {
        var token = args[i];

        if (token.StartsWith("--log-format=", StringComparison.OrdinalIgnoreCase))
        {
            detected = token.Substring("--log-format=".Length).Trim();
            if (keepLogFormat)
            {
                outputArgs.Add(token);
            }
            continue;
        }

        if (string.Equals(token, "--log-format", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.OrdinalIgnoreCase))
            {
                detected = args[i + 1].Trim();
                i++;
                if (keepLogFormat)
                {
                    outputArgs.Add("--log-format");
                    outputArgs.Add(detected);
                }
                continue;
            }
        }

        outputArgs.Add(token);
    }

    return detected;
}

static int PrintDiagnostics(string? command, int exitCode, IReadOnlyList<CliDiagnostic> diagnostics, string? usage, bool isJsonErrorMode)
{
    var errors = diagnostics
        .Select(d => new CliErrorRenderer.CliErrorDiagnostic(d.Code, d.Message, d.ShowUsage))
        .ToList();
    var effectiveUsage = diagnostics.Any(d => d.ShowUsage) ? usage : null;
    var payload = CliErrorRenderer.RenderJson(command, exitCode, errors, usage: effectiveUsage);

    if (isJsonErrorMode)
    {
        Console.Error.WriteLine(payload);
        return exitCode;
    }

    if (diagnostics.Count > 0)
    {
        Console.Error.WriteLine(CliErrorRenderer.Render(diagnostics[0]));
    }
    else
    {
        Console.Error.WriteLine("Error.");
    }

    if (effectiveUsage is not null)
    {
        Console.Error.WriteLine(effectiveUsage);
    }

    return exitCode;
}

static void PrintError(string? command, int exitCode, Exception ex, bool isJsonErrorMode, string? diagnosticCode = null)
{
    var diagnostics = new[]
    {
        new CliDiagnostic(diagnosticCode ?? "cli-error", ex.Message, ShowUsage: false)
    };

    _ = PrintDiagnostics(command, exitCode, diagnostics, usage: null, isJsonErrorMode);
}

static int PrintUnhandledError(string? command, Exception ex, bool isJsonErrorMode)
{
    PrintError(command, 1, ex, isJsonErrorMode);
    return 1;
}
