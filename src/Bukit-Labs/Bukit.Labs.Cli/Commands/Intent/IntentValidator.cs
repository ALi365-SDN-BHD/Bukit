namespace Bukit.Labs.Cli.Commands;

public static class IntentValidator
{
    public static IntentValidationResult Validate(SiteIntent intent, string rootDir)
    {
        var result = new IntentValidationResult();

        if (string.IsNullOrWhiteSpace(intent.Site.Name))
        {
            result.Errors.Add("site.name is required.");
        }

        if (string.IsNullOrWhiteSpace(intent.Site.Title))
        {
            result.Errors.Add("site.title is required.");
        }

        if (string.IsNullOrWhiteSpace(intent.Site.BaseUrl))
        {
            result.Errors.Add("site.base_url is required.");
        }
        else if (!intent.Site.BaseUrl.Trim().StartsWith('/'))
        {
            result.Errors.Add("site.base_url must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(intent.Site.Url) &&
            !(intent.Site.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
              intent.Site.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add("site.url must start with http:// or https:// when set.");
        }

        if (intent.Languages is not null)
        {
            if (string.IsNullOrWhiteSpace(intent.Languages.Default))
            {
                result.Errors.Add("languages.default is required when languages section is present.");
            }

            if (intent.Languages.Supported is not { Count: > 0 })
            {
                result.Errors.Add("languages.supported must be a non-empty list when languages section is present.");
            }
            else
            {
                var dup = intent.Languages.Supported
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(g => g.Count() > 1);
                if (dup is not null)
                {
                    result.Errors.Add($"languages.supported has duplicate language: {dup.Key}");
                }

                if (!intent.Languages.Supported.Contains(intent.Languages.Default, StringComparer.OrdinalIgnoreCase))
                {
                    result.Errors.Add("languages.default must be included in languages.supported.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(intent.Content.Kind))
        {
            result.Errors.Add("content.kind is required.");
        }
        else
        {
            var kind = intent.Content.Kind.Trim().ToLowerInvariant();
            if (kind is not ("markdown" or "notion"))
            {
                result.Errors.Add("content.kind must be markdown|notion.");
            }
            else if (kind == "markdown")
            {
                var dir = intent.Content.Markdown?.Dir ?? "content";
                if (string.IsNullOrWhiteSpace(dir))
                {
                    result.Errors.Add("content.markdown.dir must not be empty.");
                }
                else
                {
                    var abs = Path.IsPathRooted(dir) ? dir : Path.GetFullPath(Path.Combine(rootDir, dir));
                    if (!Directory.Exists(abs))
                    {
                        result.Warnings.Add($"content.markdown.dir not found: {dir}");
                    }
                }
            }
            else if (kind == "notion")
            {
                if (intent.Content.Notion is null || string.IsNullOrWhiteSpace(intent.Content.Notion.DatabaseId))
                {
                    result.Errors.Add("content.notion.database_id is required when content.kind is notion.");
                }

                var mode = (intent.Content.Notion?.FieldPolicy.Mode ?? "whitelist").Trim().ToLowerInvariant();
                if (mode is not ("whitelist" or "all"))
                {
                    result.Errors.Add("content.notion.field_policy.mode must be whitelist|all.");
                }

                var token = Bukit.Shared.EnvironmentHelper.GetNotionToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    result.Warnings.Add("NOTION_TOKEN is required for notion content kind at build time (doctor/build will fail without it).");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(intent.Theme.Name))
        {
            result.Errors.Add("theme.name is required.");
        }
        else
        {
            var themeRoot = Path.Combine(rootDir, "themes", intent.Theme.Name.Trim());
            if (!Directory.Exists(themeRoot))
            {
                result.Warnings.Add($"theme not found under themes/: {intent.Theme.Name}");
            }
        }

        var siteType = (intent.Site.Type ?? string.Empty).Trim().ToLowerInvariant();
        if (siteType == "blog" && intent.Features is not null)
        {
            if (intent.Features.Rss is false)
            {
                result.Warnings.Add("Blog site with rss=false is not recommended.");
            }

            if (intent.Features.Search is false)
            {
                result.Warnings.Add("Blog site with search=false is not recommended.");
            }
        }

        return result;
    }
}
