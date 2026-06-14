using Xunit;

namespace Bukit.Labs.Cli.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntentApplierCollection
{
    public const string Name = "IntentApplier serial";
}
