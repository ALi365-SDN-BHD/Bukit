using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.PluginHost;

public sealed class PluginConfigLoader : IPluginConfigLoader
{
    public async Task<PluginHostConfig> LoadAsync(string projectRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
        {
            throw new ConfigException("Plugin project root is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        string configPath = Path.Combine(projectRoot, ".bukit", "plugins.yaml");
        if (!File.Exists(configPath))
        {
            return new PluginHostConfig(Version: 1);
        }

        await using var stream = File.OpenRead(configPath);
        using var reader = new StreamReader(stream);
        YamlMappingNode root = LoadRoot(reader, configPath);

        int version = PluginYaml.GetOptionalInt(root, "version") ?? 1;
        if (version != 1)
        {
            throw new ConfigException("plugins.yaml version must be 1.", DiagnosticCode.ConfigInvalidValue);
        }

        var plugins = new Dictionary<string, PluginConfigEntry>(StringComparer.Ordinal);
        YamlMappingNode? pluginsNode = PluginYaml.GetOptionalMapping(root, "plugins");
        if (pluginsNode is not null)
        {
            foreach ((YamlNode keyNode, YamlNode valueNode) in pluginsNode.Children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string id = PluginYaml.RequireKey(keyNode, "plugins");
                if (valueNode is not YamlMappingNode pluginNode)
                {
                    throw new ConfigException($"plugins.{id} must be a mapping.", DiagnosticCode.ConfigInvalidValue);
                }

                plugins[id] = ReadPluginEntry(pluginNode, id);
            }
        }

        return new PluginHostConfig(version, plugins);
    }

    private static YamlMappingNode LoadRoot(TextReader reader, string path)
    {
        var yaml = new YamlStream();
        try
        {
            yaml.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new ConfigException($"Invalid YAML syntax in plugin config file: {path}", ex, DiagnosticCode.ConfigYamlSyntaxError);
        }

        if (yaml.Documents.Count == 0)
        {
            throw new ConfigException("Plugin config file is empty.", DiagnosticCode.ConfigYamlSyntaxError);
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ConfigException("Plugin config root must be a mapping.", DiagnosticCode.ConfigYamlSyntaxError);
        }

        return root;
    }

    private static PluginConfigEntry ReadPluginEntry(YamlMappingNode node, string id)
    {
        bool enabled = PluginYaml.GetOptionalBool(node, "enabled") ?? false;
        string source = PluginYaml.GetRequiredString(node, "source", $"plugins.{id}.source");
        (bool exposeCommandsDeclared, IReadOnlyList<string> exposeCommands) =
            PluginYaml.ReadStringListWithPresence(node, "exposeCommands");
        string failMode = PluginYaml.GetOptionalString(node, "failMode") ?? "strict";
        bool allowInCi = PluginYaml.GetOptionalBool(node, "allowInCi") ?? false;
        string? description = PluginYaml.GetOptionalString(node, "description");

        var permissions = ReadPermissions(PluginYaml.GetOptionalMapping(node, "permissions"));
        bool permissionsExplicit = PluginYaml.GetOptionalMapping(node, "permissions") is not null;
        var timeout = ReadTimeout(PluginYaml.GetOptionalMapping(node, "timeout"));
        var output = ReadOutput(PluginYaml.GetOptionalMapping(node, "output"));

        return new PluginConfigEntry(
            enabled,
            source,
            exposeCommands,
            permissions,
            timeout,
            output,
            failMode,
            allowInCi,
            description,
            permissionsExplicit,
            exposeCommandsDeclared);
    }

    private static PluginPermissionSet ReadPermissions(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new PluginPermissionSet();
        }

        bool network = PluginYaml.GetOptionalBool(node, "network") ?? false;
        YamlMappingNode? fileSystemNode = PluginYaml.GetOptionalMapping(node, "fileSystem");
        YamlMappingNode? environmentNode = PluginYaml.GetOptionalMapping(node, "environment");

        return new PluginPermissionSet(
            FileSystem: new PluginFileSystemPermission(
                Read: PluginYaml.ReadStringList(fileSystemNode, "read"),
                Write: PluginYaml.ReadStringList(fileSystemNode, "write")),
            Network: network,
            Environment: new PluginEnvironmentPermission(
                Read: ReadEnvironmentList(environmentNode)));
    }

    private static IReadOnlyList<string> ReadEnvironmentList(YamlMappingNode? node)
    {
        IReadOnlyList<string> values = PluginYaml.ReadStringList(node, "read");
        PluginPermissionEvaluator.ValidateNoEnvironmentWildcard(values);
        return values;
    }

    private static PluginTimeoutOptions ReadTimeout(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new PluginTimeoutOptions();
        }

        return new PluginTimeoutOptions(
            HandshakeMs: PluginYaml.GetOptionalInt(node, "handshakeMs") ?? 5000,
            ManifestMs: PluginYaml.GetOptionalInt(node, "manifestMs") ?? 5000,
            InvokeMs: PluginYaml.GetOptionalInt(node, "invokeMs") ?? 120000);
    }

    private static PluginOutputLimitOptions ReadOutput(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new PluginOutputLimitOptions();
        }

        return new PluginOutputLimitOptions(
            StdoutMaxBytes: PluginYaml.GetOptionalInt(node, "stdoutMaxBytes") ?? 4194304,
            StderrMaxBytes: PluginYaml.GetOptionalInt(node, "stderrMaxBytes") ?? 4194304,
            ResponseMaxBytes: PluginYaml.GetOptionalInt(node, "responseMaxBytes") ?? 4194304);
    }
}
