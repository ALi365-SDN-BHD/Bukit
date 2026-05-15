using System.Globalization;
using System.Text.Json.Nodes;

namespace Bukit.Engine.Plugins.Protocol;

internal static class ProtocolJsonHelper
{
    public static JsonNode? ToJsonNode(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            return node.DeepClone();
        }

        if (value is IReadOnlyDictionary<string, object> readOnlyMap)
        {
            return ToJsonObject(readOnlyMap);
        }

        if (value is IDictionary<string, object> map)
        {
            return ToJsonObject(map);
        }

        if (value is IEnumerable<object> sequence && value is not string)
        {
            var array = new JsonArray();
            foreach (var item in sequence)
            {
                array.Add(ToJsonNode(item));
            }

            return array;
        }

        return value switch
        {
            string text => JsonValue.Create(text),
            bool boolean => JsonValue.Create(boolean),
            byte number => JsonValue.Create(number),
            sbyte number => JsonValue.Create(number),
            short number => JsonValue.Create(number),
            ushort number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            uint number => JsonValue.Create(number),
            long number => JsonValue.Create(number),
            ulong number => JsonValue.Create(number),
            float number => JsonValue.Create(number),
            double number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            DateTime dateTime => JsonValue.Create(dateTime),
            DateTimeOffset dateTimeOffset => JsonValue.Create(dateTimeOffset),
            Guid guid => JsonValue.Create(guid),
            Enum enumValue => JsonValue.Create(Convert.ToString(enumValue, CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value.ToString())
        };
    }

    public static JsonObject ToJsonObject(IEnumerable<KeyValuePair<string, object>> map)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in map)
        {
            obj[key] = ToJsonNode(value);
        }

        return obj;
    }
}
