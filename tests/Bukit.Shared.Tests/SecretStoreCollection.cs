using Xunit;

namespace Bukit.Shared.Tests;

// SecretStore is process-wide and ConsoleLogger redacts through it; tests that install
// secrets, read NOTION_TOKEN, or swap Console.Error must not run in parallel.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SecretStoreCollection
{
    public const string Name = "SecretStore";
}
