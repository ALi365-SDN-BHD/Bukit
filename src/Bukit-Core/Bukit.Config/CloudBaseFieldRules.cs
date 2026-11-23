namespace Bukit.Config;

internal static class CloudBaseFieldRules
{
    /// <summary>Personal and operator columns are never projected, whatever the field policy says (D7).</summary>
    internal static readonly HashSet<string> HardExcluded = new(StringComparer.OrdinalIgnoreCase)
    {
        "user_id", "owner", "created_by", "updated_by", "_openid"
    };

    internal static bool IsHardExcluded(string name) => HardExcluded.Contains(name);
}
