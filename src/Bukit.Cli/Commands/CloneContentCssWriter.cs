using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

internal static partial class CloneContentCssWriter
{
    internal static string GenerateCloneCss(IReadOnlyList<NormalizedSection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine(".clone-section { margin: var(--section-gap) 0; }");
        sb.AppendLine(".clone-section-body > :first-child { margin-top: 0; }");
        sb.AppendLine(".clone-section-body > :last-child { margin-bottom: 0; }");
        sb.AppendLine(".clone-items { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 18px; }");
        sb.AppendLine(".clone-item { min-width: 0; padding: 18px; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); box-shadow: var(--card-shadow); }");
        sb.AppendLine(".clone-button { display: inline-flex; align-items: center; justify-content: center; min-height: 42px; margin: 12px 10px 0 0; padding: 0 18px; border-radius: var(--radius); background: var(--primary); color: #fff; font-weight: 700; text-decoration: none; }");
        sb.AppendLine(".clone-button:hover { background: var(--primary-strong); color: #fff; text-decoration: none; }");
        sb.AppendLine(".clone-hero { padding: 44px 0; }");
        sb.AppendLine(".clone-hero .clone-section-title { font-size: clamp(2rem, 5vw, 4.2rem); line-height: 1.05; }");
        foreach (var section in sections)
        {
            if (section.Source.Styles is { Count: > 0 })
            {
                var declarations = section.Source.Styles
                    .Where(kv => IsSafeCssName(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
                    .Select(kv => $"  {kv.Key}: {kv.Value.Trim()};")
                    .ToList();
                if (declarations.Count > 0)
                {
                    sb.AppendLine($".{section.CssClass} {{");
                    foreach (var declaration in declarations)
                        sb.AppendLine(declaration);
                    sb.AppendLine("}");
                }
            }

            if (section.Source.Responsive is not null)
            {
                CloneSectionDataWriter.AppendResponsiveCss(sb, section);
            }
        }
        return sb.ToString();
    }

    internal static bool IsSafeCssName(string key)
        => CssNameRegex().IsMatch(key);

    [GeneratedRegex("^[a-zA-Z-][a-zA-Z0-9-]*$")]
    private static partial Regex CssNameRegex();
}
