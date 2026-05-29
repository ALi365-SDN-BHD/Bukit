using System.Net;

namespace Bukit.Cli.Commands.Dev;

internal interface IDevWebSocketHub
{
    Task HandleUpgradeAsync(HttpListenerContext context, CancellationToken ct);
    Task BroadcastReloadAsync();
    int ClientCount { get; }
}
