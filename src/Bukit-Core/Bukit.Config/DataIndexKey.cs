using System.Text.RegularExpressions;

namespace Bukit.Config;

/// <summary>
/// The key rule of a <c>dataIndex</c> row: letters, digits, <c>.</c>, <c>_</c>, <c>+</c> and
/// <c>-</c>, starting with a letter or digit, so dotted camelCase copy keys such as
/// <c>custom.days.4-5.label</c> work as they are. Source names, scopes and field names keep
/// <c>^[a-z][a-z0-9_]*$</c>.
/// </summary>
internal static partial class DataIndexKey
{
    internal const string Pattern = "^[A-Za-z0-9][A-Za-z0-9._+-]*$";

    internal static bool IsValid(string? value) => value is not null && KeyRegex().IsMatch(value);

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();
}
