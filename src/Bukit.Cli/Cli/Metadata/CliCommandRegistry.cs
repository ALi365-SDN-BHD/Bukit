namespace Bukit.Cli.Cli.Metadata;

public sealed class CliCommandRegistry
{
    private readonly Dictionary<string, CliCommandSpec> _commands;
    private readonly List<CliCommandSpec> _all;

    public IReadOnlyList<CliCommandSpec> Commands => _all;

    public CliCommandRegistry(IEnumerable<CliCommandSpec> commands)
    {
        _all = commands.ToList();
        _commands = new Dictionary<string, CliCommandSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in _all)
        {
            _commands[command.Name] = command;
            if (command.Aliases is null)
            {
                continue;
            }

            foreach (var alias in command.Aliases)
            {
                _commands[alias] = command;
            }
        }
    }

    public CliCommandSpec? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _commands.TryGetValue(name, out var command) ? command : null;
    }
}
