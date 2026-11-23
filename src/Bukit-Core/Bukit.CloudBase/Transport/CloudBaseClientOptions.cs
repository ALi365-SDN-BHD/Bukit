namespace Bukit.CloudBase.Transport;

internal sealed record CloudBaseClientOptions
{
    public required string EnvId { get; init; }
    public string Region { get; init; } = "cn";
    public required string PublishableKey { get; init; }
    public int MaxRps { get; init; } = 5;
    public int MaxRetries { get; init; } = 3;
    public int MaxResponseBytes { get; init; } = 8 * 1024 * 1024;

    public override string ToString()
        => $"CloudBaseClientOptions {{ EnvId = {EnvId}, Region = {Region}, PublishableKey = ***, MaxRps = {MaxRps}, MaxRetries = {MaxRetries}, MaxResponseBytes = {MaxResponseBytes} }}";

    internal string GatewayHost => CloudBaseEndpoints.GatewayHost(EnvId, Region);

    internal Uri GatewayOrigin => new($"https://{GatewayHost}");

    internal void Validate()
    {
        if (!CloudBaseEndpoints.EnvIdPattern().IsMatch(EnvId))
        {
            throw new ArgumentException("envId must match ^[a-z0-9][a-z0-9-]{0,62}$.", nameof(EnvId));
        }

        if (Region is not ("cn" or "intl"))
        {
            throw new ArgumentException("region must be cn or intl.", nameof(Region));
        }

        if (string.IsNullOrWhiteSpace(PublishableKey))
        {
            throw new ArgumentException("A publishable key is required.", nameof(PublishableKey));
        }

        if (MaxRps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRps));
        }

        if (MaxRetries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxRetries));
        }
    }
}
