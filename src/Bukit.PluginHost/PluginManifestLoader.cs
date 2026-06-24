using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Security;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Bukit.PluginHost;

public sealed class PluginManifestLoader : IPluginManifestLoader
{
    public async Task<PluginManifest> LoadAsync(string pluginRoot, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pluginRoot))
        {
            throw new ConfigException("Plugin root is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        string manifestPath = Path.Combine(pluginRoot, "plugin.yaml");
        if (!File.Exists(manifestPath))
        {
            throw new ConfigException($"Plugin manifest not found: {manifestPath}", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        await using var stream = File.OpenRead(manifestPath);
        using var reader = new StreamReader(stream);
        YamlMappingNode root = LoadRoot(reader, manifestPath);

        string id = PluginYaml.GetRequiredString(root, "id", "plugin.id");
        string name = PluginYaml.GetRequiredString(root, "name", "plugin.name");
        string version = PluginYaml.GetRequiredString(root, "version", "plugin.version");
        string protocol = PluginYaml.GetRequiredString(root, "protocol", "plugin.protocol");
        string kind = PluginYaml.GetRequiredString(root, "kind", "plugin.kind");
        string distribution = PluginYaml.GetOptionalString(root, "distribution") ?? "self-contained";

        if (!StringComparer.Ordinal.Equals(protocol, PluginProtocolConstants.ProtocolVersion))
        {
            throw new ConfigException("Plugin protocol must be bukit-plugin-v1.", DiagnosticCode.ConfigInvalidValue);
        }

        if (!StringComparer.Ordinal.Equals(kind, "process"))
        {
            throw new ConfigException("Plugin kind must be process.", DiagnosticCode.ConfigInvalidValue);
        }

        if (!StringComparer.Ordinal.Equals(distribution, "self-contained"))
        {
            throw new ConfigException("Plugin distribution must be self-contained.", DiagnosticCode.ConfigInvalidValue);
        }

        IReadOnlyDictionary<string, PluginPlatformEntry> platforms = ReadPlatforms(root);
        IReadOnlyList<PluginCommandSpec> commands = ReadCommands(PluginYaml.GetOptionalSequence(root, "commands"));
        PluginPermissionSet permissions = ReadRequiredPermissions(PluginYaml.GetOptionalMapping(root, "requiredPermissions"));

        cancellationToken.ThrowIfCancellationRequested();

        return new PluginManifest(
            id,
            name,
            version,
            protocol,
            kind,
            distribution,
            platforms,
            commands,
            permissions);
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
            throw new ConfigException($"Invalid YAML syntax in plugin manifest: {path}", ex, DiagnosticCode.ConfigYamlSyntaxError);
        }

        if (yaml.Documents.Count == 0)
        {
            throw new ConfigException("Plugin manifest is empty.", DiagnosticCode.ConfigYamlSyntaxError);
        }

        if (yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new ConfigException("Plugin manifest root must be a mapping.", DiagnosticCode.ConfigYamlSyntaxError);
        }

        return root;
    }

    private static IReadOnlyDictionary<string, PluginPlatformEntry> ReadPlatforms(YamlMappingNode root)
    {
        YamlMappingNode platformsNode = PluginYaml.GetRequiredMapping(root, "platforms", "plugin.platforms");
        var platforms = new Dictionary<string, PluginPlatformEntry>(StringComparer.Ordinal);
        foreach ((YamlNode keyNode, YamlNode valueNode) in platformsNode.Children)
        {
            string rid = PluginYaml.RequireKey(keyNode, "plugin.platforms");
            if (valueNode is not YamlMappingNode platformNode)
            {
                throw new ConfigException($"plugin.platforms.{rid} must be a mapping.", DiagnosticCode.ConfigInvalidValue);
            }

            string entry = PluginYaml.GetRequiredString(platformNode, "entry", $"plugin.platforms.{rid}.entry");
            string sha256 = PluginYaml.GetRequiredString(platformNode, "sha256", $"plugin.platforms.{rid}.sha256");
            platforms[rid] = new PluginPlatformEntry(entry, sha256);
        }

        if (platforms.Count == 0)
        {
            throw new ConfigException("plugin.platforms must contain at least one platform.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return platforms;
    }

    private static IReadOnlyList<PluginCommandSpec> ReadCommands(YamlSequenceNode? sequence)
    {
        if (sequence is null)
        {
            return [];
        }

        var commands = new List<PluginCommandSpec>();
        foreach (YamlNode item in sequence.Children)
        {
            if (item is not YamlMappingNode commandNode)
            {
                throw new ConfigException("plugin.commands items must be mappings.", DiagnosticCode.ConfigInvalidValue);
            }

            string name = PluginYaml.GetRequiredString(commandNode, "name", "plugin.commands[].name");
            string description = PluginYaml.GetOptionalString(commandNode, "description")
                ?? PluginYaml.GetOptionalString(commandNode, "summary")
                ?? string.Empty;
            commands.Add(new PluginCommandSpec(name, description));
        }

        return commands;
    }

    private static PluginPermissionSet ReadRequiredPermissions(YamlMappingNode? node)
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
}
