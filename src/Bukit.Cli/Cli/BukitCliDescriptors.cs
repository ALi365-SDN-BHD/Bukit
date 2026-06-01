using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Commands;
using Bukit.Cli.Commands.DocsCheck;

namespace Bukit.Cli;

public static class BukitCliDescriptors
{
    public static IReadOnlyList<CommandDescriptor> CreateDescriptors()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        var themeChildren = ThemeCommand.CreateDescriptors().ToList();

        return new CommandDescriptor[]
        {
            new(registry.Commands.First(c => c.Name == "build"), cmd => BuildCommand.RunAsync(cmd)),
            new(registry.Commands.First(c => c.Name == "clean"), CleanCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "clone"), CloneCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "completion"), CompletionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "deploy"), DeployCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "dev"), DevCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "doctor"), DoctorCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "init"), InitCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "lint"), LintCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "preview"), PreviewCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "version"), VersionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "docs"), DocsCheckCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "plugin"), PluginCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "config"), ConfigCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "seo"), SeoCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "geo"), GeoCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "data"), DataCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "route"), RouteCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "import"), ImportCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "notion"), NotionCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "template"), TemplateCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "intent"), IntentCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "visual"), VisualCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "webhook"), WebhookCommand.RunAsync),
            new(registry.Commands.First(c => c.Name == "theme"), null, themeChildren),
        };
    }

    public static CommandDescriptor? ResolveDescriptor(IReadOnlyList<CommandDescriptor> descriptors, string name)
    {
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
