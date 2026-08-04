using System.Net;
using System.Text;
using Bukit.Notion.Transport;

namespace Bukit.Notion.Tests;

internal static class CanonicalBlockRendererTestSupport
{
    internal static NotionClient CreateClient(HttpMessageHandler handler)
    {
        var options = new NotionClientOptions
        {
            Token = "token",
            RequestDelayMs = 0,
            MaxRetries = 0
        };
        return CreateClient(options, handler);
    }

    internal static NotionClient CreateClient(
        NotionClientOptions options,
        HttpMessageHandler handler)
        => new(
            options,
            handler,
            (_, _) => Task.CompletedTask,
            () => DateTimeOffset.UtcNow);

    internal sealed class JsonHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
    }

    internal sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private int _index;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var json = _index < responses.Length
                ? responses[_index]
                : "{\"has_more\":false,\"results\":[]}";
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}
