using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Template context used for page, component and section rendering.
/// Scriban 7.2.5 assigns <c>array[index] = value</c> through the non-generic <see cref="System.Collections.IList"/>
/// indexer, which ignores <see cref="ScriptArray{T}.IsReadOnly"/>. This context makes such
/// assignments fail for read-only arrays, so shared template values cannot be changed by one page.
/// <para>
/// It also replaces Scriban's <c>object.to_json</c>, which needs reflection-based serialization that
/// Native AOT disables, with the AOT-safe <see cref="ToJsonFunction"/> also exposed as <c>util.to_json</c>.
/// Each context owns its copy of the built-in <c>object</c> functions, so the override never leaks.
/// </para>
/// </summary>
internal sealed class BukitTemplateContext : TemplateContext
{
    public BukitTemplateContext()
    {
        if (BuiltinObject.TryGetValue("object", out var objectFunctions) && objectFunctions is ScriptObject functions)
        {
            functions.SetValue("to_json", ToJsonFunction.Instance, readOnly: true);
        }
    }

    protected override IListAccessor? GetListAccessorImpl(object target, Type type)
    {
        var accessor = base.GetListAccessorImpl(target, type);
        return accessor is not null && target is ScriptArray<object>
            ? new ReadOnlyAwareListAccessor(accessor)
            : accessor;
    }

    private sealed class ReadOnlyAwareListAccessor(IListAccessor inner) : IListAccessor
    {
        public int GetLength(TemplateContext context, SourceSpan span, object target) =>
            inner.GetLength(context, span, target);

        public object? GetValue(TemplateContext context, SourceSpan span, object target, int index) =>
            inner.GetValue(context, span, target, index);

        public void SetValue(TemplateContext context, SourceSpan span, object target, int index, object? value)
        {
            if (target is ScriptArray<object> { IsReadOnly: true })
            {
                throw new ScriptRuntimeException(span, "The array is readonly");
            }

            inner.SetValue(context, span, target, index, value);
        }
    }
}
