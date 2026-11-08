using System.Diagnostics;
using System.Text;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class NotionIdentifierMatcherTests
{
    private const string Compact = "343fa39a5013839fbd0681d287b003ba";
    private const string Dashed = "343fa39a-5013-839f-bd06-81d287b003ba";

    private static PublicOutputPrivacyCheck.NotionIdentifierMatcher Matcher(params string[] tokens)
        => PublicOutputPrivacyCheck.NotionIdentifierMatcher.From(new HashSet<string>(tokens, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData("see " + Compact + " here")]
    [InlineData("see " + Dashed + " here")]
    [InlineData("/page/343FA39A5013839FBD0681D287B003BA/")]
    [InlineData("https://www.notion.so/Title-" + Compact)]
    [InlineData("prefixabcdef" + Compact)]
    public void ContainsAny_FindsIdInAnyForm(string text)
    {
        Assert.True(Matcher(Dashed, Compact).ContainsAny(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no ids here")]
    [InlineData("other 0000000000000000000000000000000a id")]
    [InlineData("343fa39a5013839fbd0681d287b003b")]
    public void ContainsAny_IgnoresUnknownOrPartialIds(string text)
    {
        Assert.False(Matcher(Compact).ContainsAny(text));
    }

    [Fact]
    public void ContainsAny_FullValueTokenIsCoveredByItsId()
    {
        var matcher = Matcher("https://www.notion.so/Title-" + Compact);
        Assert.True(matcher.ContainsAny("x " + Dashed + " y"));
    }

    [Fact]
    public void ContainsAny_ManyTokensLargeText_IsLinear()
    {
        // ~3500-page sites made the old tokens x text scan run for many minutes per file.
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 8000; i++)
        {
            tokens.Add(Guid.NewGuid().ToString("N"));
        }

        var matcher = PublicOutputPrivacyCheck.NotionIdentifierMatcher.From(tokens);
        var text = new StringBuilder();
        while (text.Length < 5_000_000)
        {
            text.Append("{\"title\":\"文章标题\",\"url\":\"/1234/\",\"hash\":\"").Append(Guid.NewGuid().ToString("N")).Append("\"},");
        }

        var stopwatch = Stopwatch.StartNew();
        Assert.False(matcher.ContainsAny(text.ToString()));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"scan took {stopwatch.Elapsed}");
    }

    [Fact]
    public void ScanJson_DetectsEscapedAndPlainIds()
    {
        var tokens = new HashSet<string>([Compact], StringComparer.OrdinalIgnoreCase);
        Assert.Equal(JsonPrivacyScan.Leak, PublicOutputPrivacyCheck.ScanJson("{\"a\":\"" + Dashed + "\"}", tokens));
        Assert.Equal(JsonPrivacyScan.Leak, PublicOutputPrivacyCheck.ScanJson("{\"a\":\"\\u0033" + Compact[1..] + "\"}", tokens));
        Assert.Equal(JsonPrivacyScan.Clean, PublicOutputPrivacyCheck.ScanJson("{\"a\":\"clean\"}", tokens));
    }
}
