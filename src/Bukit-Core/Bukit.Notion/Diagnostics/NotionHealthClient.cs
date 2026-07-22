using Bukit.Notion.Transport;
using System.Net;
using System.Text.Json;

namespace Bukit.Notion.Diagnostics;

public sealed class NotionHealthClient
{
    private readonly NotionClient _client;

    public NotionHealthClient(NotionClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public Task<NotionHealthResult> CheckConnectivityAsync(
        CancellationToken cancellationToken = default)
        => ProbeAsync(NotionApiUrls.UsersMe(), cancellationToken);

    public Task<NotionHealthResult> CheckDatabaseAsync(
        string databaseId,
        CancellationToken cancellationToken = default)
    {
        ValidateDatabaseId(databaseId);
        return ProbeAsync(NotionApiUrls.Database(databaseId), cancellationToken);
    }

    public async Task<NotionDatabaseSchema> InspectDatabaseSchemaAsync(
        string databaseId,
        CancellationToken cancellationToken = default)
    {
        ValidateDatabaseId(databaseId);
        try
        {
            using var document = await _client.GetAsync(
                NotionApiUrls.Database(databaseId),
                cancellationToken);
            var properties = new List<NotionDatabaseProperty>();
            if (document.RootElement.TryGetProperty("properties", out var propertyObject) &&
                propertyObject.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in propertyObject.EnumerateObject())
                {
                    var type = property.Value.TryGetProperty("type", out var typeElement) &&
                               typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString() ?? string.Empty
                        : string.Empty;
                    properties.Add(new NotionDatabaseProperty(property.Name, type));
                }
            }

            return new NotionDatabaseSchema(
                IsSuccess: true,
                Properties: properties,
                StatusCode: HttpStatusCode.OK);
        }
        catch (NotionApiException exception)
        {
            return new NotionDatabaseSchema(
                IsSuccess: false,
                Properties: [],
                StatusCode: exception.StatusCode,
                ReasonPhrase: exception.ReasonPhrase,
                ErrorKind: exception.Kind,
                ErrorMessage: exception.Message);
        }
    }

    private async Task<NotionHealthResult> ProbeAsync(
        string url,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await _client.GetAsync(url, cancellationToken);
            return new NotionHealthResult(IsSuccess: true, StatusCode: HttpStatusCode.OK);
        }
        catch (NotionApiException exception)
        {
            if (exception.Kind == NotionApiErrorKind.InvalidJson &&
                IsSuccessfulStatus(exception.StatusCode))
            {
                return new NotionHealthResult(IsSuccess: true, StatusCode: exception.StatusCode);
            }

            return new NotionHealthResult(
                IsSuccess: false,
                StatusCode: exception.StatusCode,
                ReasonPhrase: exception.ReasonPhrase,
                ErrorKind: exception.Kind,
                ErrorMessage: exception.Message);
        }
    }

    private static bool IsSuccessfulStatus(HttpStatusCode? statusCode)
        => statusCode is not null && (int)statusCode >= 200 && (int)statusCode <= 299;

    private static void ValidateDatabaseId(string databaseId)
    {
        if (string.IsNullOrWhiteSpace(databaseId))
        {
            throw new ArgumentException("Notion database ID is required.", nameof(databaseId));
        }
    }
}
