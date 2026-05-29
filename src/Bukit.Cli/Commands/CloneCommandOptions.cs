using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

internal sealed record CloneCommandOptions
{
    public string? Tokens { get; init; }
    public string? Layout { get; init; }
    public string? Page { get; init; }
    public string? Sections { get; init; }
    public string Theme { get; init; } = "cloned";
    public string? Brand { get; init; }
    public string? Behaviors { get; init; }
    public string? Icons { get; init; }
    public string? Assets { get; init; }
    public double VisualThreshold { get; init; }
    public string? Fidelity { get; init; }
    public bool Use { get; init; }
    public bool Force { get; init; }
    public bool Verify { get; init; }
    public bool FailOnVisualDiff { get; init; }

    public static (CloneCommandOptions? options, int errorCode) Parse(CliBoundCommand command)
    {
        var visualThreshold = ParseVisualThreshold(command.GetString("--visual-threshold"));
        if (visualThreshold is null)
            return (null, 2);

        var themeName = command.GetString("--theme") ?? "cloned";
        if (!CloneModels.IsSafeThemeName(themeName))
        {
            Console.Error.WriteLine("Invalid theme name.");
            return (null, 2);
        }

        var options = new CloneCommandOptions
        {
            Tokens = command.GetString("--tokens"),
            Layout = command.GetString("--layout"),
            Page = command.GetString("--page"),
            Sections = command.GetString("--sections"),
            Theme = themeName,
            Brand = command.GetString("--brand"),
            Behaviors = command.GetString("--behaviors"),
            Icons = command.GetString("--icons"),
            Assets = command.GetString("--assets"),
            VisualThreshold = visualThreshold.Value,
            Fidelity = command.GetString("--fidelity"),
            Use = command.GetBool("--use"),
            Force = command.GetBool("--force"),
            Verify = command.GetBool("--verify"),
            FailOnVisualDiff = command.GetBool("--fail-on-visual-diff")
        };

        return (options, 0);
    }

    public static CliBoundCommand BuildCommand(ArgReader reader)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["--tokens"] = reader.GetOption("--tokens"),
            ["--layout"] = reader.GetOption("--layout"),
            ["--page"] = reader.GetOption("--page"),
            ["--sections"] = reader.GetOption("--sections"),
            ["--theme"] = reader.GetOption("--theme"),
            ["--brand"] = reader.GetOption("--brand"),
            ["--behaviors"] = reader.GetOption("--behaviors"),
            ["--icons"] = reader.GetOption("--icons"),
            ["--assets"] = reader.GetOption("--assets"),
            ["--visual-threshold"] = reader.GetOption("--visual-threshold"),
            ["--fidelity"] = reader.GetOption("--fidelity"),
        };
        if (reader.HasFlag("--use")) options["--use"] = "true";
        if (reader.HasFlag("--force")) options["--force"] = "true";
        if (reader.HasFlag("--verify")) options["--verify"] = "true";
        if (reader.HasFlag("--fail-on-visual-diff")) options["--fail-on-visual-diff"] = "true";

        return new CliBoundCommand(
            options
                .Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>());
    }

    internal static double? ParseVisualThreshold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0.03d;

        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value) &&
            value is >= 0 and <= 1)
        {
            return value;
        }

        Console.Error.WriteLine("Invalid --visual-threshold value. Expected a number between 0 and 1, for example 0.03.");
        return null;
    }
}
