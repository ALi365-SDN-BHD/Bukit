using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

public static class SeoInjectionPolicy
{
    public static bool ShouldSkip(IReadOnlyDictionary<string, ContentField>? fields)
    {
        var value = ContentFieldReader.GetText(fields, "seo_inject");
        return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
               ContentFieldReader.GetBool(fields, "seo_inject") is false;
    }
}
