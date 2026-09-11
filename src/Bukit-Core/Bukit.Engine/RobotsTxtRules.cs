using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Engine;

internal sealed class RobotsTxtRules
{
    private readonly List<(List<string> Agents, List<(bool Allow, int Length, Regex Matcher)> Rules)> _groups = [];

    internal RobotsTxtRules(string? text)
    {
        List<string>? agents = null;
        List<(bool Allow, int Length, Regex Matcher)>? rules = null;
        var hasRules = false;
        foreach (var rawLine in (text ?? string.Empty).Split('\n'))
        {
            var line = rawLine.Split('#', 2)[0].Trim();
            var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (key.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (agents is null || hasRules)
                {
                    agents = [];
                    rules = [];
                    _groups.Add((agents, rules));
                    hasRules = false;
                }
                agents.Add(value);
                continue;
            }

            var allow = key.Equals("Allow", StringComparison.OrdinalIgnoreCase);
            if (rules is null || !allow && !key.Equals("Disallow", StringComparison.OrdinalIgnoreCase)) continue;
            hasRules = true;
            if (!value.StartsWith('/')) continue;
            var endAnchor = value.EndsWith('$');
            var pattern = NormalizePath(endAnchor ? value[..^1] : value);
            var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal) + (endAnchor ? "\\z" : string.Empty);
            rules.Add((allow, pattern.Length + (endAnchor ? 1 : 0), new Regex(expression, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)));
        }
    }

    internal bool IsBlocked(string userAgent, string routeUrl)
    {
        var groups = _groups.Where(group => group.Agents.Contains(userAgent, StringComparer.OrdinalIgnoreCase)).ToArray();
        if (groups.Length == 0)
            groups = _groups.Where(group => group.Agents.Contains("*", StringComparer.Ordinal)).ToArray();

        var path = NormalizePath(string.IsNullOrWhiteSpace(routeUrl) ? "/" : routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl);
        var longest = -1;
        var allowed = true;
        foreach (var rule in groups.SelectMany(group => group.Rules))
        {
            if (rule.Length < longest || !rule.Matcher.IsMatch(path)) continue;
            allowed = rule.Length == longest ? allowed || rule.Allow : rule.Allow;
            longest = rule.Length;
        }
        return !allowed;
    }

    private static string NormalizePath(string path)
    {
        var result = new StringBuilder(path.Length);
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '%' && i + 2 < path.Length &&
                byte.TryParse(path.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                var character = (char)value;
                if (char.IsAsciiLetterOrDigit(character) || character is '-' or '.' or '_' or '~') result.Append(character);
                else result.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
                i += 2;
            }
            else if (path[i] > 0x7e || path[i] <= 0x20)
            {
                var length = char.IsSurrogatePair(path, i) ? 2 : 1;
                result.Append(Uri.EscapeDataString(path.Substring(i, length)));
                i += length - 1;
            }
            else result.Append(path[i]);
        }
        return result.ToString();
    }
}
