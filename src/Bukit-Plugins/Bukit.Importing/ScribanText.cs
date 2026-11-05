using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>
/// Demo markup is literal text. Template delimiters in it (Vue/Angular "{{ }}",
/// "{% %}" directives) must print as-is instead of running when Core renders the theme.
/// </summary>
internal static partial class ScribanText
{
    /// <summary>Makes every template delimiter in demo text print literally.</summary>
    internal static string Escape(string text)
        => string.IsNullOrEmpty(text)
            ? text
            : DelimiterPattern().Replace(text, m => "{{ '" + m.Value + "' }}");

    [GeneratedRegex(@"\{\{|\}\}|\{%|%\}")]
    private static partial Regex DelimiterPattern();
}
