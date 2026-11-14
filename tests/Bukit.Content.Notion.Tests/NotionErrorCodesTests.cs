using System.Net;
using Bukit.Notion.Transport;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Notion.Tests;

public sealed class NotionErrorCodesTests
{
    [Theory]
    [InlineData(NotionApiErrorKind.Transport, null, DiagnosticCode.NotionNetwork)]
    [InlineData(NotionApiErrorKind.InvalidJson, HttpStatusCode.OK, DiagnosticCode.NotionInvalidResponse)]
    [InlineData(NotionApiErrorKind.InvalidJson, HttpStatusCode.BadRequest, DiagnosticCode.NotionInvalidResponse)]
    [InlineData(NotionApiErrorKind.RateLimited, HttpStatusCode.TooManyRequests, DiagnosticCode.NotionRateLimited)]
    [InlineData(NotionApiErrorKind.HttpStatus, HttpStatusCode.Unauthorized, DiagnosticCode.NotionUnauthorized)]
    [InlineData(NotionApiErrorKind.HttpStatus, HttpStatusCode.Forbidden, DiagnosticCode.NotionForbidden)]
    [InlineData(NotionApiErrorKind.HttpStatus, HttpStatusCode.NotFound, DiagnosticCode.NotionNotFound)]
    [InlineData(NotionApiErrorKind.HttpStatus, HttpStatusCode.TooManyRequests, DiagnosticCode.NotionRateLimited)]
    [InlineData(NotionApiErrorKind.HttpStatus, HttpStatusCode.InternalServerError, DiagnosticCode.NotionRequestFailed)]
    [InlineData(null, null, DiagnosticCode.NotionRequestFailed)]
    public void For_MapsKindBeforeStatus(
        NotionApiErrorKind? kind,
        HttpStatusCode? status,
        DiagnosticCode expected)
    {
        Assert.Equal(expected, NotionErrorCodes.For(kind, status));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, DiagnosticCode.NotionUnauthorized)]
    [InlineData(HttpStatusCode.NotFound, DiagnosticCode.NotionNotFound)]
    public async Task ContentSource_WrapsHttpFailuresWithTheCode(
        HttpStatusCode status,
        DiagnosticCode expected)
    {
        var previous = NotionContentClient.TransportOverride.Value;
        NotionContentClient.TransportOverride.Value = () => new StatusHandler(status);
        try
        {
            var source = new NotionContentSource(new NotionContentSourceOptions
            {
                DatabaseId = "database-id",
                Token = "test-token",
                FilterType = "none",
                FieldPolicyMode = "all",
                RenderContent = false,
                MaxRetries = 0
            });

            ContentException exception = await Assert.ThrowsAsync<ContentException>(
                () => source.LoadRawAsync());

            Assert.Equal(expected, exception.Code);
        }
        finally
        {
            NotionContentClient.TransportOverride.Value = previous;
        }
    }

    [Fact]
    public void Wrap_KeepsTheOriginalExceptionAndAddsTheCode()
    {
        var apiException = new NotionApiException(
            NotionApiErrorKind.HttpStatus,
            "Notion request failed: 401 Unauthorized",
            HttpStatusCode.Unauthorized);

        ContentException wrapped = NotionErrorCodes.Wrap(apiException);

        Assert.Equal(DiagnosticCode.NotionUnauthorized, wrapped.Code);
        Assert.Same(apiException, wrapped.InnerException);
        Assert.Equal(apiException.Message, wrapped.Message);
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            });
    }
}
