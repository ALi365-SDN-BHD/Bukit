using System.Text.Json;

namespace Bukit.CloudBase.Transport;

internal sealed record CloudBaseCredentialRole(string? Role, bool IsUnparseable)
{
    /// <summary>An unreadable role is treated like service_role: it cannot prove read-only access.</summary>
    internal bool IsAdminRisk => IsUnparseable || string.Equals(Role, "service_role", StringComparison.Ordinal);
}

internal static class CloudBaseCredentials
{
    /// <summary>Decodes the JWT payload only; the signature is never verified.</summary>
    internal static CloudBaseCredentialRole DecodeRole(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CloudBaseCredentialRole(null, IsUnparseable: true);
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return new CloudBaseCredentialRole(null, IsUnparseable: true);
        }

        try
        {
            var payload = DecodeBase64Url(parts[1]);
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("role", out var role) &&
                role.ValueKind == JsonValueKind.String)
            {
                return new CloudBaseCredentialRole(role.GetString(), IsUnparseable: false);
            }
        }
        catch (FormatException)
        {
        }
        catch (JsonException)
        {
        }

        return new CloudBaseCredentialRole(null, IsUnparseable: true);
    }

    private static byte[] DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = normalized.PadRight(normalized.Length + ((4 - (normalized.Length % 4)) % 4), '=');
        return Convert.FromBase64String(normalized);
    }
}
