namespace Bukit.Clone;

public sealed class CloneDomainBlueprint : ICloneDomainBlueprint
{
    public const string SkeletonStatus = "skeleton";

    private static readonly CloneDomainAreaDescriptor[] DomainAreas =
    [
        new(CloneDomainArea.Models, "Models", ContainsMigratedBusinessLogic: false),
        new(CloneDomainArea.Input, "Input", ContainsMigratedBusinessLogic: false),
        new(CloneDomainArea.Assets, "Assets", ContainsMigratedBusinessLogic: false),
        new(CloneDomainArea.Generation, "Generation", ContainsMigratedBusinessLogic: false),
        new(CloneDomainArea.Verification, "Verification", ContainsMigratedBusinessLogic: false)
    ];

    public string Status => SkeletonStatus;

    public IReadOnlyList<CloneDomainAreaDescriptor> Areas => DomainAreas;
}
