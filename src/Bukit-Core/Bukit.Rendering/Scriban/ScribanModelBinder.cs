using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanModelBinder
{
    public static ScriptObject ToScriptObject(PageModel model) =>
        ScribanRootModelMapper.ToScriptObject(model);

    public static ScriptObject ToScriptObject(ListPageModel model) =>
        ScribanRootModelMapper.ToScriptObject(model);
}
