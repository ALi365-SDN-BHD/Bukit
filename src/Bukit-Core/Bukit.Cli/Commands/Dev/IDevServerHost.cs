using System.Net;

namespace Bukit.Cli.Commands.Dev;

internal interface IDevServerHost : IDisposable
{
    int Port { get; }
    string Prefix { get; }
    Task RunAcceptLoopAsync(Func<HttpListenerContext, Task> dispatchAsync, CancellationToken ct);
}
