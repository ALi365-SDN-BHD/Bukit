using System.Net;
using System.Text.Json;
using static Bukit.Notion.Rendering.BlockRenderers.NotionBlockHelpers;

namespace Bukit.Notion.Rendering.BlockRenderers;

/// <summary>Renders Notion equation blocks as MathJax/KaTeX display math.</summary>
public sealed class EquationBlockRenderer : INotionBlockRenderer
{
    public Task<string?> RenderAsync(JsonElement block, NotionRenderContext context, CancellationToken cancellationToken)
    {
        if (!block.TryGetProperty("equation", out var equation) || equation.ValueKind != JsonValueKind.Object)
        {
            return Task.FromResult<string?>(null);
        }

        var expression = GetString(equation, "expression");
        if (string.IsNullOrWhiteSpace(expression))
        {
            return Task.FromResult<string?>(null);
        }

        var color = GetBlockColor(equation);
        var cssClasses = color is not null ? $"math-block notion-{WebUtility.HtmlEncode(color)}" : "math-block";

        return Task.FromResult<string?>($"<div class=\"{cssClasses}\">\\[{WebUtility.HtmlEncode(expression)}\\]</div>");
    }
}
