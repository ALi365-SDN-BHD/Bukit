using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class TemplateResidueAnalyzer
{
    private static readonly HashSet<string> WhitelistWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Email", "Phone", "Phone Number", "Address", "Contact", "Contact Us",
        "Home", "About", "About Us", "Services", "Companies", "Insights",
        "Privacy Policy", "Terms", "Terms of Service", "FAQ", "FAQs",
        "Read More", "Learn More", "View All", "Load More", "Previous", "Next",
        "Submit", "Search", "Menu", "Close", "Open", "Share", "Follow Us",
        "All Rights Reserved", "Copyright", "Subscribe", "Newsletter",
        "Back to Top", "Scroll Down", "Loading", "Error", "Not Found",
        "©", "&copy;", "&reg;", "&trade;"
    };

    internal static HardcodedContentReport Analyze(
        string themePath,
        List<ExtractedContent>? contents,
        RouteMapConfig? routeMap)
    {
        var residues = new List<TemplateResidueAnalysis>();
        var templateFiles = FindTemplateFiles(themePath);

        foreach (var file in templateFiles)
        {
            var analysis = AnalyzeTemplate(file);
            if (analysis.ResidualTextCount > 0)
                residues.Add(analysis);
        }

        var totalCount = residues.Sum(r => r.ResidualTextCount);
        var overallScore = CalculateOverallScore(residues, totalCount);

        return new HardcodedContentReport
        {
            OverallScore = overallScore,
            Residues = residues,
            TotalResidualCount = totalCount
        };
    }

    internal static TemplateResidueAnalysis AnalyzeTemplate(string templatePath)
    {
        if (!File.Exists(templatePath))
            return new TemplateResidueAnalysis { TemplatePath = templatePath };

        var content = File.ReadAllText(templatePath);
        var residualSegments = new List<string>();
        int totalSegments = 0;

        var textSegments = ExtractTextSegments(content);
        foreach (var segment in textSegments)
        {
            totalSegments++;
            var cleaned = segment.Trim();

            if (IsResidualText(cleaned))
                residualSegments.Add(cleaned);
        }

        var severity = residualSegments.Count switch
        {
            0 => "low",
            <= 10 => "low",
            <= 25 => "medium",
            _ => "high"
        };

        return new TemplateResidueAnalysis
        {
            TemplatePath = templatePath,
            ResidualTextCount = residualSegments.Count,
            TotalTextSegments = totalSegments,
            Severity = severity,
            ResidualSamples = residualSegments.Take(5).ToList()
        };
    }

    private static List<string> FindTemplateFiles(string themePath)
    {
        var files = new List<string>();
        var pagesDir = Path.Combine(themePath, "pages");
        var layoutsDir = Path.Combine(themePath, "layouts");
        var partialsDir = Path.Combine(themePath, "partials");

        foreach (var dir in new[] { pagesDir, layoutsDir, partialsDir })
        {
            if (Directory.Exists(dir))
                files.AddRange(Directory.GetFiles(dir, "*.html", SearchOption.AllDirectories));
        }

        return files;
    }

    private static List<string> ExtractTextSegments(string html)
    {
        var segments = new List<string>();

        var textMatches = TextBetweenTagsPattern().Matches(html);
        foreach (Match m in textMatches)
        {
            var text = m.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                segments.Add(text);
        }

        return segments;
    }

    private static bool IsResidualText(string text)
    {
        if (text.Length < 4)
            return false;

        if (text.Contains("{{", StringComparison.Ordinal) ||
            text.Contains("{%", StringComparison.Ordinal) ||
            text.Contains("{#", StringComparison.Ordinal))
            return false;

        if (text.StartsWith("{{", StringComparison.Ordinal) ||
            text.StartsWith("{%", StringComparison.Ordinal))
            return false;

        if (text.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("false", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("end", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("if", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("else", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("for", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsNumericOnly(text))
            return false;

        if (IsHtmlCommentOrScript(text))
            return false;

        if (IsWhitelisted(text))
            return false;

        return LooksLikeBusinessText(text);
    }

    private static bool IsNumericOnly(string text)
    {
        var stripped = text.Replace(",", "").Replace(".", "").Replace(" ", "")
            .Replace("%", "").Replace("$", "").Replace("¥", "").Trim();
        return stripped.Length > 0 && stripped.All(c => char.IsDigit(c));
    }

    private static bool IsHtmlCommentOrScript(string text)
    {
        return text.StartsWith("<!--", StringComparison.Ordinal) ||
               text.StartsWith("//", StringComparison.Ordinal) ||
               text.Contains("function(", StringComparison.Ordinal) ||
               text.Contains("=>", StringComparison.Ordinal) ||
               text.Contains("const ", StringComparison.Ordinal) ||
               text.Contains("let ", StringComparison.Ordinal) ||
               text.Contains("var ", StringComparison.Ordinal);
    }

    private static bool IsWhitelisted(string text)
    {
        foreach (var word in WhitelistWords)
        {
            if (text.Equals(word, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool LooksLikeBusinessText(string text)
    {
        var wordCount = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        if (IsCssSelector(text))
            return false;

        if (IsImagePath(text))
            return false;

        if (IsDateOrTimeFormat(text))
            return false;

        if (text.All(c => char.IsUpper(c) || char.IsDigit(c) || c == '-'))
            return false;

        return wordCount >= 2;
    }

    private static bool IsCssSelector(string text)
        => text.Contains('.') && !text.Contains(' ') && !text.Contains("。");

    private static bool IsImagePath(string text)
        => text.StartsWith('/') && (text.Contains(".png") || text.Contains(".jpg") ||
            text.Contains(".svg") || text.Contains(".webp") || text.Contains(".ico"));

    private static bool IsDateOrTimeFormat(string text)
        => Regex.IsMatch(text, @"^\d{2,4}[-/]\d{1,2}[-/]\d{1,2}");

    private static int CalculateOverallScore(List<TemplateResidueAnalysis> residues, int totalCount)
    {
        if (totalCount == 0) return 0;

        var highCount = residues.Count(r => r.Severity == "high");
        var mediumCount = residues.Count(r => r.Severity == "medium");

        var score = Math.Min(100, totalCount * 2 + highCount * 10 + mediumCount * 5);
        return Math.Min(100, score);
    }

    [GeneratedRegex(@">\s*([^<]{2,}?)\s*<", RegexOptions.Singleline)]
    private static partial Regex TextBetweenTagsPattern();
}
