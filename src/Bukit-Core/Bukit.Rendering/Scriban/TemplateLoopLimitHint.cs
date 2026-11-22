using System.Globalization;
using Scriban.Syntax;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Explains Scriban's loop-limit error, which names neither its cause nor the setting that changes it.
/// </summary>
internal static class TemplateLoopLimitHint
{
    /// <summary>
    /// Returns the exception message, with a pointer to build.templateLoopLimit when the error
    /// is Scriban's loop-limit error.
    /// </summary>
    internal static string Describe(Exception exception, int loopLimit)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return IsLoopLimitError(exception)
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{exception.Message} Scriban counts every iteration inside one outermost loop, including nested loops and loops in functions it calls; filter with array.filter or array.map, use limit: or pagination, or raise build.templateLoopLimit in site.yaml (current: {loopLimit}).")
            : exception.Message;
    }

    /// <summary>
    /// Scriban 7.2.5 reports the limit as "Exceeding number of iteration limit" for loops and
    /// "exceeds LoopLimit" for ranges.
    /// </summary>
    internal static bool IsLoopLimitError(Exception exception)
        => exception is ScriptRuntimeException
            && (exception.Message.Contains("iteration limit", StringComparison.Ordinal)
                || exception.Message.Contains("exceeds LoopLimit", StringComparison.Ordinal));
}
