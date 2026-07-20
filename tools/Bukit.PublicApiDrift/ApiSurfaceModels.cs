namespace Bukit.PublicApiDrift;

internal sealed record ApiBaseline(
    string Schema,
    int SchemaVersion,
    string TargetFramework,
    string SdkPolicy,
    IReadOnlyList<ApiAssembly> Assemblies,
    IReadOnlyList<ApiType> Types);

internal sealed record ApiAssembly(string Assembly, string Project);

internal sealed record ApiType(
    string Assembly,
    string Name,
    string Owner,
    string Classification,
    string Compatibility,
    string MigrationHorizon,
    string Signature,
    IReadOnlyList<string> PublicMembers,
    IReadOnlyList<string> ProtectedMembers);

internal sealed record DriftDiagnostic(string Category, string Assembly, string TypeName, string Detail)
{
    public override string ToString() => $"{Category}: {Assembly}::{TypeName}: {Detail}";
}

internal enum BaselineValidationMode { Committed, Candidate }

internal static class ApiPolicy
{
    public const string Schema = "bukit-core-public-api-baseline-v1";
    public static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
    {
        "aot-serialization-surface", "cross-assembly-implementation", "implementation-public",
        "persisted-internal-format", "plugin-wire-contract", "serialized-contract"
    };
    public static readonly HashSet<string> Compatibility = new(StringComparer.Ordinal)
    {
        "1.x-do-not-narrow", "1.x-migration-safe", "1.x-shape-stable",
        "2.0-candidate", "not-a-clr-contract"
    };
}
