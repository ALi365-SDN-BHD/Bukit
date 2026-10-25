namespace Bukit.Engine.Abstractions.Plugins;

/// <summary>
/// A named, typed slot in <see cref="BuildContext"/> shared data.
/// Names are compared case-insensitively and must be unique per value type.
/// </summary>
/// <typeparam name="T">The value type stored under this key.</typeparam>
public sealed class BuildDataKey<T>
    where T : notnull
{
    public BuildDataKey(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>The storage name. Names without a <c>__</c> prefix are exposed to templates as <c>site.data.&lt;name&gt;</c>.</summary>
    public string Name { get; }

    public override string ToString() => $"{Name} ({typeof(T).Name})";
}
