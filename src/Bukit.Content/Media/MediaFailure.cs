namespace Bukit.Content.Media;

/// <summary>Records a single image URL that failed to localize.</summary>
public sealed record MediaFailure(string SourceUrl, string Reason);
