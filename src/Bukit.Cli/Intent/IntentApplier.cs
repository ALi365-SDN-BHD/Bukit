using Bukit.Config;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Cli.Intent;

public static class IntentApplier
{
    public static (IntentValidationResult Validation, string RootDir) Apply(string intentPath, string outPath)
    {
        var fullOutPath = Path.GetFullPath(outPath);
        var rootDir = ResolveRootDir(fullOutPath);

        var intent = IntentLoader.Load(intentPath);
        var validation = IntentValidator.Validate(intent, rootDir);
        if (!validation.IsValid)
        {
            return (validation, rootDir);
        }

        var config = ConvertToConfig(intent);
        ValidateConfig(config, validation);
        if (!validation.IsValid)
        {
            return (validation, rootDir);
        }

        WriteConfigYaml(fullOutPath, rootDir, config);
        return (validation, rootDir);
    }

    private static string ResolveRootDir(string fullOutPath)
    {
        var cwd = Directory.GetCurrentDirectory();
        var sitesDir = Path.GetFullPath(Path.Combine(cwd, "sites"));

        if (fullOutPath.StartsWith(sitesDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return cwd;
        }

        var dir = Path.GetDirectoryName(fullOutPath);
        return string.IsNullOrWhiteSpace(dir) ? cwd : dir;
    }

    private static AppConfig ConvertToConfig(SiteIntent intent)
    {
        var site = new SiteConfig
        {
            Name = intent.Site.Name.Trim(),
            Title = intent.Site.Title.Trim(),
            Url = string.IsNullOrWhiteSpace(intent.Site.Url) ? null : intent.Site.Url.Trim(),
            BaseUrl = string.IsNullOrWhiteSpace(intent.Site.BaseUrl) ? "/" : intent.Site.BaseUrl.Trim(),
            Collections = BuildDefaultCollections()
        };

        if (intent.Languages is not null)
        {
            site = site with
            {
                Languages = intent.Languages.Supported.Select(x => x.Trim()).ToList(),
                DefaultLanguage = intent.Languages.Default.Trim(),
                Language = intent.Languages.Default.Trim()
            };
        }
        else if (!string.IsNullOrWhiteSpace(intent.Site.Language))
        {
            site = site with { Language = intent.Site.Language.Trim() };
        }

        var contentProvider = intent.Content.Provider.Trim().ToLowerInvariant();
        var content = contentProvider switch
        {
            "markdown" => new ContentConfig
            {
                Provider = "markdown",
                Markdown = new MarkdownConfig { Dir = intent.Content.Markdown?.Dir ?? "content" }
            },
            "notion" => new ContentConfig
            {
                Provider = "notion",
                Notion = new NotionConfig
                {
                    DatabaseId = intent.Content.Notion!.DatabaseId,
                    FieldPolicy = new NotionFieldPolicyConfig
                    {
                        Mode = intent.Content.Notion!.FieldPolicy.Mode,
                        Allowed = intent.Content.Notion!.FieldPolicy.Allowed
                    }
                }
            },
            _ => new ContentConfig { Provider = intent.Content.Provider }
        };

        return new AppConfig
        {
            Site = site,
            Content = content,
            Build = new BuildConfig(),
            Theme = new ThemeConfig
            {
                Name = intent.Theme.Name,
                Params = intent.Theme.Params
            },
            Logging = new LoggingConfig()
        };
    }

    private static void ValidateConfig(AppConfig config, IntentValidationResult validation)
    {
        try
        {
            ConfigValidator.Validate(config);
        }
        catch (ConfigException ex)
        {
            validation.Errors.Add(ex.Message);
        }
    }

    private static void WriteConfigYaml(string fullOutPath, string rootDir, AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutPath) ?? rootDir);

        var root = new YamlMappingNode();

        var site = new YamlMappingNode
        {
            { "name", config.Site.Name },
            { "title", config.Site.Title },
            { "baseUrl", config.Site.BaseUrl }
        };

        if (!string.IsNullOrWhiteSpace(config.Site.Url))
        {
            site.Add("url", config.Site.Url);
        }

        if (!string.IsNullOrWhiteSpace(config.Site.Language))
        {
            site.Add("language", config.Site.Language);
        }

        if (config.Site.Languages is { Count: > 0 })
        {
            var seq = new YamlSequenceNode(config.Site.Languages.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => new YamlScalarNode(x.Trim())));
            site.Add("languages", seq);

            if (!string.IsNullOrWhiteSpace(config.Site.DefaultLanguage))
            {
                site.Add("defaultLanguage", config.Site.DefaultLanguage);
            }
        }

        if (config.Site.Collections is { Count: > 0 })
        {
            var collections = new YamlMappingNode();
            foreach (var (key, collection) in config.Site.Collections)
            {
                var node = new YamlMappingNode
                {
                    { "permalink", collection.Permalink },
                    { "template", collection.Template }
                };

                if (!string.IsNullOrWhiteSpace(collection.ListRoute))
                {
                    node.Add("listRoute", collection.ListRoute);
                }

                collections.Add(key, node);
            }

            site.Add("collections", collections);
        }

        root.Add("site", site);

        var content = new YamlMappingNode
        {
            { "provider", config.Content.Provider }
        };

        if (config.Content.Provider.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            var md = new YamlMappingNode();
            if (config.Content.Markdown is not null && !string.IsNullOrWhiteSpace(config.Content.Markdown.Dir))
            {
                md.Add("dir", MakeRelPath(rootDir, config.Content.Markdown.Dir));
            }
            content.Add("markdown", md);
        }

        if (config.Content.Provider.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            var notion = new YamlMappingNode();
            if (config.Content.Notion is not null)
            {
                notion.Add("databaseId", config.Content.Notion.DatabaseId);
                if (config.Content.Notion.FieldPolicy is not null)
                {
                    var fp = new YamlMappingNode
                    {
                        { "mode", config.Content.Notion.FieldPolicy.Mode ?? "whitelist" }
                    };

                    if (config.Content.Notion.FieldPolicy.Allowed is { Count: > 0 } allowed)
                    {
                        fp.Add("allowed", new YamlSequenceNode(allowed.Select(x => new YamlScalarNode(x))));
                    }

                    notion.Add("fieldPolicy", fp);
                }
            }
            content.Add("notion", notion);
        }

        root.Add("content", content);

        var build = new YamlMappingNode
        {
            { "output", MakeRelPath(rootDir, config.Build.Output) },
            { "clean", config.Build.Clean ? "true" : "false" }
        };
        root.Add("build", build);

        var theme = new YamlMappingNode();
        if (!string.IsNullOrWhiteSpace(config.Theme.Name))
        {
            theme.Add("name", config.Theme.Name);
        }

        if (config.Theme.Params is not null && config.Theme.Params.Count > 0)
        {
            var paramsNode = new YamlMappingNode();
            foreach (var kv in config.Theme.Params)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                {
                    continue;
                }

                paramsNode.Add(kv.Key, ToYamlNode(kv.Value));
            }

            theme.Add("params", paramsNode);
        }

        root.Add("theme", theme);

        var logging = new YamlMappingNode
        {
            { "level", config.Logging.Level }
        };
        root.Add("logging", logging);

        var stream = new YamlStream(new YamlDocument(root));
        using var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        File.WriteAllText(fullOutPath, writer.ToString());
    }

    private static IReadOnlyDictionary<string, CollectionConfig> BuildDefaultCollections()
    {
        return new Dictionary<string, CollectionConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["post"] = new()
            {
                Permalink = "/blog/{slug}/",
                Template = "pages/post.html",
                ListRoute = "/blog/"
            },
            ["page"] = new()
            {
                Permalink = "/pages/{slug}/",
                Template = "pages/page.html",
                ListRoute = "/pages/"
            }
        };
    }

    private static string MakeRelPath(string rootDir, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (!Path.IsPathRooted(path))
        {
            return path.Replace('\\', '/');
        }

        var rel = Path.GetRelativePath(rootDir, path);
        return rel.Replace('\\', '/');
    }

    private static YamlNode ToYamlNode(object? value)
    {
        if (value is null)
        {
            return new YamlScalarNode(string.Empty);
        }

        if (value is bool b)
        {
            return new YamlScalarNode(b ? "true" : "false");
        }

        if (value is string s)
        {
            return new YamlScalarNode(s);
        }

        if (value is int or long or float or double or decimal)
        {
            return new YamlScalarNode(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        if (value is IReadOnlyDictionary<string, object> roDict)
        {
            var map = new YamlMappingNode();
            foreach (var kv in roDict)
            {
                map.Add(kv.Key, ToYamlNode(kv.Value));
            }
            return map;
        }

        if (value is IDictionary<string, object> dict)
        {
            var map = new YamlMappingNode();
            foreach (var kv in dict)
            {
                map.Add(kv.Key, ToYamlNode(kv.Value));
            }
            return map;
        }

        if (value is IEnumerable<object> seq)
        {
            return new YamlSequenceNode(seq.Select(ToYamlNode));
        }

        return new YamlScalarNode(value.ToString() ?? string.Empty);
    }
}

