using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Plugin.Abstractions.Config;
using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.PluginHost;
using Bukit.Shared;

namespace Bukit.Cli;

public sealed class PluginCliLoader
{
    private readonly IPluginConfigLoader _configLoader;
    private readonly IPluginManifestLoader _manifestLoader;
    private readonly IPluginPathValidator _pathValidator;
    private readonly IPluginPlatformResolver _platformResolver;
    private readonly IPluginHashVerifier _hashVerifier;
    private readonly IPluginProtocolClient _protocolClient;
    private readonly PluginCiPolicy _ciPolicy;
    private readonly PluginLockFileWriter _lockFileWriter;
    private readonly PluginPermissionEvaluator _permissionEvaluator;

    public PluginCliLoader(
        IPluginConfigLoader configLoader,
        IPluginManifestLoader manifestLoader,
        IPluginPathValidator pathValidator,
        IPluginPlatformResolver platformResolver,
        IPluginHashVerifier hashVerifier,
        IPluginProtocolClient protocolClient,
        PluginCiPolicy? ciPolicy = null,
        PluginLockFileWriter? lockFileWriter = null,
        PluginPermissionEvaluator? permissionEvaluator = null)
    {
        _configLoader = configLoader;
        _manifestLoader = manifestLoader;
        _pathValidator = pathValidator;
        _platformResolver = platformResolver;
        _hashVerifier = hashVerifier;
        _protocolClient = protocolClient;
        _ciPolicy = ciPolicy ?? new PluginCiPolicy();
        _lockFileWriter = lockFileWriter ?? new PluginLockFileWriter();
        _permissionEvaluator = permissionEvaluator ?? new PluginPermissionEvaluator();
    }

    public static PluginCliLoader CreateDefault()
    {
        var processInvoker = new PluginProcessInvoker(new SystemProcessRunner());
        return new PluginCliLoader(
            new PluginConfigLoader(),
            new PluginManifestLoader(),
            new PluginPathValidator(),
            new PluginPlatformResolver(),
            new PluginHashVerifier(),
            new PluginProtocolClient(processInvoker, new PluginRequestIdFactory()));
    }

    public async Task<PluginCliLoadResult> LoadAsync(string projectRoot, CancellationToken cancellationToken)
    {
        PluginHostConfig config = await _configLoader.LoadAsync(projectRoot, cancellationToken);
        var descriptors = new List<CommandDescriptor>();
        var records = new List<PluginListRecord>();
        var lockEntries = new List<PluginLockEntry>();
        string rid = _platformResolver.GetCurrentRid();
        bool isCi = IsCi();

        foreach ((string pluginId, PluginConfigEntry entry) in config.Plugins)
        {
            PluginPathValidationResult source = _pathValidator.ValidatePluginSource(projectRoot, entry.Source);
            if (!source.Success || source.FullPath is null)
            {
                throw new ConfigException(source.Message ?? $"Invalid plugin source: {entry.Source}", DiagnosticCode.ConfigPathTraversal);
            }

            EnsureExposeCommandsDeclared(pluginId, entry);
            if (!entry.Enabled)
            {
                foreach (string command in entry.ExposeCommands)
                {
                    descriptors.Add(PluginCommandDescriptorFactory.CreateDisabled(command, pluginId));
                }
                records.Add(new PluginListRecord(pluginId, "disabled", Enabled: false, rid, entry.ExposeCommands));
                continue;
            }

            PluginManifest manifest = await _manifestLoader.LoadAsync(source.FullPath, cancellationToken);
            _permissionEvaluator.ValidateGrantedPermissions(pluginId, entry.Permissions, manifest.RequiredPermissions);

            if (!manifest.Platforms.TryGetValue(rid, out PluginPlatformEntry? platform))
            {
                throw new ConfigException($"Plugin {pluginId} does not provide platform {rid}.", DiagnosticCode.PluginCapabilityMissing);
            }

            PluginPathValidationResult entryPath = _pathValidator.ValidatePluginEntry(projectRoot, source.FullPath, platform.Entry);
            if (!entryPath.Success || entryPath.FullPath is null)
            {
                throw new ConfigException(entryPath.Message ?? $"Invalid plugin entry: {platform.Entry}", DiagnosticCode.ConfigPathTraversal);
            }

            PluginHashVerificationResult hash = await _hashVerifier.VerifySha256Async(entryPath.FullPath, platform.Sha256, cancellationToken);
            if (!hash.Success)
            {
                throw new ConfigException(hash.Message ?? $"Plugin {pluginId} sha256 mismatch.", DiagnosticCode.PluginExecutionFailed);
            }

            _ciPolicy.Validate(pluginId, entry, platform, hash.Success, isCi);

            var resolved = new ResolvedPlugin(
                pluginId,
                manifest.Version,
                rid,
                entryPath.FullPath,
                source.FullPath,
                new PluginHostInfo("Bukit", CliBuildInfo.Version, rid),
                ProjectRoot: projectRoot,
                Timeout: entry.Timeout,
                Output: entry.Output,
                GrantedPermissions: entry.Permissions,
                EnvironmentVariables: CreateAllowedEnvironment(entry.Permissions.Environment.Read));

            PluginHandshakeResponse handshake = await _protocolClient.HandshakeAsync(resolved, cancellationToken);
            PluginManifestResponse runtimeManifest = await _protocolClient.GetManifestAsync(resolved, cancellationToken);
            _permissionEvaluator.ValidateGrantedPermissions(pluginId, entry.Permissions, runtimeManifest.RequiredPermissions);
            IReadOnlyList<PluginCommandSpec> exposedCommands = SelectExposedCommands(
                pluginId,
                entry,
                runtimeManifest.Commands);

            foreach (PluginCommandSpec command in exposedCommands)
            {
                descriptors.Add(PluginCommandDescriptorFactory.Create(resolved, command, _protocolClient));
            }

            records.Add(new PluginListRecord(
                pluginId,
                handshake.Plugin?.Version ?? manifest.Version,
                Enabled: true,
                rid,
                exposedCommands.Select(c => c.Name).ToArray()));

            lockEntries.Add(new PluginLockEntry(
                pluginId,
                manifest.Version,
                entry.Source,
                manifest.Version,
                manifest.Protocol,
                CombinePluginEntry(entry.Source, platform.Entry),
                rid,
                platform.Sha256,
                exposedCommands.Select(command => command.Name).ToArray(),
                DateTimeOffset.UtcNow,
                Sha256Verified: true));
        }

        if (lockEntries.Count > 0)
        {
            await _lockFileWriter.WriteAsync(projectRoot, lockEntries, cancellationToken);
        }

        descriptors.Add(PluginListCommand.Create(records));
        return new PluginCliLoadResult(descriptors, records);
    }

    private static bool IsCi()
        => string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<PluginCommandSpec> SelectExposedCommands(
        string pluginId,
        PluginConfigEntry entry,
        IReadOnlyList<PluginCommandSpec> runtimeCommands)
    {
        IReadOnlyList<string> exposeCommands = entry.ExposeCommands;
        var byName = runtimeCommands.ToDictionary(command => command.Name, StringComparer.Ordinal);
        var selected = new List<PluginCommandSpec>(exposeCommands.Count);
        foreach (string commandName in exposeCommands)
        {
            if (!byName.TryGetValue(commandName, out PluginCommandSpec? command))
            {
                throw new ConfigException(
                    $"Plugin {pluginId} exposeCommands contains unknown command: {commandName}.",
                    DiagnosticCode.PluginCapabilityMissing);
            }

            selected.Add(command);
        }

        return selected;
    }

    private static void EnsureExposeCommandsDeclared(string pluginId, PluginConfigEntry entry)
    {
        if (!entry.ExposeCommandsDeclared)
        {
            throw new ConfigException(
                $"Plugin {pluginId} exposeCommands must be declared.",
                DiagnosticCode.ConfigRequiredFieldMissing);
        }
    }

    private static IReadOnlyDictionary<string, string?> CreateAllowedEnvironment(IReadOnlyList<string> names)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            variables[name] = Environment.GetEnvironmentVariable(name);
        }

        return variables;
    }

    private static string CombinePluginEntry(string source, string entry)
        => $"{source.TrimEnd('/', '\\').Replace('\\', '/')}/{entry.Replace('\\', '/')}";
}
