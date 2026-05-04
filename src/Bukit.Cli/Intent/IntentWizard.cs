using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Intent;

public static class IntentWizard
{
    public static void RunInteractive(string outPath)
    {
        var siteName = Ask("site.name", defaultValue: "my-site", required: true);
        var siteTitle = Ask("site.title", defaultValue: "My Site", required: true);
        var baseUrl = Ask("site.base_url", defaultValue: "/", required: true);
        var siteUrl = Ask("site.url (optional)", defaultValue: string.Empty, required: false);

        var multiLang = AskYesNo("Enable multi-language?", defaultYes: false);
        string? siteLanguage = null;
        string? defaultLanguage = null;
        IReadOnlyList<string>? supportedLanguages = null;
        if (multiLang)
        {
            defaultLanguage = Ask("languages.default", defaultValue: "zh-CN", required: true);
            supportedLanguages = AskList("languages.supported (comma-separated)", defaultValue: new[] { defaultLanguage });
        }
        else
        {
            siteLanguage = Ask("site.language", defaultValue: "zh-CN", required: true);
        }

        var provider = AskChoice("content.provider", defaultValue: "markdown", choices: new[] { "markdown", "notion" });
        string mdDir = "content";
        string notionDatabaseId = string.Empty;
        string fieldPolicyMode = "whitelist";
        IReadOnlyList<string>? allowedFields = null;

        if (provider == "markdown")
        {
            mdDir = Ask("content.markdown.dir", defaultValue: "content", required: true);
        }
        else
        {
            notionDatabaseId = Ask("content.notion.database_id", defaultValue: string.Empty, required: true);
            fieldPolicyMode = AskChoice("content.notion.field_policy.mode", defaultValue: "whitelist", choices: new[] { "whitelist", "all" });
            if (fieldPolicyMode == "whitelist")
            {
                allowedFields = AskList("content.notion.field_policy.allowed (comma-separated)", defaultValue: new[] { "cover", "seo_title", "seo_desc", "tags", "categories", "og_image", "i18n_key", "language" });
            }
        }

        var themeName = Ask("theme.name", defaultValue: "starter", required: true);

        var enableSitemap = AskYesNo("features.sitemap?", defaultYes: true);
        var enableRss = AskYesNo("features.rss?", defaultYes: true);
        var enableSearch = AskYesNo("features.search?", defaultYes: true);

        var root = new YamlMappingNode();
        root.Add("site", BuildSiteNode(siteName, siteTitle, baseUrl, siteUrl, siteLanguage));

        if (multiLang && defaultLanguage is not null && supportedLanguages is not null)
        {
            root.Add("languages", BuildLanguagesNode(defaultLanguage, supportedLanguages));
        }

        root.Add("content", BuildContentNode(provider, mdDir, notionDatabaseId, fieldPolicyMode, allowedFields));
        root.Add("theme", BuildThemeNode(themeName));
        root.Add("features", BuildFeaturesNode(enableSitemap, enableRss, enableSearch));

        var yaml = new YamlStream(new YamlDocument(root));
        var dir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        File.WriteAllText(outPath, writer.ToString());
    }

    private static YamlMappingNode BuildSiteNode(string name, string title, string baseUrl, string siteUrl, string? language)
    {
        var site = new YamlMappingNode();
        site.Add("name", name);
        site.Add("title", title);
        site.Add("base_url", baseUrl);
        if (!string.IsNullOrWhiteSpace(siteUrl))
        {
            site.Add("url", siteUrl);
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            site.Add("language", language);
        }

        return site;
    }

    private static YamlMappingNode BuildLanguagesNode(string def, IReadOnlyList<string> supported)
    {
        var node = new YamlMappingNode();
        node.Add("default", def);
        var seq = new YamlSequenceNode(supported.Select(x => new YamlScalarNode(x)));
        node.Add("supported", seq);
        return node;
    }

    private static YamlMappingNode BuildContentNode(string provider, string mdDir, string notionDatabaseId, string fieldPolicyMode, IReadOnlyList<string>? allowedFields)
    {
        var node = new YamlMappingNode();
        node.Add("provider", provider);

        if (provider == "markdown")
        {
            var md = new YamlMappingNode();
            md.Add("dir", mdDir);
            node.Add("markdown", md);
            return node;
        }

        var notion = new YamlMappingNode();
        notion.Add("database_id", notionDatabaseId);
        var fp = new YamlMappingNode();
        fp.Add("mode", fieldPolicyMode);
        if (allowedFields is not null && allowedFields.Count > 0)
        {
            fp.Add("allowed", new YamlSequenceNode(allowedFields.Select(x => new YamlScalarNode(x))));
        }

        notion.Add("field_policy", fp);
        node.Add("notion", notion);
        return node;
    }

    private static YamlMappingNode BuildThemeNode(string name)
    {
        var node = new YamlMappingNode();
        node.Add("name", name);
        return node;
    }

    private static YamlMappingNode BuildFeaturesNode(bool sitemap, bool rss, bool search)
    {
        var node = new YamlMappingNode();
        node.Add("sitemap", sitemap ? "true" : "false");
        node.Add("rss", rss ? "true" : "false");
        node.Add("search", search ? "true" : "false");
        return node;
    }

    private static string Ask(string label, string defaultValue, bool required)
    {
        while (true)
        {
            var prompt = string.IsNullOrWhiteSpace(defaultValue) ? $"{label}: " : $"{label} ({defaultValue}): ";
            Console.Write(prompt);
            var input = Console.ReadLine() ?? string.Empty;
            var value = string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
            if (!required || !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
    }

    private static bool AskYesNo(string label, bool defaultYes)
    {
        while (true)
        {
            Console.Write($"{label} ({(defaultYes ? "Y/n" : "y/N")}): ");
            var input = (Console.ReadLine() ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return defaultYes;
            }

            if (input.Equals("y", StringComparison.OrdinalIgnoreCase) || input.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (input.Equals("n", StringComparison.OrdinalIgnoreCase) || input.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
    }

    private static string AskChoice(string label, string defaultValue, IReadOnlyList<string> choices)
    {
        while (true)
        {
            Console.Write($"{label} ({string.Join("|", choices)}, default={defaultValue}): ");
            var input = (Console.ReadLine() ?? string.Empty).Trim();
            var value = string.IsNullOrWhiteSpace(input) ? defaultValue : input;
            if (choices.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return choices.First(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    private static IReadOnlyList<string> AskList(string label, IReadOnlyList<string> defaultValue)
    {
        Console.Write($"{label} ({string.Join(",", defaultValue)}): ");
        var input = (Console.ReadLine() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return defaultValue;
        }

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = parts.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return list.Count == 0 ? defaultValue : list;
    }
}

