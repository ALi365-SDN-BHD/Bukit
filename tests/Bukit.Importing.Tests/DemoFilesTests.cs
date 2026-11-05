using System.Text;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class DemoFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bukit-demo-files-" + Guid.NewGuid().ToString("N"));

    public DemoFilesTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Theory]
    [InlineData("gbk")]
    [InlineData("GB2312")]
    [InlineData("gb18030")]
    public void DecodeHtml_DeclaredChineseCharset_ReadsText(string charset)
    {
        var html = $"<html><head><meta charset=\"{charset}\"><title>联系我们</title></head><body>电话</body></html>";

        var decoded = DemoFiles.DecodeHtml(Encoding.GetEncoding("GB18030").GetBytes(html));

        Assert.Contains("联系我们", decoded, StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeHtml_ContentTypeMeta_ReadsText()
    {
        var html = "<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=gbk\"></head><body>新闻</body></html>";

        Assert.Contains("新闻", DemoFiles.DecodeHtml(Encoding.GetEncoding("GB18030").GetBytes(html)), StringComparison.Ordinal);
    }

    [Fact]
    public void DecodeHtml_UndeclaredChinese_GuessesGb18030()
    {
        var html = "<html><body>关于我们，公司简介</body></html>";

        var decoded = DemoFiles.DecodeHtml(Encoding.GetEncoding("GB18030").GetBytes(html), out var guessed);

        Assert.Contains("关于我们", decoded, StringComparison.Ordinal);
        Assert.Equal("GB18030", guessed);
    }

    [Fact]
    public void DecodeHtml_UndeclaredWestern_GuessesWindows1252()
    {
        var bytes = Encoding.GetEncoding(1252).GetBytes("<html><body>Café résumé naïve</body></html>");

        var decoded = DemoFiles.DecodeHtml(bytes, out var guessed);

        Assert.Contains("Café résumé naïve", decoded, StringComparison.Ordinal);
        Assert.Equal("Windows-1252", guessed);
    }

    [Fact]
    public void DecodeHtml_Utf8WithAndWithoutBom_ReadsText()
    {
        var bytes = Encoding.UTF8.GetBytes("<p>首页</p>");

        Assert.Equal("<p>首页</p>", DemoFiles.DecodeHtml(bytes));
        Assert.Equal("<p>首页</p>", DemoFiles.DecodeHtml([0xEF, 0xBB, 0xBF, .. bytes]));
    }

    [Fact]
    public void Enumerate_SkipsSymbolicLinks()
    {
        var outside = Path.Combine(Path.GetTempPath(), "bukit-outside-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(outside, "secret");
        try
        {
            File.WriteAllText(Path.Combine(_dir, "index.html"), "<html></html>");
            File.CreateSymbolicLink(Path.Combine(_dir, "leak.txt"), outside);

            var files = DemoFiles.Enumerate(_dir).Select(Path.GetFileName).ToList();

            Assert.Contains("index.html", files);
            Assert.DoesNotContain("leak.txt", files);
            Assert.Single(DemoFiles.SymbolicLinks(_dir));

            // A link back to the demo folder is reported once, not walked.
            Directory.CreateDirectory(Path.Combine(_dir, "a"));
            Directory.CreateSymbolicLink(Path.Combine(_dir, "a", "loop"), _dir);
            Assert.Equal(2, DemoFiles.SymbolicLinks(_dir).Count());
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void DecodeHtml_Utf8FileWithStaleGbkDeclaration_ReadsUtf8AndSaysSo()
    {
        var bytes = Encoding.UTF8.GetBytes("<html><head><meta charset=\"gb2312\"></head><body>关于我们</body></html>");

        var decoded = DemoFiles.DecodeHtml(bytes, out var guessed);

        Assert.Contains("关于我们", decoded, StringComparison.Ordinal);
        Assert.StartsWith("UTF-8", guessed);
    }
}
