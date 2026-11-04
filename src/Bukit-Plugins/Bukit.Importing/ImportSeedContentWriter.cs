using Bukit.Shared;
using System.Text;

namespace Bukit.Importing;

public static class ImportSeedContentWriter
{
    public static IReadOnlyList<string> WriteMarkdown(
        string outputDir,
        IReadOnlyList<ImportSeedRecord> records,
        bool overwrite)
    {
        Directory.CreateDirectory(outputDir);
        var written = new List<string>();
        var claimed = new HashSet<string>(PathComparer);

        foreach (var record in records)
        {
            var slug = GetEffectiveSlug(record);
            var path = ResolvePath(outputDir, record, slug);
            if (!claimed.Add(path))
            {
                // Two records normalize to the same file; writing the second would
                // silently destroy the first, so keep the first and report it.
                Console.Error.WriteLine(
                    $"Skipped duplicate seed output '{path}' (record '{record.Title}'). Check slugs that normalize to the same name.");
                continue;
            }

            if (File.Exists(path) && !overwrite) continue;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, BuildMarkdown(record, slug));
            written.Add(path);
        }

        return written;
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static string ResolvePath(string outputDir, ImportSeedRecord record, string slug)
    {
        return record.Collection switch
        {
            "navigation" => Path.Combine(outputDir, "navigation", $"{slug}.md"),
            "post" => Path.Combine(outputDir, "posts", $"{slug}.md"),
            "company" => Path.Combine(outputDir, "companies", $"{slug}.md"),
            "service" => Path.Combine(outputDir, "services", $"{slug}.md"),
            _ when slug.Equals("index", StringComparison.OrdinalIgnoreCase) =>
                Path.Combine(outputDir, "index.md"),
            _ => Path.Combine(outputDir, "pages", $"{slug}.md")
        };
    }

    private static string GetEffectiveSlug(ImportSeedRecord record)
    {
        var slug = string.IsNullOrWhiteSpace(record.Slug)
            ? SlugHelper.Slugify(record.Title)
            : SlugHelper.Slugify(record.Slug);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "index";

        return slug;
    }

    private static string BuildMarkdown(ImportSeedRecord record, string slug)
    {
        var fields = new List<KeyValuePair<string, object?>>
        {
            new("title", record.Title),
            new("slug", slug),
            new("type", record.Collection),
            new("summary", record.Summary),
            new("language", record.Language),
            new("seo_title", record.SeoTitle),
            new("seo_desc", record.SeoDescription),
            // Core only understands draft:true as unpublished; "published: false" is inert.
            new("draft", !record.Published)
        };

        var known = new HashSet<string>(fields.Select(f => f.Key), StringComparer.OrdinalIgnoreCase);
        if (record.ExtraFields is not null)
        {
            foreach (var (key, value) in record.ExtraFields)
            {
                if (value is null || !known.Add(key)) continue;
                fields.Add(new(key, value));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append(YamlEmitter.Emit(fields));
        sb.AppendLine("---");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(record.Content))
            sb.AppendLine(record.Content);
        return sb.ToString();
    }
}
