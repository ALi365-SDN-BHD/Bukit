using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

public sealed class ThemeTemplateResolver
{
    /// <summary>
    /// The only template key that is hardcoded as a fixed role in the engine.
    /// Every site requires a home page, so the home template is always resolved.
    /// </summary>
    private const string HomeTemplateKey = "home";

    /// <summary>
    /// The fallback template path used when the theme manifest does not declare a home template.
    /// </summary>
    internal const string DefaultHomeTemplate = "pages/index.html";

    private readonly ThemeManifestV2? _manifest;

    internal ThemeTemplateResolver(ThemeManifestV2? manifest)
    {
        _manifest = manifest;
    }

    internal string ResolveHomeTemplate()
    {
        ValidateRequiredTemplates();
        if (_manifest?.Templates is not null &&
            _manifest.Templates.TryGetValue(HomeTemplateKey, out var home) &&
            !string.IsNullOrWhiteSpace(home.Template))
        {
            return NormalizeTemplatePath(home.Template);
        }

        return DefaultHomeTemplate;
    }

    /// <summary>
    /// Ensures the home template's <c>required</c> field is not explicitly set to <c>false</c>.
    /// The home template is always required and cannot be opted out.
    /// </summary>
    internal void ValidateRequiredTemplates()
    {
        if (_manifest?.Templates is not null &&
            _manifest.Templates.TryGetValue(HomeTemplateKey, out var home) &&
            !home.Required)
        {
            throw new ConfigException("templates.home.required cannot be false. The home template is always required.");
        }
    }

    internal string ResolveContentTemplate(ContentDocument document, string? kind = null)
    {
        var matched = TryResolveContentTemplate(document, kind);
        if (!string.IsNullOrWhiteSpace(matched))
        {
            return matched!;
        }

        var type = GetContentType(document);
        var collection = ContentFieldReader.GetCollection(document);
        throw new ConfigException(
            $"No theme template matches content document '{document.Id}' (type='{type}', collection='{collection}', kind='{kind ?? "detail"}'). " +
            "Add a matching theme.yaml templates entry or set route.template/site.collections.*.template.");
    }

    internal string ResolveKindTemplate(string kind)
    {
        if (TryResolveKindTemplate(kind, out var template))
        {
            return template;
        }

        throw new ConfigException(
            $"No theme template matches kind='{kind}'. Add a matching theme.yaml templates entry or configure an explicit template.");
    }

    internal bool TryResolveKindTemplate(string kind, out string template)
    {
        template = string.Empty;
        if (_manifest?.Templates is null || string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        foreach (var (_, def) in _manifest.Templates)
        {
            if (def.Accepts is null || string.IsNullOrWhiteSpace(def.Template))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(def.Accepts.Kind) && Matches(def.Accepts.Kind, kind))
            {
                template = NormalizeTemplatePath(def.Template);
                return true;
            }
        }

        return false;
    }

    internal IReadOnlyDictionary<string, ThemeTemplateDefinition> DeclaredTemplates =>
        _manifest?.Templates ?? new Dictionary<string, ThemeTemplateDefinition>(StringComparer.OrdinalIgnoreCase);

    internal IReadOnlyList<string> GetRequiredTemplatePaths()
    {
        ValidateRequiredTemplates();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ResolveHomeTemplate()
        };

        if (_manifest?.Templates is not null)
        {
            foreach (var (_, def) in _manifest.Templates)
            {
                if (def.Required && !string.IsNullOrWhiteSpace(def.Template))
                {
                    paths.Add(NormalizeTemplatePath(def.Template));
                }
            }
        }

        return paths.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private string? TryResolveContentTemplate(ContentDocument document, string? kind)
    {
        if (_manifest?.Templates is null)
        {
            return null;
        }

        var type = GetContentType(document);
        var collection = ContentFieldReader.GetCollection(document);
        foreach (var (_, def) in _manifest.Templates)
        {
            if (def.Accepts is null || string.IsNullOrWhiteSpace(def.Template))
            {
                continue;
            }

            if (!Matches(def.Accepts.Type, type) ||
                !Matches(def.Accepts.Collection, collection) ||
                !Matches(def.Accepts.Kind, kind))
            {
                continue;
            }

            return NormalizeTemplatePath(def.Template);
        }

        return null;
    }

    private static string GetContentType(ContentDocument document)
        => ContentFieldReader.GetContentType(document);

    private static bool Matches(string? expected, string? actual)
        => string.IsNullOrWhiteSpace(expected) ||
           string.Equals(expected.Trim(), actual?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string NormalizeTemplatePath(string template)
        => template.Trim().Replace('\\', '/');
}
