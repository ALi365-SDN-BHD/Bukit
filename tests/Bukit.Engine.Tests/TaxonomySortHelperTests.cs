using Xunit;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine.Tests;

/// <summary>
/// Tests for TaxonomySortHelper comparison and pinning logic.
/// </summary>
public sealed class TaxonomySortHelperTests
{
    private static TaxonomyPage MakePage(string title, string url, DateTimeOffset? publishAt = null, bool pinned = false, int? pinOrder = null)
        => new(
            Id: url,
            Title: title,
            Url: url,
            PublishAt: publishAt ?? new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Summary: null,
            Extra: null,
            IsPinned: pinned,
            PinOrder: pinOrder);

    // ── ComparePages ────────────────────────────────────────────────

    [Fact]
    public void ComparePages_PinnedBeforeUnpinned()
    {
        var pinned = MakePage("A", "/a/", pinned: true);
        var unpinned = MakePage("B", "/b/");
        Assert.True(TaxonomySortHelper.ComparePages(pinned, unpinned) < 0);
    }

    [Fact]
    public void ComparePages_UnpinnedAfterPinned()
    {
        var pinned = MakePage("A", "/a/", pinned: true);
        var unpinned = MakePage("B", "/b/");
        Assert.True(TaxonomySortHelper.ComparePages(unpinned, pinned) > 0);
    }

    [Fact]
    public void ComparePages_BothPinned_ByPinOrder()
    {
        var first = MakePage("A", "/a/", pinned: true, pinOrder: 1);
        var second = MakePage("B", "/b/", pinned: true, pinOrder: 2);
        Assert.True(TaxonomySortHelper.ComparePages(first, second) < 0);
    }

    [Fact]
    public void ComparePages_NewerPublishAtFirst()
    {
        var newer = MakePage("A", "/a/", new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var older = MakePage("B", "/b/", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(TaxonomySortHelper.ComparePages(newer, older) < 0);
    }

    [Fact]
    public void ComparePages_SamePublishAt_ByTitle()
    {
        var alpha = MakePage("Alpha", "/alpha/", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var bravo = MakePage("Bravo", "/bravo/", new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(TaxonomySortHelper.ComparePages(alpha, bravo) < 0);
    }

    [Fact]
    public void ComparePages_SameEverything_ByUrl()
    {
        var a = MakePage("Same", "/a/");
        var b = MakePage("Same", "/b/");
        Assert.True(TaxonomySortHelper.ComparePages(a, b) < 0);
    }

    // ── ComparePinOrder ─────────────────────────────────────────────

    [Fact]
    public void ComparePinOrder_HasValueBeforeNull()
    {
        Assert.True(TaxonomySortHelper.ComparePinOrder(1, null) < 0);
    }

    [Fact]
    public void ComparePinOrder_NullAfterValue()
    {
        Assert.True(TaxonomySortHelper.ComparePinOrder(null, 1) > 0);
    }

    [Fact]
    public void ComparePinOrder_BothNull_Equal()
    {
        Assert.Equal(0, TaxonomySortHelper.ComparePinOrder(null, null));
    }

    [Fact]
    public void ComparePinOrder_BothValues_Compare()
    {
        Assert.True(TaxonomySortHelper.ComparePinOrder(1, 2) < 0);
        Assert.True(TaxonomySortHelper.ComparePinOrder(5, 3) > 0);
        Assert.Equal(0, TaxonomySortHelper.ComparePinOrder(4, 4));
    }

    // ── TryGetPinned ────────────────────────────────────────────────

    [Fact]
    public void TryGetPinned_EmptyField_ReturnsFalse()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null, null);
        Assert.False(TaxonomySortHelper.TryGetPinned(doc, ""));
    }

    [Fact]
    public void TryGetPinned_BoolTrue_ReturnsTrue()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["pinned"] = new("bool", true) });
        Assert.True(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    [Fact]
    public void TryGetPinned_IntNonZero_ReturnsTrue()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["pinned"] = new("int", 1) });
        Assert.True(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    [Fact]
    public void TryGetPinned_IntZero_ReturnsFalse()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["pinned"] = new("int", 0) });
        Assert.False(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    [Fact]
    public void TryGetPinned_StringYes_ReturnsTrue()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["pinned"] = new("text", "yes") });
        Assert.True(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    [Fact]
    public void TryGetPinned_StringNo_ReturnsFalse()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["pinned"] = new("text", "no") });
        Assert.False(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    [Fact]
    public void TryGetPinned_StringOne_ReturnsTrue()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["pinned"] = new("text", "1") });
        Assert.True(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    [Fact]
    public void TryGetPinned_MissingField_ReturnsFalse()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null, null);
        Assert.False(TaxonomySortHelper.TryGetPinned(doc, "pinned"));
    }

    // ── TryGetPinOrder ──────────────────────────────────────────────

    [Fact]
    public void TryGetPinOrder_EmptyField_ReturnsNull()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null, null);
        Assert.Null(TaxonomySortHelper.TryGetPinOrder(doc, ""));
    }

    [Fact]
    public void TryGetPinOrder_IntValue_ReturnsInt()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["order"] = new("int", 42) });
        Assert.Equal(42, TaxonomySortHelper.TryGetPinOrder(doc, "order"));
    }

    [Fact]
    public void TryGetPinOrder_StringValue_Parses()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["order"] = new("text", "7") });
        Assert.Equal(7, TaxonomySortHelper.TryGetPinOrder(doc, "order"));
    }

    [Fact]
    public void TryGetPinOrder_InvalidString_ReturnsNull()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["order"] = new("text", "abc") });
        Assert.Null(TaxonomySortHelper.TryGetPinOrder(doc, "order"));
    }

    [Fact]
    public void TryGetPinOrder_DoubleRounds()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null,
            new Dictionary<string, ContentField> { ["order"] = new("double", 3.7) });
        Assert.Equal(4, TaxonomySortHelper.TryGetPinOrder(doc, "order"));
    }

    [Fact]
    public void TryGetPinOrder_MissingField_ReturnsNull()
    {
        var doc = ContentDocument.Create("1", "T", "1", DateTimeOffset.UtcNow, null, null);
        Assert.Null(TaxonomySortHelper.TryGetPinOrder(doc, "order"));
    }

    // ── ParseBoolLike ───────────────────────────────────────────────

    [Theory]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("yes", true)]
    [InlineData("no", false)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("YES", true)]
    [InlineData("NO", false)]
    [InlineData("TRUE", true)]
    [InlineData("garbage", false)]
    [InlineData(null, false)]
    public void ParseBoolLike_VariousInputs(string? input, bool expected)
    {
        Assert.Equal(expected, TaxonomySortHelper.ParseBoolLike(input!));
    }
}
