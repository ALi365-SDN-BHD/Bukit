using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;

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
            "bash" => RenderBash(registry, commands),
            "zsh" => RenderZsh(registry, commands),
            "fish" => RenderFish(registry),
            _ => string.Empty
        };
    }

    private static string RenderBash(CliCommandRegistry registry, string commands)
    {
        var cases = string.Join(
            Environment.NewLine,
            CommandsWithSubcommands(registry).Select(command =>
                $"            {command.Name}) candidates=\"{SubcommandNames(command)}\" ;;"));
        return $$"""
            _bukit_completion()
            {
                local cur="${COMP_WORDS[COMP_CWORD]}"
                local candidates="{{commands}}"
                if [[ ${COMP_CWORD} -eq 2 ]]; then
                    case "${COMP_WORDS[1]}" in
            {{cases}}
                    esac
                fi
                COMPREPLY=( $(compgen -W "$candidates" -- "$cur") )
            }
            complete -F _bukit_completion bukit
            """;
    }

    private static string RenderZsh(CliCommandRegistry registry, string commands)
    {
        var cases = string.Join(
            Environment.NewLine,
            CommandsWithSubcommands(registry).Select(command =>
                $"    {command.Name}) _values 'subcommand' {SubcommandNames(command)} ;;"));
        return $$"""
            #compdef bukit
            _arguments '1:command:({{commands}})' '2:subcommand:->subcommands'
            case "$words[2]" in
            {{cases}}
            esac
            """;
    }

    private static string RenderFish(CliCommandRegistry registry)
    {
        var topLevel = registry.Commands.Select(command =>
            $"complete -c bukit -n '__fish_use_subcommand' -f -a {command.Name} -d '{EscapeFish(command.Description)}'");
        var subcommands = CommandsWithSubcommands(registry).SelectMany(command => command.Subcommands!.Select(subcommand =>
            $"complete -c bukit -n '__fish_seen_subcommand_from {command.Name}; and not __fish_seen_subcommand_from {SubcommandNames(command)}' -f -a {subcommand.Name} -d '{EscapeFish(subcommand.Description)}'"));
        return string.Join('\n', topLevel.Concat(subcommands));
    }

    private static IEnumerable<CliCommandSpec> CommandsWithSubcommands(CliCommandRegistry registry)
        => registry.Commands.Where(command => command.Subcommands is { Count: > 0 });

    private static string SubcommandNames(CliCommandSpec command)
        => string.Join(' ', command.Subcommands!.Select(subcommand => subcommand.Name));

    private static string EscapeFish(string value)
        => value.Replace("'", "\\'", StringComparison.Ordinal);
}
