using Xunit;

namespace Bukit.Engine.Tests;

public sealed class OrdinalIgnoreCaseMultiMatcherTests
{
    [Fact]
    public void CharFolding_MatchesOrdinalIgnoreCaseForEveryBmpChar()
    {
        // Each char must equal its fold under
        // OrdinalIgnoreCase, and no two distinct folds may be equal, so folded equality is
        // exactly ordinal ignore-case equality outside surrogates.
        var folds = new HashSet<char>();
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var c = (char)i;
            if (char.IsSurrogate(c))
            {
                continue;
            }

            var fold = OrdinalIgnoreCaseMultiMatcher.Fold(c);
            Assert.True(string.Equals(c.ToString(), fold.ToString(), StringComparison.OrdinalIgnoreCase), $"U+{i:X4}");
            Assert.Equal(fold, OrdinalIgnoreCaseMultiMatcher.Fold(fold));
            folds.Add(fold);
        }

        var sorted = folds.Select(c => c.ToString()).ToList();
        sorted.Sort(StringComparer.OrdinalIgnoreCase);
        for (var i = 1; i < sorted.Count; i++)
        {
            Assert.False(string.Equals(sorted[i - 1], sorted[i], StringComparison.OrdinalIgnoreCase),
                $"U+{(int)sorted[i - 1][0]:X4} and U+{(int)sorted[i][0]:X4} fold apart but compare equal");
        }
    }

    [Theory]
    [InlineData("", new[] { "/a/" })]
    [InlineData("<loc>https://Example.com/A/</loc>", new[] { "https://example.com/a/", "/a/", "/b/", "EXAMPLE" })]
    [InlineData("aaaa", new[] { "a", "aa", "aaa", "aaaaa", "b" })]
    [InlineData("she sells", new[] { "he", "she", "hers", "his", "sell", "ells" })]
    [InlineData("/文章/第一篇/ /Ärger/", new[] { "/文章/", "/ärger/", "/ÄRGER/", "/文章/第二篇/" })]
    [InlineData("dotless ı and İ and ſ and K", new[] { "I", "i", "S", "k", "ı", "İ" })]
    [InlineData("deseret 𐐀 and 𐐨", new[] { "𐐀", "𐐨", "x𐐀" })]
    [InlineData("abc", new[] { "", "abc", "abcd" })]
    public void FindPresent_MatchesContainsOrdinalIgnoreCase(string text, string[] patterns)
        => AssertSameAsContains(text, patterns);

    [Fact]
    public void FindPresent_MatchesContainsOnRandomInput()
    {
        var random = new Random(20261110);
        const string alphabet = "aAbB/-.ıIİiſsSKkÄä文章𐐀";
        for (var round = 0; round < 500; round++)
        {
            var text = RandomString(random, alphabet, random.Next(0, 200));
            var patterns = Enumerable.Range(0, random.Next(1, 30))
                .Select(_ => random.Next(3) == 0 && text.Length > 0
                    ? Mutate(random, Slice(random, text))
                    : RandomString(random, alphabet, random.Next(1, 6)))
                .ToArray();
            AssertSameAsContains(text, patterns);
        }
    }

    private static void AssertSameAsContains(string text, IReadOnlyList<string> patterns)
    {
        var present = OrdinalIgnoreCaseMultiMatcher.FindPresent(text, patterns);
        foreach (var pattern in patterns)
        {
            Assert.True(text.Contains(pattern, StringComparison.OrdinalIgnoreCase) == present.Contains(pattern),
                $"pattern '{pattern}' in '{text}'");
        }

        Assert.All(present, pattern => Assert.Contains(pattern, patterns));
    }

    private static string RandomString(Random random, string alphabet, int length)
    {
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(alphabet);
        var units = new List<string>();
        while (elements.MoveNext())
        {
            units.Add((string)elements.Current);
        }

        return string.Concat(Enumerable.Range(0, length).Select(_ => units[random.Next(units.Count)]));
    }

    private static string Slice(Random random, string text)
    {
        var start = random.Next(text.Length);
        var length = random.Next(1, Math.Min(8, text.Length - start) + 1);
        return text.Substring(start, length);
    }

    private static string Mutate(Random random, string value) => random.Next(3) switch
    {
        0 => value.ToUpperInvariant(),
        1 => value.ToLowerInvariant(),
        _ => value
    };
}
