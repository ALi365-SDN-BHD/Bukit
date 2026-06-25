namespace Bukit.Importing.HtmlDemo;

public sealed record HtmlDemoDryRunOptions(
    string ProjectRoot,
    string DemoDirectory,
    string? RouteMapPath = null);
