using System.Text.Json;

namespace Bukit.Engine.Abstractions.Plugins.Protocol;

internal static class JsonElementMaterializer
{
    internal static object? Materialize(object? value)
    {
        if (value is JsonElement element)
        {
            return MaterializeElement(element);
        }

        if (value is IReadOnlyList<JsonElement> jsonElementList)
        {
            var list = new List<object>(jsonElementList.Count);
            foreach (var item in jsonElementList)
            {
                list.Add(MaterializeElement(item)!);
            }

            return list;
        }

        return value;
    }

    internal static IReadOnlyDictionary<string, object>? Materialize(IReadOnlyDictionary<string, object>? dict)
    {
        if (dict is null)
        {
            return null;
        }

        var changed = false;
        var result = new Dictionary<string, object>(dict.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in dict)
        {
            if (value is JsonElement)
            {
                changed = true;
                result[key] = Materialize(value)!;
            }
            else if (value is IReadOnlyList<JsonElement>)
            {
                changed = true;
                result[key] = Materialize(value)!;
            }
            else
            {
                result[key] = value;
            }
        }

        return changed ? result : dict;
    }

    private static object? MaterializeElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Number => MaterializeNumber(element),
            JsonValueKind.Array => MaterializeArray(element),
            JsonValueKind.Object => MaterializeObject(element),
            _ => element.GetRawText()
        };
    }

    private static object MaterializeNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var l))
        {
            return l;
        }

        return element.GetDouble();
    }

    private static List<object> MaterializeArray(JsonElement element)
    {
        var list = new List<object>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(MaterializeElement(item)!);
        }

        return list;
    }

    private static Dictionary<string, object> MaterializeObject(JsonElement element)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = MaterializeElement(property.Value)!;
        }

        return dict;
    }
}
