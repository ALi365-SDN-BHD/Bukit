using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bukit.CloudBase.Models;

internal sealed record CloudBaseQueryRequest
{
    public required string Table { get; init; }
    public IReadOnlyList<string>? Select { get; init; }
    public string? FilterField { get; init; }
    public string? FilterValue { get; init; }
    public string? SortField { get; init; }
    public string SortDirection { get; init; } = "asc";
    public required string IdField { get; init; }
    public int PageSize { get; init; } = 100;
    public int? MaxItems { get; init; }
}

internal sealed record CloudBaseQueryResult(IReadOnlyList<JsonElement> Rows, int? TotalCount);

internal sealed record CloudBaseSignedObject(string ObjectName, string? Url, string? Error)
{
    public override string ToString()
        => $"CloudBaseSignedObject {{ ObjectName = {ObjectName}, HasUrl = {Url is not null}, Error = {Error} }}";
}

internal sealed record CloudBaseSignRequestPayload(
    [property: JsonPropertyName("expiresIn")] int ExpiresIn,
    [property: JsonPropertyName("paths")] IReadOnlyList<string> Paths);
