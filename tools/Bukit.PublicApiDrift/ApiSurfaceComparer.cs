namespace Bukit.PublicApiDrift;

internal static class ApiSurfaceComparer
{
    public static IReadOnlyList<DriftDiagnostic> Compare(ApiBaseline baseline, ApiBaseline current)
    {
        var diagnostics = new List<DriftDiagnostic>();
        var oldTypes = baseline.Types.ToDictionary(TypeKey, StringComparer.Ordinal);
        var newTypes = current.Types.ToDictionary(TypeKey, StringComparer.Ordinal);

        foreach (var key in oldTypes.Keys.Except(newTypes.Keys, StringComparer.Ordinal))
        {
            var type = oldTypes[key];
            Add(diagnostics, type, "breaking", "exported type removed");
            AddWholeTypeReview(diagnostics, type, "removed");
        }

        foreach (var key in newTypes.Keys.Except(oldTypes.Keys, StringComparer.Ordinal))
        {
            var type = newTypes[key];
            Add(diagnostics, type, "review-required", "exported type added");
            if (!ApiPolicy.Classifications.Contains(type.Classification))
                Add(diagnostics, type, "unclassified", "new type requires approved classification");
            AddWholeTypeReview(diagnostics, type, "added");
        }

        foreach (var key in oldTypes.Keys.Intersect(newTypes.Keys, StringComparer.Ordinal))
            CompareType(oldTypes[key], newTypes[key], diagnostics);

        return diagnostics.OrderBy(static item => item.Category, StringComparer.Ordinal)
            .ThenBy(static item => item.Assembly, StringComparer.Ordinal)
            .ThenBy(static item => item.TypeName, StringComparer.Ordinal)
            .ThenBy(static item => item.Detail, StringComparer.Ordinal)
            .ToArray();
    }

    private static void CompareType(ApiType baseline, ApiType current, List<DriftDiagnostic> diagnostics)
    {
        var changed = false;
        changed |= CompareMetadata(baseline, current, diagnostics, "owner", baseline.Owner, current.Owner);
        changed |= CompareMetadata(baseline, current, diagnostics, "classification", baseline.Classification, current.Classification);
        changed |= CompareMetadata(baseline, current, diagnostics, "compatibility", baseline.Compatibility, current.Compatibility);
        changed |= CompareMetadata(baseline, current, diagnostics, "migrationHorizon", baseline.MigrationHorizon, current.MigrationHorizon);

        if (!StringComparer.Ordinal.Equals(baseline.Signature, current.Signature))
        {
            Add(diagnostics, current, "type-shape-review", $"type signature changed: {baseline.Signature} -> {current.Signature}");
            changed = true;
        }

        changed |= CompareMembers(baseline, current, baseline.PublicMembers, current.PublicMembers, "public member", "breaking", "review-required", diagnostics);
        changed |= CompareMembers(baseline, current, baseline.ProtectedMembers, current.ProtectedMembers, "protected member", "protected-review", "protected-review", diagnostics);

        if (!changed) return;
        if (IsContractSurface(baseline) || IsContractSurface(current))
            Add(diagnostics, current, "contract-shape-review", "contract-classified type changed");
        if (IsAotSurface(baseline) || IsAotSurface(current))
            Add(diagnostics, current, "aot-review", "AOT serialization surface changed");
    }

    private static bool CompareMetadata(ApiType baseline, ApiType current, List<DriftDiagnostic> diagnostics, string name, string oldValue, string newValue)
    {
        if (StringComparer.Ordinal.Equals(oldValue, newValue)) return false;
        Add(diagnostics, current, "review-required", $"{name} changed: {oldValue} -> {newValue}");
        return true;
    }

    private static bool CompareMembers(ApiType baseline, ApiType current, IReadOnlyList<string> oldMembers, IReadOnlyList<string> newMembers, string memberKind, string removedCategory, string addedCategory, List<DriftDiagnostic> diagnostics)
    {
        var changed = false;
        foreach (var member in oldMembers.Except(newMembers, StringComparer.Ordinal))
        {
            Add(diagnostics, current, removedCategory, $"{memberKind} removed: {member}");
            changed = true;
        }
        foreach (var member in newMembers.Except(oldMembers, StringComparer.Ordinal))
        {
            Add(diagnostics, current, addedCategory, $"{memberKind} added: {member}");
            changed = true;
        }
        return changed;
    }

    private static bool IsContractSurface(ApiType type) =>
        StringComparer.Ordinal.Equals(type.Classification, "plugin-wire-contract") ||
        StringComparer.Ordinal.Equals(type.Classification, "serialized-contract");

    private static bool IsAotSurface(ApiType type) =>
        StringComparer.Ordinal.Equals(type.Classification, "aot-serialization-surface");

    private static void AddWholeTypeReview(List<DriftDiagnostic> diagnostics, ApiType type, string change)
    {
        if (IsContractSurface(type))
            Add(diagnostics, type, "contract-shape-review", $"contract-classified type {change}");
        if (IsAotSurface(type))
            Add(diagnostics, type, "aot-review", $"AOT serialization type {change}");
    }

    private static string TypeKey(ApiType type) => $"{type.Assembly}\u0000{type.Name}";
    private static void Add(List<DriftDiagnostic> items, ApiType type, string category, string detail) =>
        items.Add(new(category, type.Assembly, type.Name, detail));
}
