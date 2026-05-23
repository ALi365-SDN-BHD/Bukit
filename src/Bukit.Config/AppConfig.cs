namespace Bukit.Config;

public sealed record AppConfig
{
    public required SiteConfig Site { get; init; }
    public required ContentConfig Content { get; init; }
    public BuildConfig Build { get; init; } = new();
    public ThemeConfig Theme { get; init; } = new();
    public TaxonomyConfig Taxonomy { get; init; } = new();
    public LoggingConfig Logging { get; init; } = new();
    public DeployConfig? Deploy { get; init; }
}

public sealed record SiteConfig
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public SeoConfig Seo { get; init; } = new();
    public AnalyticsConfig Analytics { get; init; } = new();
    public bool AutoSummary { get; init; }
    public int AutoSummaryMaxLength { get; init; } = 200;
    public string BaseUrl { get; init; } = "/";
    public string OutputPathEncoding { get; init; } = "none";
    public string Language { get; init; } = "zh-CN";
    public IReadOnlyList<string>? Languages { get; init; }
    public string? DefaultLanguage { get; init; }
    public string SitemapMode { get; init; } = "split";
    public string RssMode { get; init; } = "split";
    public string SearchMode { get; init; } = "split";
    public bool SearchIncludeDerived { get; init; }
    public bool ExternalProtocolIncludeRoutedPages { get; init; }
    public string PluginFailMode { get; init; } = "strict";
    public string DeriveConflictPolicy { get; init; } = "fail";
    public string Timezone { get; init; } = "Asia/Shanghai";
    public IReadOnlyDictionary<string, string>? Permalinks { get; init; }
    public IReadOnlyDictionary<string, CollectionConfig>? Collections { get; init; }
    public IReadOnlyDictionary<string, ExternalPluginConfig>? ExternalPlugins { get; init; }
    // DESKTOP-REMOVED: ExternalAssembly loading disabled (AOT-only).
    // public string ExternalAssemblyTrustMode { get; init; } = "warn";
    // public IReadOnlyDictionary<string, string>? ExternalAssemblyAllowlist { get; init; }
    public IReadOnlyDictionary<string, PluginToggleConfig>? Plugins { get; init; }
}

public sealed record SeoConfig
{
    public bool Enabled { get; init; } = true;
    public string RenderMode { get; init; } = "inject";
    public string Diagnostics { get; init; } = "warn";
    public string? DefaultImage { get; init; }
    public string? TwitterSite { get; init; }
    public SeoOrganizationConfig? Organization { get; init; }
    public SeoRobotsTxtConfig RobotsTxt { get; init; } = new();
    public SeoSchemaConfig Schema { get; init; } = new();
    public SeoGeoConfig Geo { get; init; } = new();
}

public sealed record SeoOrganizationConfig
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Logo { get; init; }
}

public sealed record AnalyticsConfig
{
    public bool Enabled { get; init; } = true;
    public string? GoogleAnalyticsId { get; init; }
    public bool DisableInPreview { get; init; } = true;
}

public sealed record SeoRobotsTxtConfig
{
    public bool Enabled { get; init; }
}

public sealed record SeoSchemaConfig
{
    public bool WebPage { get; init; } = true;
    public bool CollectionPage { get; init; } = true;
    public bool SearchAction { get; init; } = true;
}

public sealed record SeoGeoConfig
{
    public bool Enabled { get; init; } = true;
    public bool LlmsTxt { get; init; } = true;
    public bool LlmsFullTxt { get; init; }
    public int LlmsTxtMaxArticles { get; init; } = 20;
    public string AiBotMode { get; init; } = "allow";
    public IReadOnlyList<string>? AiBotAllowList { get; init; }
    public IReadOnlyList<string>? AiBotBlockList { get; init; }
    public IReadOnlyList<LlmsTxtOptionalLink>? LlmsTxtOptionalLinks { get; init; }
}

public sealed record LlmsTxtOptionalLink
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
}

public sealed record CollectionConfig
{
    public required string Permalink { get; init; }
    public required string Template { get; init; }
    public string? ListRoute { get; init; }
    public string? ListTemplate { get; init; }
    public CollectionPaginationConfig Pagination { get; init; } = new();
    public CollectionOutputConfig Output { get; init; } = new();
    public IReadOnlyList<FilteredListConfig>? FilteredLists { get; init; }
    public IReadOnlyList<SchemaFieldDefinition>? Schema { get; init; }
}

public sealed record SchemaFieldDefinition
{
    public required string Name { get; init; }
    public string Type { get; init; } = "string";
    public string? Label { get; init; }
    public bool Required { get; init; }
    public object? Default { get; init; }
}

public sealed record CollectionPaginationConfig
{
    public bool Enabled { get; init; }
    public int PageSize { get; init; } = 10;
}

public sealed record CollectionOutputConfig
{
    public bool Rss { get; init; } = true;
    public bool Sitemap { get; init; } = true;
    public bool Archive { get; init; }
}

public sealed record FilteredListConfig
{
    public required string Field { get; init; }
    public required string Value { get; init; }
    public required string ListRoute { get; init; }
    public string? ListTemplate { get; init; }
}

public sealed record ExternalPluginConfig
{
    public required string Runtime { get; init; }
    public required string Entry { get; init; }
    public IReadOnlyList<string> Hooks { get; init; } = Array.Empty<string>();
    public bool Enabled { get; init; } = true;
    public int TimeoutMs { get; init; } = 5000;
    // DESKTOP-REMOVED: wasm runtime disabled (AOT-only).
    // public string WasmProfile { get; init; } = "wasi-preview1";
    // public int MaxMemoryMb { get; init; } = 64;
    // public string WasmFsMode { get; init; } = "output-only";
    // public bool WasmAllowNetwork { get; init; }
    // public IReadOnlyList<string>? Capabilities { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}

public sealed record ContentConfig
{
    public required string Provider { get; init; }
    public IReadOnlyList<ContentSourceConfig>? Sources { get; init; }
    public NotionConfig? Notion { get; init; }
    public MarkdownConfig? Markdown { get; init; }
    public MediaConfig Media { get; init; } = new();
}

public sealed record ContentSourceConfig
{
    public required string Type { get; init; }
    public string? Name { get; init; }
    public string Mode { get; init; } = "content";
    public string? Collection { get; init; }
    public IReadOnlyList<string>? AddToCollections { get; init; }
    public NotionConfig? Notion { get; init; }
    public MarkdownConfig? Markdown { get; init; }
}

public sealed record NotionConfig
{
    public required string DatabaseId { get; init; }
    public int PageSize { get; init; } = 50;
    public int? MaxItems { get; init; }
    public bool? RenderContent { get; init; }
    public int? RenderConcurrency { get; init; }
    public int? MaxRps { get; init; }
    public int? MaxRetries { get; init; }
    public NotionFieldPolicyConfig FieldPolicy { get; init; } = new();
    public string FilterProperty { get; init; } = "Published";
    public string FilterType { get; init; } = "checkbox_true";
    public string? FilterValue { get; init; }
    public string? SortProperty { get; init; }
    public string SortDirection { get; init; } = "ascending";
    public IReadOnlyList<string>? IncludeSlugs { get; init; }
    public string IncludeSlugProperty { get; init; } = "Slug";
    public string CacheMode { get; init; } = "off";
    public string? CacheDir { get; init; }
}

public sealed record MediaConfig
{
    public bool DownloadToLocal { get; init; } = true;
    public string DownloadDir { get; init; } = "assets/uploads";
    public string UrlBase { get; init; } = "/assets/uploads";
    public string DefaultImageUrl { get; init; } = "/assets/images/noneimg-news.jpg";
    public IReadOnlyList<string> FieldKeys { get; init; } = new[] { "cover", "image", "thumbnail", "og_image", "icon" };
    public int? MaxConcurrency { get; init; } = 4;
    public int? MaxRetries { get; init; } = 3;
    public int? TimeoutMs { get; init; } = 10000;
    public long? MaxFileSizeBytes { get; init; } = 50 * 1024 * 1024;
    public bool BlockPrivateNetworks { get; init; } = true;
    public int? RetryBaseDelayMs { get; init; } = 500;
}

public sealed record NotionFieldPolicyConfig
{
    public string Mode { get; init; } = "whitelist";
    public IReadOnlyList<string>? Allowed { get; init; }
}

public sealed record MarkdownConfig
{
    public string Dir { get; init; } = "content";
    public string DefaultType { get; init; } = "page";
    public int? MaxItems { get; init; }
    public IReadOnlyList<string>? IncludePaths { get; init; }
    public IReadOnlyList<string>? IncludeGlobs { get; init; }
}

public sealed record BuildConfig
{
    public string Output { get; init; } = "dist";
    public bool Clean { get; init; } = true;
    public bool Draft { get; init; }
    public string ListPageContentMode { get; init; } = "auto";
    public string SchemaFailMode { get; init; } = "warn";
}

public sealed record ThemeConfig
{
    public string? Name { get; init; }
    public string? Source { get; init; }
    public string? Extends { get; init; }
    public string Layouts { get; init; } = "layouts";
    public string Assets { get; init; } = "assets";
    public string Static { get; init; } = "static";
    public string? StaticTemplate { get; init; }
    public IReadOnlyDictionary<string, object>? Params { get; init; }
    public IReadOnlyDictionary<string, string>? Shortcodes { get; init; }
    public IReadOnlyDictionary<string, ComponentDefinition>? Components { get; init; }
    public ScssConfig? Scss { get; init; }
    public ImageOptimizationConfig? Images { get; init; }
    public string ComponentValidation { get; init; } = "off";
}

public sealed record ComponentDefinition
{
    public required string Template { get; init; }
    public IReadOnlyDictionary<string, string>? Props { get; init; }
}

public sealed record ScssConfig
{
    public bool Enabled { get; init; }
    public string? EntryPoint { get; init; }
    public string OutputDir { get; init; } = "assets";
}

public sealed record ImageOptimizationConfig
{
    public bool Enabled { get; init; }
    public IReadOnlyList<string> Formats { get; init; } = new[] { "webp" };
    public IReadOnlyList<int> Sizes { get; init; } = new[] { 480, 768, 1200 };
    public int Quality { get; init; } = 80;
}

public sealed record TaxonomyConfig
{
    public string Template { get; init; } = "pages/taxonomy-term.html";
    public string? IndexTemplate { get; init; }
    public string? TermTemplate { get; init; }
    public TaxonomyTemplatesConfig Templates { get; init; } = new();
    public IReadOnlyList<TaxonomyKindConfig>? Kinds { get; init; }
    public string OutputMode { get; init; } = "both";
    public IReadOnlyList<string>? ItemFields { get; init; }
    public int PageSize { get; init; } = 10;
    public bool IndexEnabled { get; init; } = true;
    public string PinField { get; init; } = "pinned";
    public string? PinOrderField { get; init; }
    public IReadOnlyDictionary<string, string>? PinFieldBySource { get; init; }
    public IReadOnlyDictionary<string, string>? PinOrderFieldBySource { get; init; }
}

public sealed record TaxonomyTemplatesConfig
{
    public TaxonomyKindTemplateConfig Tags { get; init; } = new();
    public TaxonomyKindTemplateConfig Categories { get; init; } = new();
}

public sealed record TaxonomyKindConfig
{
    public required string Key { get; init; }
    public string? Kind { get; init; }
    public string? Title { get; init; }
    public string? SingularTitlePrefix { get; init; }
    public string? Template { get; init; }
    public string? IndexTemplate { get; init; }
    public string? TermTemplate { get; init; }
    public bool? IndexEnabled { get; init; }
}

public sealed record TaxonomyKindTemplateConfig
{
    public string? Template { get; init; }
    public string? IndexTemplate { get; init; }
    public string? TermTemplate { get; init; }
}

public sealed record LoggingConfig
{
    public string Level { get; init; } = "info";
}

public sealed record PluginToggleConfig
{
    public bool Enabled { get; init; } = true;
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}
