namespace Bukit.Engine;

public static class SeoInjectionPolicy
{
    public static bool ShouldSkip(IReadOnlyDictionary<string, object> meta)
    {
        if (!meta.TryGetValue("seo_inject", out var value) || value is null)
        {
            return false;
        }

        return value is false or "false" or "off";
    }
}
