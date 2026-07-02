using Scriban;
using Scriban.Syntax;

namespace Bukit.Engine;

internal static class ScribanVariableCollector
{
    internal static HashSet<string> Collect(Template template)
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (template.HasErrors || template.Page?.Body is null)
        {
            return variables;
        }

        WalkNode(template.Page.Body, variables);
        return variables;
    }

    private static void WalkNode(ScriptNode node, HashSet<string> variables)
    {
        if (node is ScriptExpressionStatement exprStmt)
        {
            WalkExpressionRecursive(exprStmt.Expression, variables);
        }
        else if (node is ScriptIfStatement ifStmt)
        {
            if (ifStmt.Condition is not null) WalkExpressionRecursive(ifStmt.Condition, variables);
            if (ifStmt.Then is not null) WalkNode(ifStmt.Then, variables);
            if (ifStmt.Else is not null) WalkNode(ifStmt.Else, variables);
        }
        else if (node is ScriptForStatement forStmt)
        {
            if (forStmt.Iterator is not null) WalkExpressionRecursive(forStmt.Iterator, variables);
            if (forStmt.Body is not null) WalkNode(forStmt.Body, variables);
            if (forStmt.Else is not null) WalkNode(forStmt.Else, variables);
        }
        else if (node is ScriptWhileStatement whileStmt)
        {
            if (whileStmt.Condition is not null) WalkExpressionRecursive(whileStmt.Condition, variables);
        }
        else
        {
            foreach (var child in node.Children)
            {
                if (child is not null) WalkNode(child, variables);
            }
        }
    }

    private static void WalkExpressionRecursive(ScriptExpression? expr, HashSet<string> variables)
    {
        if (expr is null) return;

        if (expr is ScriptVariableGlobal g)
        {
            if (!string.IsNullOrWhiteSpace(g.Name) && !IsLiteralOrBuiltin(g.Name))
            {
                variables.Add(g.Name);
            }
            return;
        }

        if (expr is ScriptMemberExpression member)
        {
            var target = ExtractFullPath(member.Target);
            var name = member.Member?.Name;
            if (!string.IsNullOrWhiteSpace(target) && !string.IsNullOrWhiteSpace(name))
            {
                variables.Add($"{target}.{name}");
            }
            else if (!string.IsNullOrWhiteSpace(target))
            {
                variables.Add(target);
            }
            return;
        }

        if (expr is ScriptIndexerExpression idx)
        {
            WalkExpressionRecursive(idx.Target, variables);
            return;
        }

        if (expr is ScriptUnaryExpression unary)
        {
            WalkExpressionRecursive(unary.Right, variables);
            return;
        }

        if (expr is ScriptBinaryExpression bin)
        {
            WalkExpressionRecursive(bin.Left, variables);
            WalkExpressionRecursive(bin.Right, variables);
            return;
        }

        if (expr is ScriptFunctionCall func)
        {
            WalkExpressionRecursive(func.Target, variables);
            foreach (var arg in func.Arguments)
            {
                WalkExpressionRecursive(arg, variables);
            }
            return;
        }

        foreach (var child in expr.Children)
        {
            if (child is ScriptExpression childExpr)
            {
                WalkExpressionRecursive(childExpr, variables);
            }
        }
    }

    private static string? ExtractFullPath(ScriptExpression? expr)
    {
        if (expr is ScriptVariableGlobal g)
        {
            return g.Name;
        }

        if (expr is ScriptMemberExpression m)
        {
            var parent = ExtractFullPath(m.Target);
            var name = m.Member?.Name;
            return parent is not null && name is not null ? $"{parent}.{name}" : parent;
        }

        if (expr is ScriptIndexerExpression idx)
        {
            return ExtractFullPath(idx.Target);
        }

        return null;
    }

    private static bool IsLiteralOrBuiltin(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        if (char.IsDigit(name[0]))
        {
            return true;
        }

        if (name.StartsWith('\'') || name.StartsWith('"'))
        {
            return true;
        }

        return false;
    }
}
