using Bukit.Engine.Abstractions.Plugins.Protocol;
using System.Globalization;

namespace Bukit.Engine.Plugins.Protocol;

using Bukit.Engine.Abstractions.Plugins;
internal static class ProcessArgumentsBuilder
{
    internal static string? Build(IReadOnlyDictionary<string, object>? options)
    {
        if (options is null)
        {
            return null;
        }

        if (options.TryGetValue("arguments", out var legacy) && legacy is not null)
        {
            throw new InvalidOperationException("options.arguments is not allowed. Use options.processArgs.");
        }

        if (!options.TryGetValue("processArgs", out var processArgsObj) || processArgsObj is null)
        {
            return null;
        }

        var processArgs = AsObjectMap(processArgsObj)
            ?? throw new InvalidOperationException("options.processArgs must be a mapping.");
        var parts = new List<string>();

        if (processArgs.TryGetValue("positionals", out var positionalsObj) && positionalsObj is not null)
        {
            if (positionalsObj is string || positionalsObj is not IEnumerable<object> positionals)
            {
                throw new InvalidOperationException("options.processArgs.positionals must be a sequence.");
            }

            foreach (var item in positionals)
            {
                if (item is null)
                {
                    throw new InvalidOperationException("options.processArgs.positionals must not contain null.");
                }

                parts.Add(Quote(ConvertToText(item)));
            }
        }

        if (processArgs.TryGetValue("named", out var namedObj) && namedObj is not null)
        {
            var named = AsObjectMap(namedObj)
                ?? throw new InvalidOperationException("options.processArgs.named must be a mapping.");
            foreach (var (key, value) in named.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (value is null)
                {
                    continue;
                }

                if (value is bool b)
                {
                    if (b)
                    {
                        parts.Add($"--{key}");
                    }

                    continue;
                }

                parts.Add($"--{key}");
                parts.Add(Quote(ConvertToText(value)));
            }
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static IReadOnlyDictionary<string, object>? AsObjectMap(object value)
    {
        if (value is IReadOnlyDictionary<string, object> readOnlyMap)
        {
            return readOnlyMap;
        }

        if (value is IDictionary<string, object> map)
        {
            return new Dictionary<string, object>(map, StringComparer.OrdinalIgnoreCase);
        }

        return null;
    }

    private static string ConvertToText(object value)
    {
        return value switch
        {
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string Quote(string text)
    {
        if (text.Length == 0)
        {
            return "\"\"";
        }

        if (text.IndexOfAny([' ', '\t', '"']) < 0)
        {
            return text;
        }

        return "\"" + text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }
}
