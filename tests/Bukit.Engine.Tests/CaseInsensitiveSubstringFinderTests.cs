using Bukit.Engine.Audit;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class CaseInsensitiveSubstringFinderTests
{
    [Fact]
    public void FindAll_ReportsOverlappingAndNestedNeedles()
    {
        var finder = new CaseInsensitiveSubstringFinder(["/blog/", "/blog/a/", "<loc>https://x.test/blog/a/</loc>", "/missing/"]);

        var found = finder.FindAll("<urlset><loc>https://x.test/blog/a/</loc></urlset>");

        Assert.Equal(["/blog/", "/blog/a/", "<loc>https://x.test/blog/a/</loc>"], found.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void FindAll_IgnoresCaseLikeOrdinalIgnoreCase()
    {
        var finder = new CaseInsensitiveSubstringFinder(["/Blog/Post/", "/blog/post/"]);

        var found = finder.FindAll("see /BLOG/POST/ here");

        Assert.Equal(["/Blog/Post/", "/blog/post/"], found.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void FindAll_HandlesNullHaystackNullAndEmptyNeedlesAndSurrogates()
    {
        var finder = new CaseInsensitiveSubstringFinder(["", null, "/emoji-\U0001F600/", "/x/"]);

        Assert.Empty(finder.FindAll(null));
        Assert.Equal(["", "/emoji-\U0001F600/"], finder.FindAll("a /emoji-\U0001F600/ b").Order(StringComparer.Ordinal));
    }

    [Fact]
    public void FindAll_DoesNotFoldLongSToAsciiS()
    {
        // ICU invariant casing maps U+017F to 'S'; OrdinalIgnoreCase keeps them distinct.
        var finder = new CaseInsensitiveSubstringFinder(["/posts/"]);

        Assert.Empty(finder.FindAll("/poſts/"));
        Assert.False("/poſts/".Contains("/posts/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Fold_MatchesOrdinalIgnoreCaseForEveryBmpChar()
    {
        for (var i = 0; i <= 0xFFFF; i++)
        {
            var ch = (char)i;
            if (char.IsSurrogate(ch))
            {
                continue;
            }

            var folded = CaseInsensitiveSubstringFinder.Fold(ch);
            Assert.True(
                string.Equals(ch.ToString(), folded.ToString(), StringComparison.OrdinalIgnoreCase),
                $"U+{i:X4} folds to U+{(int)folded:X4}, which OrdinalIgnoreCase treats as different");
        }
    }

    [Fact]
    public void FindAll_MatchesContainsForRandomInputs()
    {
        var random = new Random(1234);
        const string alphabet = "abAB/-.sſSıiIİKkKΣσςЖжß";
        string Next(int max) => new(Enumerable.Range(0, random.Next(1, max)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());

        for (var round = 0; round < 500; round++)
        {
            var needles = Enumerable.Range(0, 12).Select(_ => Next(6)).ToArray();
            var haystack = Next(80);

            var found = new CaseInsensitiveSubstringFinder(needles).FindAll(haystack);

            var expected = needles.Where(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.Ordinal);
            Assert.True(expected.SetEquals(found), $"round {round}: '{haystack}'");
        }
    }
}
