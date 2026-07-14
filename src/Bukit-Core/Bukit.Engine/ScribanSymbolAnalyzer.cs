using Scriban;
using Scriban.Syntax;

namespace Bukit.Engine;

internal enum ScribanSymbolReferenceKind
{
    External,
    Local,
    PageItem,
    CurrentContext
}

internal sealed record ScribanSymbolReference(
    string Path,
    ScribanSymbolReferenceKind Kind);

internal sealed record ScribanSymbolAnalysis(
    IReadOnlyList<ScribanSymbolReference> References,
    IReadOnlySet<string> Declarations);

internal static class ScribanSymbolAnalyzer
{
    internal static ScribanSymbolAnalysis Analyze(Template template)
    {
        if (template.HasErrors || template.Page?.Body is null)
        {
            return new ScribanSymbolAnalysis([], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var analyzer = new Analyzer();
        analyzer.VisitNode(template.Page.Body, new SymbolScope(parent: null));
        return new ScribanSymbolAnalysis(analyzer.References, analyzer.Declarations);
    }

    private sealed class Analyzer
    {
        private readonly List<ScribanSymbolReference> _references = [];
        private readonly HashSet<string> _referenceKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _declarations = new(StringComparer.OrdinalIgnoreCase);

        internal IReadOnlyList<ScribanSymbolReference> References => _references;
        internal IReadOnlySet<string> Declarations => _declarations;

        internal void VisitNode(ScriptNode? node, SymbolScope scope)
        {
            switch (node)
            {
                case null:
                    return;
                case ScriptExpression expression:
                    VisitExpression(expression, scope);
                    return;
                case ScriptExpressionStatement expressionStatement:
                    VisitExpression(expressionStatement.Expression, scope);
                    return;
                case ScriptForStatement forStatement:
                    VisitFor(forStatement, scope);
                    return;
                case ScriptFunction function:
                    VisitFunction(function, scope);
                    return;
                case ScriptCaptureStatement capture:
                    VisitNode(capture.Body, scope);
                    DeclareTarget(capture.Target, ScribanSymbolReferenceKind.Local, scope);
                    return;
                default:
                    foreach (var child in node.Children)
                    {
                        VisitNode(child, scope);
                    }
                    return;
            }
        }

        private void VisitFor(ScriptForStatement statement, SymbolScope scope)
        {
            VisitExpression(statement.Iterator, scope);
            foreach (var argument in statement.NamedArguments)
            {
                VisitNode(argument, scope);
            }

            var bodyScope = new SymbolScope(scope);
            var kind = IsPageItemIterator(statement.Iterator)
                ? ScribanSymbolReferenceKind.PageItem
                : ScribanSymbolReferenceKind.Local;
            DeclareTarget(statement.Variable, kind, bodyScope);
            Declare("for", ScribanSymbolReferenceKind.Local, bodyScope);
            VisitNode(statement.Body, bodyScope);
            VisitNode(statement.Else, scope);
        }

        private void VisitFunction(ScriptFunction function, SymbolScope scope)
        {
            if (function.NameOrDoToken is ScriptVariable functionName)
            {
                Declare(functionName.Name, ScribanSymbolReferenceKind.Local, scope);
            }

            var functionScope = new SymbolScope(scope);
            if (function.Parameters is not null)
            {
                foreach (var parameter in function.Parameters)
                {
                    if (parameter.Name is not null)
                    {
                        Declare(parameter.Name.Name, ScribanSymbolReferenceKind.Local, functionScope);
                    }

                    VisitExpression(parameter.DefaultValue, scope);
                }
            }

            VisitNode(function.Body, functionScope);
        }

        private void VisitExpression(ScriptExpression? expression, SymbolScope scope)
        {
            switch (expression)
            {
                case null:
                    return;
                case ScriptAssignExpression assignment:
                    VisitExpression(assignment.Value, scope);
                    if (!DeclareTarget(assignment.Target, ScribanSymbolReferenceKind.Local, scope))
                    {
                        VisitExpression(assignment.Target, scope);
                    }
                    return;
                case ScriptMemberExpression member:
                    var memberPath = ExtractFullPath(member);
                    if (!string.IsNullOrWhiteSpace(memberPath))
                    {
                        AddReference(memberPath, ResolveKind(memberPath, scope));
                    }
                    VisitIndexerArguments(member.Target, scope);
                    return;
                case ScriptIndexerExpression indexer:
                    var indexedPath = ExtractFullPath(indexer.Target);
                    if (!string.IsNullOrWhiteSpace(indexedPath))
                    {
                        AddReference(indexedPath, ResolveKind(indexedPath, scope));
                    }
                    VisitExpression(indexer.Index, scope);
                    VisitIndexerArguments(indexer.Target, scope);
                    return;
                case ScriptVariable variable:
                    AddReference(variable.Name, ResolveKind(variable.Name, scope));
                    return;
                case ScriptThisExpression:
                    AddReference("this", ScribanSymbolReferenceKind.CurrentContext);
                    return;
                case ScriptAnonymousFunction anonymousFunction:
                    VisitFunction(anonymousFunction.Function!, scope);
                    return;
                default:
                    foreach (var child in expression.Children)
                    {
                        VisitNode(child, scope);
                    }
                    return;
            }
        }

        private void VisitIndexerArguments(ScriptExpression? expression, SymbolScope scope)
        {
            switch (expression)
            {
                case ScriptIndexerExpression indexer:
                    VisitExpression(indexer.Index, scope);
                    VisitIndexerArguments(indexer.Target, scope);
                    break;
                case ScriptMemberExpression member:
                    VisitIndexerArguments(member.Target, scope);
                    break;
            }
        }

        private bool DeclareTarget(
            ScriptExpression? target,
            ScribanSymbolReferenceKind kind,
            SymbolScope scope)
        {
            if (target is ScriptVariable variable)
            {
                Declare(variable.Name, kind, scope);
                return true;
            }

            return false;
        }

        private void Declare(string? name, ScribanSymbolReferenceKind kind, SymbolScope scope)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            scope.Declare(name, kind);
            _declarations.Add(name);
        }

        private void AddReference(string path, ScribanSymbolReferenceKind kind)
        {
            var key = $"{kind}:{path}";
            if (_referenceKeys.Add(key))
            {
                _references.Add(new ScribanSymbolReference(path, kind));
            }
        }

        private static ScribanSymbolReferenceKind ResolveKind(string path, SymbolScope scope)
        {
            var root = GetRoot(path);
            if (root.Equals("this", StringComparison.OrdinalIgnoreCase))
            {
                return ScribanSymbolReferenceKind.CurrentContext;
            }

            return scope.TryResolve(root, out var kind)
                ? kind
                : ScribanSymbolReferenceKind.External;
        }

        private static bool IsPageItemIterator(ScriptExpression? iterator)
        {
            var path = ExtractFullPath(iterator);
            return path is not null &&
                   (path.Equals("pages", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("items", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetRoot(string path)
        {
            var dot = path.IndexOf('.');
            return dot < 0 ? path : path[..dot];
        }

        private static string? ExtractFullPath(ScriptExpression? expression)
        {
            return expression switch
            {
                ScriptVariable variable => variable.Name,
                ScriptThisExpression => "this",
                ScriptMemberExpression member => AppendMember(ExtractFullPath(member.Target), member.Member?.Name),
                ScriptIndexerExpression indexer => ExtractFullPath(indexer.Target),
                _ => null
            };
        }

        private static string? AppendMember(string? target, string? member)
        {
            if (string.IsNullOrWhiteSpace(target)) return null;
            return string.IsNullOrWhiteSpace(member) ? target : $"{target}.{member}";
        }
    }

    private sealed class SymbolScope(SymbolScope? parent)
    {
        private readonly Dictionary<string, ScribanSymbolReferenceKind> _symbols =
            new(StringComparer.OrdinalIgnoreCase);

        internal void Declare(string name, ScribanSymbolReferenceKind kind)
            => _symbols[name] = kind;

        internal bool TryResolve(string name, out ScribanSymbolReferenceKind kind)
        {
            if (_symbols.TryGetValue(name, out kind))
            {
                return true;
            }

            if (parent is not null)
            {
                return parent.TryResolve(name, out kind);
            }

            kind = default;
            return false;
        }
    }
}
