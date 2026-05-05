namespace Bukit.Cli.Cli.Binding;

public sealed class CliBoundCommand
{
    private readonly IReadOnlyDictionary<string, string?> _options;
    private readonly IReadOnlyList<string> _arguments;

    public CliBoundCommand(IReadOnlyDictionary<string, string?> options, IReadOnlyList<string> arguments)
    {
        _options = options;
        _arguments = arguments;
    }

    public string? GetString(string name) => _options.TryGetValue(name, out var value) ? value : null;

    public bool GetBool(string name) => _options.ContainsKey(name);

    public int? GetInt(string name)
    {
        var text = GetString(name);
        return int.TryParse(text, out var value) ? value : null;
    }

    public string? GetArgument(int index) => index >= 0 && index < _arguments.Count ? _arguments[index] : null;
}
