namespace Bukit.Config;

public static class ContentConfigResolver
{
    public static string Describe(ContentConfig content)
    {
        return content.Sources is { Count: > 0 } ? "sources" : "unknown";
    }
}
