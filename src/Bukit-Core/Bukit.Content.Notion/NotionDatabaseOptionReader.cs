using System.Text;
using System.Text.Json;
using Bukit.Notion;
using Bukit.Notion.Transport;
using Bukit.Shared;

namespace Bukit.Content.Notion;

internal static class NotionDatabaseOptionReader
{
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadAsync(
        NotionContentClient client,
        string databaseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        return await ReadCoreAsync(client.GetAsync, databaseId, cancellationToken);
    }

    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadAsync(
        NotionClient client,
        string databaseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        try
        {
            return await ReadCoreAsync(client.GetAsync, databaseId, cancellationToken);
        }
        catch (NotionApiException exception)
        {
            throw new ContentException(exception.Message, exception);
        }
    }

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadCoreAsync(
        Func<string, CancellationToken, Task<JsonDocument>> getAsync,
        string databaseId,
        CancellationToken cancellationToken)
    {
        using var document = await getAsync(NotionApiUrls.Database(databaseId), cancellationToken);
        if (!document.RootElement.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties.EnumerateObject())
        {
            var key = NormalizeFieldKey(property.Name);
            if (key.Length == 0 || !TryReadOptions(property.Value, out var options))
            {
                continue;
            }

            result[key] = options;
        }

        return result;
    }

    internal static string NormalizeFieldKey(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(trimmed.Length);
        var underscore = false;
        foreach (var character in trimmed)
        {
            var lower = char.ToLowerInvariant(character);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(lower);
                underscore = false;
            }
            else if (!underscore)
            {
                builder.Append('_');
                underscore = true;
            }
        }

        return builder.ToString().Trim('_');
    }

    private static bool TryReadOptions(JsonElement property, out IReadOnlyList<string> options)
    {
        options = [];
        var type = NotionContentSource.GetString(property, "type");
        if (type is not ("select" or "multi_select" or "status") ||
            !property.TryGetProperty(type, out var container) ||
            container.ValueKind != JsonValueKind.Object ||
            !container.TryGetProperty("options", out var optionElements) ||
            optionElements.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        options = optionElements.EnumerateArray()
            .Select(static option => NotionContentSource.GetString(option, "name")?.Trim())
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!)
            .ToArray();
        return true;
    }
}
