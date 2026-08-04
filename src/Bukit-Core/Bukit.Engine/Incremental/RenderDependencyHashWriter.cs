using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

internal sealed class RenderDependencyHashWriter
{
    private const int MaxDepth = 64;
    private const int MaxNodes = 100_000;

    private static readonly byte[] s_newline = [(byte)'\n'];
    private readonly IncrementalHash _hasher;
    private readonly HashSet<object> _activeContainers = new(ReferenceEqualityComparer.Instance);
    private int _depth;
    private int _nodeCount;

    internal RenderDependencyHashWriter(IncrementalHash hasher)
    {
        _hasher = hasher;
    }

    internal void AppendUtf8(string? value) => IncrementalBuildEngine.AppendUtf8(_hasher, value);

    internal void AppendNewline() => _hasher.AppendData(s_newline);

    internal void AppendFramedValue(string label, string? value)
    {
        AppendUtf8(label);
        AppendUtf8(":");
        var byteLength = value is null ? -1 : Encoding.UTF8.GetByteCount(value);
        AppendUtf8(byteLength.ToString(CultureInfo.InvariantCulture));
        AppendUtf8(":");
        if (value is not null)
        {
            AppendUtf8(value);
        }
    }

    internal void AppendDictionary(IReadOnlyDictionary<string, object>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            return;
        }

        foreach (var entry in dictionary.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            AppendNewline();
            AppendUtf8(entry.Key);
            AppendNewline();
            AppendCanonicalValue(entry.Value);
        }
    }

    internal void AppendObjectValue(object? value) => AppendCanonicalValue(value);

    /// <summary>
    /// Canonical render dependency encoder. Every value carries an explicit type tag,
    /// scalars are formatted with <see cref="CultureInfo.InvariantCulture"/>, map keys
    /// use Ordinal ordering, and containers are protected by active-reference cycle
    /// detection, a depth budget and a node budget. Unsupported values fail closed.
    /// </summary>
    internal void AppendCanonicalValue(object? value)
    {
        _nodeCount++;
        if (_nodeCount > MaxNodes)
        {
            throw new InvalidOperationException(
                $"Render dependency value graph exceeds the {MaxNodes.ToString(CultureInfo.InvariantCulture)} node budget.");
        }

        if (_depth > MaxDepth)
        {
            throw new InvalidOperationException(
                $"Render dependency value graph exceeds the maximum depth of {MaxDepth.ToString(CultureInfo.InvariantCulture)}.");
        }

        switch (value)
        {
            case null:
                AppendUtf8("null;");
                return;
            case string text:
                AppendFramedScalar("string", text);
                return;
            case bool boolean:
                AppendUtf8(boolean ? "bool;1" : "bool;0");
                return;
            case sbyte or byte or short or ushort or int or long:
                AppendUtf8("int64;");
                AppendUtf8(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                return;
            case uint or ulong:
                AppendUtf8("uint64;");
                AppendUtf8(((IFormattable)value).ToString(format: null, CultureInfo.InvariantCulture));
                return;
            case decimal number:
                AppendUtf8("decimal;");
                AppendUtf8(number.ToString(CultureInfo.InvariantCulture));
                return;
            case double real:
                AppendUtf8("double;");
                AppendUtf8(real.ToString("R", CultureInfo.InvariantCulture));
                return;
            case float real:
                AppendUtf8("double;");
                AppendUtf8(real.ToString("R", CultureInfo.InvariantCulture));
                return;
            case DateTimeOffset timestamp:
                AppendUtf8("date;");
                AppendUtf8(timestamp.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
                return;
            case DateTime timestamp:
                AppendUtf8("date;");
                AppendUtf8(ToUtcCanonical(timestamp));
                return;
            case ContentField field:
                AppendUtf8("content-field;");
                AppendCanonicalValue(field.Type);
                AppendUtf8("|");
                AppendCanonicalValue(field.Value);
                return;
            case TableOfContentsEntry entry:
                AppendUtf8("toc-entry;");
                AppendCanonicalValue(entry.Level);
                AppendUtf8("|");
                AppendCanonicalValue(entry.Text);
                AppendUtf8("|");
                AppendCanonicalValue(entry.Id);
                return;
            case ModuleInfo module:
                AppendCanonicalValue(new Dictionary<string, object?>
                {
                    ["__record"] = "module-info",
                    ["id"] = module.Id,
                    ["title"] = module.Title,
                    ["slug"] = module.Slug,
                    ["content"] = module.Content,
                    ["fields"] = module.Fields
                });
                return;
            case PageInfo page:
                AppendCanonicalValue(new Dictionary<string, object?>
                {
                    ["__record"] = "page-info",
                    ["title"] = page.Title,
                    ["url"] = page.Url,
                    ["content"] = page.Content,
                    ["summary"] = page.Summary,
                    ["publishDate"] = page.PublishDate,
                    ["updatedAt"] = page.UpdatedAt,
                    ["fields"] = page.Fields,
                    ["tableOfContents"] = page.TableOfContents
                });
                return;
            case IReadOnlyDictionary<string, object> map:
                AppendCanonicalStringMap(map, map.Count, map.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => (x.Key, (object?)x.Value)));
                return;
            case IReadOnlyDictionary<string, ContentField> contentFields:
                AppendCanonicalStringMap(
                    contentFields,
                    contentFields.Count,
                    contentFields.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => (x.Key, (object?)x.Value)));
                return;
            case IEnumerable sequence:
                AppendUtf8("seq;");
                EnterContainer(sequence);
                try
                {
                    foreach (var element in sequence)
                    {
                        AppendCanonicalValue(element);
                    }
                }
                finally
                {
                    ExitContainer(sequence);
                }

                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported render dependency value type '{value.GetType().FullName}'.");
        }
    }

    private void AppendCanonicalStringMap(object container, int count, IEnumerable<(string Key, object? Value)> orderedEntries)
    {
        AppendUtf8("map;");
        AppendUtf8(count.ToString(CultureInfo.InvariantCulture));
        AppendUtf8(":");

        // Materialize before container entry so enumeration order is fixed by Ordinal keys.
        var entries = orderedEntries.ToList();
        EnterContainer(container);
        try
        {
            foreach (var (key, entryValue) in entries)
            {
                AppendFramedScalar("key", key);
                AppendCanonicalValue(entryValue);
            }
        }
        finally
        {
            ExitContainer(container);
        }
    }

    private void AppendFramedScalar(string tag, string text)
    {
        AppendUtf8(tag);
        AppendUtf8(":");
        AppendUtf8(Encoding.UTF8.GetByteCount(text).ToString(CultureInfo.InvariantCulture));
        AppendUtf8(":");
        AppendUtf8(text);
    }

    private static string ToUtcCanonical(DateTime timestamp)
    {
        var utc = timestamp.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
            : timestamp.ToUniversalTime();
        return utc.ToString("o", CultureInfo.InvariantCulture);
    }

    private void EnterContainer(object container)
    {
        if (!_activeContainers.Add(container))
        {
            throw new InvalidOperationException("Detected a render dependency value cycle.");
        }

        _depth++;
    }

    private void ExitContainer(object container)
    {
        _activeContainers.Remove(container);
        _depth--;
    }
}
