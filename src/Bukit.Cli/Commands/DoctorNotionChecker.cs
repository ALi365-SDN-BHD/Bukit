using System.Net.Http.Headers;
using System.Text.Json;
using Bukit.Config;
using Bukit.Shared.Notion;

namespace Bukit.Cli.Commands;

internal static class DoctorNotionChecker
{
    public static async Task<bool> CheckNotionAsync(string token, string databaseId)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Notion-Version", NotionApiUrls.NotionVersion);

        var url = NotionApiUrls.Database(databaseId);
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✖ Notion request failed: {ex.Message}");
            return false;
        }

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✔ Notion database reachable");
            return true;
        }

        Console.WriteLine($"✖ Notion database check failed: {(int)response.StatusCode} {response.ReasonPhrase}");
        return false;
    }

    public static async Task CheckNotionSchemaAsync(string token, NotionConfig config)
    {
        Console.WriteLine($"Notion Schema Check for database {config.DatabaseId}:");
        Console.WriteLine();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.DefaultRequestHeaders.Add("Notion-Version", NotionApiUrls.NotionVersion);

        var url = NotionApiUrls.Database(config.DatabaseId);
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✖ Schema check failed: {ex.Message}");
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"✖ Cannot fetch database: {(int)response.StatusCode}");
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var schemaProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in props.EnumerateObject())
            {
                var propType = string.Empty;
                if (prop.Value.TryGetProperty("type", out var typeEl))
                {
                    propType = typeEl.GetString() ?? string.Empty;
                }

                schemaProperties.Add(prop.Name);
                schemaProperties.Add(prop.Name + $" ({propType})");
            }
        }

        var pm = config.PropertyMap;
        var hasErrors = false;

        var checkField = (string mapKey, string? mappedName, string defaultName, string expectedType) =>
        {
            var effectiveName = mappedName ?? defaultName;
            var label = mappedName is not null ? $"  {mapKey}" : $"  {mapKey} (default)";
            Console.Write($"{label,-18} → \"{effectiveName}\"");

            var found = false;
            var typeCorrect = false;
            foreach (var sp in schemaProperties)
            {
                if (sp.StartsWith(effectiveName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    if (sp.Contains($"({expectedType})", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCorrect = true;
                    }
                    else
                    {
                        var actualType = sp[(sp.IndexOf('(') + 1)..].TrimEnd(')');
                        Console.WriteLine($" — type mismatch: expected {expectedType}, got {actualType}");
                        hasErrors = true;
                    }
                    break;
                }
            }

            if (!found)
            {
                Console.WriteLine(" — NOT FOUND in database");
                hasErrors = true;
            }
            else if (typeCorrect)
            {
                Console.WriteLine(" — OK");
            }
        };

        checkField("title", pm?.Title, "Title", "title");
        checkField("slug", pm?.Slug, "Slug", "rich_text");
        checkField("type", pm?.Type, "Type", "select");
        checkField("publishAt", pm?.PublishAt, "PublishAt", "date");
        checkField("language", pm?.Language, "language", "select");
        checkField("i18nKey", pm?.I18nKey, "i18n_key", "rich_text");
        checkField("summary", pm?.Summary, "summary", "rich_text");
        checkField("collection", pm?.Collection, "collection", "select");

        if (hasErrors)
        {
            Console.WriteLine();
            Console.WriteLine("✖ Some mapped properties have issues. Please check your propertyMap configuration.");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("✔ All mapped properties found in Notion database.");
        }
    }
}
