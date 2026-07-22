using System.Net;
using Bukit.Cli.Commands;
using Bukit.Config;
using Bukit.Notion.Diagnostics;
using Bukit.Notion.Transport;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorNotionCheckerTests
{
    [Fact]
    public async Task CheckNotionAsync_PreservesSuccessOutput()
    {
        using var client = CreateHealthClient(HttpStatusCode.OK, "{}");

        var (result, output) = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionAsync(client, "database-id"));

        Assert.True(result);
        Assert.Equal($"✔ Notion database reachable{Environment.NewLine}", output);
    }

    [Fact]
    public async Task CheckNotionAsync_PreservesHttpFailureOutput()
    {
        using var client = CreateHealthClient(HttpStatusCode.Unauthorized, "sensitive response body");

        var (result, output) = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionAsync(client, "database-id"));

        Assert.False(result);
        Assert.Equal(
            $"✖ Notion database check failed: 401 Unauthorized{Environment.NewLine}",
            output);
        Assert.DoesNotContain("sensitive", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CheckNotionConnectivityAsync_PreservesHttpFailureOutput()
    {
        using var client = CreateHealthClient(HttpStatusCode.Unauthorized, "{}");

        var output = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionConnectivityAsync(client));

        Assert.Equal($"⚠ Notion API unreachable: HTTP 401{Environment.NewLine}", output);
    }

    [Fact]
    public async Task CheckNotionConnectivityAsync_PreservesFailurePrefixWithoutLeakingInnerDetails()
    {
        const string secret = "secret-from-inner-exception";
        using var client = CreateHealthClient(new HttpRequestException(secret));

        var output = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionConnectivityAsync(client));

        Assert.Equal(
            $"⚠ Notion API connectivity check failed: Notion request failed due to a transport error.{Environment.NewLine}",
            output);
        Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckNotionSchemaAsync_PreservesOneXPropertyMapComparisonOutput()
    {
        const string body = """
            {
              "properties": {
                "Headline": { "type": "title" },
                "Slug": { "type": "rich_text" },
                "Type": { "type": "select" },
                "PublishAt": { "type": "date" },
                "language": { "type": "select" },
                "i18n_key": { "type": "rich_text" },
                "summary": { "type": "rich_text" },
                "collection": { "type": "select" }
              }
            }
            """;
        using var client = CreateHealthClient(HttpStatusCode.OK, body);
        var config = new NotionConfig
        {
            DatabaseId = "database-id",
            PropertyMap = new NotionPropertyMapConfig { Title = "Headline" }
        };

        var output = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionSchemaAsync(client, config));

        Assert.Contains("Notion Schema Check for database database-id:", output, StringComparison.Ordinal);
        Assert.Contains(
            "  title            → \"Headline\" — type mismatch: expected title, got Headline",
            output,
            StringComparison.Ordinal);
        Assert.Contains(
            "  slug (default)   → \"Slug\" — type mismatch: expected rich_text, got Slug",
            output,
            StringComparison.Ordinal);
        Assert.EndsWith(
            $"✖ Some mapped properties have issues. Please check your propertyMap configuration.{Environment.NewLine}",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckNotionSchemaAsync_PreservesMismatchAndMissingOutput()
    {
        const string body = """
            { "properties": { "Title": { "type": "rich_text" } } }
            """;
        using var client = CreateHealthClient(HttpStatusCode.OK, body);
        var config = new NotionConfig { DatabaseId = "database-id" };

        var output = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionSchemaAsync(client, config));

        Assert.Contains(
            "  title (default)  → \"Title\" — type mismatch: expected title, got Title",
            output,
            StringComparison.Ordinal);
        Assert.Contains(
            "  slug (default)   → \"Slug\" — NOT FOUND in database",
            output,
            StringComparison.Ordinal);
        Assert.EndsWith(
            $"✖ Some mapped properties have issues. Please check your propertyMap configuration.{Environment.NewLine}",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckNotionSchemaAsync_ReportsInvalidJsonAsSchemaFailure()
    {
        using var client = CreateHealthClient(HttpStatusCode.OK, "not-json");
        var config = new NotionConfig { DatabaseId = "database-id" };

        var output = await CaptureAsync(
            () => DoctorNotionChecker.CheckNotionSchemaAsync(client, config));

        Assert.Equal(
            $"Notion Schema Check for database database-id:{Environment.NewLine}" +
            Environment.NewLine +
            $"✖ Schema check failed: Notion returned invalid json.{Environment.NewLine}",
            output);
    }

    private static NotionHealthClientLease CreateHealthClient(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body)
        }));
        var http = new HttpClient(handler);
        var transport = new NotionClient(
            new NotionClientOptions { Token = "test-token", MaxRetries = 0 },
            http);
        return new NotionHealthClientLease(new NotionHealthClient(transport), transport, http);
    }

    private static NotionHealthClientLease CreateHealthClient(Exception exception)
    {
        var handler = new StubHandler((_, _) => Task.FromException<HttpResponseMessage>(exception));
        var http = new HttpClient(handler);
        var transport = new NotionClient(
            new NotionClientOptions { Token = "test-token", MaxRetries = 0 },
            http);
        return new NotionHealthClientLease(new NotionHealthClient(transport), transport, http);
    }

    private static async Task<(bool Result, string Output)> CaptureAsync(Func<Task<bool>> action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            var result = await action();
            return (result, writer.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private static async Task<string> CaptureAsync(Func<Task> action)
    {
        var original = Console.Out;
        using var writer = new StringWriter();
        try
        {
            Console.SetOut(writer);
            await action();
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(original);
        }
    }

    private sealed class NotionHealthClientLease : IDisposable
    {
        private readonly NotionClient _transport;
        private readonly HttpClient _http;

        public NotionHealthClientLease(
            NotionHealthClient client,
            NotionClient transport,
            HttpClient http)
        {
            Client = client;
            _transport = transport;
            _http = http;
        }

        public NotionHealthClient Client { get; }

        public static implicit operator NotionHealthClient(NotionHealthClientLease lease) => lease.Client;

        public void Dispose()
        {
            _transport.Dispose();
            _http.Dispose();
        }
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => sendAsync(request, cancellationToken);
    }
}
