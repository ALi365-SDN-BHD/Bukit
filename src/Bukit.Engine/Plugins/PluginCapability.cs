using System.Collections.ObjectModel;

namespace Bukit.Engine.Plugins;

public static class PluginCapability
{
    public const string EmitOutputs = "emit-outputs";
    public const string DerivePages = "derive-pages";

    private static readonly HashSet<string> _knownCapabilities = new(StringComparer.OrdinalIgnoreCase)
    {
        EmitOutputs,
        DerivePages
    };

    public static IReadOnlyCollection<string> AllCapabilities { get; } =
        new ReadOnlyCollection<string>(_knownCapabilities.ToList());

    public static bool IsKnown(string? capability)
    {
        return !string.IsNullOrWhiteSpace(capability) && _knownCapabilities.Contains(capability);
    }
}
