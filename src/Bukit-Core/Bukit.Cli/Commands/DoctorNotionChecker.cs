using Bukit.Config;
using Bukit.Engine;
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
            Console.WriteLine($"✖ [{NotionDiagnostics.Format(result.ErrorKind, result.StatusCode)}] Notion database check failed: {(int)result.StatusCode} {result.ReasonPhrase}");
            return false;
        }

        Console.WriteLine($"✖ [{NotionDiagnostics.Format(result.ErrorKind, result.StatusCode)}] Notion request failed: {result.ErrorMessage}");
        return false;
    }

    public static Task CheckNotionSchemaAsync(string token, NotionConfig config)
        => DoctorNotionSchemaChecker.CheckAsync(token, config);

    internal static Task CheckNotionSchemaAsync(
        NotionHealthClient client,
        NotionConfig config,
        CancellationToken cancellationToken = default)
        => DoctorNotionSchemaChecker.CheckAsync(client, config, cancellationToken);

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
            Console.WriteLine($"⚠ [{NotionDiagnostics.Format(result.ErrorKind, result.StatusCode)}] Notion API unreachable: HTTP {(int)result.StatusCode}");
            return;
        }

        Console.WriteLine($"⚠ [{NotionDiagnostics.Format(result.ErrorKind, result.StatusCode)}] Notion API connectivity check failed: {result.ErrorMessage}");
    }

    internal static NotionClient CreateClient(string token, TimeSpan timeout)
        => new(new NotionClientOptions
        {
            Token = token,
            Timeout = timeout,
            MaxRetries = 0
        });

    internal static bool IsHttpFailure(NotionApiErrorKind? kind)
        => kind is NotionApiErrorKind.HttpStatus or NotionApiErrorKind.RateLimited;
}
