namespace Bukit.Engine.Primitives;

/// <summary>
/// Finds which of many patterns occur in one text in a single pass (Aho-Corasick).
/// The result for each pattern equals
/// <c>text.Contains(pattern, StringComparison.OrdinalIgnoreCase)</c>: both sides are folded
/// one char at a time with <see cref="Fold"/>, which maps each UTF-16 code unit outside
/// surrogate pairs to the form ordinal ignore-case compares. Patterns that contain a
/// surrogate are checked with <see cref="string.Contains(string, StringComparison)"/>
/// instead, so their result is the framework's own.
/// </summary>
internal static class OrdinalIgnoreCaseMultiMatcher
{
    // Ordinal ignore-case uses its own simple uppercase table, which is not exactly
    // char.ToUpperInvariant (for example U+017F stays as is). Each invariant uppercase
    // mapping is kept only when ordinal ignore-case agrees the two chars are equal.
    private static readonly char[] FoldTable = BuildFoldTable();

    internal static char Fold(char c) => FoldTable[c];

    private static char[] BuildFoldTable()
    {
        var table = new char[char.MaxValue + 1];
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var c = (char)i;
            var upper = char.ToUpperInvariant(c);
            table[i] = upper != c && !char.IsSurrogate(c) &&
                       MemoryExtensions.Equals([c], [upper], StringComparison.OrdinalIgnoreCase)
                ? upper
                : c;
        }

        return table;
    }

    /// <summary>Returns the patterns that occur in <paramref name="text"/>, compared ordinally.</summary>
    internal static HashSet<string> FindPresent(string text, IEnumerable<string> patterns)
        => Compile(patterns).FindPresent(text);

    /// <summary>
    /// Builds the automaton once so the same patterns can be searched in many texts,
    /// each in a single pass.
    /// </summary>
    internal static CompiledPatterns Compile(IEnumerable<string> patterns)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        return new CompiledPatterns(patterns);
    }

    /// <summary>A fixed pattern set compiled for repeated <see cref="FindPresent(string)"/> calls.</summary>
    internal sealed class CompiledPatterns
    {
        private readonly List<string> _emptyPatterns = [];
        private readonly List<string> _surrogatePatterns = [];
        private readonly List<string> _automatonPatterns = [];
        private readonly Automaton? _automaton;

        internal CompiledPatterns(IEnumerable<string> patterns)
        {
            foreach (var pattern in patterns.Distinct(StringComparer.Ordinal))
            {
                ArgumentNullException.ThrowIfNull(pattern, nameof(patterns));
                if (pattern.Length == 0)
                {
                    // Contains("") is true for any text.
                    _emptyPatterns.Add(pattern);
                }
                else if (HasSurrogate(pattern))
                {
                    _surrogatePatterns.Add(pattern);
                }
                else
                {
                    _automatonPatterns.Add(pattern);
                }
            }

            _automaton = _automatonPatterns.Count == 0 ? null : new Automaton(_automatonPatterns);
        }

        /// <summary>Returns the patterns that occur in <paramref name="text"/>, compared ordinally.</summary>
        internal HashSet<string> FindPresent(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            var present = new HashSet<string>(_emptyPatterns, StringComparer.Ordinal);
            foreach (var pattern in _surrogatePatterns)
            {
                if (text.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    present.Add(pattern);
                }
            }

            if (_automaton is null || text.Length == 0)
            {
                return present;
            }

            foreach (var index in _automaton.Scan(text))
            {
                present.Add(_automatonPatterns[index]);
            }

            return present;
        }
    }

    private static bool HasSurrogate(string value)
    {
        foreach (var c in value)
        {
            if (char.IsSurrogate(c))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class Automaton
    {
        // Transition key = (state << 16) | folded char.
        private readonly Dictionary<long, int> _next = [];
        private readonly List<int> _fail = [0];
        private readonly List<int> _outputLink = [-1];
        private readonly List<List<int>?> _matches = [null];

        internal Automaton(IReadOnlyList<string> patterns)
        {
            for (var i = 0; i < patterns.Count; i++)
            {
                var state = 0;
                foreach (var c in patterns[i])
                {
                    var key = Key(state, Fold(c));
                    if (!_next.TryGetValue(key, out var child))
                    {
                        child = _fail.Count;
                        _fail.Add(0);
                        _outputLink.Add(-1);
                        _matches.Add(null);
                        _next.Add(key, child);
                    }

                    state = child;
                }

                (_matches[state] ??= []).Add(i);
            }

            BuildFailureLinks();
        }

        internal IEnumerable<int> Scan(string text)
        {
            var reported = new bool[_fail.Count];
            var state = 0;
            foreach (var raw in text)
            {
                var c = Fold(raw);
                while (true)
                {
                    if (_next.TryGetValue(Key(state, c), out var next))
                    {
                        state = next;
                        break;
                    }

                    if (state == 0)
                    {
                        break;
                    }

                    state = _fail[state];
                }

                // Walk this state's match chain once; later visits stop at the first reported state.
                for (var s = _matches[state] is null ? _outputLink[state] : state; s > 0 && !reported[s]; s = _outputLink[s])
                {
                    reported[s] = true;
                    foreach (var index in _matches[s]!)
                    {
                        yield return index;
                    }
                }
            }
        }

        private void BuildFailureLinks()
        {
            var children = new List<(int Child, char C)>[_fail.Count];
            foreach (var (key, child) in _next)
            {
                var parent = (int)(key >> 16);
                (children[parent] ??= []).Add((child, (char)(key & 0xFFFF)));
            }

            var queue = new Queue<int>();
            foreach (var (child, _) in children[0] ?? [])
            {
                queue.Enqueue(child);
            }

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                foreach (var (child, c) in children[state] ?? [])
                {
                    var fallback = _fail[state];
                    int target;
                    while (!_next.TryGetValue(Key(fallback, c), out target) && fallback != 0)
                    {
                        fallback = _fail[fallback];
                    }

                    if (target == child)
                    {
                        target = 0;
                    }

                    _fail[child] = target;
                    _outputLink[child] = _matches[target] is not null ? target : _outputLink[target];
                    queue.Enqueue(child);
                }
            }
        }

        private static long Key(int state, char c) => ((long)state << 16) | c;
    }
}
