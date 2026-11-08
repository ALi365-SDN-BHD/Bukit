using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

/// <summary>
/// Taxonomy entry point for term value cleanup; the rules live in
/// <see cref="TermValueNormalizer"/> so templates can share them via <c>util.term_slug</c>.
/// </summary>
internal static class TaxonomyTermValueNormalizer
{
    internal static string Normalize(string? raw, out bool decoded) => TermValueNormalizer.Normalize(raw, out decoded);
}
