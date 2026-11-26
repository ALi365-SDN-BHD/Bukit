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
    public void MapKind_MatchesBuildErrorMapperForEveryKind()
    {
        foreach (var kind in Enum.GetValues<CloudBaseErrorKind>())
        {
            Assert.Equal(Bukit.Content.CloudBase.CloudBaseErrorMapper.Map(kind), DoctorCloudBaseChecker.MapKind(kind));
        }
    }

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

    [Theory]
    [InlineData("cloud://env-old.media/posts/a.png")]
    [InlineData("cloud://env-abc.Media/posts/a.png")]
    [InlineData("cloud://env-abc.media/../a.png")]
    public async Task StorageReferenceBuildWouldReject_WarnsWithoutSigning(string cover)
    {
        var output = await RunWithCover(cover);

        Assert.Contains("BKT-0C08", output);
        Assert.Contains("build cannot resolve", output, StringComparison.Ordinal);
        Assert.DoesNotContain("signing failed", output, StringComparison.Ordinal);
        Assert.DoesNotContain("env-old", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StorageReferenceToConfiguredBucket_IsSigned()
    {
        var output = await RunWithCover("cloud://env-abc.media/posts/a.png");

        Assert.DoesNotContain("build cannot resolve", output, StringComparison.Ordinal);
        Assert.Contains("signing failed", output, StringComparison.Ordinal);
    }

    private static async Task<string> RunWithCover(string cover)
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
                Row = $$"""{"id":1,"title":"t","slug":"s","publish_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-02T00:00:00Z","content":"body","cover":"{{cover}}"}"""
            };
            var cloudBase = CloudBase() with { FileFields = ["cover"], StorageBucket = "media" };
            await DoctorCloudBaseChecker.CheckAsync(cloudBase, () => Client(api));
        }
        finally
        {
            Console.SetOut(previous);
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", original);
        }

        return writer.ToString();
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
    public async Task FilterEnvReference_QueriesWithTheResolvedValue()
    {
        var api = new FakeApi { Total = 1, Row = Row() };
        var original = Environment.GetEnvironmentVariable("BUKIT_DOCTOR_RELEASE");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_DOCTOR_RELEASE", "r42");
            var output = await RunWithConfig(FilteredCloudBase(), Jwt("""{"role":"anon"}"""), api);

            Assert.DoesNotContain("matched no rows", output, StringComparison.Ordinal);
            Assert.Contains(api.Requests, uri => uri.Contains("release_id=eq.r42", StringComparison.Ordinal));
            Assert.DoesNotContain(api.Requests, uri => uri.Contains("%24%7B", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_DOCTOR_RELEASE", original);
        }
    }

    [Fact]
    public async Task FilterEnvReference_UnsetVariableFailsWithoutQuerying()
    {
        var api = new FakeApi { Total = 1, Row = Row() };
        var original = Environment.GetEnvironmentVariable("BUKIT_DOCTOR_RELEASE");
        try
        {
            Environment.SetEnvironmentVariable("BUKIT_DOCTOR_RELEASE", null);
            var output = await RunWithConfig(FilteredCloudBase(), Jwt("""{"role":"anon"}"""), api);

            Assert.Contains("✖", output, StringComparison.Ordinal);
            Assert.Contains("BUKIT_DOCTOR_RELEASE, which is not set", output, StringComparison.Ordinal);
            Assert.Empty(api.Requests);
        }
        finally
        {
            Environment.SetEnvironmentVariable("BUKIT_DOCTOR_RELEASE", original);
        }
    }

    private static CloudBaseConfig FilteredCloudBase() => CloudBase() with
    {
        Filter = new CloudBaseFilterConfig { Field = "release_id", Value = "${env:BUKIT_DOCTOR_RELEASE}" }
    };

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
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.ToString() ?? string.Empty);
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
