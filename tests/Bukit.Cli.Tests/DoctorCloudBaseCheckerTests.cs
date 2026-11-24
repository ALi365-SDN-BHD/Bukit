using System.Net;
using System.Text;
using Bukit.Cli.Commands;
using Bukit.CloudBase.Transport;
using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class DoctorCloudBaseCheckerTests
{
    [Fact]
    public async Task ServiceRoleKey_WarnsAdminCredential()
    {
        var output = await Run(
            Jwt("""{"role":"service_role"}"""),
            new FakeApi { Total = 1, Row = Row() });

        Assert.Contains("BKT-0C0A", output);
    }

    [Fact]
    public async Task TableNotFound_ReportsNotFound()
    {
        var output = await Run(Jwt("""{"role":"anon"}"""), new FakeApi { Status = HttpStatusCode.NotFound });

        Assert.Contains("BKT-0C04", output);
    }

    [Fact]
    public async Task EmptyTable_HintsRlsWithoutError()
    {
        var output = await Run(Jwt("""{"role":"anon"}"""), new FakeApi { Total = 0 });

        Assert.Contains("Row-Level Security", output);
        Assert.DoesNotContain("✖", output, StringComparison.Ordinal);
        Assert.DoesNotContain("BKT-0C", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingMappedField_Warns()
    {
        var output = await Run(
            Jwt("""{"role":"anon"}"""),
            new FakeApi { Total = 1, Row = """{"id":1,"slug":"a","content":"x"}""" });

        Assert.Contains("missing mapped", output);
    }

    [Fact]
    public async Task BlankSampleIdField_IsFatal()
    {
        var output = await Run(
            Jwt("""{"role":"anon"}"""),
            new FakeApi { Total = 1, Row = """{"id":"","title":"t","slug":"s","publish_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","content":"body"}""" });

        Assert.Contains("BKT-0C07", output);
    }

    [Fact]
    public async Task HealthyTable_NoDiagnosticCodes()
    {
        var output = await Run(Jwt("""{"role":"anon"}"""), new FakeApi { Total = 1, Row = Row() });

        Assert.DoesNotContain("BKT-0C0", output);
        Assert.Contains("CloudBase checks finished", output);
    }

    [Fact]
    public async Task Unauthorized_ReportsAuthFailed()
    {
        var output = await Run(Jwt("""{"role":"anon"}"""), new FakeApi { Status = HttpStatusCode.Unauthorized });

        Assert.Contains("BKT-0C03", output);
    }

    [Fact]
    public async Task StorageSigningFailure_ReportsFileResolveFailed()
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        var original = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", Jwt("""{"role":"anon"}"""));
            Console.SetOut(writer);
            var api = new FakeApi
            {
                Total = 1,
                Row = """{"id":1,"title":"t","slug":"s","publish_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","content":"body","cover":"posts/a.png"}"""
            };
            var cloudBase = CloudBase() with { FileFields = ["cover"], StorageBucket = "media" };
            await DoctorCloudBaseChecker.CheckAsync(cloudBase, () => Client(api));
        }
        finally
        {
            Console.SetOut(previous);
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }

        Assert.Contains("BKT-0C08", writer.ToString());
    }

    [Fact]
    public async Task Forbidden_ReportsAuthFailed()
    {
        var output = await Run(Jwt("""{"role":"anon"}"""), new FakeApi { Status = HttpStatusCode.Forbidden });

        Assert.Contains("BKT-0C03", output);
    }

    [Fact]
    public async Task UnparseableRole_WarnsAdminCredential()
    {
        var output = await Run("pk_not_a_jwt", new FakeApi { Total = 1, Row = Row() });

        Assert.Contains("BKT-0C0A", output);
    }

    [Fact]
    public async Task IdFieldInAllowed_Warns()
    {
        var cloudBase = CloudBase() with
        {
            FieldPolicy = new NotionFieldPolicyConfig { Mode = "all", Allowed = ["id"] }
        };
        var output = await RunWithConfig(cloudBase, Jwt("""{"role":"anon"}"""), new FakeApi { Total = 1, Row = Row() });

        Assert.Contains("publishes idField", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Output_DoesNotContainCredential()
    {
        var key = Jwt("""{"role":"service_role"}""");
        var output = await Run(key, new FakeApi { Total = 1, Row = Row() });

        Assert.DoesNotContain(key, output, StringComparison.Ordinal);
    }

    private static async Task<string> RunWithConfig(CloudBaseConfig cloudBase, string key, FakeApi api)
    {
        var original = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        var writer = new StringWriter();
        var previous = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", key);
            Console.SetOut(writer);
            var config = Config() with
            {
                Content = ContentConfigFactory.FromSources(
                [
                    new ContentSourceConfig { Type = "cloudbase", Name = "page", Collection = "page", CloudBase = cloudBase }
                ])
            };
            await DoctorCloudBaseChecker.CheckAsync(cloudBase, () => Client(api));
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previous);
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }
    }

    [Fact]
    public async Task MissingCredential_ReportsCredentialMissing()
    {
        var original = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", null);
            var output = await RunRaw(new FakeApi());
            Assert.Contains("BKT-0C02", output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }
    }

    [Fact]
    public async Task NotFound_ReturnsFatalErrorCount()
    {
        var original = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        var previous = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", Jwt("""{"role":"anon"}"""));
            Console.SetOut(new StringWriter());
            var errors = await DoctorCloudBaseChecker.CheckAsync(CloudBase(), () => Client(new FakeApi { Status = HttpStatusCode.NotFound }));
            Assert.Equal(1, errors);
        }
        finally
        {
            Console.SetOut(previous);
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }
    }

    [Fact]
    public async Task RlsHint_IsNotAFatalError()
    {
        var original = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        var previous = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", Jwt("""{"role":"anon"}"""));
            Console.SetOut(new StringWriter());
            var errors = await DoctorCloudBaseChecker.CheckAsync(CloudBase(), () => Client(new FakeApi { Total = 0 }));
            Assert.Equal(0, errors);
        }
        finally
        {
            Console.SetOut(previous);
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }
    }

    private static async Task<string> Run(string key, FakeApi api)
    {
        var original = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        var writer = new StringWriter();
        var previous = Console.Out;
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", key);
            Console.SetOut(writer);
            await DoctorCloudBaseChecker.CheckAsync(CloudBase(), () => Client(api));
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previous);
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }
    }

    private static async Task<string> RunRaw(FakeApi api)
    {
        var writer = new StringWriter();
        var previous = Console.Out;
        try
        {
            Console.SetOut(writer);
            await DoctorCloudBaseChecker.CheckAsync(CloudBase(), () => Client(api));
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    private static CloudBaseClient Client(FakeApi api)
        => new(
            new CloudBaseClientOptions { EnvId = "env-abc", PublishableKey = "pk", MaxRps = 1000, MaxRetries = 0 },
            api);

    private static CloudBaseConfig CloudBase() => new()
    {
        EnvId = "env-abc",
        Table = "articles",
        BodyField = "content"
    };

    private static AppConfig Config() => new()
    {
        Site = new SiteConfig { Name = "site", Title = "Site" },
        Content = ContentConfigFactory.FromSources(
        [
            new ContentSourceConfig { Type = "cloudbase", Name = "page", Collection = "page", CloudBase = CloudBase() }
        ])
    };

    private static string Row()
        => """{"id":1,"title":"t","slug":"s","publish_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","content":"body"}""";

    private static string Jwt(string payload)
        => $"header.{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.signature";

    private sealed class FakeApi : HttpMessageHandler
    {
        public int Total { get; init; }
        public string Row { get; init; } = "[]";
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(Status);
            if (request.Method == HttpMethod.Head)
            {
                if (Status == HttpStatusCode.OK)
                {
                    response.Content = new StringContent(string.Empty);
                    response.Content.Headers.TryAddWithoutValidation("Content-Range", $"*/{Total}");
                }

                return Task.FromResult(response);
            }

            response.Content = new StringContent($"[{Row}]", Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }
}
