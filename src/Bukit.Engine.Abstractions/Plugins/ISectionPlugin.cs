namespace Bukit.Engine.Abstractions.Plugins;

public enum SectionHook
{
    BeforeRender,
    AfterRender,
    ResolveItems
}

public sealed class SectionContext
{
    public required string SectionType { get; init; }
    public string? Variant { get; init; }
    public Dictionary<string, object?>? Props { get; set; }
    public string? RenderedHtml { get; set; }
    public Dictionary<string, object?> Data { get; init; } = new();
}

public interface ISectionPlugin
{
    SectionHook SupportedHook { get; }
    Task ExecuteAsync(SectionContext context, CancellationToken ct = default);
}
