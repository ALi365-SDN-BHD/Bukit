namespace Bukit.Labs.Cli.Commands;

public sealed record SiteIntent
{
    public required SiteIntentSite Site { get; init; }
    public SiteIntentLanguages? Languages { get; init; }
    public required SiteIntentContent Content { get; init; }
    public required SiteIntentTheme Theme { get; init; }
    public SiteIntentFeatures? Features { get; init; }
    public SiteIntentDeployment? Deployment { get; init; }
}

public sealed record SiteIntentSite
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public string BaseUrl { get; init; } = "/";
    public string? Url { get; init; }
    public string? Type { get; init; }
}

public sealed record SiteIntentLanguages
{
    /// <summary>The default language. May be omitted when <see cref="Supported"/> has one language.</summary>
    public string? Default { get; init; }
    public required IReadOnlyList<string> Supported { get; init; }

    /// <summary>The supported languages, trimmed, without blank entries.</summary>
    public IReadOnlyList<string> CleanSupported =>
        Supported.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();

    /// <summary><see cref="Default"/> when set, otherwise the only supported language.</summary>
    public string? ResolvedDefault => !string.IsNullOrWhiteSpace(Default)
        ? Default.Trim()
        : CleanSupported.Count == 1 ? CleanSupported[0] : null;
}

public sealed record SiteIntentContent
{
    public required string Kind { get; init; }
    public SiteIntentMarkdownContent? Markdown { get; init; }
    public SiteIntentNotionContent? Notion { get; init; }
}

public sealed record SiteIntentMarkdownContent
{
    public string Dir { get; init; } = "content";
}

public sealed record SiteIntentNotionContent
{
    public required string DatabaseId { get; init; }
    public SiteIntentNotionFieldPolicy FieldPolicy { get; init; } = new();
}

public sealed record SiteIntentNotionFieldPolicy
{
    public string Mode { get; init; } = "whitelist";
    public IReadOnlyList<string>? Allowed { get; init; }
}

public sealed record SiteIntentTheme
{
    public required string Name { get; init; }
    public IReadOnlyDictionary<string, object>? Params { get; init; }
}

public sealed record SiteIntentFeatures
{
    public bool? Sitemap { get; init; }
    public bool? Rss { get; init; }
    public bool? Search { get; init; }
}

public sealed record SiteIntentDeployment
{
    public string? Target { get; init; }
}
