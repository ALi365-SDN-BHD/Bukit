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

    /// <summary>The opt-in mini-program data output. Internal until the contract is released.</summary>
    /// <remarks>
    /// Included explicitly because multilingual builds hand the config to worktree workers as JSON,
    /// and JSON serialization skips internal members otherwise.
    /// </remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal MiniProgramConfig MiniProgram { get; init; } = new();
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
    /// <summary>The language used when <c>site.languages</c> is not set.</summary>
    internal const string DefaultSiteLanguage = "zh-CN";

    /// <summary>
    /// The site language. Configuration files do not set it: it is derived from <see cref="Languages"/>
    /// (the default language, or the only entry).
    /// </summary>
    public string Language { get; init; } = DefaultSiteLanguage;

    /// <summary>The site languages. One entry is a single-language site; the loader defaults it to <c>[zh-CN]</c>.</summary>
    public IReadOnlyList<string>? Languages { get; init; }
    public string? DefaultLanguage { get; init; }
    /// <summary>
    /// Whether the default language of a multilingual site builds into its own <c>/&lt;lang&gt;/</c>
    /// directory. Defaults to <c>false</c>: the default language builds into the output root.
    /// </summary>
    public bool DefaultLanguageInSubdir { get; init; }
    public bool DefaultLanguageRedirect { get; init; } = true;
    public bool DefaultLanguageLegacyRedirects { get; init; }
    public string SitemapMode { get; init; } = "split";
    public bool SearchIncludeDerived { get; init; }
    public string PluginFailMode { get; init; } = "strict";
    public string DeriveConflictPolicy { get; init; } = "fail";
    public string Timezone { get; init; } = "Asia/Shanghai";
    public IReadOnlyDictionary<string, string>? Permalinks { get; init; }
    public IReadOnlyDictionary<string, CollectionConfig>? Collections { get; init; }
    public IReadOnlyDictionary<string, PluginToggleConfig>? Plugins { get; init; }
    public FeedConfig Feed { get; init; } = new();
    public SitemapDetailConfig SitemapDetail { get; init; } = new();
    public PaginationGlobalConfig Pagination { get; init; } = new();
    public SearchDetailConfig Search { get; init; } = new();
    public RelatedConfig Related { get; init; } = new();
    /// <summary>site.queries: named lists computed from the site's entries, read in templates as <c>site.queries.&lt;name&gt;</c>.</summary>
    /// <remarks>Internal: configured only through site.yaml, so it does not widen the public API.</remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyList<SiteQueryConfig> Queries { get; init; } = [];
    public IReadOnlyDictionary<string, IReadOnlyList<MenuConfig>>? Menus { get; init; }
    public SharedAssetsConfig SharedAssets { get; init; } = new();
    /// <summary>Per-language site identity, keyed by a <see cref="Languages"/> entry; applied to that language's build.</summary>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyDictionary<string, SiteLanguageIdentityConfig>? I18n { get; init; }
}

/// <summary>Values that replace the top-level site identity in one language's build.</summary>
internal sealed record SiteLanguageIdentityConfig
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    /// <summary>Fields that replace the matching <c>site.seo.organization</c> fields; unset fields keep theirs.</summary>
    public SiteLanguageOrganizationConfig? Organization { get; init; }
}

internal sealed record SiteLanguageOrganizationConfig
{
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Logo { get; init; }
    public IReadOnlyList<string>? SameAs { get; init; }
}

public sealed record SharedAssetsConfig
{
    public bool Enabled { get; init; }
    public IReadOnlyList<string>? Paths { get; init; }
}

public sealed record SeoConfig
{
    public bool Enabled { get; init; } = true;
    public string RenderMode { get; init; } = "inject";
    public string Diagnostics { get; init; } = "warn";
    public string HomeTitleTemplate { get; init; } = "{siteTitle}";
    public string PageTitleTemplate { get; init; } = "{pageTitle}";
    public string TitleSeparator { get; init; } = " | ";
    public string? DefaultImage { get; init; }
    public string? TwitterSite { get; init; }
    /// <summary>SEO author used when content has no author, author relation, or GEO author.</summary>
    public string? DefaultAuthor { get; init; }
    /// <summary>Schema type of <see cref="DefaultAuthor"/>: Person (default) or Organization.</summary>
    public string? DefaultAuthorType { get; init; }
    public SeoOrganizationConfig? Organization { get; init; }
    public SeoRobotsTxtConfig RobotsTxt { get; init; } = new();
    public SeoSchemaConfig Schema { get; init; } = new();
    public SeoGeoConfig Geo { get; init; } = new();
    /// <summary>Author profiles keyed (case-insensitively) by author name or <c>authoredBy</c> id.</summary>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyDictionary<string, SeoAuthorProfileConfig>? Authors { get; init; }
}

/// <summary>A <c>site.seo.authors</c> entry: the structured-data profile of one author.</summary>
internal sealed record SeoAuthorProfileConfig
{
    /// <summary>Display name for an <c>authoredBy</c> id; a text author keeps its own byline.</summary>
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? Url { get; init; }
    public string? Image { get; init; }
    public IReadOnlyList<string> SameAs { get; init; } = [];
}

public sealed record SeoOrganizationConfig
{
    public string Type { get; init; } = "Organization";
    public string? Name { get; init; }
    public string? Url { get; init; }
    public string? Logo { get; init; }
    public IReadOnlyList<string> SameAs { get; init; } = Array.Empty<string>();
}

public sealed record AnalyticsConfig
{
    public bool Enabled { get; init; } = true;
    public bool ProductionOnly { get; init; } = true;
    public AnalyticsConsentConfig? Consent { get; init; }
    public AnalyticsCspConfig? Csp { get; init; }
    public IReadOnlyList<AnalyticsProviderConfig> Providers { get; init; } = Array.Empty<AnalyticsProviderConfig>();
}

public sealed record AnalyticsCspConfig
{
    public string? Mode { get; init; }
}

public sealed record AnalyticsConsentConfig
{
    public AnalyticsGoogleConsentConfig? Google { get; init; }
}

public sealed record AnalyticsGoogleConsentConfig
{
    public string? Mode { get; init; }
    public AnalyticsGoogleConsentDefaultsConfig? Defaults { get; init; }
    public int? WaitForUpdateMs { get; init; }
}

public sealed record AnalyticsGoogleConsentDefaultsConfig
{
    public string? AdStorage { get; init; }
    public string? AnalyticsStorage { get; init; }
    public string? AdUserData { get; init; }
    public string? AdPersonalization { get; init; }
}

public sealed record AnalyticsProviderConfig
{
    public required string Type { get; init; }
    public string? MeasurementId { get; init; }
    public string? ContainerId { get; init; }
    public string? Domain { get; init; }
    public string? SnippetMode { get; init; }
    public string? WebsiteId { get; init; }
    public string? ScriptUrl { get; init; }
}

public sealed record SeoRobotsTxtConfig
{
    public bool Enabled { get; init; }

    // Content Signals (https://contentsignals.org), from site.seo.robotsTxt.contentSignal:
    // how AI companies may use the site's content. Unset values are left out of the line.
    public bool? ContentSignalSearch { get; init; }
    public bool? ContentSignalAiInput { get; init; }
    public bool? ContentSignalAiTrain { get; init; }
}

public sealed record SeoSchemaConfig
{
    public bool WebPage { get; init; } = true;
    public bool CollectionPage { get; init; } = true;
    public bool SearchAction { get; init; } = true;
    /// <summary>Starts every BreadcrumbList with the home page.</summary>
    public bool BreadcrumbHome { get; init; }
    /// <summary>Taxonomy kind whose first term page becomes the breadcrumb parent of content pages.</summary>
    public string? BreadcrumbParent { get; init; }
    /// <summary>Schema type of article pages that set none; defaults to BlogPosting.</summary>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal string? ArticleType { get; init; }
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
    public string? Template { get; init; }
    public string? ListRoute { get; init; }
    public string? ListTitle { get; init; }
    public string? ListDescription { get; init; }
    public string? ListTemplate { get; init; }
    public string? SchemaFailMode { get; init; }
    public bool NoindexWhenEmpty { get; init; }
    public CollectionIndexPolicyConfig IndexPolicy { get; init; } = new();
    public CollectionPaginationConfig Pagination { get; init; } = new();
    public CollectionOutputConfig Output { get; init; } = new();
    public IReadOnlyList<FilteredListConfig>? FilteredLists { get; init; }
    /// <summary>From <c>seo.articleType</c>: overrides <see cref="SeoSchemaConfig.ArticleType"/> for this collection's articles.</summary>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal string? ArticleType { get; init; }
    /// <summary>
    /// From <c>routeWhen</c>: only entries whose field equals the value get a page; the others stay
    /// available to content links. Null gives every entry a page.
    /// </summary>
    // Internal: configured only through site.yaml, so it does not widen the public API.
    [System.Text.Json.Serialization.JsonInclude]
    internal CollectionRouteWhenConfig? RouteWhen { get; init; }
}

/// <summary>One site.queries.&lt;name&gt; entry: filter, order and cut one collection's entries.</summary>
internal sealed record SiteQueryConfig
{
    public required string Name { get; init; }
    /// <summary>The collection read, pages and entries without a page alike.</summary>
    public required string From { get; init; }
    /// <summary>Every filter must match.</summary>
    public IReadOnlyList<SiteQueryFilterConfig> Where { get; init; } = [];
    /// <summary>Applied in order after the collection list order, so equal entries keep that order.</summary>
    public IReadOnlyList<SiteQuerySortConfig> Sort { get; init; } = [];
    /// <summary>Fields copied into each result entry, next to key, title, slug, url and collection.</summary>
    public IReadOnlyList<string> Select { get; init; } = [];
    /// <summary>When set, the result is the distinct values of this field instead of entries.</summary>
    public string? Distinct { get; init; }
    public SiteQueryGroupConfig? Group { get; init; }
    /// <summary>The most entries or values kept; per group when grouped.</summary>
    public int? Limit { get; init; }
}

/// <summary>A site.queries filter: <c>field</c> with <c>eq</c> or <c>in</c>, or <c>hasBackref</c>.</summary>
internal sealed record SiteQueryFilterConfig
{
    public string? Field { get; init; }
    /// <summary>The field, or any item of a list field, must equal one of these.</summary>
    public IReadOnlyList<string> Values { get; init; } = [];
    /// <summary>The entry must have at least one page linking to it through this backref.</summary>
    public string? HasBackref { get; init; }
}

/// <summary>A site.queries sort key: by value and <c>direction</c>, or by position in <c>values</c>.</summary>
internal sealed record SiteQuerySortConfig
{
    public required string Field { get; init; }
    public string Direction { get; init; } = "asc";
    public IReadOnlyList<string> Values { get; init; } = [];
}

/// <summary>site.queries group: one group per value, holding the entries whose field (or any list item) equals it.</summary>
internal sealed record SiteQueryGroupConfig
{
    public required string Field { get; init; }
    public required IReadOnlyList<string> Values { get; init; }
}

/// <summary>site.collections.*.routeWhen: the field test an entry must pass to get a page.</summary>
internal sealed record CollectionRouteWhenConfig
{
    /// <summary>The top-level content field tested.</summary>
    public required string Field { get; init; }
    /// <summary>The value it must equal: text compared case-insensitively, or a boolean or whole number.</summary>
    public required string Eq { get; init; }
}

public sealed record CollectionIndexPolicyConfig
{
    public int MinimumItems { get; init; }
    public string BelowMinimum { get; init; } = "index";
}

public sealed record CollectionPaginationConfig
{
    public bool Enabled { get; init; }
    public int PageSize { get; init; } = 10;
    public string UrlPattern { get; init; } = "page/:num/";
    public bool FirstPageUsesListRoute { get; init; } = true;
}

public sealed record PaginationGlobalConfig
{
    public bool Enabled { get; init; }
    public int PageSize { get; init; } = 10;
}

public sealed record CollectionOutputConfig
{
    public bool Rss { get; init; } = true;
    public bool? Feed { get; init; }
    public bool Sitemap { get; init; } = true;
    public bool Archive { get; init; }
    public string? FeedPath { get; init; }
    public string? FeedTitle { get; init; }
    public string? FeedDescription { get; init; }
    public ArchiveDetailConfig? ArchiveDetail { get; init; }
}

public sealed record ArchiveDetailConfig
{
    public string Depth { get; init; } = "monthly";
    public string? Template { get; init; }
    public string? RoutePrefix { get; init; }
}

public sealed record FilteredListConfig
{
    public required string Field { get; init; }
    public string Operator { get; init; } = "equals";
    public string? Value { get; init; }
    public IReadOnlyList<string>? Values { get; init; }
    public required string ListRoute { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? ListTemplate { get; init; }
    public int? PageSize { get; init; }
    public string? UrlPattern { get; init; }
    public string EmptyBehavior { get; init; } = "render";
}

public sealed record ContentConfig
{
    public IReadOnlyList<ContentSourceConfig>? Sources { get; init; }
    public MediaConfig Media { get; init; } = new();
    public ContentModelSchemaConfig? ModelSchema { get; init; }
    public RouteMetadataConfig? RouteMetadata { get; init; }
    /// <summary>
    /// content.links: resolves key fields of content pages into <c>&lt;field&gt;_links</c> lists of the
    /// referenced documents. Empty when unset.
    /// </summary>
    /// <remarks>Internal: configured only through site.yaml, so it does not widen the public API.</remarks>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyList<ContentLinkConfig> Links { get; init; } = [];
    /// <summary>
    /// content.derivedFields: page fields computed from Scriban expressions over the page's own fields,
    /// in declaration order. Empty when unset.
    /// </summary>
    /// <remarks>Internal: configured only through site.yaml, so it does not widen the public API.</remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyList<ContentDerivedFieldConfig> DerivedFields { get; init; } = [];
    /// <summary>
    /// content.ordering.tieBreakField: orders entries with the same publish time in every list (home page,
    /// collection lists and pagination, archives, site feeds and llms.txt). Use url, title or any content field;
    /// numbers compare numerically. Unset keeps each list's built-in order. Term pages use it when
    /// taxonomy.tieBreakField is unset.
    /// </summary>
    public string? OrderingTieBreakField { get; init; }
    /// <summary>content.ordering.tieBreakOrder: asc (default) or desc. Entries without the field come last.</summary>
    public string OrderingTieBreakOrder { get; init; } = "asc";
}

public sealed record RouteMetadataConfig
{
    public required string Source { get; init; }
    public string RouteField { get; init; } = "route";
    public string TitleField { get; init; } = "title";
    public string SummaryField { get; init; } = "summary";
    public string SeoTitleField { get; init; } = "seo_title";
    public string SeoDescriptionField { get; init; } = "seo_description";
    public IReadOnlyList<string> RequiredRoutes { get; init; } = Array.Empty<string>();
}

/// <summary>One content.links entry: which page field holds keys of which target documents.</summary>
internal sealed record ContentLinkConfig
{
    /// <summary>The collection whose pages hold the keys.</summary>
    public required string Collection { get; init; }
    /// <summary>The key field: <c>name</c>, or a nested path such as <c>days[].attractions</c>.</summary>
    public required string Field { get; init; }
    public required ContentLinkTargetConfig Target { get; init; }
    /// <summary>Top-level target fields copied into each link.</summary>
    public IReadOnlyList<string> Select { get; init; } = [];
    /// <summary>Optional reverse list written on each target page that this link reaches.</summary>
    public ContentLinkBackrefConfig? Backref { get; init; }
}

/// <summary>One content.derivedFields.&lt;collection&gt;.&lt;field&gt; entry.</summary>
internal sealed record ContentDerivedFieldConfig
{
    public required string Collection { get; init; }
    /// <summary>The page field written.</summary>
    public required string Field { get; init; }
    /// <summary>A Scriban template evaluated over the page's top-level fields.</summary>
    public required string Value { get; init; }
    /// <summary><c>default</c> writes only when the field is missing or empty; <c>override</c> always writes.</summary>
    public string Mode { get; init; } = "default";
}

/// <summary>content.links[].backref: lists, on each target page, the pages that link to it.</summary>
internal sealed record ContentLinkBackrefConfig
{
    /// <summary>The target page field that receives the list; <c>&lt;name&gt;_count</c> holds the total.</summary>
    public required string Name { get; init; }
    /// <summary>The most entries kept, in collection order; 0 keeps them all.</summary>
    public int Limit { get; init; }
    /// <summary>Top-level fields of the linking page copied into each entry.</summary>
    public IReadOnlyList<string> Select { get; init; } = [];
}

internal sealed record ContentLinkTargetConfig
{
    /// <summary>The collection holding the referenced documents, pages or data.</summary>
    public required string Collection { get; init; }
    /// <summary>The target field matched against the keys: <c>slug</c> (default) or a content field.</summary>
    public string Key { get; init; } = "slug";
}

public sealed record ContentModelSchemaConfig
{
    public IReadOnlyList<string>? ContentTypes { get; init; }
    public IReadOnlyList<string>? Statuses { get; init; }
    public IReadOnlyList<string>? ReviewStatuses { get; init; }
    public IReadOnlyList<string>? SyncStatuses { get; init; }
    public IReadOnlyList<CanonicalFieldMappingConfig>? CanonicalMappings { get; init; }
    public IReadOnlyList<CustomFieldDefinitionConfig>? CustomFields { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<CustomFieldDefinitionConfig>>? FieldScopes { get; init; }
    public IReadOnlyList<EntityMappingConfig>? EntityMappings { get; init; }
    public IReadOnlyList<RelationMappingConfig>? RelationMappings { get; init; }
    public MediaPolicyConfig? Media { get; init; }
    public bool RejectUnknownRawKeys { get; init; }
    public bool RequireSummary { get; init; }
    public bool RequireAuthor { get; init; }
    public bool RequireOrganization { get; init; }
    public bool RequireUpdatedAt { get; init; }
    public bool RequireProvenance { get; init; }
    public bool RequireReviewedAt { get; init; }
    public bool RequireMediaAlt { get; init; } = true;
    public bool RequireMediaDescription { get; init; }
    public bool RequireMediaLicense { get; init; }
    public bool RequireEntityIds { get; init; }
    public bool RequireRelationTargets { get; init; } = true;
}

public sealed record CanonicalFieldMappingConfig
{
    public required string CanonicalField { get; init; }
    public string? RawKey { get; init; }
    public string? SemanticType { get; init; }
    public bool Required { get; init; }
}

public sealed record CustomFieldDefinitionConfig
{
    public required string Name { get; init; }
    public string FieldType { get; init; } = "string";
    public bool Required { get; init; }
    public string? SemanticType { get; init; }
    public string? Label { get; init; }
    public string? Format { get; init; }
    public IReadOnlyList<string>? Enum { get; init; }
    public double? Min { get; init; }
    public double? Max { get; init; }
    public object? Default { get; init; }
    public string? SourcePolicy { get; init; }
    public ContentReferenceRuleConfig? Reference { get; init; }
    /// <summary>
    /// Display labels for <see cref="Enum"/> values, keyed by value, set when an enum item is
    /// written as <c>{ value, label }</c>. Templates read options in definition order as
    /// <c>site.field_enums.&lt;field&gt;</c>.
    /// </summary>
    /// <remarks>Internal: configured only through site.yaml, so it does not widen the public API.</remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyDictionary<string, string>? EnumLabels { get; init; }
}

public sealed record EntityMappingConfig
{
    public required string RawKey { get; init; }
    public required string EntityType { get; init; }
    public string? IdField { get; init; }
    public string? NameField { get; init; }
    public string? DescriptionField { get; init; }
    public string? UrlField { get; init; }
    public string? SameAsField { get; init; }
    public bool Required { get; init; }
    public ContentReferenceRuleConfig? Reference { get; init; }
}

public sealed record RelationMappingConfig
{
    public required string RawKey { get; init; }
    public required string RelationType { get; init; }
    public string? TargetType { get; init; }
    public string? TargetField { get; init; }
    public string? TargetIdField { get; init; }
    public bool Required { get; init; }
    public ContentReferenceRuleConfig? Reference { get; init; }
}

public sealed record ContentReferenceRuleConfig
{
    public string? TargetType { get; init; }
    public string? IdField { get; init; }
    public string? LabelField { get; init; }
    public string? UrlField { get; init; }
    public bool Required { get; init; }
}

public sealed record MediaPolicyConfig
{
    public bool RequireAlt { get; init; } = true;
    public bool RequireDescription { get; init; }
    public bool RequireLicense { get; init; }
    public IReadOnlyList<string>? AllowedKinds { get; init; }
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
    /// <remarks>
    /// Included explicitly because multilingual builds hand the config to worktree workers as JSON,
    /// and JSON serialization skips internal members otherwise.
    /// </remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal CloudBaseConfig? CloudBase { get; init; }
    public DataIndexConfig? DataIndex { get; init; }
}

public sealed record DataIndexConfig
{
    public string ScopeField { get; init; } = "scope";
    public string KeyField { get; init; } = "key";
    public string ValueField { get; init; } = "value";
    public string ValueTypeField { get; init; } = "value_type";
    public IReadOnlyList<string>? RequiredKeys { get; init; }
}

internal sealed record CloudBaseConfig
{
    public string EnvId { get; init; } = string.Empty;
    public string Region { get; init; } = "cn";
    public string Table { get; init; } = string.Empty;
    public string IdField { get; init; } = "id";
    public int PageSize { get; init; } = 100;
    public int? MaxItems { get; init; }
    public CloudBaseFilterConfig? Filter { get; init; }
    public CloudBaseSortConfig? Sort { get; init; }
    public string TitleField { get; init; } = "title";
    public string SlugField { get; init; } = "slug";
    public string PublishAtField { get; init; } = "publish_at";
    public string UpdatedAtField { get; init; } = "updated_at";
    public string BodyField { get; init; } = string.Empty;
    public string BodyFormat { get; init; } = "markdown";
    public NotionFieldPolicyConfig FieldPolicy { get; init; } = new();
    public IReadOnlyList<string> FileFields { get; init; } = [];
    public string? StorageBucket { get; init; }
    public int SignExpiresSeconds { get; init; } = 3600;
    public string CacheMode { get; init; } = "off";
    public int? MaxRps { get; init; } = 5;
    public int? MaxRetries { get; init; } = 3;
}

internal sealed record CloudBaseFilterConfig
{
    public string Field { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

internal sealed record CloudBaseSortConfig
{
    public string Field { get; init; } = string.Empty;
    public string Direction { get; init; } = "asc";
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
    public NotionPropertyMapConfig? PropertyMap { get; init; }
}

public sealed record NotionPropertyMapConfig
{
    public string? Title { get; init; }
    public string? Slug { get; init; }
    public string? Type { get; init; }
    public string? PublishAt { get; init; }
    public string? Language { get; init; }
    public string? I18nKey { get; init; }
    public string? Summary { get; init; }
    public string? Collection { get; init; }
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
    public string? SeoImage { get; init; }
    public string? Canonical { get; init; }
    public string? OriginalUrl { get; init; }
    public string? References { get; init; }
    public string? EntitiesJson { get; init; }
    public string? Cover { get; init; }
    public string? CoverAlt { get; init; }
    public string? CoverCaption { get; init; }
}

public sealed record MediaConfig
{
    public bool DownloadToLocal { get; init; } = true;
    public string DownloadDir { get; init; } = "assets/uploads";
    public string UrlBase { get; init; } = "/assets/uploads";
    public string DefaultImageUrl { get; init; } = "/assets/images/noneimg-news.jpg";
    public IReadOnlyList<string> FieldKeys { get; init; } = new[] { "cover", "image", "thumbnail", "og_image", "seo_image", "icon" };
    public int? MaxConcurrency { get; init; } = 4;
    public int? MaxRetries { get; init; } = 3;
    public int? TimeoutMs { get; init; } = 10000;
    public long? MaxFileSizeBytes { get; init; } = 50 * 1024 * 1024;
    public bool BlockPrivateNetworks { get; init; } = true;
    public int? RetryBaseDelayMs { get; init; } = 500;
    /// <summary>Result of a failed download: "placeholder" (defaultImageUrl) or "keep" (original URL).</summary>
    public string OnFailure { get; init; } = "placeholder";
    /// <summary>
    /// Set by the build, never read from site.yaml: true when <see cref="OnFailure"/> is "placeholder"
    /// but no theme or site directory provides <see cref="DefaultImageUrl"/>. Failed downloads then keep
    /// their original URL (as "keep" does) and log a warning instead of failing the build.
    /// </summary>
    internal bool PlaceholderMissing { get; init; }
    /// <summary>When non-empty, only these hosts are downloaded. Supports "*." prefix wildcards.</summary>
    public IReadOnlyList<string> DownloadHosts { get; init; } = [];
    /// <summary>Hosts that are never downloaded; their URLs stay unchanged. Supports "*." prefix wildcards.</summary>
    public IReadOnlyList<string> SkipHosts { get; init; } = [];
}

public sealed record NotionFieldPolicyConfig
{
    public string Mode { get; init; } = "whitelist";
    public IReadOnlyList<string>? Allowed { get; init; }
}

public sealed record MarkdownConfig
{
    public string Dir { get; init; } = "content";
    public string DefaultType { get; init; } = string.Empty;
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
    public BuildReportConfig Report { get; init; } = new();
    public string FingerprintMode { get; init; } = "size-time";
    public bool PublishDotFiles { get; init; }
    public bool FollowSymlinks { get; init; }
    public int LanguageJobs { get; init; } = 1;

    /// <summary>Default for <see cref="TemplateLoopLimit"/>; matches the template engine's own default.</summary>
    public const int DefaultTemplateLoopLimit = 1000;

    /// <summary>
    /// Maximum template loop iterations allowed inside one outermost loop, counting nested loops and
    /// loops in functions called from it (build.templateLoopLimit).
    /// </summary>
    public int TemplateLoopLimit { get; init; } = DefaultTemplateLoopLimit;
}

public sealed record BuildReportConfig
{
    public bool Enabled { get; init; } = true;
    public string SecurityFailMode { get; init; } = "auto";
}

public sealed record ThemeConfig
{
    public string? Name { get; init; }
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
    /// <summary>
    /// CSS <c>sizes</c> value added beside a generated srcset when the author set none; lazy images
    /// get <c>auto, </c> in front. Unset adds nothing.
    /// </summary>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal string? SizesAttribute { get; init; }
    /// <summary>
    /// <c>none</c> drops the resized original-format variants (and the fallback img srcset) of JPEG/PNG
    /// media images when WebP is generated; <c>all</c> (default) keeps them for browsers without WebP.
    /// </summary>
    // JsonInclude: the i18n worktree protocol sends the config to language workers and hashes it.
    [System.Text.Json.Serialization.JsonInclude]
    internal string FallbackVariants { get; init; } = ImageFallbackVariants.All;

    /// <summary>True when JPEG/PNG media images get only WebP variants.</summary>
    internal bool WebpOnlyVariants
        => FallbackVariants == ImageFallbackVariants.None &&
           Formats.Any(format => string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase));
}

internal static class ImageFallbackVariants
{
    internal const string All = "all";
    internal const string None = "none";
}

public sealed record TaxonomyConfig
{
    public IReadOnlyList<TaxonomyKindConfig>? Kinds { get; init; }
    public string OutputMode { get; init; } = "both";
    public IReadOnlyList<string>? ItemFields { get; init; }
    public int PageSize { get; init; } = 10;
    public bool IndexEnabled { get; init; } = true;
    public string PinField { get; init; } = "pinned";
    public string? PinOrderField { get; init; }
    public IReadOnlyDictionary<string, string>? PinFieldBySource { get; init; }
    public IReadOnlyDictionary<string, string>? PinOrderFieldBySource { get; init; }
    /// <summary>How to emit term pages with no content: render (default), noindex, or skip.</summary>
    public string EmptyTerms { get; init; } = "render";
    /// <summary>Orders term-page entries published at the same time: title, url (the route URL), or any content field. Numbers compare numerically. Unset uses content.ordering, then title.</summary>
    public string? TieBreakField { get; init; }
    /// <summary>Direction for <see cref="TieBreakField"/>: asc (default) or desc. Entries without the field come last.</summary>
    public string? TieBreakOrder { get; init; }
}

public sealed record TaxonomyKindConfig
{
    public required string Key { get; init; }
    public string? Kind { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? SingularTitlePrefix { get; init; }
    public string? Template { get; init; }
    public string? IndexTemplate { get; init; }
    public string? TermTemplate { get; init; }
    public bool? IndexEnabled { get; init; }
    public bool Hierarchical { get; init; }
    public string? RoutePrefix { get; init; }
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

public sealed record FeedConfig
{
    public string Mode { get; init; } = "split";
    public IReadOnlyList<string> Formats { get; init; } = new[] { "rss" };
    public int Limit { get; init; } = 20;
    public string Path { get; init; } = "feed";
}

public sealed record SitemapDetailConfig
{
    public double DefaultPriority { get; init; } = 0.5;
    public string DefaultChangefreq { get; init; } = "weekly";
    public bool ImageEnabled { get; init; }
    public bool VideoEnabled { get; init; }
}

public sealed record SearchDetailConfig
{
    public string Mode { get; init; } = "split";
    public string? Route { get; init; }
    public string Ui { get; init; } = "default";
    public string UiTheme { get; init; } = "light";
    public string? PlaceholderText { get; init; }
    public int MaxContentLength { get; init; } = 8000;
}

public sealed record RelatedConfig
{
    public bool Enabled { get; init; }
    public int Threshold { get; init; } = 80;
    public int Limit { get; init; } = 5;
    public IReadOnlyList<RelatedIndexConfig> Indices { get; init; } = new[]
    {
        new RelatedIndexConfig { Name = "tags", Weight = 80 },
        new RelatedIndexConfig { Name = "categories", Weight = 60 }
    };
    /// <summary>
    /// site.related.collections: rules for pages of one collection, compared only with pages of
    /// the same collection. Pages of other collections keep the rules above.
    /// </summary>
    /// <remarks>Internal: configured only through site.yaml, so it does not widen the public API.</remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal IReadOnlyList<RelatedCollectionConfig> Collections { get; init; } = [];
}

public sealed record RelatedIndexConfig
{
    public required string Name { get; init; }
    public int Weight { get; init; } = 100;
    /// <summary>
    /// A content field scored like tags: the number of shared values times <see cref="Weight"/>.
    /// Set instead of a built-in <see cref="Name"/> (which is then <c>field</c>).
    /// </summary>
    /// <remarks>Internal: configured only through site.yaml, so it does not widen the public API.</remarks>
    [System.Text.Json.Serialization.JsonInclude]
    internal string? Field { get; init; }
}

/// <summary>One site.related.collections.&lt;collection&gt; entry.</summary>
internal sealed record RelatedCollectionConfig
{
    public required string Collection { get; init; }
    public required int Threshold { get; init; }
    public required int Limit { get; init; }
    public required IReadOnlyList<RelatedIndexConfig> Indices { get; init; }
}

public sealed record MenuConfig
{
    public required string Identifier { get; init; }
    public required string Name { get; init; }
    public required string Url { get; init; }
    public int Weight { get; init; } = 1;
    public IReadOnlyList<MenuConfig>? Children { get; init; }
}
