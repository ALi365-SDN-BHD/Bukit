using YamlDotNet.Serialization;

namespace Bukit.Importing;

/// <summary>
/// Single place that turns generated data into YAML. Hand-built YAML produced
/// unquoted keys and broke Core's fail-closed front matter parser.
/// </summary>
internal static class YamlEmitter
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    internal static string Emit(IEnumerable<KeyValuePair<string, object?>> fields)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in fields)
        {
            if (value is not null)
                map[key] = value;
        }

        return Serializer.Serialize(map);
    }
}
