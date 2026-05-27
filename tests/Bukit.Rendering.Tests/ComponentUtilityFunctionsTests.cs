using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class ComponentUtilityFunctionsTests
{
    [Fact]
    public void FormatDate_DateTimeOffset_FormatsCorrectly()
    {
        var dto = new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var result = ComponentUtilityFunctions.FormatDate(dto, "yyyy-MM-dd");
        Assert.Equal("2024-06-15", result);
    }

    [Fact]
    public void FormatDate_DateTime_FormatsCorrectly()
    {
        var dt = new DateTime(2024, 6, 15);
        var result = ComponentUtilityFunctions.FormatDate(dt, "yyyy/MM/dd");
        Assert.Equal("2024/06/15", result);
    }

    [Fact]
    public void FormatDate_ValidString_ParsesAndFormats()
    {
        var result = ComponentUtilityFunctions.FormatDate("2024-06-15T10:30:00+00:00", "yyyy-MM-dd");
        Assert.Equal("2024-06-15", result);
    }

    [Fact]
    public void FormatDate_DateOnlyString_ParsesAndFormats()
    {
        var result = ComponentUtilityFunctions.FormatDate("2024-06-15", "yyyy-MM-dd");
        Assert.Equal("2024-06-15", result);
    }

    [Fact]
    public void FormatDate_InvalidString_ReturnsOriginal()
    {
        var result = ComponentUtilityFunctions.FormatDate("not-a-date", "yyyy-MM-dd");
        Assert.Equal("not-a-date", result);
    }

    [Fact]
    public void FormatDate_Null_ReturnsEmpty()
    {
        var result = ComponentUtilityFunctions.FormatDate(null);
        Assert.Equal("", result);
    }

    [Fact]
    public void FormatDate_DefaultFormat_UsesIsoFormat()
    {
        var dto = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var result = ComponentUtilityFunctions.FormatDate(dto);
        Assert.Equal("2024-01-01", result);
    }

    [Fact]
    public void Truncate_Null_ReturnsEmpty()
    {
        var result = ComponentUtilityFunctions.Truncate(null);
        Assert.Equal("", result);
    }

    [Fact]
    public void Truncate_ShorterThanMax_ReturnsOriginal()
    {
        var result = ComponentUtilityFunctions.Truncate("hello", 100);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Truncate_LongerThanMax_TruncatesWithEllipsis()
    {
        var result = ComponentUtilityFunctions.Truncate("hello world this is long", 10);
        Assert.Equal("hello worl…", result);
    }

    [Fact]
    public void Truncate_DefaultMaxLength_Uses100()
    {
        var longText = new string('a', 120);
        var result = ComponentUtilityFunctions.Truncate(longText);
        Assert.Equal(101, result.Length);
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void Titleize_Null_ReturnsEmpty()
    {
        var result = ComponentUtilityFunctions.Titleize(null);
        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("hello_world", "Hello World")]
    [InlineData("my_section_name", "My Section Name")]
    [InlineData("camelCase", "Camelcase")]
    [InlineData("already-title", "Already Title")]
    public void Titleize_VariousInputs_ReturnsTitleCase(string input, string expected)
    {
        var result = ComponentUtilityFunctions.Titleize(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Slugify_Null_ReturnsEmpty()
    {
        var result = ComponentUtilityFunctions.Slugify(null);
        Assert.Equal("", result);
    }

    [Theory]
    [InlineData("Hello World", "hello-world")]
    [InlineData("My Page Title", "my-page-title")]
    [InlineData("hello_world", "hello-world")]
    [InlineData("c#-sharp", "c-sharp")]
    public void Slugify_VariousInputs_ReturnsSlug(string input, string expected)
    {
        var result = ComponentUtilityFunctions.Slugify(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Slugify_EmptyString_ReturnsEmpty()
    {
        var result = ComponentUtilityFunctions.Slugify("");
        Assert.Equal("", result);
    }
}
