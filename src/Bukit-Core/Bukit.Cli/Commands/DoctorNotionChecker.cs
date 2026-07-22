using Bukit.Config;
using Bukit.Notion.Diagnostics;
using Bukit.Notion.Transport;

namespace Bukit.Cli.Commands;

internal static class DoctorNotionChecker
{
    public static async Task<bool> CheckNotionAsync(string token, string databaseId)
    {
        using var transport = CreateClient(token, TimeSpan.FromSeconds(30));
        return await CheckNotionAsync(new NotionHealthClient(transport), databaseId);
    }

    internal static async Task<bool> CheckNotionAsync(
        NotionHealthClient client,
        string databaseId,
        CancellationToken cancellationToken = default)
    {
        var result = await client.CheckDatabaseAsync(databaseId, cancellationToken);
        if (result.IsSuccess)
        {
            Console.WriteLine("✔ Notion database reachable");
            return true;
        }

        if (IsHttpFailure(result.ErrorKind) && result.StatusCode is not null)
        {
            Console.WriteLine($"✖ Notion database check failed: {(int)result.StatusCode} {result.ReasonPhrase}");
            return false;
        }

        Console.WriteLine($"✖ Notion request failed: {result.ErrorMessage}");
        return false;
    }

    public static async Task CheckNotionSchemaAsync(string token, NotionConfig config)
    {
        using var transport = CreateClient(token, TimeSpan.FromSeconds(30));
        await CheckNotionSchemaAsync(new NotionHealthClient(transport), config);
    }

    internal static async Task CheckNotionSchemaAsync(
        NotionHealthClient client,
        NotionConfig config,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Notion Schema Check for database {config.DatabaseId}:");
        Console.WriteLine();

        var result = await client.InspectDatabaseSchemaAsync(config.DatabaseId, cancellationToken);
        if (!result.IsSuccess)
        {
            if (IsHttpFailure(result.ErrorKind) && result.StatusCode is not null)
            {
                Console.WriteLine($"✖ Cannot fetch database: {(int)result.StatusCode}");
                return;
            }

            Console.WriteLine($"✖ Schema check failed: {result.ErrorMessage}");
            return;
        }

        // Preserve the 1.x Doctor comparison contract exactly. The two entries per property and
        // their lookup order are intentionally retained until a separately approved CLI fix.
        var schemaProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in result.Properties)
        {
            schemaProperties.Add(property.Name);
            schemaProperties.Add($"{property.Name} ({property.Type})");
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
            foreach (var schemaProperty in schemaProperties)
            {
                if (schemaProperty.StartsWith(effectiveName, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    if (schemaProperty.Contains($"({expectedType})", StringComparison.OrdinalIgnoreCase))
                    {
                        typeCorrect = true;
                    }
                    else
                    {
                        var actualType = schemaProperty[(schemaProperty.IndexOf('(') + 1)..].TrimEnd(')');
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

    public static async Task CheckNotionConnectivityAsync(string token)
    {
        using var transport = CreateClient(token, TimeSpan.FromSeconds(10));
        await CheckNotionConnectivityAsync(new NotionHealthClient(transport));
    }

    internal static async Task CheckNotionConnectivityAsync(
        NotionHealthClient client,
        CancellationToken cancellationToken = default)
    {
        var result = await client.CheckConnectivityAsync(cancellationToken);
        if (result.IsSuccess)
        {
            Console.WriteLine("✔ Notion API reachable");
            return;
        }

        if (IsHttpFailure(result.ErrorKind) && result.StatusCode is not null)
        {
            Console.WriteLine($"⚠ Notion API unreachable: HTTP {(int)result.StatusCode}");
            return;
        }

        Console.WriteLine($"⚠ Notion API connectivity check failed: {result.ErrorMessage}");
    }

    private static NotionClient CreateClient(string token, TimeSpan timeout)
        => new(new NotionClientOptions
        {
            Token = token,
            Timeout = timeout,
            MaxRetries = 0
        });

    private static bool IsHttpFailure(NotionApiErrorKind? kind)
        => kind is NotionApiErrorKind.HttpStatus or NotionApiErrorKind.RateLimited;
}
