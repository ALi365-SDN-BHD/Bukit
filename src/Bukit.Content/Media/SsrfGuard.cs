using System.Net;
using System.Net.Sockets;

namespace Bukit.Content.Media;

internal static class SsrfGuard
{
    internal static async ValueTask<Stream> SsrfSafeConnectAsync(
        SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var port = context.DnsEndPoint.Port;

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var safeAddress = Array.Find(addresses, static a => !IsPrivateAddress(a))
                          ?? throw new HttpRequestException(
                              $"SSRF blocked: all resolved addresses for '{host}' are private/reserved.");

        var socket = new Socket(safeAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            await socket.ConnectAsync(new IPEndPoint(safeAddress, port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    internal static async Task<bool> IsPrivateHostAsync(
        string host, CancellationToken cancellationToken)
    {
        try
        {
            if (IPAddress.TryParse(host, out var directIp))
            {
                return IsPrivateAddress(directIp);
            }

            var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
            return addresses.Length > 0 && Array.Exists(addresses, IsPrivateAddress);
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsPrivateAddress(IPAddress address)
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
            return b[0] switch
            {
                0 => true,
                10 => true,
                127 => true,
                169 => b[1] == 254,
                172 => b[1] >= 16 && b[1] <= 31,
                192 => b[1] == 168,
                _ => false
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal;
        }

        return false;
    }
}
