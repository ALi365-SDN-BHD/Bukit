namespace Bukit.Content.Media;

public interface IImageAssetLocalizer
{
    Task<string> LocalizeAsync(string? sourceUrl, CancellationToken cancellationToken);
}
