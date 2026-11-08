using Xunit;
using Bukit.Engine.Primitives;

namespace Bukit.Engine.Tests;

public sealed class SeoTextWidthTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 3)]
    [InlineData("丝路", 4)]
    [InlineData("ＡＢ", 4)]
    [InlineData("a丝b", 4)]
    public void Measure_CountsWideCharactersAsTwo(string text, int expected)
        => Assert.Equal(expected, SeoTextWidth.Measure(text));

    [Fact]
    public void Truncate_ShortText_IsUnchanged()
        => Assert.Equal("短文本", SeoTextWidth.Truncate("短文本", 160));

    [Fact]
    public void Truncate_LongCjkText_FitsBudgetAndEndsWithEllipsis()
    {
        var text = new string('字', 100); // width 200
        var cut = SeoTextWidth.Truncate(text, 160);
        Assert.EndsWith("…", cut, StringComparison.Ordinal);
        Assert.True(SeoTextWidth.Measure(cut) <= 160);
        Assert.Equal(79, cut.Length - 1);
    }

    [Fact]
    public void Truncate_DoesNotSplitSurrogatePairs()
    {
        var text = string.Concat(Enumerable.Repeat("𠀀", 10)); // width 20
        var cut = SeoTextWidth.Truncate(text, 9);
        Assert.Equal(string.Concat(Enumerable.Repeat("𠀀", 4)) + "…", cut);
    }

    [Fact]
    public void DerivedDescription_LongSummaryIsCut_NullStaysNull()
    {
        Assert.Null(SeoModelBuilder.DerivedDescription(null));
        var cut = SeoModelBuilder.DerivedDescription(new string('a', 300))!;
        Assert.Equal(160, SeoTextWidth.Measure(cut));
    }
}
