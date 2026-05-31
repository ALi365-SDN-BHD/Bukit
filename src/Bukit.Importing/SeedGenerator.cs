using System.Text;

namespace Bukit.Importing;

internal static class SeedGenerator
{
    internal static bool Generate(HtmlDemoImportOptions options, ExtractedContent content,
        List<DiscoveredComponent> components, List<DiscoveredPage> pages)
    {
        var siteDir = options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
        var seedDir = options.ContentSource == "json"
            ? Path.Combine(siteDir, "data")
            : Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);

        WritePages(Path.Combine(seedDir, "pages.json"), content.Pages, options.Overwrite);
        WriteSections(Path.Combine(seedDir, "sections.json"), content.Sections, options.Overwrite);
        WritePosts(Path.Combine(seedDir, "posts.json"), content.Posts, options.Overwrite);
        WriteCompanies(Path.Combine(seedDir, "companies.json"), content.Companies, options.Overwrite);
        WriteFaqs(Path.Combine(seedDir, "faqs.json"), content.Faqs, options.Overwrite);
        WriteMedia(Path.Combine(seedDir, "media.json"), pages, options.Overwrite);
        WriteComponents(Path.Combine(seedDir, "components.json"), components, options.Overwrite);

        Console.WriteLine($"  种子数据生成完成: {seedDir}");
        Console.WriteLine($"    pages:     {content.Pages.Count}");
        Console.WriteLine($"    sections:  {content.Sections.Count}");
        Console.WriteLine($"    posts:     {content.Posts.Count}");
        Console.WriteLine($"    companies: {content.Companies.Count}");
        Console.WriteLine($"    faqs:      {content.Faqs.Count}");
        Console.WriteLine($"    media:     {pages.Sum(p => p.AssetPaths.Count)}");
        Console.WriteLine($"    components: {components.Count}");

        return true;
    }

    private static void WritePages(string path, List<PageRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"type\": {JsonStr(r.Type)},");
            sb.AppendLine($"    \"template\": {JsonStr(r.Template)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteSections(string path, List<SectionRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"page_slug\": {JsonVal(r.PageSlug)},");
            sb.AppendLine($"    \"section_type\": {JsonStr(r.SectionType)},");
            sb.AppendLine($"    \"heading\": {JsonVal(r.Heading)},");
            sb.AppendLine($"    \"subheading\": {JsonVal(r.Subheading)},");
            sb.AppendLine($"    \"button_text\": {JsonVal(r.ButtonText)},");
            sb.AppendLine($"    \"button_url\": {JsonVal(r.ButtonUrl)},");
            sb.AppendLine($"    \"sort_order\": {r.SortOrder},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.Append($"    \"published\": {JsonBool(r.Published)}");
        });
    }

    private static void WritePosts(string path, List<PostRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"category\": {JsonVal(r.Category)},");
            sb.AppendLine($"    \"tags\": [{string.Join(", ", r.Tags.Select(JsonStr))}],");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteCompanies(string path, List<CompanyRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"country\": {JsonVal(r.Country)},");
            sb.AppendLine($"    \"industry\": {JsonVal(r.Industry)},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteFaqs(string path, List<FaqRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"question\": {JsonStr(r.Question)},");
            sb.AppendLine($"    \"answer\": {JsonStr(r.Answer)},");
            sb.AppendLine($"    \"page_slug\": {JsonVal(r.PageSlug)},");
            sb.AppendLine($"    \"category\": {JsonVal(r.Category)},");
            sb.AppendLine($"    \"sort_order\": {r.SortOrder},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.Append($"    \"published\": {JsonBool(r.Published)}");
        });
    }

    private static void WriteMedia(string path, List<DiscoveredPage> pages, bool overwrite)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mediaItems = new List<(string source, string target, List<string> usedBy, string status)>();

        foreach (var page in pages)
        {
            foreach (var asset in page.AssetPaths)
            {
                var ext = Path.GetExtension(asset).ToLowerInvariant();
                var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico" or ".bmp";
                if (!isImage) continue;

                if (!seen.Add(asset))
                {
                    var existing = mediaItems.FindIndex(m => m.source == asset);
                    if (existing >= 0)
                    {
                        var item = mediaItems[existing];
                        if (!item.usedBy.Contains(page.RelativePath))
                            item.usedBy.Add(page.RelativePath);
                        mediaItems[existing] = item;
                    }
                    continue;
                }

                var destSubDir = isImage ? "assets" : "static";
                var target = "/" + destSubDir + "/" + asset.TrimStart('/').Replace('\\', '/');

                mediaItems.Add((asset, target, [page.RelativePath], "copied"));
            }
        }

        WriteArray(path, mediaItems, overwrite, (sb, m, i, last) =>
        {
            sb.AppendLine($"    \"source\": {JsonStr(m.source)},");
            sb.AppendLine($"    \"target\": {JsonStr(m.target)},");
            sb.Append($"    \"used_by\": [{string.Join(", ", m.usedBy.Select(JsonStr))}],");
            sb.AppendLine();
            sb.Append($"    \"status\": {JsonStr(m.status)}");
        });
    }

    private static void WriteComponents(string path, List<DiscoveredComponent> components, bool overwrite)
    {
        WriteArray(path, components, overwrite, (sb, c, i, last) =>
        {
            sb.AppendLine($"    \"name\": {JsonStr(c.Name)},");
            sb.AppendLine($"    \"template\": {JsonVal(c.NormalizedTemplate)},");
            sb.Append("    \"used_in_pages\": [");
            var pageNames = c.UsedBy.Select(p => p.RelativePath).ToList();
            sb.Append(string.Join(", ", pageNames.Select(JsonStr)));
            sb.Append("]");
        });
    }

    private static void WriteArray<T>(string path, List<T> records, bool overwrite,
        Action<StringBuilder, T, int, bool> writeItem)
    {
        if (File.Exists(path) && !overwrite)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (var i = 0; i < records.Count; i++)
        {
            sb.AppendLine("  {");
            writeItem(sb, records[i], i, i == records.Count - 1);
            sb.AppendLine();
            sb.Append(i < records.Count - 1 ? "  }," : "  }");
            sb.AppendLine();
        }
        sb.AppendLine("]");
        File.WriteAllText(path, sb.ToString());
    }

    private static string JsonStr(string value)
    {
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        return $"\"{escaped}\"";
    }

    private static string JsonVal(string? value)
    {
        return value is null ? "null" : JsonStr(value);
    }

    private static string JsonBool(bool value) => value ? "true" : "false";
}
