using System.Net;
using System.Net.Sockets;

namespace Bukit.Shared;

public static class SsrfGuard
{
    public static async ValueTask<Stream> SsrfSafeConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        return await SsrfSafeConnectAsync(
            context.DnsEndPoint.Host,
            context.DnsEndPoint.Port,
            cancellationToken,
            Dns.GetHostAddressesAsync,
            ConnectSocketAsync).ConfigureAwait(false);
    }

    internal static async ValueTask<Stream> SsrfSafeConnectAsync(
        string host,
        int port,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync,
        Func<IPAddress, int, CancellationToken, ValueTask<Stream>> connectAsync)
    {
        ArgumentNullException.ThrowIfNull(resolveAddressesAsync);
        ArgumentNullException.ThrowIfNull(connectAsync);
        cancellationToken.ThrowIfCancellationRequested();

        var addresses = await resolveAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        Exception? lastFailure = null;
        var foundPublicAddress = false;
        foreach (var address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsPrivateAddress(address))
            {
                continue;
            }

            foundPublicAddress = true;
            try
            {
                return await connectAsync(address, port, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is SocketException or IOException or HttpRequestException)
            {
                lastFailure = exception;
            }
        }

        if (!foundPublicAddress)
        {
            throw new HttpRequestException(
                $"SSRF blocked: all resolved addresses for '{host}' are private/reserved.");
        }

        throw new HttpRequestException(
            $"Unable to connect to any public address resolved for '{host}'.",
            lastFailure);
    }

    private static async ValueTask<Stream> ConnectSocketAsync(
        IPAddress address,
        int port,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when any resolved address is private/reserved
    /// or the host cannot be safely resolved. Cancellation and unexpected resolver
    /// failures propagate. Use <see cref="CreateSafeHandler"/> for connection-time
    /// SSRF enforcement instead of treating this preflight result as authorization.
    /// </summary>
    public static async Task<bool> IsPrivateHostAsync(
        string host, CancellationToken cancellationToken)
        => await IsPrivateHostAsync(
            host,
            cancellationToken,
            Dns.GetHostAddressesAsync).ConfigureAwait(false);

    internal static async Task<bool> IsPrivateHostAsync(
        string host,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveAddressesAsync)
    {
        ArgumentNullException.ThrowIfNull(resolveAddressesAsync);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (IPAddress.TryParse(host, out var directIp))
            {
                return IsPrivateAddress(directIp);
            }

            var addresses = await resolveAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            return addresses.Length == 0 || Array.Exists(addresses, IsPrivateAddress);
        }
        catch (Exception exception) when (exception is SocketException or ArgumentException)
        {
            return true;
        }
    }

    public static bool IsPrivateAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            var value = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            return IsInCidr(value, 0x00000000u, 8) ||
                   IsInCidr(value, 0x0A000000u, 8) ||
                   IsInCidr(value, 0x64400000u, 10) ||
                   IsInCidr(value, 0x7F000000u, 8) ||
                   IsInCidr(value, 0xA9FE0000u, 16) ||
                   IsInCidr(value, 0xAC100000u, 12) ||
                   IsInCidr(value, 0xC0000000u, 24) ||
                   IsInCidr(value, 0xC0000200u, 24) ||
                   IsInCidr(value, 0xC0586300u, 24) ||
                   IsInCidr(value, 0xC0A80000u, 16) ||
                   IsInCidr(value, 0xC6120000u, 15) ||
                   IsInCidr(value, 0xC6336400u, 24) ||
                   IsInCidr(value, 0xCB007100u, 24) ||
                   IsInCidr(value, 0xE0000000u, 4) ||
                   IsInCidr(value, 0xF0000000u, 4);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return address.Equals(IPAddress.IPv6Any) ||
                   address.Equals(IPAddress.IPv6None) ||
                   address.IsIPv6LinkLocal ||
                   address.IsIPv6SiteLocal ||
                   address.IsIPv6Multicast ||
                   (b[0] & 0xFE) == 0xFC ||
                   (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8);
        }

        return false;
    }

    private static bool IsInCidr(uint value, uint network, int prefixLength)
    {
        var mask = uint.MaxValue << (32 - prefixLength);
        return (value & mask) == (network & mask);
    }

    // ── Unified HttpClient factory ─────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="SocketsHttpHandler"/> whose <c>ConnectCallback</c>
    /// is wired to <see cref="SsrfSafeConnectAsync"/>. Use this to inject SSRF
    /// protection into APIs that accept a handler factory.
    /// </summary>
    public static SocketsHttpHandler CreateSafeHandler()
        => new() { ConnectCallback = SsrfSafeConnectAsync };

    /// <summary>
    /// Creates an <see cref="HttpClient"/> with a <see cref="SocketsHttpHandler"/>
    /// whose <c>ConnectCallback</c> is wired to <see cref="SsrfSafeConnectAsync"/>.
    /// All outbound HTTP in the engine should flow through this factory.
    /// </summary>
    public static HttpClient CreateSafeHttpClient(
        TimeSpan? timeout = null,
        string? userAgent = null)
    {
        var handler = CreateSafeHandler();
        var client = new HttpClient(handler, disposeHandler: true);
        if (timeout.HasValue)
        {
            client.Timeout = timeout.Value;
        }

        if (!string.IsNullOrEmpty(userAgent))
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }

        return client;
    }
}
