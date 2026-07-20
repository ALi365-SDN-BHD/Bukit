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

internal enum BaselineValidationMode { Governed, Fixture, Candidate }

internal static class ApiPolicy
{
    public const string Schema = "bukit-core-public-api-baseline-v1";
    public static readonly IReadOnlyList<ApiAssembly> GovernedAssemblies =
    [
        new("Bukit.Cli.Shared", "src/Bukit-Core/Bukit.Cli.Shared/Bukit.Cli.Shared.csproj"),
        new("Bukit.Config", "src/Bukit-Core/Bukit.Config/Bukit.Config.csproj"),
        new("Bukit.Content", "src/Bukit-Core/Bukit.Content/Bukit.Content.csproj"),
        new("Bukit.Engine", "src/Bukit-Core/Bukit.Engine/Bukit.Engine.csproj"),
        new("Bukit.Engine.Abstractions", "src/Bukit-Core/Bukit.Engine.Abstractions/Bukit.Engine.Abstractions.csproj"),
        new("Bukit.Plugin.Abstractions", "src/Bukit-Core/Bukit.Plugin.Abstractions/Bukit.Plugin.Abstractions.csproj"),
        new("Bukit.PluginHost", "src/Bukit-Core/Bukit.PluginHost/Bukit.PluginHost.csproj"),
        new("Bukit.Rendering", "src/Bukit-Core/Bukit.Rendering/Bukit.Rendering.csproj"),
        new("Bukit.Routing", "src/Bukit-Core/Bukit.Routing/Bukit.Routing.csproj"),
        new("Bukit.Shared", "src/Bukit-Core/Bukit.Shared/Bukit.Shared.csproj"),
        new("Bukit.Theme", "src/Bukit-Core/Bukit.Theme/Bukit.Theme.csproj"),
        new("bukit", "src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj")
    ];
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
