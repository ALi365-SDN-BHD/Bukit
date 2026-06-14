using Bukit.Config;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Labs.Cli.Commands;

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

        var config = ConvertToConfig(intent, rootDir);
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
        var cwd = GetCurrentDirectoryOrFallback(fullOutPath);
        var dir = Path.GetDirectoryName(fullOutPath);
        var sitesAncestor = FindAncestorNamed(dir, "sites");
        if (!string.IsNullOrWhiteSpace(sitesAncestor))
        {
            return Path.GetDirectoryName(sitesAncestor) ?? cwd;
        }

        var sitesDir = Path.GetFullPath(Path.Combine(cwd, "sites"));

        if (PathUtils.IsSubPathOf(fullOutPath, sitesDir))
        {
            return cwd;
        }

        return string.IsNullOrWhiteSpace(dir) ? cwd : dir;
    }

    private static string GetCurrentDirectoryOrFallback(string fullOutPath)
    {
        try
        {
            return Directory.GetCurrentDirectory();
        }
        catch (FileNotFoundException)
        {
            return Path.GetDirectoryName(fullOutPath) ?? AppContext.BaseDirectory;
        }
    }

    private static string? FindAncestorNamed(string? path, string name)
    {
        while (!string.IsNullOrWhiteSpace(path))
        {
            var current = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(Path.GetFileName(current), name, PlatformPathHelper.PathComparison))
            {
                return current;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, current, PlatformPathHelper.PathComparison))
            {
                break;
            }

            path = parent;
        }

        return null;
    }

    private static AppConfig ConvertToConfig(SiteIntent intent, string rootDir)
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

        var contentKind = intent.Content.Kind.Trim().ToLowerInvariant();
        var content = contentKind switch
        {
            "markdown" => ContentConfigFactory.FromSources(BuildMarkdownSources(rootDir, intent.Content.Markdown?.Dir ?? "content")),
            "notion" => ContentConfigFactory.FromSources(BuildNotionSources(intent.Content.Notion!)),
            _ => ContentConfigFactory.FromSources(new List<ContentSourceConfig>())
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
                    { "permalink", collection.Permalink }
                };

                if (!string.IsNullOrWhiteSpace(collection.Template))
                {
                    node.Add("template", collection.Template);
                }

                if (!string.IsNullOrWhiteSpace(collection.ListRoute))
                {
                    node.Add("listRoute", collection.ListRoute);
                }

                collections.Add(key, node);
            }

            site.Add("collections", collections);
        }

        root.Add("site", site);

        var content = new YamlMappingNode();
        var sources = new YamlSequenceNode();

        if (config.Content.Sources is { Count: > 0 })
        {
            foreach (var source in config.Content.Sources)
            {
                var sourceNode = new YamlMappingNode();
                sourceNode.Add("type", source.Type);
                if (!string.IsNullOrWhiteSpace(source.Name))
                {
                    sourceNode.Add("name", source.Name);
                }

                if (!string.IsNullOrWhiteSpace(source.Collection))
                {
                    sourceNode.Add("collection", source.Collection);
                }

                if (source.Type.Equals("markdown", StringComparison.OrdinalIgnoreCase))
                {
                    var md = new YamlMappingNode();
                    if (source.Markdown is not null && !string.IsNullOrWhiteSpace(source.Markdown.Dir))
                    {
                        md.Add("dir", MakeRelPath(rootDir, source.Markdown.Dir));
                    }
                    sourceNode.Add("markdown", md);
                }

                if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase))
                {
                    var notion = new YamlMappingNode();
                    if (source.Notion is not null)
                    {
                        notion.Add("databaseId", source.Notion.DatabaseId);
                        if (source.Notion.FieldPolicy is not null)
                        {
                            var fp = new YamlMappingNode
                            {
                                { "mode", source.Notion.FieldPolicy.Mode ?? "whitelist" }
                            };

                            if (source.Notion.FieldPolicy.Allowed is { Count: > 0 } allowed)
                            {
                                fp.Add("allowed", new YamlSequenceNode(allowed.Select(x => new YamlScalarNode(x))));
                            }

                            notion.Add("fieldPolicy", fp);
                        }
                    }
                    sourceNode.Add("notion", notion);
                }

                sources.Add(sourceNode);
            }
        }

        content.Add("sources", sources);

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
                ListRoute = "/blog/"
            },
            ["page"] = new()
            {
                Permalink = "/pages/{slug}/",
                ListRoute = "/pages/"
            },
            ["about"] = new()
            {
                Permalink = "/{slug}/",
                Template = "pages/page.html"
            }
        };
    }

    private static List<ContentSourceConfig> BuildMarkdownSources(string rootDir, string dir)
    {
        var normalizedDir = NormalizeConfigPath(rootDir, dir);
        return BuildDefaultCollections().Keys.Select(collection => new ContentSourceConfig
        {
            Type = "markdown",
            Name = collection,
            Collection = collection,
            Markdown = new MarkdownConfig { Dir = normalizedDir }
        }).ToList();
    }

    private static List<ContentSourceConfig> BuildNotionSources(SiteIntentNotionContent notion)
    {
        return BuildDefaultCollections().Keys.Select(collection => new ContentSourceConfig
        {
            Type = "notion",
            Name = collection,
            Collection = collection,
            Notion = new NotionConfig
            {
                DatabaseId = notion.DatabaseId,
                FieldPolicy = new NotionFieldPolicyConfig
                {
                    Mode = notion.FieldPolicy.Mode,
                    Allowed = notion.FieldPolicy.Allowed
                }
            }
        }).ToList();
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

    private static string NormalizeConfigPath(string rootDir, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        if (!Path.IsPathRooted(path))
        {
            return path.Replace('\\', '/');
        }

        var normalizedRoot = NormalizeExistingFullPath(rootDir);
        var normalizedPath = NormalizeExistingFullPath(path);
        return Path.GetRelativePath(normalizedRoot, normalizedPath).Replace('\\', '/');
    }

    private static string NormalizeExistingFullPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return fullPath;
        }

        var current = TrimTrailingSeparators(root);
        var remainder = fullPath[root.Length..]
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < remainder.Length; i++)
        {
            var candidate = Path.Combine(current, remainder[i]);
            var resolved = ResolveLinkTarget(candidate);
            if (resolved is null)
            {
                for (var j = i; j < remainder.Length; j++)
                {
                    current = Path.Combine(current, remainder[j]);
                }

                return TrimTrailingSeparators(Path.GetFullPath(current));
            }

            current = resolved;
        }

        return TrimTrailingSeparators(Path.GetFullPath(current));
    }

    private static string? ResolveLinkTarget(string path)
    {
        FileSystemInfo info;
        if (Directory.Exists(path))
        {
            info = new DirectoryInfo(path);
        }
        else if (File.Exists(path))
        {
            info = new FileInfo(path);
        }
        else
        {
            return null;
        }

        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        return TrimTrailingSeparators(Path.GetFullPath(target?.FullName ?? info.FullName));
    }

    private static string TrimTrailingSeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        if (!string.IsNullOrEmpty(root) &&
            string.Equals(path, root, PlatformPathHelper.PathComparison))
        {
            return root;
        }

        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrEmpty(trimmed) && !string.IsNullOrEmpty(root) ? root : trimmed;
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
