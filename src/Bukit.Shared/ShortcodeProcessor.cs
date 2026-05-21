using System.Text.RegularExpressions;

namespace Bukit.Shared;

public static partial class ShortcodeProcessor
{
    public static string RenderShortcodes(string html, IReadOnlyDictionary<string, string>? shortcodeTemplates)
    {
        if (shortcodeTemplates is null || shortcodeTemplates.Count == 0 || string.IsNullOrWhiteSpace(html))
        {
            return html;
        }

        return ShortcodeRegex().Replace(html, match =>
        {
            var name = match.Groups[1].Value.Trim();
            if (!shortcodeTemplates.TryGetValue(name, out var template))
            {
                return match.Value;
            }

            var rawArgs = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);
            var args = ParseShortcodeArgs(rawArgs);
            return ApplyShortcodeTemplate(template, args);
        });
    }

    public static string RenderShortcode(string name, IReadOnlyDictionary<string, string>? shortcodeTemplates, params string[] positionalArgs)
    {
        if (shortcodeTemplates is null || !shortcodeTemplates.TryGetValue(name, out var template))
        {
            return string.Empty;
        }

        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < positionalArgs.Length; i++)
        {
            args[$"${i + 1}"] = positionalArgs[i];
        }

        return ApplyShortcodeTemplate(template, args);
    }

    private static string ApplyShortcodeTemplate(string template, IReadOnlyDictionary<string, string> args)
    {
        return ShortcodeArgRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return args.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    internal static Dictionary<string, string> ParseShortcodeArgs(string rawArgs)
    {
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = ShortcodeArgParseRegex().Matches(rawArgs);
        for (var i = 0; i < matches.Count; i++)
        {
            var value = matches[i].Groups[1].Success
                ? matches[i].Groups[1].Value
                : matches[i].Groups[2].Value;

            args[$"${i + 1}"] = value;
        }

        return args;
    }

    [GeneratedRegex(@"\{%\s*(\w[\w-]*)\s*(.*?)\s*%\}", RegexOptions.Singleline)]
    private static partial Regex ShortcodeRegex();

    [GeneratedRegex(@"\{\{\s*(\$\d+)\s*\}\}")]
    private static partial Regex ShortcodeArgRegex();

    [GeneratedRegex(@"""([^""]*)""|'([^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex ShortcodeArgParseRegex();
}
