using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Plugins.Protocol;
using System.Text.Json;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class ProtocolModelsTests
{
    [Fact]
    public void AfterBuildRoutedPage_AllProperties()
    {
        var page = new AfterBuildRoutedPage
        {
            Id = "page-1",
            Url = "/test/",
            OutputPath = "test/index.html",
            Meta = new Dictionary<string, object> { ["tags"] = new[] { "a", "b" } }
        };

        Assert.Equal("page-1", page.Id);
        Assert.Equal("/test/", page.Url);
        Assert.Equal("test/index.html", page.OutputPath);
        Assert.NotNull(page.Meta);
    }

    [Fact]
    public void AfterBuildRequestPayload_Defaults()
    {
        var payload = new AfterBuildRequestPayload { OutputDir = "/dist" };
        Assert.Equal("/dist", payload.OutputDir);
        Assert.Empty(payload.RoutedPages);
    }

    [Fact]
    public void AfterBuildOutputFile_AllProperties()
    {
        var file = new AfterBuildOutputFile { Path = "index.html", ContentType = "text/html", Text = "<html></html>" };
        Assert.Equal("index.html", file.Path);
        Assert.Equal("text/html", file.ContentType);
        Assert.Equal("<html></html>", file.Text);
        Assert.Null(file.Base64);
    }

    [Fact]
    public void DerivePagesRequestPayload_Defaults()
    {
        var payload = new DerivePagesRequestPayload();
        Assert.Empty(payload.RoutedPages);
    }

    [Fact]
    public void DerivePagesResponsePayload_Success()
    {
        var response = new DerivePagesResponsePayload { Ok = true };
        Assert.True(response.Ok);
        Assert.Empty(response.DerivedPages);
        Assert.Null(response.Error);
    }

    [Fact]
    public void DerivePagesResponsePayload_Error()
    {
        var response = new DerivePagesResponsePayload { Ok = false, Error = new ProtocolPluginError { Code = "ERR", Message = "Failed" } };
        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
        Assert.Equal("ERR", response.Error.Code);
    }

    [Fact]
    public void ProtocolDerivedPage_RequiredProperties()
    {
        var page = new ProtocolDerivedPage
        {
            Id = "dp-1",
            Title = "Derived Page",
            Slug = "derived-page",
            Url = "/derived/",
            OutputPath = "derived/index.html",
            Template = "pages/page.html",
            ContentHtml = "<p>content</p>",
            PublishAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        Assert.Equal("Derived Page", page.Title);
        Assert.Equal("derived-page", page.Slug);
        Assert.Equal("<p>content</p>", page.ContentHtml);
        Assert.Equal(2026, page.PublishAt.Year);
    }

    [Fact]
    public void ProtocolHandshakeRequest_Defaults()
    {
        var request = new ProtocolHandshakeRequest { RequestedHook = "after-build" };
        Assert.Equal("2", request.SchemaVersion);
        Assert.Equal("handshake", request.Hook);
        Assert.Equal("after-build", request.RequestedHook);
        Assert.Contains("2", request.HostSupportedSchemaVersions);
    }

    [Fact]
    public void ProtocolHandshakeResponse_Success()
    {
        var response = new ProtocolHandshakeResponse { Ok = true, NegotiatedSchemaVersion = "2" };
        Assert.True(response.Ok);
        Assert.Equal("2", response.NegotiatedSchemaVersion);
        Assert.Null(response.Error);
    }

    [Fact]
    public void ProtocolHandshakeResponse_Error()
    {
        var response = new ProtocolHandshakeResponse { Ok = false, Error = new ProtocolPluginError { Code = "E001", Message = "Failed" } };
        Assert.False(response.Ok);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void SectionContext_Creation()
    {
        var ctx = new SectionContext { SectionType = "hero", Variant = "dark", Props = new Dictionary<string, object?> { ["title"] = "Hello" } };
        Assert.Equal("hero", ctx.SectionType);
        Assert.Equal("dark", ctx.Variant);
        Assert.Equal("Hello", ctx.Props!["title"]);
        Assert.NotNull(ctx.Data);
        Assert.Empty(ctx.Data);
        Assert.Null(ctx.RenderedHtml);
    }

    [Fact]
    public void SectionContext_RenderedHtml_SetAfterRender()
    {
        var ctx = new SectionContext { SectionType = "hero" };
        ctx.RenderedHtml = "<div>rendered</div>";
        Assert.Equal("<div>rendered</div>", ctx.RenderedHtml);
    }

    [Fact]
    public void SectionHook_EnumValues()
    {
        Assert.Equal(0, (int)SectionHook.BeforeRender);
        Assert.Equal(1, (int)SectionHook.AfterRender);
        Assert.Equal(2, (int)SectionHook.ResolveItems);
    }

    [Fact]
    public void ProtocolSiteInfo_RequiredProperties()
    {
        var info = new ProtocolSiteInfo { BaseUrl = "/", Language = "zh-CN", Title = "Test Site" };
        Assert.Equal("/", info.BaseUrl);
        Assert.Equal("zh-CN", info.Language);
        Assert.Equal("Test Site", info.Title);
    }

    [Fact]
    public void ProtocolPluginConfig_Defaults()
    {
        var config = new ProtocolPluginConfig();
        Assert.Null(config.PluginOptions);
    }

    [Fact]
    public void ProtocolPluginConfig_WithOptions()
    {
        var config = new ProtocolPluginConfig { PluginOptions = new Dictionary<string, object> { ["key"] = "value" } };
        Assert.Equal("value", config.PluginOptions!["key"]);
    }

    [Fact]
    public void ProtocolPluginIdentity_RequiredProperties()
    {
        var id = new ProtocolPluginIdentity { Name = "test-plugin", Version = "1.0.0" };
        Assert.Equal("test-plugin", id.Name);
        Assert.Equal("1.0.0", id.Version);
    }

    [Fact]
    public void ProtocolPluginInvocationRequest_Defaults()
    {
        var request = new ProtocolPluginInvocationRequest
        {
            Hook = "after-build",
            Plugin = new ProtocolPluginIdentity { Name = "p", Version = "1.0" },
            Site = new ProtocolSiteInfo { BaseUrl = "/", Language = "en", Title = "Test" }
        };

        Assert.Equal("after-build", request.Hook);
        Assert.NotNull(request.Plugin);
        Assert.NotNull(request.Site);
        Assert.Equal("1", request.SchemaVersion);
    }

    [Fact]
    public void ProtocolPluginInvocationResponse_Success()
    {
        var response = new ProtocolPluginInvocationResponse
        {
            Ok = true,
            Logs = new[] { new ProtocolPluginLogEntry { Level = "info", Message = "done" } }
        };

        Assert.True(response.Ok);
        Assert.Single(response.Logs);
        Assert.Equal("info", response.Logs[0].Level);
        Assert.Equal("done", response.Logs[0].Message);
    }

    [Fact]
    public void ProtocolPluginInvocationResponse_WithOutputs()
    {
        var response = new ProtocolPluginInvocationResponse
        {
            Ok = true,
            Outputs = new[] { new AfterBuildOutputFile { Path = "out.html", ContentType = "text/html" } }
        };

        Assert.Single(response.Outputs);
        Assert.Equal("out.html", response.Outputs[0].Path);
    }

    [Fact]
    public void PluginContentDocumentDto_ShouldSerializeWithoutMeta_WhenUsingProtocolV2()
    {
        var document = new PluginContentDocumentDto
        {
            Content = new ContentRecordDto
            {
                Id = "post-1",
                Slug = "hello",
                Title = "Hello",
                Type = "post",
                Collection = "posts",
                Language = "en",
                Summary = "Summary"
            },
            Route = new ContentRoutePolicyDto
            {
                Url = "/posts/hello/",
                OutputPath = "posts/hello/index.html",
                Template = "pages/post.html"
            },
            Publish = new ContentPublishPolicyDto
            {
                Draft = false,
                NoIndex = false,
                IsDataModule = false
            },
            Fields = new Dictionary<string, ContentFieldDto>
            {
                ["featured"] = new() { Type = "bool", Value = true }
            },
            Source = new ContentSourceInfoDto
            {
                Provider = "markdown",
                SourceKey = "posts",
                SourcePath = "content/posts/hello.md"
            }
        };

        var json = JsonSerializer.Serialize(document);

        Assert.Contains("\"content\"", json, StringComparison.Ordinal);
        Assert.Contains("\"route\"", json, StringComparison.Ordinal);
        Assert.Contains("\"publish\"", json, StringComparison.Ordinal);
        Assert.Contains("\"fields\"", json, StringComparison.Ordinal);
        Assert.Contains("\"source\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"meta\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
