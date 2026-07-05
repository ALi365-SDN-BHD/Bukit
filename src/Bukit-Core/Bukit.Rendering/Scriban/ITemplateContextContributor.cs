using Scriban.Runtime;
using Scriban;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Contributes custom globals, functions, or objects to a Scriban <see cref="TemplateContext"/>
/// during template rendering. Implementations are invoked in order before the main
/// model globals are pushed, allowing Core internals to register additional
/// template helpers without modifying <see cref="TemplateContextBuilder"/>.
/// </summary>
internal interface ITemplateContextContributor
{
    /// <summary>
    /// Contribute to the given <paramref name="context"/> before rendering begins.
    /// The <paramref name="modelGlobals"/> contain the page/list model that will be
    /// pushed after all contributors have run.
    /// </summary>
    void Contribute(TemplateContext context, ScriptObject modelGlobals);
}
