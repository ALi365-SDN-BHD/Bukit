using System.Net;

namespace Bukit.Engine;

internal static class HtmlTagAttributeReader
{
    internal static string? GetAttribute(string tag, string name)
    {
        var index = 1;
        while (index < tag.Length)
        {
            while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] != '>')
            {
                index++;
            }

            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length || tag[index] is '>' or '/')
            {
                return null;
            }

            var attrStart = index;
            while (index < tag.Length && (char.IsLetterOrDigit(tag[index]) || tag[index] is '-' or '_' or ':'))
            {
                index++;
            }

            if (attrStart == index)
            {
                index++;
                continue;
            }

            var attrName = tag[attrStart..index];
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            if (index >= tag.Length || tag[index] != '=')
            {
                if (string.Equals(attrName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                continue;
            }

            index++;
            while (index < tag.Length && char.IsWhiteSpace(tag[index]))
            {
                index++;
            }

            string value;
            if (index < tag.Length && tag[index] is '"' or '\'')
            {
                var quote = tag[index++];
                var valueStart = index;
                while (index < tag.Length && tag[index] != quote)
                {
                    index++;
                }

                value = tag[valueStart..Math.Min(index, tag.Length)];
                if (index < tag.Length)
                {
                    index++;
                }
            }
            else
            {
                var valueStart = index;
                while (index < tag.Length && !char.IsWhiteSpace(tag[index]) && tag[index] != '>')
                {
                    index++;
                }

                value = tag[valueStart..index];
            }

            if (string.Equals(attrName, name, StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.HtmlDecode(value);
            }
        }

        return null;
    }
}
