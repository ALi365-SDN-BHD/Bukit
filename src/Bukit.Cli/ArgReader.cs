namespace Bukit.Cli;

public sealed class ArgReader
{
    private readonly List<string> _args;

    public ArgReader(IEnumerable<string> args)
    {
        _args = args.ToList();
    }

    public string? Command => _args.Count == 0 ? null : _args[0];

    public IReadOnlyList<string> RemainingArgs => _args.Skip(1).ToList();

    public bool HasFlag(string name)
    {
        return _args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    }

    public string? GetOption(string name)
    {
        for (var i = 0; i < _args.Count; i++)
        {
            var arg = _args[i];
            if (arg.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[(name.Length + 1)..];
            }

            if (!string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= _args.Count)
            {
                return null;
            }

            return _args[i + 1];
        }

        return null;
    }

    public string? GetArg(int index)
    {
        if (index < 0 || index >= _args.Count)
        {
            return null;
        }

        return _args[index];
    }
}

