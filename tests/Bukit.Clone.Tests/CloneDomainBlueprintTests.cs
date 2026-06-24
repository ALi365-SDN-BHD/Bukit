using Bukit.Clone;
using Xunit;

namespace Bukit.Clone.Tests;

public sealed class CloneDomainBlueprintTests
{
    [Fact]
    public void Status_RemainsSkeleton()
    {
        var blueprint = new CloneDomainBlueprint();

        Assert.Equal(CloneDomainBlueprint.SkeletonStatus, blueprint.Status);
    }

    [Fact]
    public void Areas_DeclarePlannedDomainBoundariesWithoutMigratedLogic()
    {
        var blueprint = new CloneDomainBlueprint();

        Assert.Equal(
            [
                CloneDomainArea.Models,
                CloneDomainArea.Input,
                CloneDomainArea.Assets,
                CloneDomainArea.Generation,
                CloneDomainArea.Verification
            ],
            blueprint.Areas.Select(area => area.Area));
        Assert.All(blueprint.Areas, area => Assert.False(area.ContainsMigratedBusinessLogic));
    }
}
