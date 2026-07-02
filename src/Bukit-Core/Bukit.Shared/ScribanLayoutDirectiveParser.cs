namespace Bukit.Shared;

public static class ScribanLayoutDirectiveParser
{
    public static bool TryExtractLayoutDirective(string templateText, out string layoutTemplateRelativePath, out string bodyTemplateText)
    {
        layoutTemplateRelativePath = string.Empty;
        bodyTemplateText = templateText;

        var lines = templateText.ReplaceLineEndings("\n").Split('\n').ToList();

        var firstContentLineIndex = -1;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                firstContentLineIndex = i;
                break;
            }
        }

        if (firstContentLineIndex < 0)
        {
            return false;
        }

        var firstLine = lines[firstContentLineIndex].Trim();
        if (!TryParseLayoutLine(firstLine, out layoutTemplateRelativePath))
        {
            return false;
        }

        lines.RemoveAt(firstContentLineIndex);
        bodyTemplateText = string.Join('\n', lines);
        return true;
    }

    public static bool TryParseLayoutLine(string line, out string layoutTemplateRelativePath)
    {
        layoutTemplateRelativePath = string.Empty;

        if (TryParseDirective("{%", "%}", line, out var inner) || TryParseDirective("{{", "}}", line, out inner))
        {
            if (!inner.StartsWith("layout", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rest = inner["layout".Length..].Trim();
            if (TryExtractQuotedString(rest, out var path))
            {
                layoutTemplateRelativePath = path;
                return true;
            }
        }

        return false;
    }

    public static bool TryParseDirective(string open, string close, string line, out string inner)
    {
        inner = string.Empty;
        if (!line.StartsWith(open, StringComparison.Ordinal) || !line.EndsWith(close, StringComparison.Ordinal))
        {
            return false;
        }

        inner = line.Substring(open.Length, line.Length - open.Length - close.Length).Trim();
        return true;
    }

    public static bool TryExtractQuotedString(string text, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var q1 = text.IndexOf('"');
        var q2 = q1 < 0 ? text.IndexOf('\'') : -1;
        var quote = q1 >= 0 ? '"' : q2 >= 0 ? '\'' : '\0';
        var start = q1 >= 0 ? q1 : q2;
        if (start < 0)
        {
            return false;
        }

        var end = text.IndexOf(quote, start + 1);
        if (end <= start)
        {
            return false;
        }

        value = text.Substring(start + 1, end - start - 1);
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string NormalizePath(string templateRelativePath)
        => templateRelativePath.Replace('\\', '/');
}
