using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static class SeedGenerator
{
    private static readonly Regex CssUrlRegex = new(
        @"url\(\s*(?<quote>['""]?)(?<value>[^'"")]+)\k<quote>\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static bool Generate(HtmlDemoImportOptions options, ExtractedContent content,
        List<DiscoveredComponent> components, List<DiscoveredPage> pages,
        ImportManifest? manifest = null)
    {
        var siteDir = options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
        var language = SiteConfigGenerator.ResolveLanguage(options.Language, pages);
        content.Pages = content.Pages.Select(record => record with { Language = language }).ToList();
        content.Posts = content.Posts.Select(record => record with { Language = language }).ToList();
        content.Companies = content.Companies.Select(record => record with { Language = language }).ToList();
        content.Services = content.Services.Select(record => record with { Language = language }).ToList();
        content.Navigation = content.Navigation.Select(record => record with { Language = language }).ToList();
        content.Sections = content.Sections.Select(record => record with { Language = language }).ToList();
        content.Faqs = content.Faqs.Select(record => record with { Language = language }).ToList();
        var isJson = options.ContentSource.Equals("json", StringComparison.OrdinalIgnoreCase);
        var isYaml = options.ContentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase);
        var seedDir = isJson || isYaml
            ? Path.Combine(siteDir, "data")
            : Path.Combine(siteDir, "notion-seed");
        Directory.CreateDirectory(seedDir);
        manifest?.Produced(seedDir);

        if (isYaml)
        {
            WritePagesYaml(Path.Combine(seedDir, "pages.yaml"), SeedPages(content), options.Overwrite, manifest);
            WriteNavigationYaml(Path.Combine(seedDir, "navigation.yaml"), content.Navigation, options.Overwrite, manifest);
            WriteSectionsYaml(Path.Combine(seedDir, "sections.yaml"), content.Sections, options.Overwrite, manifest);
            WritePostsYaml(Path.Combine(seedDir, "posts.yaml"), content.Posts, options.Overwrite, manifest);
            WriteCompaniesYaml(Path.Combine(seedDir, "companies.yaml"), content.Companies, options.Overwrite, manifest);
            WriteServicesYaml(Path.Combine(seedDir, "services.yaml"), content.Services, options.Overwrite, manifest);
            WriteFaqsYaml(Path.Combine(seedDir, "faqs.yaml"), content.Faqs, options.Overwrite, manifest);
            WriteMediaYaml(Path.Combine(seedDir, "media.yaml"), pages, options.InputPath, options.Overwrite, manifest);
            WriteComponentsYaml(Path.Combine(seedDir, "components.yaml"), components, options.Overwrite, manifest);
        }
        else
        {
            WritePages(Path.Combine(seedDir, "pages.json"), SeedPages(content), options.Overwrite, manifest);
            WriteNavigation(Path.Combine(seedDir, "navigation.json"), content.Navigation, options.Overwrite, manifest);
            WriteSections(Path.Combine(seedDir, "sections.json"), content.Sections, options.Overwrite, manifest);
            WritePosts(Path.Combine(seedDir, "posts.json"), content.Posts, options.Overwrite, manifest);
            WriteCompanies(Path.Combine(seedDir, "companies.json"), content.Companies, options.Overwrite, manifest);
            WriteServices(Path.Combine(seedDir, "services.json"), content.Services, options.Overwrite, manifest);
            WriteFaqs(Path.Combine(seedDir, "faqs.json"), content.Faqs, options.Overwrite, manifest);
            WriteMedia(Path.Combine(seedDir, "media.json"), pages, options.InputPath, options.Overwrite, manifest);
            WriteComponents(Path.Combine(seedDir, "components.json"), components, options.Overwrite, manifest);
            if (!isJson)
                WriteDefaultNotionDatabaseMap(Path.Combine(seedDir, "notion-database-map.yaml"));
        }

        Console.WriteLine($"  种子数据生成完成: {seedDir}");
        Console.WriteLine($"    pages:     {SeedPages(content).Count}");
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

    private static void WritePages(string path, List<PageRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"type\": {JsonStr(r.Type)},");
            sb.AppendLine($"    \"template\": {JsonVal(r.TemplatePath)},");
            sb.AppendLine($"    \"url\": {JsonVal(r.Url)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteNavigation(string path, List<NavigationRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
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

    private static void WriteSections(string path, List<SectionRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
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

    private static void WritePosts(string path, List<PostRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"image\": {JsonVal(r.Image)},");
            sb.AppendLine($"    \"category\": {JsonVal(r.Category)},");
            sb.AppendLine($"    \"tags\": [{string.Join(", ", r.Tags.Select(JsonStr))}],");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.AppendLine($"    \"template\": {JsonVal(r.TemplatePath)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteCompanies(string path, List<CompanyRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"image\": {JsonVal(r.Image)},");
            sb.AppendLine($"    \"country\": {JsonVal(r.Country)},");
            sb.AppendLine($"    \"industry\": {JsonVal(r.Industry)},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.AppendLine($"    \"template\": {JsonVal(r.TemplatePath)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteServices(string path, List<ServiceRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
        {
            sb.AppendLine($"    \"title\": {JsonStr(r.Title)},");
            sb.AppendLine($"    \"slug\": {JsonStr(r.Slug)},");
            sb.AppendLine($"    \"summary\": {JsonVal(r.Summary)},");
            sb.AppendLine($"    \"content\": {JsonVal(r.Content)},");
            sb.AppendLine($"    \"image\": {JsonVal(r.Image)},");
            sb.AppendLine($"    \"language\": {JsonStr(r.Language)},");
            sb.AppendLine($"    \"published\": {JsonBool(r.Published)},");
            sb.AppendLine($"    \"seo_title\": {JsonVal(r.SeoTitle)},");
            sb.AppendLine($"    \"template\": {JsonVal(r.TemplatePath)},");
            sb.Append($"    \"seo_description\": {JsonVal(r.SeoDescription)}");
        });
    }

    private static void WriteFaqs(string path, List<FaqRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, records, overwrite, manifest, (sb, r, i, last) =>
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

    private static void WriteMedia(
        string path, List<DiscoveredPage> pages, string inputPath, bool overwrite, ImportManifest? manifest = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mediaItems = new List<(string source, string target, List<string> usedBy, string status)>();

        foreach (var (source, target, usedBy, status) in CollectMedia(pages, inputPath))
        {
            if (!seen.Add(source))
            {
                var existing = mediaItems.FindIndex(m => m.source == source);
                if (existing >= 0)
                {
                    var item = mediaItems[existing];
                    foreach (var page in usedBy)
                    {
                        if (!item.usedBy.Contains(page))
                            item.usedBy.Add(page);
                    }
                    mediaItems[existing] = item;
                }
                continue;
            }

            mediaItems.Add((source, target, usedBy, status));
        }

        WriteArray(path, mediaItems, overwrite, manifest, (sb, m, i, last) =>
        {
            sb.AppendLine($"    \"source\": {JsonStr(m.source)},");
            sb.AppendLine($"    \"target\": {JsonStr(m.target)},");
            sb.Append($"    \"used_by\": [{string.Join(", ", m.usedBy.Select(JsonStr))}],");
            sb.AppendLine();
            sb.Append($"    \"status\": {JsonStr(m.status)}");
        });
    }

    private static IEnumerable<(string source, string target, List<string> usedBy, string status)> CollectMedia(
        List<DiscoveredPage> pages, string inputPath)
    {
        foreach (var page in pages)
        {
            foreach (var asset in page.AssetPaths)
            {
                var extension = Path.GetExtension(asset).ToLowerInvariant();
                var isImage = extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico" or ".bmp";
                if (!isImage) continue;

                var resolved = DemoLinks.ResolvePath(page.RelativePath, asset);
                var source = resolved ?? asset;
                var target = resolved is null ? "" : "/" + resolved;
                var status = resolved is not null && File.Exists(Path.Combine(inputPath, resolved))
                    ? "copied"
                    : "missing";

                yield return (source, target, [page.RelativePath], status);
            }

            foreach (var asset in page.AssetPaths)
            {
                if (!Path.GetExtension(asset).Equals(".css", StringComparison.OrdinalIgnoreCase))
                    continue;

                var cssRelative = DemoLinks.ResolvePath(page.RelativePath, asset);
                if (cssRelative is null)
                    continue;
                var cssPath = Path.Combine(inputPath, cssRelative);
                if (!File.Exists(cssPath))
                    continue;

                foreach (var source in CssImagePaths(cssPath, inputPath))
                {
                    yield return (source, "/" + source, [page.RelativePath], "copied");
                }
            }
        }
    }

    private static IEnumerable<string> CssImagePaths(string cssPath, string inputPath)
    {
        string css;
        try
        {
            css = DemoFiles.DecodeCss(File.ReadAllBytes(cssPath));
        }
        catch (IOException)
        {
            yield break;
        }

        var cssDir = Path.GetDirectoryName(cssPath) ?? inputPath;
        foreach (Match match in CssUrlRegex.Matches(css))
        {
            var value = match.Groups["value"].Value.Trim();
            var cut = value.IndexOfAny(['?', '#']);
            if (cut >= 0)
                value = value[..cut];
            if (string.IsNullOrWhiteSpace(value) ||
                value.StartsWith('#') ||
                value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            var imagePath = Path.GetFullPath(value.StartsWith('/')
                ? Path.Combine(inputPath, value.TrimStart('/'))
                : Path.Combine(cssDir, value));
            var relative = Path.GetRelativePath(inputPath, imagePath).Replace('\\', '/');
            if (relative.StartsWith("../", StringComparison.Ordinal) ||
                !IsImageExtension(Path.GetExtension(relative)) ||
                !File.Exists(imagePath))
            {
                continue;
            }

            yield return relative;
        }
    }

    private static bool IsImageExtension(string extension)
        => extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico" or ".bmp";

    private static void WriteComponents(string path, List<DiscoveredComponent> components, bool overwrite, ImportManifest? manifest = null)
    {
        WriteArray(path, components, overwrite, manifest, (sb, c, i, last) =>
        {
            sb.AppendLine($"    \"name\": {JsonStr(c.Name)},");
            sb.AppendLine($"    \"template\": {JsonVal(c.NormalizedTemplate)},");
            sb.Append("    \"used_in_pages\": [");
            var pageNames = c.UsedBy.Select(p => p.RelativePath).ToList();
            sb.Append(string.Join(", ", pageNames.Select(JsonStr)));
            sb.Append("]");
        });
    }

    private static void WriteDefaultNotionDatabaseMap(string path)
    {
        // Created once and then owned by the user / the Notion push, which writes the
        // database ids into it. Never replace it, or --force would wipe those ids.
        if (File.Exists(path))
            return;

        var sb = new StringBuilder();
        sb.AppendLine("databases:");
        WriteDatabaseMapEntry(sb, "pages", "Pages", "pages.json", "page");
        WriteDatabaseMapEntry(sb, "navigation", "Navigation", "navigation.json", "navigation");
        WriteDatabaseMapEntry(sb, "posts", "Posts", "posts.json", "post");
        WriteDatabaseMapEntry(sb, "companies", "Companies", "companies.json", "company");
        WriteDatabaseMapEntry(sb, "services", "Services", "services.json", "service");
        ImportWriteGuard.WriteAllTextAtomic(path, sb.ToString());
    }

    private static void WriteDatabaseMapEntry(StringBuilder sb, string key, string title, string seed, string collection)
    {
        sb.AppendLine($"  {key}:");
        sb.AppendLine($"    title: {title}");
        sb.AppendLine($"    seed: {seed}");
        sb.AppendLine($"    collection: {collection}");
        sb.AppendLine("    databaseId: \"\"");
        sb.AppendLine("    uniqueField: Slug");
    }

    private static void WritePagesYaml(string path, List<PageRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "type", r.Type);
            YamlField(sb, "template", r.TemplatePath);
            YamlField(sb, "url", r.Url);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteNavigationYaml(string path, List<NavigationRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
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

    private static void WriteSectionsYaml(string path, List<SectionRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
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

    private static void WritePostsYaml(string path, List<PostRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "image", r.Image);
            YamlField(sb, "category", r.Category);
            YamlList(sb, "tags", r.Tags);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "template", r.TemplatePath);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteCompaniesYaml(string path, List<CompanyRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "image", r.Image);
            YamlField(sb, "country", r.Country);
            YamlField(sb, "industry", r.Industry);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "template", r.TemplatePath);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteServicesYaml(string path, List<ServiceRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
        {
            YamlField(sb, "title", r.Title);
            YamlField(sb, "slug", r.Slug);
            YamlField(sb, "summary", r.Summary);
            YamlField(sb, "content", r.Content);
            YamlField(sb, "image", r.Image);
            YamlField(sb, "language", r.Language);
            YamlBool(sb, "published", r.Published);
            YamlField(sb, "seo_title", r.SeoTitle);
            YamlField(sb, "template", r.TemplatePath);
            YamlField(sb, "seo_description", r.SeoDescription);
        });
    }

    private static void WriteFaqsYaml(string path, List<FaqRecord> records, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, records, overwrite, manifest, (sb, r) =>
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

    private static void WriteMediaYaml(
        string path, List<DiscoveredPage> pages, string inputPath, bool overwrite, ImportManifest? manifest = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mediaItems = new List<(string source, string target, List<string> usedBy, string status)>();

        foreach (var (source, target, usedBy, status) in CollectMedia(pages, inputPath))
        {
            if (!seen.Add(source))
            {
                var existing = mediaItems.FindIndex(m => m.source == source);
                if (existing >= 0)
                {
                    var item = mediaItems[existing];
                    foreach (var page in usedBy)
                    {
                        if (!item.usedBy.Contains(page))
                            item.usedBy.Add(page);
                    }
                    mediaItems[existing] = item;
                }
                continue;
            }

            mediaItems.Add((source, target, usedBy, status));
        }

        WriteYamlArray(path, mediaItems, overwrite, manifest, (sb, m) =>
        {
            YamlField(sb, "source", m.source);
            YamlField(sb, "target", m.target);
            YamlList(sb, "used_by", m.usedBy);
            YamlField(sb, "status", m.status);
        });
    }

    private static void WriteComponentsYaml(string path, List<DiscoveredComponent> components, bool overwrite, ImportManifest? manifest = null)
    {
        WriteYamlArray(path, components, overwrite, manifest, (sb, c) =>
        {
            YamlField(sb, "name", c.Name);
            YamlField(sb, "template", c.NormalizedTemplate);
            YamlList(sb, "used_in_pages", c.UsedBy.Select(p => p.RelativePath));
        });
    }

    private static void WriteArray<T>(string path, List<T> records, bool overwrite,
        ImportManifest? manifest, Action<StringBuilder, T, int, bool> writeItem)
    {
        if (manifest is not null ? !manifest.CanWrite(path) : File.Exists(path) && !overwrite)
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
        ImportWriteGuard.WriteAllTextAtomic(path, sb.ToString());
        manifest?.Record(path);
    }

    private static void WriteYamlArray<T>(string path, List<T> records, bool overwrite,
        ImportManifest? manifest, Action<StringBuilder, T> writeItem)
    {
        if (manifest is not null ? !manifest.CanWrite(path) : File.Exists(path) && !overwrite)
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
        ImportWriteGuard.WriteAllTextAtomic(path, sb.ToString());
        manifest?.Record(path);
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

    // Core renders the home page from pages/index.html. A home record would publish a
    // second copy (its empty slug falls back to the title, e.g. /首页/). Detail pages
    // already live in posts/companies/services; a page record would duplicate them.
    internal static List<PageRecord> SeedPages(ExtractedContent content)
        => content.Pages
            .Where(page => !page.Type.Equals("Home", StringComparison.OrdinalIgnoreCase))
            .Where(page => page.Template.Length == 0 ||
                           page.Template.Equals("page", StringComparison.OrdinalIgnoreCase))
            .ToList();
}
