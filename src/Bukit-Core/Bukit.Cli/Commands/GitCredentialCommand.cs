using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using Bukit.Cli.Deploy;

namespace Bukit.Cli.Commands;

/// <summary>
/// Hidden git credential helper (plan 3.2): <c>bukit __git-credential &lt;endpoint&gt; &lt;get|store|erase&gt;</c>.
/// Dispatched before any configuration, logging or telemetry so stdout carries only the git
/// credential protocol. It forwards protocol and host to the deploy's broker and prints the
/// answer; every failure prints nothing and exits 0, so git reports an authentication error.
/// </summary>
internal static class GitCredentialCommand
{
    internal const string Name = GitCredentialPeerPolicy.HelperCommand;

    private const int MaxInputBytes = 64 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RegistrationRetryWindow = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RegistrationRetryDelay = TimeSpan.FromMilliseconds(100);

    internal static async Task<int> RunAsync(string[] args, Stream input, Stream output, TextWriter error)
    {
        if (args is not [var endpoint, var action] || string.IsNullOrEmpty(endpoint))
        {
            await error.WriteLineAsync("bukit: internal git credential helper; not for direct use.").ConfigureAwait(false);
            return 2;
        }

        using var timeout = new CancellationTokenSource(Timeout);
        Dictionary<string, string> request;
        try
        {
            request = await ReadRequestAsync(input, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException)
        {
            await error.WriteLineAsync($"bukit: git credential request unreadable ({ex.GetType().Name})").ConfigureAwait(false);
            return 0;
        }

        // git calls store after a successful login and erase after a failed one: nothing to keep.
        if (!string.Equals(action, "get", StringComparison.Ordinal))
        {
            return 0;
        }

        var query = new GitCredentialRequest(
            GitCredentialProtocol.Version,
            request.GetValueOrDefault("protocol"),
            request.GetValueOrDefault("host"));
        GitCredentialResponse? response;
        try
        {
            response = await AskBrokerAsync(endpoint, query, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or UnauthorizedAccessException or TimeoutException)
        {
            await error.WriteLineAsync($"bukit: credential broker unavailable ({ex.GetType().Name})").ConfigureAwait(false);
            return 0;
        }

        if (response is not { Deny: null, Username: { } username, Password: { } password } ||
            ContainsLineBreak(username) || ContainsLineBreak(password))
        {
            await error.WriteLineAsync($"bukit: credential refused ({response?.Deny ?? "no answer"})").ConfigureAwait(false);
            return 0;
        }

        var answer = Encoding.UTF8.GetBytes($"username={username}\npassword={password}\n\n");
        await output.WriteAsync(answer, CancellationToken.None).ConfigureAwait(false);
        await output.FlushAsync(CancellationToken.None).ConfigureAwait(false);
        return 0;
    }

    /// <summary>Reads git's key=value lines up to a blank line or EOF; unknown keys are ignored.</summary>
    private static async Task<Dictionary<string, string>> ReadRequestAsync(Stream input, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var read = await input.ReadAsync(chunk, ct).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
            if (buffer.Length > MaxInputBytes)
            {
                throw new InvalidDataException("git credential request too large");
            }

            if (EndsRequest(buffer))
            {
                break;
            }
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length).Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                break;
            }

            var separator = trimmed.IndexOf('=');
            if (separator > 0)
            {
                values.TryAdd(trimmed[..separator], trimmed[(separator + 1)..]);
            }
        }

        return values;
    }

    private static bool EndsRequest(MemoryStream buffer)
    {
        var data = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);
        return data.StartsWith("\n"u8) || data.StartsWith("\r\n"u8) ||
               data.IndexOf("\n\n"u8) >= 0 || data.IndexOf("\n\r\n"u8) >= 0;
    }

    /// <summary>
    /// Asks the broker; while it answers "unregistered" (git's pid not yet recorded after
    /// Process.Start) retries briefly (plan 3.4, registration race).
    /// </summary>
    private static async Task<GitCredentialResponse?> AskBrokerAsync(string endpoint, GitCredentialRequest request, CancellationToken ct)
    {
        var retryUntil = DateTime.UtcNow + RegistrationRetryWindow;
        while (true)
        {
            var response = await ExchangeAsync(endpoint, request, ct).ConfigureAwait(false);
            if (response?.Deny != GitCredentialDenial.Unregistered || DateTime.UtcNow >= retryUntil)
            {
                return response;
            }

            await Task.Delay(RegistrationRetryDelay, ct).ConfigureAwait(false);
        }
    }

    private static async Task<GitCredentialResponse?> ExchangeAsync(string endpoint, GitCredentialRequest request, CancellationToken ct)
    {
        if (OperatingSystem.IsWindows())
        {
            await using var pipe = new NamedPipeClientStream(".", endpoint, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(ct).ConfigureAwait(false);
            return await ExchangeOverAsync(pipe, request, ct).ConfigureAwait(false);
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), ct).ConfigureAwait(false);
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        return await ExchangeOverAsync(stream, request, ct).ConfigureAwait(false);
    }

    private static async Task<GitCredentialResponse?> ExchangeOverAsync(Stream stream, GitCredentialRequest request, CancellationToken ct)
    {
        await stream.WriteAsync(GitCredentialProtocol.Serialize(request), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        var line = await GitCredentialProtocol.ReadLineAsync(stream, ct).ConfigureAwait(false);
        var response = line is null ? null : GitCredentialProtocol.ParseResponse(line);
        return response?.V == GitCredentialProtocol.Version ? response : null;
    }

    private static bool ContainsLineBreak(string value) => value.AsSpan().IndexOfAny('\n', '\r', '\0') >= 0;
}
