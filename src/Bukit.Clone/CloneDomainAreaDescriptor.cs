namespace Bukit.Clone;

public sealed record CloneDomainAreaDescriptor(
    CloneDomainArea Area,
    string DisplayName,
    bool ContainsMigratedBusinessLogic);
