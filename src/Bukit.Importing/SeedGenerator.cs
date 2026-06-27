using System.Text;

namespace Bukit.Importing;

internal static class SeedGenerator
{
    internal static bool Generate(HtmlDemoImportOptions options, ExtractedContent content,
        List<DiscoveredComponent> components, List<DiscoveredPage> pages)
    {
        var siteDir = options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
        var isJson = options.ContentSource.Equals("json", StringComparison.OrdinalIgnoreCase);
        var isYaml = options.ContentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase);
        var seedDir = isJson || isYaml
            ? Path.Combine(siteDir, "data")
            : Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);

        if (isYaml)
        {
            WritePagesYaml(Path.Combine(seedDir, "pages.yaml"), content.Pages, options.Overwrite);
            WriteNavigationYaml(Path.Combine(seedDir, "navigation.yaml"), content.Navigation, options.Overwrite);
            WriteSectionsYaml(Path.Combine(seedDir, "sections.yaml"), content.Sections, options.Overwrite);
            WritePostsYaml(Path.Combine(seedDir, "posts.yaml"), content.Posts, options.Overwrite);
            WriteCompaniesYaml(Path.Combine(seedDir, "companies.yaml"), content.Companies, options.Overwrite);
            WriteServicesYaml(Path.Combine(seedDir, "services.yaml"), content.Services, options.Overwrite);
            WriteFaqsYaml(Path.Combine(seedDir, "faqs.yaml"), content.Faqs, options.Overwrite);
            WriteMediaYaml(Path.Combine(seedDir, "media.yaml"), pages, options.Overwrite);
            WriteComponentsYaml(Path.Combine(seedDir, "components.yaml"), components, options.Overwrite);
        }
        else
        {
            WritePages(Path.Combine(seedDir, "pages.json"), content.Pages, options.Overwrite);
            WriteNavigation(Path.Combine(seedDir, "navigation.json"), content.Navigation, options.Overwrite);
            WriteSections(Path.Combine(seedDir, "sections.json"), content.Sections, options.Overwrite);
            WritePosts(Path.Combine(seedDir, "posts.json"), content.Posts, options.Overwrite);
            WriteCompanies(Path.Combine(seedDir, "companies.json"), content.Companies, options.Overwrite);
            WriteServices(Path.Combine(seedDir, "services.json"), content.Services, options.Overwrite);
            WriteFaqs(Path.Combine(seedDir, "faqs.json"), content.Faqs, options.Overwrite);
            WriteMedia(Path.Combine(seedDir, "media.json"), pages, options.Overwrite);
            WriteComponents(Path.Combine(seedDir, "components.json"), components, options.Overwrite);
            if (!isJson)
                WriteDefaultNotionDatabaseMap(Path.Combine(seedDir, "notion-database-map.yaml"), options.Overwrite);
        }

        Console.WriteLine($"  种子数据生成完成: {seedDir}");
        Console.WriteLine($"    pages:     {content.Pages.Count}");
        Console.WriteLine($"    navigation: {content.Navigation.Count}");
        Console.WriteLine($"    sections:  {content.Sections.Count}");
        Console.WriteLine($"    posts:     {content.Posts.Count}");
        Console.WriteLine($"    companies: {content.Companies.Count}");
        Console.WriteLine($"    services:  {content.Services.Count}");
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

    private static void WriteNavigation(string path, List<NavigationRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"type\": {JsonStr(r.Type)},");
            sb.AppendLine($"    \"link\": {JsonVal(r.Link)},");
            sb.AppendLine($"    \"order\": {r.Order},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.Append($"    \"published\": {JsonBool(r.Published)}");
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

    private static void WriteServices(string path, List<ServiceRecord> records, bool overwrite)
    {
        WriteArray(path, records, overwrite, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
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

                var target = "/" + asset.TrimStart('/').Replace('\\', '/');

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

    private static void WriteDefaultNotionDatabaseMap(string path, bool overwrite)
    {
        if (File.Exists(path) && !overwrite)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("databases:");
        WriteDatabaseMapEntry(sb, "pages", "Pages", "pages.json", "page");
        WriteDatabaseMapEntry(sb, "navigation", "Navigation", "navigation.json", "navigation");
        WriteDatabaseMapEntry(sb, "posts", "Posts", "posts.json", "post");
        WriteDatabaseMapEntry(sb, "companies", "Companies", "companies.json", "company");
        WriteDatabaseMapEntry(sb, "services", "Services", "services.json", "service");
        File.WriteAllText(path, sb.ToString());
    }

    private static void WriteDatabaseMapEntry(StringBuilder sb, string key, string title, string seed, string collection)
    {
        sb.AppendLine($"  {key}:");
        sb.AppendLine($"    title: {title}");
        sb.AppendLine($"    seed: {seed}");
        sb.AppendLine($"    collection: {collection}");
        sb.AppendLine("    databaseId: \"\"");
        sb.AppendLine("    uniqueField: Slug");
        sb.AppendLine("    properties:");
        sb.AppendLine("      Title:");
        sb.AppendLine("        source: title");
        sb.AppendLine("        type: title");
        sb.AppendLine("      Slug:");
        sb.AppendLine("        source: slug");
        sb.AppendLine("        type: rich_text");
        sb.AppendLine("      Published:");
        sb.AppendLine("        source: published");
        sb.AppendLine("        type: checkbox");
    }

    private static void WritePagesYaml(string path, List<PageRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "type", r.Type);
            YamlField(sb, "template", r.Template);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteNavigationYaml(string path, List<NavigationRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "type", r.Type);
            YamlField(sb, "link", r.Link);
            YamlNumber(sb, "order", r.Order);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
        });
    }

    private static void WriteSectionsYaml(string path, List<SectionRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "page_slug", r.PageSlug);
            YamlField(sb, "section_type", r.SectionType);
            YamlField(sb, "heading", r.Heading);
            YamlField(sb, "subheading", r.Subheading);
            YamlField(sb, "button_text", r.ButtonText);
            YamlField(sb, "button_url", r.ButtonUrl);
            YamlNumber(sb, "sort_order", r.SortOrder);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
        });
    }

    private static void WritePostsYaml(string path, List<PostRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "category", r.Category);
            YamlList(sb, "tags", r.Tags);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteCompaniesYaml(string path, List<CompanyRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "country", r.Country);
            YamlField(sb, "industry", r.Industry);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteServicesYaml(string path, List<ServiceRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteFaqsYaml(string path, List<FaqRecord> records, bool overwrite)
    {
        WriteYamlArray(path, records, overwrite, (sb, r) =>
        {
            YamlField(sb, "question", r.Question);
            YamlField(sb, "answer", r.Answer);
            YamlField(sb, "page_slug", r.PageSlug);
            YamlField(sb, "category", r.Category);
            YamlNumber(sb, "sort_order", r.SortOrder);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
        });
    }

    private static void WriteMediaYaml(string path, List<DiscoveredPage> pages, bool overwrite)
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

                mediaItems.Add((asset, "/" + asset.TrimStart('/').Replace('\\', '/'), [page.RelativePath], "copied"));
            }
        }

        WriteYamlArray(path, mediaItems, overwrite, (sb, m) =>
        {
            YamlField(sb, "source", m.source);
            YamlField(sb, "target", m.target);
            YamlList(sb, "used_by", m.usedBy);
            YamlField(sb, "status", m.status);
        });
    }

    private static void WriteComponentsYaml(string path, List<DiscoveredComponent> components, bool overwrite)
    {
        WriteYamlArray(path, components, overwrite, (sb, c) =>
        {
            YamlField(sb, "name", c.Name);
            YamlField(sb, "template", c.NormalizedTemplate);
            YamlList(sb, "used_in_pages", c.UsedBy.Select(p => p.RelativePath));
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

    private static void WriteYamlArray<T>(string path, List<T> records, bool overwrite,
        Action<StringBuilder, T> writeItem)
    {
        if (File.Exists(path) && !overwrite)
            return;

        var sb = new StringBuilder();
        if (records.Count == 0)
        {
            sb.AppendLine("[]");
        }
        else
        {
            foreach (var record in records)
            {
                sb.AppendLine("-");
                writeItem(sb, record);
            }
        }
        File.WriteAllText(path, sb.ToString());
    }

    private static void YamlField(StringBuilder sb, string key, string? value)
    {
        sb.AppendLine($"  {key}: {YamlVal(value)}");
    }

    private static void YamlBool(StringBuilder sb, string key, bool value)
    {
        sb.AppendLine($"  {key}: {value.ToString().ToLowerInvariant()}");
    }

    private static void YamlNumber(StringBuilder sb, string key, int value)
    {
        sb.AppendLine($"  {key}: {value}");
    }

    private static void YamlList(StringBuilder sb, string key, IEnumerable<string> values)
    {
        var items = values.ToList();
        if (items.Count == 0)
        {
            sb.AppendLine($"  {key}: []");
            return;
        }

        sb.AppendLine($"  {key}:");
        foreach (var value in items)
            sb.AppendLine($"    - {YamlVal(value)}");
    }

    private static string YamlVal(string? value)
    {
        if (value is null) return "null";
        var escaped = value.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        return $"\"{escaped}\"";
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
