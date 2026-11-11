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
/// </summary>
internal sealed class BukitTemplateContext : TemplateContext
{
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
