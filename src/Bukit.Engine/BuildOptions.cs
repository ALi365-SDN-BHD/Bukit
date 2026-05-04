namespace Bukit.Engine;

public sealed record BuildOptions
{
    public string OutputDir { get; init; } = "dist";
    public string? SiteUrl { get; init; }
    public string SiteTitle { get; init; } = "Bukit";
    public string BaseUrl { get; init; } = "/";
    public string OutputPathEncoding { get; init; } = "none";
    public string? AssetsDir { get; init; }
    public bool Clean { get; init; } = true;
    public bool IsCI { get; init; }
    public bool GenerateSitemap { get; init; } = true;
    public bool GenerateRss { get; init; } = true;
}
