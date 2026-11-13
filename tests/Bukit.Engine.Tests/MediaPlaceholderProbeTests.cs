using Bukit.Config;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class MediaPlaceholderProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "bukit-placeholder-probe-" + Guid.NewGuid().ToString("N"));
    private readonly ILogger _logger = new ConsoleLogger(LogLevel.Error);

    public MediaPlaceholderProbeTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FindMissing_ReportsAPlaceholderNoDirectoryProvides()
    {
        Assert.Equal("/assets/images/noneimg-news.jpg", MediaPlaceholderProbe.FindMissing(Config(), _root, _logger));
        var warning = MediaPlaceholderProbe.CheckWarning(Config(), _root, _logger);
        Assert.NotNull(warning);
        Assert.Contains("/assets/images/noneimg-news.jpg", warning);
        Assert.Contains("content.media.onFailure: keep", warning);
    }

    [Theory]
    [InlineData("static/assets/images/noneimg-news.jpg", null)]
    [InlineData("assets/images/noneimg-news.jpg", null)]
    [InlineData("themes/plain/static/assets/images/noneimg-news.jpg", "plain")]
    [InlineData("themes/plain/assets/images/noneimg-news.jpg", "plain")]
    public void FindMissing_AcceptsThePlaceholderInTheThemeOrSite(string file, string? theme)
    {
        if (theme is not null) Directory.CreateDirectory(Path.Combine(_root, "themes", theme));
        Touch(file);
        var config = Config() with { Theme = new ThemeConfig { Name = theme } };

        Assert.Null(MediaPlaceholderProbe.FindMissing(config, _root, _logger));
        Assert.Null(MediaPlaceholderProbe.CheckWarning(config, _root, _logger));
    }

    [Fact]
    public void FindMissing_UsesACustomDefaultImageUrl()
    {
        Touch("assets/images/noneimg-news.jpg");
        var config = Config(new MediaConfig { DefaultImageUrl = "img/fallback.png" });
        Assert.Equal("/img/fallback.png", MediaPlaceholderProbe.FindMissing(config, _root, _logger));

        Touch("static/img/fallback.png");
        Assert.Null(MediaPlaceholderProbe.FindMissing(config, _root, _logger));
    }

    [Theory]
    [InlineData("keep", "/assets/images/noneimg-news.jpg", true)]
    [InlineData("placeholder", "https://cdn.example/fallback.jpg", true)]
    [InlineData("placeholder", "/assets/images/noneimg-news.jpg", false)]
    public void FindMissing_IgnoresKeepRemotePlaceholdersAndDisabledDownloads(string onFailure, string defaultImageUrl, bool downloadToLocal)
    {
        var config = Config(new MediaConfig { OnFailure = onFailure, DefaultImageUrl = defaultImageUrl, DownloadToLocal = downloadToLocal });
        if (downloadToLocal) Assert.Null(MediaPlaceholderProbe.FindMissing(config, _root, _logger));
        Assert.Null(MediaPlaceholderProbe.CheckWarning(config, _root, _logger));
    }

    private void Touch(string relative)
    {
        var path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [1]);
    }

    private static AppConfig Config(MediaConfig? media = null) => new()
    {
        Site = new SiteConfig { Name = "probe", Title = "Probe", BaseUrl = "/" },
        Content = new ContentConfig { Media = media ?? new MediaConfig() }
    };
}
