using Scriban;
using Scriban.Runtime;
using Scriban.Syntax;
using System.Net;
using System.Text;

namespace Bukit.Rendering.Scriban;

internal static class ImageHelper
{
    internal static string BuildSrcset(string imagePath, string sizes = "480,768,1200")
    {
        if (!IsSafeImageSource(imagePath))
        {
            return string.Empty;
        }

        var encodedImagePath = WebUtility.HtmlEncode(imagePath.Trim());
        var sb = new StringBuilder();
        var sizeList = sizes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var size in sizeList)
        {
            if (!int.TryParse(size, out var width) || width <= 0)
            {
                continue;
            }

            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"{encodedImagePath}?w={width} {width}w");
        }
        return sb.ToString();
    }

    internal static string BuildImgTag(string src, string alt = "", string sizes = "480,768,1200", string className = "")
    {
        if (!IsSafeImageSource(src))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var safeSrc = WebUtility.HtmlEncode(src.Trim());
        var safeAlt = WebUtility.HtmlEncode(alt ?? string.Empty);
        var safeClass = WebUtility.HtmlEncode(className ?? string.Empty);
        var sizesAttr = $"(max-width: 480px) 480px, (max-width: 768px) 768px, 1200px";

        sb.Append($"<img src=\"{safeSrc}\"");
        sb.Append($" srcset=\"{BuildSrcset(src, sizes)}\"");
        sb.Append($" sizes=\"{sizesAttr}\"");
        if (!string.IsNullOrWhiteSpace(alt))
        {
            sb.Append($" alt=\"{safeAlt}\"");
        }
        if (!string.IsNullOrWhiteSpace(className))
        {
            sb.Append($" class=\"{safeClass}\"");
        }
        sb.Append(" loading=\"lazy\" decoding=\"async\" />");
        return sb.ToString();
    }

    private static bool IsSafeImageSource(string? src)
    {
        if (string.IsNullOrWhiteSpace(src))
        {
            return false;
        }

        var value = src.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

internal sealed class ImageSrcsetFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var src = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var sizes = arguments.Count > 1 ? arguments[1]?.ToString() ?? "480,768,1200" : "480,768,1200";
        return ImageHelper.BuildSrcset(src, sizes);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        => new(Invoke(context, callerContext, arguments, blockStatement));

    public int RequiredParameterCount => 1;
    public int ParameterCount => 2;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index) => new(typeof(string), index == 0 ? "src" : "sizes");
}

internal sealed class ImageImgFunction : IScriptCustomFunction
{
    public object? Invoke(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
    {
        var src = arguments.Count > 0 ? arguments[0]?.ToString() ?? string.Empty : string.Empty;
        var alt = arguments.Count > 1 ? arguments[1]?.ToString() ?? string.Empty : string.Empty;
        var sizes = arguments.Count > 2 ? arguments[2]?.ToString() ?? "480,768,1200" : "480,768,1200";
        var className = arguments.Count > 3 ? arguments[3]?.ToString() ?? string.Empty : string.Empty;
        return ImageHelper.BuildImgTag(src, alt, sizes, className);
    }

    public ValueTask<object?> InvokeAsync(TemplateContext context, ScriptNode? callerContext, ScriptArray arguments, ScriptBlockStatement? blockStatement)
        => new(Invoke(context, callerContext, arguments, blockStatement));

    public int RequiredParameterCount => 1;
    public int ParameterCount => 4;
    public ScriptVarParamKind VarParamKind => ScriptVarParamKind.None;
    public Type ReturnType => typeof(string);
    public ScriptParameterInfo GetParameterInfo(int index)
        => new(typeof(string), index switch
        {
            0 => "src",
            1 => "alt",
            2 => "sizes",
            _ => "className"
        });
}
