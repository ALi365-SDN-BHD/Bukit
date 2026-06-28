using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Parsing;

namespace Bukit.Cli.Shared.Cli.Metadata;

public sealed record CommandDescriptor(
    CliCommandSpec Spec,
    Func<CliBoundCommand, Task<int>>? Handler = null,
    IReadOnlyList<CommandDescriptor>? Children = null)
{
    public CommandDescriptor? ResolveChild(string name)
    {
        if (Children is null) return null;

        foreach (var child in Children)
        {
            if (string.Equals(child.Spec.Name, name, StringComparison.OrdinalIgnoreCase))
                return child;

            if (child.Spec.Aliases is not null)
            {
                foreach (var alias in child.Spec.Aliases)
                {
                    if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
                        return child;
                }
            }
        }

        return null;
    }

    public async Task<int> DispatchAsync(CliParseResult result)
    {
        switch (result)
        {
            case SimpleParseResult simple:
                return await DispatchSimpleAsync(simple);

            case SubcommandParseResult sub:
                return await DispatchSubcommandAsync(sub);

            default:
                return 2;
        }
    }

    private async Task<int> DispatchSimpleAsync(SimpleParseResult result)
    {
        if (Handler is null)
            return UnknownCommand(Spec.Name);

        return await Handler(result.BoundCommand);
    }

    private async Task<int> DispatchSubcommandAsync(SubcommandParseResult sub)
    {
        var child = ResolveChild(sub.SubcommandName);
        if (child is not null && child.Handler is not null)
        {
            CliBoundCommand merged = MergeSubcommandHierarchy(sub);
            return await child.Handler(merged);
        }

        if (Handler is not null)
        {
            CliBoundCommand merged = MergeSubcommandHierarchy(sub);
            return await Handler(merged);
        }

        return UnknownCommand($"{Spec.Name} {sub.SubcommandName}");
    }

    private static CliBoundCommand MergeSubcommandHierarchy(SubcommandParseResult sub)
    {
        CliBoundCommand innerBound = sub.InnerResult is SubcommandParseResult nested
            ? MergeSubcommandHierarchy(nested)
            : sub.InnerResult.BoundCommand;
        return CliBoundCommand.MergeForSubcommand(
            sub.BoundCommand,
            sub.SubcommandName,
            innerBound);
    }

    public IEnumerable<CommandDescriptor> Flatten()
    {
        yield return this;
        if (Children is null) yield break;
        foreach (var child in Children)
        {
            foreach (var nested in child.Flatten())
                yield return nested;
        }
    }

    public IReadOnlyList<string> ExtractCommandPaths(string? parentPath = null)
    {
        var prefix = parentPath is null ? Spec.Name : $"{parentPath} {Spec.Name}";
        var results = new List<string>();

        if (Handler is not null)
        {
            results.Add(prefix);
        }

        if (Children is not null)
        {
            foreach (var child in Children)
            {
                results.AddRange(child.ExtractCommandPaths(prefix));
            }
        }

        return results;
    }

    internal static int UnknownCommand(string name)
    {
        Console.Error.WriteLine($"Unknown command: {name}");
        return 2;
    }
}
