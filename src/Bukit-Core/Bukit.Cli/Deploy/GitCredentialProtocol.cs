using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bukit.Cli.Deploy;

/// <summary>
/// One-line JSON exchanged once per connection between the <c>__git-credential</c> helper
/// and the deploy's credential broker. The request never carries a secret.
/// </summary>
internal static class GitCredentialProtocol
{
    internal const int Version = 1;
    internal const int MaxLineBytes = 4096;
    internal const string Username = "x-access-token";

    internal static byte[] Serialize(GitCredentialRequest request) =>
        Line(JsonSerializer.SerializeToUtf8Bytes(request, GitCredentialJsonContext.Default.GitCredentialRequest));

    internal static byte[] Serialize(GitCredentialResponse response) =>
        Line(JsonSerializer.SerializeToUtf8Bytes(response, GitCredentialJsonContext.Default.GitCredentialResponse));

    /// <summary>Parses a request line; null for malformed JSON or a missing body.</summary>
    internal static GitCredentialRequest? ParseRequest(ReadOnlySpan<byte> line) =>
        Parse(line, GitCredentialJsonContext.Default.GitCredentialRequest);

    /// <summary>Parses a response line; null for malformed JSON or a missing body.</summary>
    internal static GitCredentialResponse? ParseResponse(ReadOnlySpan<byte> line) =>
        Parse(line, GitCredentialJsonContext.Default.GitCredentialResponse);

    /// <summary>
    /// Reads bytes up to the first newline. Returns null when the peer closes early, sends
    /// more than <see cref="MaxLineBytes"/>, or the token is cancelled.
    /// </summary>
    internal static async Task<byte[]?> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[MaxLineBytes + 1];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            var newline = Array.IndexOf(buffer, (byte)'\n', length, read);
            length += read;
            if (newline >= 0)
            {
                return newline > MaxLineBytes ? null : buffer[..newline];
            }
        }

        return null;
    }

    private static T? Parse<T>(ReadOnlySpan<byte> line, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize(line, typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static byte[] Line(byte[] json)
    {
        var line = new byte[json.Length + 1];
        json.CopyTo(line, 0);
        line[^1] = (byte)'\n';
        return line;
    }
}

/// <summary>Why the broker refused a request; also the value logged for the refusal.</summary>
internal static class GitCredentialDenial
{
    internal const string Version = "version";
    internal const string User = "user";
    internal const string Executable = "exe";
    internal const string Peer = "peer";
    internal const string Unregistered = "unregistered";
    internal const string Protocol = "protocol";
    internal const string Host = "host";
    internal const string Limit = "limit";
}

internal sealed record GitCredentialRequest(int V, string? Protocol, string? Host);

internal sealed record GitCredentialResponse(int V, string? Username = null, string? Password = null, string? Deny = null);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GitCredentialRequest))]
[JsonSerializable(typeof(GitCredentialResponse))]
internal sealed partial class GitCredentialJsonContext : JsonSerializerContext;
