using System.Text;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class ImportSeedContentWriter
{
    internal static int WriteMarkdown(string outputDir, IReadOnlyList<ImportSeedRecord> records, bool overwrite)
    {
        Directory.CreateDirectory(outputDir);
        var written = 0;

        foreach (var record in records)
        {
            var path = ResolvePath(outputDir, record);
            if (File.Exists(path) && !overwrite) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, BuildMarkdown(record));
            written++;
        }

        return written;
    }

    private static string ResolvePath(string outputDir, ImportSeedRecord record)
    {
        var slug = string.IsNullOrWhiteSpace(record.Slug)
            ? SlugHelper.Slugify(record.Title)
            : SlugHelper.Slugify(record.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "index";

        return record.Collection switch
        {
            "post" => Path.Combine(outputDir, "posts", $"{slug}.md"),
            "company" => Path.Combine(outputDir, "companies", $"{slug}.md"),
            "service" => Path.Combine(outputDir, "services", $"{slug}.md"),
            _ when slug.Equals("index", StringComparison.OrdinalIgnoreCase) =>
                Path.Combine(outputDir, "index.md"),
            _ => Path.Combine(outputDir, "pages", $"{slug}.md")
        };
    }

    private static string BuildMarkdown(ImportSeedRecord record)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"title: \"{EscapeYaml(record.Title)}\"");
        sb.AppendLine($"slug: \"{EscapeYaml(record.Slug)}\"");
        sb.AppendLine($"type: \"{record.Collection}\"");
        if (!string.IsNullOrWhiteSpace(record.Summary))
            sb.AppendLine($"summary: \"{EscapeYaml(record.Summary)}\"");
        if (!string.IsNullOrWhiteSpace(record.Language))
            sb.AppendLine($"language: \"{EscapeYaml(record.Language)}\"");
        if (!string.IsNullOrWhiteSpace(record.SeoTitle))
            sb.AppendLine($"seo_title: \"{EscapeYaml(record.SeoTitle)}\"");
        if (!string.IsNullOrWhiteSpace(record.SeoDescription))
            sb.AppendLine($"seo_description: \"{EscapeYaml(record.SeoDescription)}\"");
        sb.AppendLine($"published: {record.Published.ToString().ToLowerInvariant()}");
        sb.AppendLine("---");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(record.Content))
            sb.AppendLine(record.Content);
        return sb.ToString();
    }

    private static string EscapeYaml(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
