namespace Bukit.Engine.Incremental;

internal sealed class ImageProcessingContributor : IRenderDependencyContributor
{
    public string Name => "image-processing";

    public void Contribute(RenderDependencyContext context, RenderDependencyHashWriter writer)
    {
        if (context.ImageSourceFingerprint is null)
        {
            return;
        }

        writer.AppendLabeledCanonicalValue("content.media.urlBase", context.Config.Content.Media.UrlBase);
        writer.AppendLabeledCanonicalValue("content.media.downloadDir", context.Config.Content.Media.DownloadDir);
        writer.AppendLabeledCanonicalValue("content.media.downloadToLocal", context.Config.Content.Media.DownloadToLocal);
        writer.AppendLabeledCanonicalValue("theme.images.sourceFingerprint", context.ImageSourceFingerprint);
    }
}
