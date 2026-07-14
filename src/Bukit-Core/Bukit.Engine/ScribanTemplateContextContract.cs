using Scriban;

namespace Bukit.Engine;

internal enum ScribanPathStatus
{
    Valid,
    Invalid,
    Indeterminate
}

internal sealed record ScribanPathValidation(
    ScribanPathStatus Status,
    string? Root = null,
    string? FieldPath = null,
    bool IsCurrentContext = false,
    bool IsPageItem = false);

internal static class ScribanTemplateContextContract
{
    private static readonly Scriban.Runtime.ScriptObject Builtins = TemplateContext.GetDefaultBuiltinObject();

    private static readonly HashSet<string> RuntimeHelpers = new(StringComparer.OrdinalIgnoreCase)
    {
        "image", "util", "comp", "render_section", "shortcode"
    };

    private static readonly HashSet<string> OpenRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "pages", "items", "pagination", "collection", "taxonomy", "filter",
        "section", "content", "list"
    };

    internal static ScribanPathValidation Validate(ScribanSymbolReference reference)
    {
        if (reference.Kind == ScribanSymbolReferenceKind.Local)
        {
            return new ScribanPathValidation(ScribanPathStatus.Valid);
        }

        if (reference.Kind == ScribanSymbolReferenceKind.PageItem)
        {
            var fieldPath = GetFieldPath(reference.Path);
            return fieldPath is null || ScribanModelKnownFields.IsKnownField("item", fieldPath)
                ? new ScribanPathValidation(ScribanPathStatus.Valid)
                : new ScribanPathValidation(
                    ScribanPathStatus.Invalid,
                    GetRoot(reference.Path),
                    fieldPath,
                    IsPageItem: true);
        }

        if (reference.Kind == ScribanSymbolReferenceKind.CurrentContext)
        {
            return ValidateCurrentContext(reference.Path);
        }

        return ValidateExternal(reference.Path);
    }

    private static ScribanPathValidation ValidateCurrentContext(string path)
    {
        if (path.Equals("this", StringComparison.OrdinalIgnoreCase))
        {
            return new ScribanPathValidation(ScribanPathStatus.Valid);
        }

        const string prefix = "this.";
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ScribanPathValidation(ScribanPathStatus.Indeterminate);
        }

        var contextPath = path[prefix.Length..];
        var root = GetRoot(contextPath);
        if (!IsKnownTopLevelRoot(root))
        {
            return contextPath.Equals("title", StringComparison.OrdinalIgnoreCase)
                ? new ScribanPathValidation(
                    ScribanPathStatus.Invalid,
                    "this",
                    contextPath,
                    IsCurrentContext: true)
                : new ScribanPathValidation(
                    ScribanPathStatus.Indeterminate,
                    "this",
                    contextPath,
                    IsCurrentContext: true);
        }

        var validation = ValidateExternal(contextPath);
        return validation with { IsCurrentContext = true };
    }

    private static ScribanPathValidation ValidateExternal(string path)
    {
        var root = GetRoot(path);
        var fieldPath = GetFieldPath(path);

        if (Builtins.Contains(root) || RuntimeHelpers.Contains(root) || OpenRoots.Contains(root))
        {
            return new ScribanPathValidation(ScribanPathStatus.Valid);
        }

        if (root.Equals("page", StringComparison.OrdinalIgnoreCase) ||
            root.Equals("site", StringComparison.OrdinalIgnoreCase) ||
            root.Equals("seo", StringComparison.OrdinalIgnoreCase))
        {
            if (fieldPath is null || ScribanModelKnownFields.IsKnownField(root, fieldPath))
            {
                return new ScribanPathValidation(ScribanPathStatus.Valid);
            }

            return new ScribanPathValidation(ScribanPathStatus.Invalid, root, fieldPath);
        }

        return new ScribanPathValidation(ScribanPathStatus.Indeterminate, root, fieldPath);
    }

    private static bool IsKnownTopLevelRoot(string root)
    {
        return Builtins.Contains(root) || RuntimeHelpers.Contains(root) || OpenRoots.Contains(root) ||
               root.Equals("page", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("site", StringComparison.OrdinalIgnoreCase) ||
               root.Equals("seo", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRoot(string path)
    {
        var dot = path.IndexOf('.');
        return dot < 0 ? path : path[..dot];
    }

    private static string? GetFieldPath(string path)
    {
        var dot = path.IndexOf('.');
        return dot < 0 || dot + 1 >= path.Length ? null : path[(dot + 1)..];
    }
}
