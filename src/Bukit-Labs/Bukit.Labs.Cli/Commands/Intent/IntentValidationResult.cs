namespace Bukit.Labs.Cli.Commands;

public sealed record IntentValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;
}

