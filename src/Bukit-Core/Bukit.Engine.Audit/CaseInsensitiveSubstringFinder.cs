namespace Bukit.Engine.Audit;

/// <summary>
/// Finds which of a fixed set of needles occur in a haystack, matching like
/// <c>haystack.Contains(needle, StringComparison.OrdinalIgnoreCase)</c> for every needle.
/// One Aho-Corasick scan per haystack replaces one full scan per needle, which keeps
/// per-route audit lookups against site-wide outputs linear in the site size.
/// </summary>
internal sealed class CaseInsensitiveSubstringFinder
{
    // All edges live in one map keyed by (state, folded char); per-node collections would
    // cost one allocation per trie node, and the trie grows with every route URL character.
    private readonly Dictionary<long, int> _edges = [];
    private readonly List<int> _parent = [0];
    private readonly List<char> _edgeChar = ['\0'];
    private readonly List<int> _depth = [0];
    private readonly Dictionary<int, List<string>> _outputs = [];
    private readonly List<string> _surrogateNeedles = [];
    private readonly List<string> _emptyNeedles = [];
    private int[] _failure = [];
    private int[] _outputLink = [];

    /// <param name="needles">Needles to find; null entries are ignored and never reported.</param>
    internal CaseInsensitiveSubstringFinder(IEnumerable<string?> needles)
    {
        ArgumentNullException.ThrowIfNull(needles);
        foreach (var needle in needles.Distinct(StringComparer.Ordinal))
        {
            if (needle is null)
            {
                continue;
            }

            if (needle.Length == 0)
            {
                _emptyNeedles.Add(needle);
            }
            else if (ContainsSurrogate(needle))
            {
                // Per-char folding is only equivalent to OrdinalIgnoreCase for BMP text.
                _surrogateNeedles.Add(needle);
            }
            else
            {
                Insert(needle);
            }
        }

        BuildLinks();
    }

    /// <summary>Returns the needles contained in <paramref name="haystack"/>; empty when it is null.</summary>
    internal HashSet<string> FindAll(string? haystack)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        if (haystack is null)
        {
            return found;
        }

        found.UnionWith(_emptyNeedles);
        foreach (var needle in _surrogateNeedles)
        {
            if (haystack.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(needle);
            }
        }

        var reported = new bool[_parent.Count];
        var state = 0;
        foreach (var ch in haystack)
        {
            state = Step(state, Fold(ch));
            ReportMatches(state, reported, found);
        }

        return found;
    }

    /// <summary>
    /// Folds a BMP char the way OrdinalIgnoreCase compares it. ICU's invariant upper-casing
    /// maps a few non-ASCII chars (U+017F LATIN SMALL LETTER LONG S to 'S') onto ASCII,
    /// which OrdinalIgnoreCase does not treat as equal, so those keep their own identity.
    /// </summary>
    internal static char Fold(char ch)
    {
        var upper = char.ToUpperInvariant(ch);
        return ch > '\u007F' && upper <= '\u007F' ? ch : upper;
    }

    private void Insert(string needle)
    {
        var state = 0;
        foreach (var ch in needle)
        {
            var folded = Fold(ch);
            var key = EdgeKey(state, folded);
            if (!_edges.TryGetValue(key, out var next))
            {
                next = _parent.Count;
                _parent.Add(state);
                _edgeChar.Add(folded);
                _depth.Add(_depth[state] + 1);
                _edges[key] = next;
            }

            state = next;
        }

        if (!_outputs.TryGetValue(state, out var list))
        {
            list = [];
            _outputs[state] = list;
        }

        list.Add(needle);
    }

    private void BuildLinks()
    {
        var count = _parent.Count;
        _failure = new int[count];
        _outputLink = new int[count];

        // Failure links need every shallower node resolved first, so visit nodes by depth.
        var byDepth = Enumerable.Range(1, count - 1).OrderBy(node => _depth[node]);
        foreach (var node in byDepth)
        {
            var parent = _parent[node];
            var fallback = parent == 0 ? 0 : Step(_failure[parent], _edgeChar[node]);
            _failure[node] = fallback;
            _outputLink[node] = _outputs.ContainsKey(fallback) ? fallback : _outputLink[fallback];
        }
    }

    private int Step(int state, char folded)
    {
        while (true)
        {
            if (_edges.TryGetValue(EdgeKey(state, folded), out var next))
            {
                return next;
            }

            if (state == 0)
            {
                return 0;
            }

            state = _failure[state];
        }
    }

    private void ReportMatches(int state, bool[] reported, HashSet<string> found)
    {
        // Every needle at a node is reported once per scan, so later visits stop early.
        var node = _outputs.ContainsKey(state) ? state : _outputLink[state];
        while (node != 0 && !reported[node])
        {
            reported[node] = true;
            found.UnionWith(_outputs[node]);
            node = _outputLink[node];
        }
    }

    private static long EdgeKey(int state, char folded) => ((long)state << 16) | folded;

    private static bool ContainsSurrogate(string value)
    {
        foreach (var ch in value)
        {
            if (char.IsSurrogate(ch))
            {
                return true;
            }
        }

        return false;
    }
}
