namespace Bukit.Clone;

public interface ICloneDomainBlueprint
{
    string Status { get; }

    IReadOnlyList<CloneDomainAreaDescriptor> Areas { get; }
}
