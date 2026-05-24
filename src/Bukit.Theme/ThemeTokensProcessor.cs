using System.Text;

namespace Bukit.Theme;

public static class ThemeTokensProcessor
{
    public static string GenerateCss(ThemeTokens tokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine(":root {");

        AppendVariables(sb, tokens.Colors, "--color");
        AppendVariables(sb, tokens.Font, "--font");
        AppendVariables(sb, tokens.Radius, "--radius");
        AppendVariables(sb, tokens.Spacing, "--spacing");
        AppendVariables(sb, tokens.Layout, "--layout");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendVariables(StringBuilder sb, Dictionary<string, string>? dict, string prefix)
    {
        if (dict is null) return;

        foreach (var kv in dict)
        {
            var key = kv.Key.Replace("_", "-").Replace(".", "-");
            sb.AppendLine($"  {prefix}-{key}: {kv.Value};");
        }
    }

    public static void WriteToFile(ThemeTokens tokens, string outputPath)
    {
        var css = GenerateCss(tokens);
        var dir = Path.GetDirectoryName(outputPath);
        if (dir is not null)
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(outputPath, css);
    }
}
