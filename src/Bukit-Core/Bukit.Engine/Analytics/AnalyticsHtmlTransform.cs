using Bukit.Config;

namespace Bukit.Engine.Analytics;

internal sealed class AnalyticsHtmlTransform : IHtmlTransform
{
    private readonly ResolvedAnalyticsConfig _config;
    private readonly AnalyticsProviderRegistry _providers;
    private readonly AnalyticsBuildState _buildState;

    internal AnalyticsHtmlTransform(
        ResolvedAnalyticsConfig config,
        AnalyticsProviderRegistry providers,
        AnalyticsBuildState? buildState = null)
    {
        _config = config;
        _providers = providers;
        _buildState = buildState ?? new AnalyticsBuildState(
            pluginEnabled: true,
            config,
            BuildExecutionMode.Production);
    }

    public string Name => "analytics";

    public string Transform(HtmlTransformContext context, string html)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(html);

        _buildState.RecordProcessed();
        try
        {
            var cleaned = AnalyticsManagedBlockFilter.Remove(html);
            if (!_config.Enabled)
            {
                _buildState.RecordSkipped(AnalyticsSkipReason.AnalyticsDisabled);
                return cleaned;
            }

            if (_config.Providers.Count == 0)
            {
                _buildState.RecordSkipped(AnalyticsSkipReason.NoProviders);
                return cleaned;
            }

            if (_config.ProductionOnly && context.ExecutionMode == BuildExecutionMode.Development)
            {
                _buildState.RecordSkipped(AnalyticsSkipReason.DevelopmentMode);
                return cleaned;
            }

            var renderContext = new AnalyticsRenderContext(
                context.RouteUrl,
                context.OutputPath,
                context.DocumentKind == HtmlDocumentKind.List,
                context.ExecutionMode);
            var fragments = AnalyticsFragmentRenderer.Render(_config, _providers, renderContext);

            var result = InjectHeadFragments(cleaned, fragments, out var headInjected, out var headMissing);
            result = InjectBodyFragments(result, fragments, out var bodyInjected, out var bodyMissing);
            if (headMissing)
            {
                _buildState.RecordSkipped(AnalyticsSkipReason.HeadMissing);
            }

            if (bodyMissing)
            {
                _buildState.RecordSkipped(AnalyticsSkipReason.BodyMissing);
            }

            if (headInjected || bodyInjected)
            {
                _buildState.RecordInjected();
            }

            return result;
        }
        catch
        {
            _buildState.RecordSkipped(AnalyticsSkipReason.TransformFailed);
            throw;
        }
    }

    private static string InjectHeadFragments(
        string html,
        IReadOnlyList<AnalyticsHtmlFragments> fragments,
        out bool injected,
        out bool missing)
    {
        injected = false;
        missing = false;
        var headStartBlocks = fragments
            .Where(fragment => fragment.HeadStart is not null)
            .Select(fragment => CreateManagedBlock(fragment.ProviderKey, "head", fragment.HeadStart!))
            .ToArray();
        var headEndBlocks = fragments
            .Where(fragment => fragment.HeadEnd is not null)
            .Select(fragment => CreateManagedBlock(fragment.ProviderKey, "head", fragment.HeadEnd!))
            .ToArray();
        if (headStartBlocks.Length == 0 && headEndBlocks.Length == 0)
        {
            return html;
        }

        if (!HtmlHeadScanner.TryFindHead(html, out var head))
        {
            missing = true;
            return html;
        }

        injected = true;
        var result = html;
        if (headEndBlocks.Length > 0)
        {
            result = result.Insert(head.ContentEnd, string.Concat(headEndBlocks));
        }

        if (headStartBlocks.Length > 0)
        {
            result = result.Insert(head.ContentStart, string.Concat(headStartBlocks));
        }

        return result;
    }

    private static string InjectBodyFragments(
        string html,
        IReadOnlyList<AnalyticsHtmlFragments> fragments,
        out bool injected,
        out bool missing)
    {
        injected = false;
        missing = false;
        var blocks = fragments
            .Where(fragment => fragment.BodyStart is not null)
            .Select(fragment => CreateManagedBlock(fragment.ProviderKey, "body", fragment.BodyStart!))
            .ToArray();
        if (blocks.Length == 0)
        {
            return html;
        }

        var bodyStart = HtmlHeadScanner.FindStartTag(html, "body", 0, html.Length);
        if (bodyStart < 0)
        {
            missing = true;
            return html;
        }

        var bodyTagEnd = HtmlHeadScanner.FindTagEnd(html, bodyStart);
        if (bodyTagEnd < 0)
        {
            missing = true;
            return html;
        }

        injected = true;
        return html.Insert(bodyTagEnd + 1, string.Concat(blocks));
    }

    private static string CreateManagedBlock(string providerKey, string location, string fragment)
        => $"<!-- bukit:analytics:{providerKey}:{location}:start -->\n{fragment}\n<!-- bukit:analytics:{providerKey}:{location}:end -->";

}
