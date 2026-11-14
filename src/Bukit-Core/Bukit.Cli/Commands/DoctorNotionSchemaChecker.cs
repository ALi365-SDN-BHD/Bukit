using Bukit.Config;
using Bukit.Engine;
using Bukit.Notion.Diagnostics;

namespace Bukit.Cli.Commands;

internal static class DoctorNotionSchemaChecker
{
    public static async Task CheckAsync(string token, NotionConfig config)
    {
        using var transport = DoctorNotionChecker.CreateClient(token, TimeSpan.FromSeconds(30));
        await CheckAsync(new NotionHealthClient(transport), config);
    }

    internal static async Task CheckAsync(
        NotionHealthClient client,
        NotionConfig config,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Notion Schema Check for database {config.DatabaseId}:");
        Console.WriteLine();

        var result = await client.InspectDatabaseSchemaAsync(config.DatabaseId, cancellationToken);
        if (!result.IsSuccess)
        {
            if (DoctorNotionChecker.IsHttpFailure(result.ErrorKind) && result.StatusCode is not null)
            {
                Console.WriteLine($"✖ [{NotionDiagnostics.Format(result.ErrorKind, result.StatusCode)}] Cannot fetch database: {(int)result.StatusCode}");
                return;
            }

            Console.WriteLine($"✖ [{NotionDiagnostics.Format(result.ErrorKind, result.StatusCode)}] Schema check failed: {result.ErrorMessage}");
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
}
