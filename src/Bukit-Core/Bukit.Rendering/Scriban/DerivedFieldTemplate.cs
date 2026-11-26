using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Bukit.Engine.Abstractions.Content;
using Scriban;
using Scriban.Functions;
using Scriban.Runtime;
using Scriban.Syntax;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// A content.derivedFields value: a Scriban template evaluated over one page's top-level fields
/// (field names are variables) with the invariant culture, a loop limit, no clock or random
/// functions, and <c>seo.describe</c>. The result is trimmed, so a page renders the same bytes in
/// every build and on every machine.
/// </summary>
internal sealed partial class DerivedFieldTemplate
{
    internal const int LoopLimit = 1000;

    /// <summary>Output larger than this is an error, not a field.</summary>
    private const int MaxOutputLength = 64 * 1024;

    private readonly Template _template;

    private DerivedFieldTemplate(Template template) => _template = template;

    /// <returns>The template, or null with <paramref name="error"/> set when it does not parse.</returns>
    internal static DerivedFieldTemplate? Parse(string text, out string? error)
    {
        var template = Template.Parse(text);
        if (template.HasErrors)
        {
            error = string.Join("; ", template.Messages.Select(message => message.Message));
            return null;
        }

        error = null;
        return new DerivedFieldTemplate(template);
    }

    /// <summary>Renders the value for one page; throws <see cref="ScriptRuntimeException"/> on a runtime error.</summary>
    internal string Render(IReadOnlyDictionary<string, ContentField>? fields)
    {
        var globals = new ScriptObject();
        foreach (var (name, field) in fields ?? new Dictionary<string, ContentField>())
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                globals.SetValue(name, ScribanDynamicValueMapper.ToScribanValue(field.Value), readOnly: true);
            }
        }

        var context = new TemplateContext(CreateBuiltins())
        {
            EnableRelaxedMemberAccess = true,
            EnableRelaxedTargetAccess = true,
            EnableNullIndexer = true,
            LoopLimit = LoopLimit,
            LimitToString = MaxOutputLength,
            StrictVariables = false
        };
        context.PushCulture(CultureInfo.InvariantCulture);
        context.PushGlobal(globals);
        var output = _template.Render(context);
        return output.Length > MaxOutputLength
            ? throw new ScriptRuntimeException(default, $"The value is longer than {MaxOutputLength} characters.")
            : output.Trim();
    }

    private static BuiltinFunctions CreateBuiltins()
    {
        var builtins = new BuiltinFunctions();
        // A value must not change between builds of the same content, so the clock and random
        // functions fail loudly instead of reading as an empty variable.
        if (builtins["date"] is ScriptObject date)
        {
            date.SetValue("now", new UnavailableFunction("date.now"), readOnly: true);
        }

        if (builtins["math"] is ScriptObject math)
        {
            math.SetValue("random", new UnavailableFunction("math.random"), readOnly: true);
            math.SetValue("uuid", new UnavailableFunction("math.uuid"), readOnly: true);
        }

        var seo = new ScriptObject();
        seo.SetValue("describe", new DescribeFunction(), readOnly: true);
        builtins.SetValue("seo", seo, readOnly: true);
        return builtins;
    }

    /// <summary>
    /// Plain text of at most <paramref name="width"/> display columns (a CJK character counts 2):
    /// tags removed, whitespace collapsed, cut after the last sentence end in the second half of the
    /// budget, otherwise cut hard with an ellipsis.
    /// </summary>
    internal static string Describe(string? text, int width)
    {
        if (string.IsNullOrWhiteSpace(text) || width <= 0)
        {
            return string.Empty;
        }

        var plain = WhitespaceRegex().Replace(TagRegex().Replace(text, " "), " ").Trim();
        if (DisplayWidth(plain) <= width)
        {
            return plain;
        }

        var budget = width - 1; // room for the ellipsis
        var used = 0;
        var end = 0;
        var sentenceEnd = -1;
        var enumerator = StringInfo.GetTextElementEnumerator(plain);
        while (enumerator.MoveNext())
        {
            var element = enumerator.GetTextElement();
            var elementWidth = DisplayWidth(element);
            if (used + elementWidth > budget)
            {
                break;
            }

            used += elementWidth;
            end = enumerator.ElementIndex + element.Length;
            if (element is "。" or "！" or "？" or "；" or "." or "!" or "?" or ";" && used * 2 >= budget)
            {
                sentenceEnd = end;
            }
        }

        return sentenceEnd > 0 ? plain[..sentenceEnd] : plain[..end].TrimEnd() + "…";
    }

    /// <summary><c>seo.describe text width</c>, registered without reflection so trimming keeps it.</summary>
    private sealed class DescribeFunction : IScriptCustomFunction
    {
        public int RequiredParameterCount => 2;
        public int ParameterCount => 2;
        public ScriptVarParamKind VarParamKind => ScriptVarParamKind.Direct;
        public Type ReturnType => typeof(string);

        public ScriptParameterInfo GetParameterInfo(int index)
            => index == 0 ? new(typeof(string), "text") : new(typeof(int), "width");

        public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        {
            if (arguments.Count != 2)
            {
                throw new ScriptRuntimeException(callerContext?.Span ?? default, "seo.describe expects a text and a width.");
            }

            var text = arguments[0] is null ? null : context.ObjectToString(arguments[0]);
            return Describe(text, context.ToInt(callerContext?.Span ?? default, arguments[1]));
        }

        public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
            => new(Invoke(context, callerContext, arguments, blockStatement));
    }

    /// <summary>A built-in removed from derived fields: any call is a runtime error naming it.</summary>
    private sealed class UnavailableFunction(string name) : IScriptCustomFunction
    {
        public int RequiredParameterCount => 0;
        public int ParameterCount => 0;
        public ScriptVarParamKind VarParamKind => ScriptVarParamKind.Direct;
        public Type ReturnType => typeof(object);

        public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(object), "value");

        public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
            => throw new ScriptRuntimeException(callerContext?.Span ?? default, $"{name} is not available in derived fields, which must build the same value every time.");

        public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
            => new(Invoke(context, callerContext, arguments, blockStatement));
    }

    private static int DisplayWidth(string text)
    {
        var width = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            width += IsWide(rune.Value) ? 2 : 1;
        }

        return width;
    }

    private static bool IsWide(int code)
        => code is (>= 0x1100 and <= 0x115F) or (>= 0x2E80 and <= 0xA4CF) or (>= 0xAC00 and <= 0xD7A3) or
           (>= 0xF900 and <= 0xFAFF) or (>= 0xFE30 and <= 0xFE4F) or (>= 0xFF00 and <= 0xFF60) or
           (>= 0xFFE0 and <= 0xFFE6) or (>= 0x1F300 and <= 0x1FAFF) or (>= 0x20000 and <= 0x3FFFD);

    [GeneratedRegex("<[^>]*>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
