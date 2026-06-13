namespace Bukit.Config;

public sealed record ConfigOverrides
{
    public string? Output { get; init; }
    public string? BaseUrl { get; init; }
    public bool? Clean { get; init; }
    public bool? Draft { get; init; }
    public bool IsCI { get; init; }
    public bool? Incremental { get; init; }
    public string? CacheDir { get; init; }
    public string? MetricsPath { get; init; }
    public int? Jobs { get; init; }
}

public static class ConfigApplier
{
    public static AppConfig Apply(AppConfig config, ConfigOverrides overrides)
    {
        var site = config.Site;
        var build = config.Build;

        if (!string.IsNullOrWhiteSpace(overrides.BaseUrl))
        {
            site = site with { BaseUrl = overrides.BaseUrl! };
        }

        if (!string.IsNullOrWhiteSpace(overrides.Output))
        {
            build = build with { Output = overrides.Output! };
        }

        if (overrides.Clean is not null)
        {
            build = build with { Clean = overrides.Clean.Value };
        }

        if (overrides.Draft is not null)
        {
            build = build with { Draft = overrides.Draft.Value };
        }

        if (overrides.IsCI)
        {
            build = build with { FollowSymlinks = false };
        }

        return config with
        {
            Site = site,
            Build = build
        };
    }
}
