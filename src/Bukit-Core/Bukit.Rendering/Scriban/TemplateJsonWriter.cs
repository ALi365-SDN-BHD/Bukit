using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Serializes template values to JSON without reflection, so it works under Native AOT.
/// Strings escape <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, U+2028 and U+2029 in addition to the
/// characters JSON requires, so the output is safe inside an HTML <c>&lt;script&gt;</c> element.
/// Object keys are written in ordinal order so build output is byte-for-byte deterministic.
/// </summary>
internal static class TemplateJsonWriter
{
    /// <summary>Nesting deeper than this is treated as a reference cycle.</summary>
    internal const int MaxDepth = 64;

    internal static string Serialize(object? value)
    {
        var sb = new StringBuilder();
        WriteValue(sb, value, 0);
        return sb.ToString();
    }

    internal static string SerializeString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        WriteString(sb, value);
        return sb.ToString();
    }

    private static void WriteValue(StringBuilder sb, object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new InvalidOperationException($"JSON value is nested deeper than {MaxDepth} levels; the value may contain a reference cycle.");
        }

        switch (value)
        {
            case null:
                sb.Append("null");
                return;
            case string s:
                WriteString(sb, s);
                return;
            case bool b:
                sb.Append(b ? "true" : "false");
                return;
            case char c:
                WriteString(sb, c.ToString());
                return;
            case sbyte or byte or short or ushort or int or uint or long or ulong or BigInteger or Int128 or UInt128:
                sb.Append(((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
                return;
            case decimal m:
                sb.Append(m.ToString(CultureInfo.InvariantCulture));
                return;
            case double d:
                WriteFloat(sb, d, double.IsFinite(d));
                return;
            // float and Half are formatted as themselves: widening to double adds digits (1.1f -> 1.100000023841858).
            case float f:
                WriteFloat(sb, f, float.IsFinite(f));
                return;
            case Half h:
                WriteFloat(sb, h, Half.IsFinite(h));
                return;
            case DateTimeOffset dto:
                WriteString(sb, dto.ToString("O", CultureInfo.InvariantCulture));
                return;
            case DateTime dt:
                WriteString(sb, dt.ToString("O", CultureInfo.InvariantCulture));
                return;
            case DateOnly date:
                WriteString(sb, date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                return;
            case TimeSpan ts:
                WriteString(sb, ts.ToString("c", CultureInfo.InvariantCulture));
                return;
            case Enum e:
                WriteString(sb, e.ToString());
                return;
            // Template functions (util.*, comp.render, ...) have no data; never publish their .NET type names.
            case IScriptCustomFunction:
                sb.Append("null");
                return;
            // ScriptObject's non-generic IDictionary view yields internal value wrappers; use its typed view.
            case ScriptObject obj:
                WriteObject(sb, obj.Select(pair => (pair.Key, (object?)pair.Value)), depth);
                return;
            case IDictionary dictionary:
                WriteObject(sb, EnumerateDictionary(dictionary), depth);
                return;
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                WriteObject(sb, pairs.Select(pair => (pair.Key, pair.Value)), depth);
                return;
            // KeyValuePair is not covariant: string-valued maps that are not IDictionary need their own case.
            // Other non-IDictionary maps serialize as arrays, because detecting KeyValuePair<,> needs reflection.
            case IEnumerable<KeyValuePair<string, string?>> stringPairs:
                WriteObject(sb, stringPairs.Select(pair => (pair.Key, (object?)pair.Value)), depth);
                return;
            case IEnumerable sequence:
                WriteArray(sb, sequence, depth);
                return;
            case IFormattable formattable:
                WriteString(sb, formattable.ToString(null, CultureInfo.InvariantCulture));
                return;
            default:
                WriteString(sb, value.ToString() ?? string.Empty);
                return;
        }
    }

    private static IEnumerable<(string Key, object? Value)> EnumerateDictionary(IDictionary dictionary)
    {
        foreach (DictionaryEntry entry in dictionary)
        {
            yield return (Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, entry.Value);
        }
    }

    private static void WriteObject(StringBuilder sb, IEnumerable<(string Key, object? Value)> members, int depth)
    {
        // OrderBy is stable: of keys that stringify alike (an IDictionary holding 1 and "1"), the first
        // in enumeration order wins and later ones are skipped, so the object never repeats a key.
        string? previousKey = null;
        sb.Append('{');
        foreach (var (key, value) in members.OrderBy(static member => member.Key, StringComparer.Ordinal))
        {
            if (previousKey is not null)
            {
                if (string.Equals(key, previousKey, StringComparison.Ordinal))
                {
                    continue;
                }

                sb.Append(',');
            }

            previousKey = key;
            WriteString(sb, key);
            sb.Append(':');
            WriteValue(sb, value, depth + 1);
        }

        sb.Append('}');
    }

    private static void WriteArray(StringBuilder sb, IEnumerable sequence, int depth)
    {
        sb.Append('[');
        var first = true;
        foreach (var item in sequence)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;
            WriteValue(sb, item, depth + 1);
        }

        sb.Append(']');
    }

    // JSON has no NaN or Infinity literals, so non-finite numbers are written as null.
    // The default format of floating-point types is the shortest round-trippable form.
    private static void WriteFloat(StringBuilder sb, IFormattable value, bool isFinite)
    {
        if (isFinite)
        {
            sb.Append(value.ToString(null, CultureInfo.InvariantCulture));
        }
        else
        {
            sb.Append("null");
        }
    }

    private static void WriteString(StringBuilder sb, string value)
    {
        sb.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var ch = value[i];
            switch (ch)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                // HTML-sensitive characters: "</script>" or "<!--" in a value must not end or alter the script element.
                case '<' or '>' or '&' or '\u2028' or '\u2029':
                    AppendUnicodeEscape(sb, ch);
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        AppendUnicodeEscape(sb, ch);
                    }
                    else if (char.IsHighSurrogate(ch) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    {
                        sb.Append(ch).Append(value[++i]);
                    }
                    else if (char.IsSurrogate(ch))
                    {
                        // A lone surrogate cannot be encoded as UTF-8; the escape keeps the JSON text valid.
                        AppendUnicodeEscape(sb, ch);
                    }
                    else
                    {
                        sb.Append(ch);
                    }

                    break;
            }
        }

        sb.Append('"');
    }

    private static void AppendUnicodeEscape(StringBuilder sb, char ch)
        => sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
}
