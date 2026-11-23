namespace Bukit.CloudBase.Transport;

/// <summary>
/// Transport failure. The message never contains the request URL, headers, credential, signed links or
/// query strings; <see cref="ProviderCode"/> is the error body's <c>code</c> only.
/// </summary>
internal sealed class CloudBaseApiException : Exception
{
    internal CloudBaseApiException(CloudBaseErrorKind kind, int? statusCode = null, string? providerCode = null, Exception? inner = null)
        : base(BuildMessage(kind, statusCode, providerCode), inner)
    {
        Kind = kind;
        StatusCode = statusCode;
        ProviderCode = providerCode;
    }

    internal CloudBaseErrorKind Kind { get; }

    internal int? StatusCode { get; }

    internal string? ProviderCode { get; }

    private static string BuildMessage(CloudBaseErrorKind kind, int? statusCode, string? providerCode)
    {
        var message = statusCode is { } status
            ? $"CloudBase request failed: {kind} (HTTP {status})."
            : $"CloudBase request failed: {kind}.";
        return string.IsNullOrWhiteSpace(providerCode) ? message : $"{message} code={providerCode}";
    }
}
