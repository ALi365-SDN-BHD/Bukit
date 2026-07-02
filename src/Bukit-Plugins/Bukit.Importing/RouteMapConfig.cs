namespace Bukit.Importing;

public sealed class RouteMapConfig
{
    public List<RouteMapPage> Pages { get; init; } = [];
}

public sealed record RouteMapPage
{
    public string Source { get; init; } = "";
    public string Route { get; init; } = "";
    public string Type { get; init; } = "";
    public string Template { get; init; } = "";
    public string? Slug { get; init; }
    public string? Description { get; init; }
}
