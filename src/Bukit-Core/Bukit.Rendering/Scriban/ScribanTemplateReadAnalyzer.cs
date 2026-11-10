using System.Runtime.CompilerServices;
using Scriban;
using Scriban.Syntax;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Finds the shared data collections a template can read (see <see cref="RenderReadRecorder"/>).
/// The analysis is conservative: a use it cannot pin to one key widens to the whole scope, and a
/// whole <c>site</c>, <c>this</c> or a parse error counts as reading everything.
/// </summary>
internal static class ScribanTemplateReadAnalyzer
{
    private static readonly ConditionalWeakTable<Template, TemplateReadSet> s_cache = new();

    internal static TemplateReadSet Analyze(Template template) =>
        s_cache.GetValue(template, static parsed => Compute(parsed));

    internal static TemplateReadSet Analyze(string text, string? path = null) =>
        Compute(Template.Parse(text, path));

    /// <summary>Records <paramref name="template"/> on the active recorder, if any.</summary>
    internal static void Record(Template template) =>
        RenderReadRecorder.Current?.AddTemplate(Analyze(template));

    private static TemplateReadSet Compute(Template template)
    {
        if (template.HasErrors || template.Page is null)
        {
            return TemplateReadSet.Everything;
        }

        var walker = new Walker();
        try
        {
            walker.Visit(template.Page);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return TemplateReadSet.Everything;
        }

        return walker.All
            ? TemplateReadSet.Everything
            : new TemplateReadSet(false, walker.Keys, walker.Roots, walker.Declarations, walker.Pages);
    }

    private sealed class Walker
    {
        internal bool All { get; private set; }
        internal bool Pages { get; private set; }
        internal HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> Roots { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal HashSet<string> Declarations { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal void Visit(ScriptNode? node)
        {
            switch (node)
            {
                case null:
                    return;
                case ScriptThisExpression:
                    All = true;
                    return;
                case ScriptNamedArgument named:
                    // `limit: 3`, `offset: n`: the name is syntax, only the value is read.
                    Visit(named.Value);
                    return;
                case ScriptObjectMember objectMember:
                    // `{ key: value }`: the key is a name, not a global.
                    Visit(objectMember.Value);
                    return;
                case ScriptMemberExpression or ScriptIndexerExpression or ScriptVariable:
                    VisitChain((ScriptExpression)node);
                    return;
                case ScriptAssignExpression assign:
                    Declare(assign.Target);
                    break;
                case ScriptForStatement loop:
                    Declare(loop.Variable);
                    break;
                case ScriptCaptureStatement capture:
                    Declare(capture.Target);
                    break;
                case ScriptFunction function:
                    Declare(function.NameOrDoToken as ScriptExpression);
                    if (function.Parameters is not null)
                    {
                        foreach (var parameter in function.Parameters)
                        {
                            Declare(parameter.Name);
                        }
                    }

                    break;
            }

            foreach (var child in node.Children)
            {
                Visit(child);
            }
        }

        private void Declare(ScriptExpression? target)
        {
            if (target is ScriptVariable variable && !string.IsNullOrWhiteSpace(variable.Name))
            {
                Declarations.Add(variable.Name);
            }
        }

        // a.b["c"][d].e: segments a→[b, "c", null, e]; dynamic indexes are visited on their own.
        private void VisitChain(ScriptExpression expression)
        {
            var segments = new List<string?>();
            var dynamicIndexes = new List<ScriptExpression>();
            var current = expression;
            while (true)
            {
                if (current is ScriptMemberExpression member)
                {
                    segments.Add(member.Member?.Name);
                    current = member.Target;
                    continue;
                }

                if (current is ScriptIndexerExpression indexer)
                {
                    if (indexer.Index is ScriptLiteral { Value: string literal })
                    {
                        segments.Add(literal);
                    }
                    else
                    {
                        segments.Add(null);
                        if (indexer.Index is not null)
                        {
                            dynamicIndexes.Add(indexer.Index);
                        }
                    }

                    current = indexer.Target;
                    continue;
                }

                break;
            }

            segments.Reverse();
            if (current is ScriptVariableGlobal variable)
            {
                if (string.Equals(variable.Name, "site", StringComparison.OrdinalIgnoreCase))
                {
                    AddSiteRead(segments);
                }
                else if (string.Equals(variable.Name, RenderReadRecorder.Pages, StringComparison.OrdinalIgnoreCase))
                {
                    Pages = true;
                }
                else if (string.Equals(variable.Name, "object", StringComparison.OrdinalIgnoreCase) &&
                         (segments.Count == 0 || segments[0]?.StartsWith("eval", StringComparison.OrdinalIgnoreCase) != false))
                {
                    // object.eval / object.eval_template run template text built at render time;
                    // a bare or aliased `object` could reach them under another name.
                    All = true;
                }
                else if (!string.IsNullOrWhiteSpace(variable.Name))
                {
                    Roots.Add(variable.Name);
                }
            }
            else if (current is not ScriptVariable)
            {
                // Function call results, parenthesised expressions, `this`: inspect them as usual.
                Visit(current);
            }

            foreach (var index in dynamicIndexes)
            {
                Visit(index);
            }
        }

        private void AddSiteRead(List<string?> segments)
        {
            var scope = segments.Count > 0 ? segments[0]?.ToLowerInvariant() : null;
            switch (scope)
            {
                case null:
                    All = true;
                    return;
                case "data" or "modules" or "data_index":
                    var key = segments.Count > 1 ? segments[1] : null;
                    Keys.Add(key is null
                        ? $"site.{scope}.*"
                        : $"site.{scope}.{key.ToLowerInvariant()}");
                    return;
                case "related_pages" or "data_files":
                    Keys.Add($"site.{scope}");
                    return;
                default:
                    // Other site values come from configuration, which the base hash covers.
                    return;
            }
        }
    }
}
