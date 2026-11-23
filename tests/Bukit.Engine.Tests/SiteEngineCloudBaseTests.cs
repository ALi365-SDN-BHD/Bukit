using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Bukit.Config;
using Bukit.CloudBase.Transport;
using Bukit.Content.CloudBase;
using Bukit.Engine;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed partial class SiteEngineIntegrationTests
{
    [Fact]
    public async Task BuildAsync_CloudBaseSource_RendersRowsAndDropsUnsafeLinks()
    {
        var (root, config) = CreateBuildReportHealthSite();
        File.WriteAllText(
            Path.Combine(root, "layouts", "pages", "page.html"),
            "<html><head><title>{{ page.title }}</title></head><body><main>{{ page.content }}</main></body></html>");
        var source = TestContent.CloudBaseSource(new CloudBaseConfig
        {
            EnvId = "env-abc",
            Table = "articles",
            BodyField = "content",
            MaxItems = 2,
            FieldPolicy = new NotionFieldPolicyConfig { Mode = "all" },
            Filter = new CloudBaseFilterConfig { Field = "status", Value = "published" },
            Sort = new CloudBaseSortConfig { Field = "publish_at", Direction = "desc" }
        });
        config = config with
        {
            Content = ContentConfigFactory.FromSources([source]) with
            {
                Media = new MediaConfig { DownloadToLocal = false }
            },
            Build = config.Build with { Clean = true, Report = new BuildReportConfig { Enabled = false } }
        };

        var api = new CloudBaseApi();
        var originalKey = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        var logger = new TestLogger();
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", "pk_engine_test");
            CloudBaseClient.TransportOverride.Value = () => api;

            await new SiteEngine(logger).BuildAsync(config, root, new ConfigOverrides { Clean = true, Incremental = false });
        }
        finally
        {
            CloudBaseClient.TransportOverride.Value = null;
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", originalKey);
        }

        Assert.Contains(logger.Infos, message =>
            message.StartsWith("event=content.source.loaded source=page count=2", StringComparison.Ordinal));
        Assert.Contains("status=eq.published", Uri.UnescapeDataString(api.FirstQuery), StringComparison.Ordinal);
        Assert.Contains("order=publish_at.desc,id.asc", Uri.UnescapeDataString(api.FirstQuery), StringComparison.Ordinal);

        var page = Path.Combine(root, "dist", "pages", "cloud-post-1", "index.html");
        Assert.True(File.Exists(page), $"Missing {page}");
        var html = File.ReadAllText(page);
        Assert.Contains("Hi", html, StringComparison.Ordinal);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("u-secret", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_CloudBaseFileFieldsWithoutPlaceholder_Throws()
    {
        var (root, config) = CreateBuildReportHealthSite();
        var source = TestContent.CloudBaseSource(new CloudBaseConfig
        {
            EnvId = "env-abc",
            Table = "articles",
            BodyField = "content",
            FileFields = ["cover"],
            StorageBucket = "media"
        });
        config = config with
        {
            Content = ContentConfigFactory.FromSources([source]) with
            {
                Media = new MediaConfig
                {
                    DownloadToLocal = true,
                    FieldKeys = ["cover"],
                    OnFailure = "placeholder",
                    DefaultImageUrl = "/assets/images/missing-placeholder.jpg"
                }
            }
        };

        var originalKey = Environment.GetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY");
        try
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", "pk_engine_test");

            var ex = Assert.Throws<ConfigException>(() =>
                ContentProviderFactory.Create(config, root, isCi: false, new TestLogger()));

            Assert.Equal(DiagnosticCode.CloudBaseConfigInvalid, ex.Code);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLOUDBASE_PUBLISHABLE_KEY", originalKey);
        }
    }

    private sealed class CloudBaseApi : HttpMessageHandler
    {
        private readonly ConcurrentQueue<string> _queries = new();

        internal string FirstQuery => _queries.ToArray().FirstOrDefault() ?? string.Empty;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            _queries.Enqueue(request.RequestUri!.Query);
            const string rows = """
                [
                  {"id":"a1","title":"Cloud Post 1","slug":"cloud-post-1","publish_at":"2026-01-02T03:04:05Z","content":"# Hi\n\n[bad](javascript:alert(1))","user_id":"u-secret","tags":["x"]},
                  {"id":"a2","title":"Cloud Post 2","slug":"cloud-post-2","publish_at":"2026-01-03T03:04:05Z","content":"Body two","user_id":"u-secret"}
                ]
                """;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(rows, Encoding.UTF8, "application/json")
            };
            response.Content.Headers.TryAddWithoutValidation("Content-Range", "0-1/2");
            return Task.FromResult(response);
        }
    }
}
