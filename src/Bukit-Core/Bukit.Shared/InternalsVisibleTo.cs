using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Bukit.Shared.Tests")]
[assembly: InternalsVisibleTo("Bukit.Content.Tests")]
[assembly: InternalsVisibleTo("Bukit.Content")]
[assembly: InternalsVisibleTo("Bukit.Engine")]
[assembly: InternalsVisibleTo("Bukit.Engine.Assets")]
[assembly: InternalsVisibleTo("Bukit.Engine.Tests")]
[assembly: InternalsVisibleTo("Bukit.Rendering")]
// Secret store (--secrets-stdin): read by the CLI and masked by the plugin host.
[assembly: InternalsVisibleTo("bukit")]
[assembly: InternalsVisibleTo("Bukit.PluginHost")]
[assembly: InternalsVisibleTo("Bukit.Cli.Tests")]
[assembly: InternalsVisibleTo("Bukit.PluginHost.Tests")]
