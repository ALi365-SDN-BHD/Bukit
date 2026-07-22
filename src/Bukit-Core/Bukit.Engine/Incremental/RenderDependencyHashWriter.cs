using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bukit.Engine.Incremental;

internal sealed class RenderDependencyHashWriter
{
    private static readonly byte[] s_newline = [(byte)'\n'];
    private readonly IncrementalHash _hasher;

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
            AppendObjectValue(entry.Value);
        }
    }

    internal void AppendObjectValue(object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string text)
        {
            AppendUtf8(text);
            return;
        }

        if (value is bool boolean)
        {
            AppendUtf8(boolean.ToString());
            return;
        }

        if (value is int or long or float or double or decimal)
        {
            AppendUtf8(value.ToString());
            return;
        }

        if (value is IReadOnlyDictionary<string, object> dictionary)
        {
            AppendDictionary(dictionary);
            return;
        }

        AppendUtf8(value.ToString() ?? string.Empty);
    }
}
