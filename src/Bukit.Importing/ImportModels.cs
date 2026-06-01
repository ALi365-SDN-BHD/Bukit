namespace Bukit.Importing;

public sealed record HtmlDemoImportOptions
{
    public required string InputPath { get; init; }
    public required string ThemeName { get; init; }
    public required string RootDir { get; init; }
    public bool Force { get; init; }
    public bool Use { get; init; }
    public bool Verify { get; init; }
    public string Language { get; init; } = "zh";
    public bool ExtractContent { get; init; } = true;
    public bool GenerateSeed { get; init; } = true;
    public string ContentSource { get; init; } = "notion";
    public string? SitePath { get; init; }
    public bool DryRun { get; init; }
    public bool Strict { get; init; }
    public bool Overwrite { get; init; }
    public bool PreserveHtml { get; init; } = true;
    public bool GenerateReport { get; init; } = true;
    public string? BaseUrl { get; init; }
    public bool NoMarkdownDraft { get; init; }
    public string? RouteMapPath { get; init; }
    public string? NotionDatabaseId { get; init; }
    public string? NotionTokenEnv { get; init; }
}

public sealed record DiscoveredPage
{
    public required string FilePath { get; init; }
    public required string RelativePath { get; init; }
    public required string Slug { get; init; }
    public required PageType Type { get; init; }
    public string? Title { get; init; }
    public string FullHtml { get; init; } = "";
    public string? HeadContent { get; init; }
    public string BodyContent { get; init; } = "";
    public string BodyOpening { get; init; } = "";
    public string UniqueBody { get; init; } = "";
    public string BodyClosing { get; init; } = "";
    public List<string> AssetPaths { get; init; } = [];
}

public enum PageType
{
    Home,
    Page,
    PostList,
    PostDetail,
    CompanyList,
    CompanyDetail,
    ServiceList,
    ServiceDetail,
    Unknown
}

public sealed record ImportResult
{
    public required string ThemePath { get; init; }
    public string? SitePath { get; init; }
    public int PagesFound { get; init; }
    public int TemplatesGenerated { get; init; }
    public int PartialsGenerated { get; init; }
    public int AssetsCopied { get; init; }
    public int ComponentsGenerated { get; init; }
    public int RecordsExtracted { get; init; }
    public bool SiteYamlCreated { get; init; }
    public bool TemplatesSynced { get; init; }
    public bool SeedGenerated { get; init; }
    public List<string> Warnings { get; init; } = [];
    public List<ImportDiagnostic> Diagnostics { get; init; } = [];
    public List<ImportReportPage> ReportPages { get; init; } = [];
    public List<ImportReportComponent> ReportComponents { get; init; } = [];
    public List<ImportReportSeedFile> ReportSeedFiles { get; init; } = [];
    public HardcodedContentReport? HardcodedContentReport { get; init; }
}

public sealed record ImportReportPage(
    string Source,
    string Route,
    string Type,
    string Template,
    string Status);

public sealed record ImportReportComponent(
    string Name,
    string Source,
    string Status);

public sealed record ImportReportSeedFile(
    string FileName,
    int Count);

public sealed record DiscoveredComponent
{
    public required string Name { get; init; }
    public required string HtmlFragment { get; init; }
    public required List<DiscoveredPage> UsedBy { get; init; }
    public string? NormalizedTemplate { get; init; }
}

public sealed record ExtractedContent
{
    public List<PageRecord> Pages { get; set; } = [];
    public List<SectionRecord> Sections { get; set; } = [];
    public List<PostRecord> Posts { get; set; } = [];
    public List<CompanyRecord> Companies { get; set; } = [];
    public List<ServiceRecord> Services { get; set; } = [];
    public List<FaqRecord> Faqs { get; set; } = [];
}

public sealed record PageRecord
{
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public string Type { get; init; } = "Page";
    public string Template { get; init; } = "page";
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string Language { get; init; } = "zh";
    public bool Published { get; init; } = true;
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
}

public sealed record SectionRecord
{
    public string? PageSlug { get; init; }
    public required string SectionType { get; init; }
    public string? Heading { get; init; }
    public string? Subheading { get; init; }
    public string? ButtonText { get; init; }
    public string? ButtonUrl { get; init; }
    public int SortOrder { get; init; }
    public string Language { get; init; } = "zh";
    public bool Published { get; init; } = true;
}

public sealed record TemplateResidueAnalysis
{
    public string TemplatePath { get; init; } = "";
    public int ResidualTextCount { get; init; }
    public int TotalTextSegments { get; init; }
    public string Severity { get; init; } = "low";
    public List<string> ResidualSamples { get; init; } = [];
}

public sealed record HardcodedContentReport
{
    public int OverallScore { get; init; }
    public List<TemplateResidueAnalysis> Residues { get; init; } = [];
    public int TotalResidualCount { get; init; }
}

public sealed record PostRecord
{
    public required string Title { get; init; }
    public string Slug { get; init; } = "";
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string? Category { get; init; }
    public List<string> Tags { get; init; } = [];
    public string Language { get; init; } = "zh";
    public bool Published { get; init; } = true;
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
}

public sealed record CompanyRecord
{
    public required string Title { get; init; }
    public string Slug { get; init; } = "";
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string? Country { get; init; }
    public string? Industry { get; init; }
    public string Language { get; init; } = "zh";
    public bool Published { get; init; } = true;
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
}

public sealed record ServiceRecord
{
    public required string Title { get; init; }
    public string Slug { get; init; } = "";
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string Language { get; init; } = "zh";
    public bool Published { get; init; } = true;
    public string? SeoTitle { get; init; }
    public string? SeoDescription { get; init; }
}

public sealed record FaqRecord
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public string? PageSlug { get; init; }
    public string? Category { get; init; }
    public int SortOrder { get; init; }
    public string Language { get; init; } = "zh";
    public bool Published { get; init; } = true;
}
