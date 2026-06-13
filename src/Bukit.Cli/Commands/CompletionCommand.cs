using Bukit.Cli.Shared.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class CompletionCommand
{
    public static Task<int> RunAsync(CliBoundCommand command)
    {
        var shell = command.GetArgument(0) ?? "bash";
        var script = Render(shell);
        if (script.Length == 0)
        {
            Console.Error.WriteLine("Usage: bukit completion [bash|zsh|fish]");
            return Task.FromResult(2);
        }

        Console.WriteLine(script);
        return Task.FromResult(0);
    }

    public static string Render(string shell)
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var commands = string.Join(' ', registry.Commands.Select(c => c.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        return (shell ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "bash" => $$"""
                _bukit_completion()
                {
                    local cur="${COMP_WORDS[COMP_CWORD]}"
                    COMPREPLY=( $(compgen -W "{{commands}}" -- "$cur") )
                }
                complete -F _bukit_completion bukit
                """,
            "zsh" => $"#compdef bukit\n_arguments '1:command:({commands})'",
            "fish" => string.Join('\n', registry.Commands.Select(c => $"complete -c bukit -f -a {c.Name} -d '{EscapeFish(c.Description)}'")),
            _ => string.Empty
        };
    }

    private static string EscapeFish(string value)
        => value.Replace("'", "\\'", StringComparison.Ordinal);
}
