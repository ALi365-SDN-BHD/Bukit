using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Rendering;
using Bukit.Theme;

namespace Bukit.Engine;

internal sealed record VariantRendererThemePlan(
    ITemplateRenderer Renderer,
    string? ThemeRootForTokens,
    string? ParentThemeRootForTokens);

internal static class VariantRendererThemePlanner
{
    internal static VariantRendererThemePlan Create(
        BuildVariantContext context,
        ThemeBootstrapResult bootstrap,
        RoutePipelineResult routeResult,
        Func<string, ITemplateRenderer>? rendererFactory)
    {
        var allPagesForSections = bootstrap.Registry is not null
            ? routeResult.RoutedDocuments
                .Select(item => (item.Document, (RouteInfo?)item.Route))
                .ToList()
            : (IReadOnlyList<(ContentDocument, RouteInfo?)>?)null;
        var renderer = rendererFactory is not null
            ? rendererFactory(context.LayoutsDir)
            : CreateRenderer(
                context,
                bootstrap.Registry,
                bootstrap.SchemaValidator,
                bootstrap.SectionPlugins,
                allPagesForSections);
        var (themeRoot, parentThemeRoot) = GetThemeRootForTokens(
            bootstrap.ThemeRoot,
            bootstrap.Registry is not null,
            bootstrap.ParentThemeRoot,
            !string.IsNullOrWhiteSpace(bootstrap.Manifest?.Extends));
        return new VariantRendererThemePlan(renderer, themeRoot, parentThemeRoot);
    }

    internal static ITemplateRenderer CreateRenderer(
        BuildVariantContext context,
        ThemeComponentRegistry? themeRegistry,
        SectionSchemaValidator? schemaValidator,
        IReadOnlyDictionary<string, ISectionPlugin>? resolvedSectionPlugins,
        IReadOnlyList<(ContentDocument, RouteInfo?)>? allPagesForSections)
    {
        var config = context.Config;
        return themeRegistry is not null
            ? new ScribanTemplateRendererAdapter(
                context.LayoutsDir,
                context.ParentLayoutsDir,
                config.Theme.Shortcodes,
                config.Theme.Components,
                context.UserLayoutsDir,
                themeRegistry,
                schemaValidator,
                null,
                config.Theme.ComponentValidation,
                allPagesForSections,
                resolvedSectionPlugins)
            : new ScribanTemplateRendererAdapter(
                context.LayoutsDir,
                context.ParentLayoutsDir,
                config.Theme.Shortcodes,
                config.Theme.Components,
                context.UserLayoutsDir);
    }

    internal static (string? ThemeRoot, string? ParentThemeRoot) GetThemeRootForTokens(
        string? themeRoot,
        bool hasRegistry,
        string? parentThemeRoot,
        bool hasExtends)
    {
        if (!hasRegistry)
        {
            return (null, null);
        }

        return (themeRoot, hasExtends ? parentThemeRoot : null);
    }
}
