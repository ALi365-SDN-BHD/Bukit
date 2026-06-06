namespace Bukit.Engine;

public static class SeoInjectionPolicy
{
    internal static bool ShouldSkip(Bukit.Engine.Abstractions.Content.ContentDocument document)
    {
        if (!document.CustomFields.TryGetValue("seo_inject", out var field))
        {
            return false;
        }

        return field.Value is false or "false" or "off";
    }

    public static bool ShouldSkip(IReadOnlyDictionary<string, object> meta)
    {
        if (!meta.TryGetValue("seo_inject", out var value) || value is null)
        {
            return false;
        }

        return value is false or "false" or "off";
    }
}
