using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Commands;

namespace Bukit.Cli;

public static class BukitCliDescriptors
{
    public static IReadOnlyList<CommandDescriptor> CreateDescriptors()
        => CreateDescriptors("text");

    internal static IReadOnlyList<CommandDescriptor> CreateDescriptors(string logFormat)
    {
        var registry = BukitCliSpecs.CreateRegistry();

        return new CommandDescriptor[]
        {
            new(registry.Commands.First(c => c.Name == "build"), cmd => BuildCommand.RunAsync(cmd, cmd.GetString("--log-format") ?? logFormat)),
            new(registry.Commands.First(c => c.Name == "clean"), cmd => CleanCommand.RunAsync(cmd, logFormat)),
            new(registry.Commands.First(c => c.Name == "completion"), CompletionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "doctor"), cmd => DoctorCommand.RunAsync(cmd, logFormat)),
            new(registry.Commands.First(c => c.Name == "preview"), cmd => PreviewCommand.RunAsync(cmd, logFormat)),
            new(registry.Commands.First(c => c.Name == "dev"), cmd => DevCommand.RunAsync(cmd, logFormat)),
            new(registry.Commands.First(c => c.Name == "version"), VersionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "config"), cmd => ConfigCommand.RunAsync(cmd, logFormat)),
            new(registry.Commands.First(c => c.Name == "seo"), SeoCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "geo"), GeoCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "publish"), PublishCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "deploy"), cmd => DeployCommand.RunAsync(cmd, logFormat)),
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
