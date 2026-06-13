using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Commands;

namespace Bukit.Cli;

public static class BukitCliDescriptors
{
    public static IReadOnlyList<CommandDescriptor> CreateDescriptors()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        return new CommandDescriptor[]
        {
            new(registry.Commands.First(c => c.Name == "build"), cmd => BuildCommand.RunAsync(cmd)),
            new(registry.Commands.First(c => c.Name == "clean"), CleanCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "completion"), CompletionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "doctor"), DoctorCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "preview"), PreviewCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "version"), VersionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "config"), ConfigCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "seo"), SeoCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "geo"), GeoCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "publish"), PublishCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "deploy"), DeployCommand.RunAsync),
        };
    }

    public static CommandDescriptor? ResolveDescriptor(IReadOnlyList<CommandDescriptor> descriptors, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach (var d in descriptors)
        {
            if (string.Equals(d.Spec.Name, name, StringComparison.OrdinalIgnoreCase))
                return d;

            if (d.Spec.Aliases is not null)
            {
                foreach (var alias in d.Spec.Aliases)
                {
                    if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
                        return d;
                }
            }
        }

        return null;
    }
}
