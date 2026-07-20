using System.Reflection;
using System.Runtime.Loader;

namespace Bukit.PublicApiDrift;

internal static class ApiSurfaceCapture
{
    public static ApiBaseline Capture(ApiBaseline policy, string repositoryRoot, string configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuration);

        var types = new List<ApiType>();
        var dependencyHost = policy.Assemblies.Single(static item => StringComparer.Ordinal.Equals(item.Assembly, "bukit"));
        var dependencyHostPath = Path.Combine(repositoryRoot, Path.GetDirectoryName(dependencyHost.Project)!, "bin",
            configuration, policy.TargetFramework, dependencyHost.Assembly + ".dll");
        foreach (var mapping in policy.Assemblies.OrderBy(static item => item.Assembly, StringComparer.Ordinal))
        {
            var dll = Path.Combine(repositoryRoot, Path.GetDirectoryName(mapping.Project)!, "bin", configuration,
                policy.TargetFramework, mapping.Assembly + ".dll");
            if (!File.Exists(dll)) throw new FileNotFoundException("compiled assembly is missing", dll);
            var context = new ApiAssemblyLoadContext(dll, dependencyHostPath);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
                if (!StringComparer.Ordinal.Equals(assembly.GetName().Name, mapping.Assembly))
                    throw new InvalidDataException($"unexpected assembly name for {mapping.Project}");
                foreach (var type in assembly.GetExportedTypes().OrderBy(static item => item.FullName, StringComparer.Ordinal))
                    types.Add(CaptureType(mapping.Assembly, type, policy));
            }
            finally
            {
                context.Unload();
            }
        }
        return policy with
        {
            Types = types.OrderBy(static item => item.Assembly, StringComparer.Ordinal)
                .ThenBy(static item => item.Name, StringComparer.Ordinal).ToArray()
        };
    }

    private static ApiType CaptureType(string assemblyName, Type type, ApiBaseline policy)
    {
        var name = type.FullName ?? type.Name;
        var previous = policy.Types.FirstOrDefault(item =>
            StringComparer.Ordinal.Equals(item.Assembly, assemblyName) && StringComparer.Ordinal.Equals(item.Name, name));
        return new ApiType(
            assemblyName,
            name,
            previous?.Owner ?? "unresolved-owner-review",
            previous?.Classification ?? "review-required",
            previous?.Compatibility ?? "review-required",
            previous?.MigrationHorizon ?? "review-required",
            ApiSignatureFormatter.FormatType(type),
            ApiSignatureFormatter.FormatPublicMembers(type),
            ApiSignatureFormatter.FormatProtectedMembers(type));
    }
}

internal sealed class ApiAssemblyLoadContext(string assemblyPath, string dependencyHostPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly string _assemblyDirectory = Path.GetDirectoryName(assemblyPath)!;
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);
    private readonly AssemblyDependencyResolver _dependencyHostResolver = new(dependencyHostPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var candidate = Path.Combine(_assemblyDirectory, assemblyName.Name + ".dll");
        if (File.Exists(candidate)) return LoadExact(assemblyName, candidate, useDefaultContext: false);
        var resolved = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolved is not null) return LoadExact(assemblyName, resolved, useDefaultContext: false);
        var dependency = _dependencyHostResolver.ResolveAssemblyToPath(assemblyName);
        if (dependency is null) return null;
        EnsureExactIdentity(assemblyName, AssemblyName.GetAssemblyName(dependency));
        var loaded = Default.Assemblies.FirstOrDefault(item => HasExactIdentity(assemblyName, item.GetName()));
        return loaded ?? LoadExact(assemblyName, dependency, useDefaultContext: true);
    }

    private Assembly LoadExact(AssemblyName requested, string path, bool useDefaultContext)
    {
        EnsureExactIdentity(requested, AssemblyName.GetAssemblyName(path));
        var loaded = useDefaultContext ? Default.LoadFromAssemblyPath(path) : LoadFromAssemblyPath(path);
        EnsureExactIdentity(requested, loaded.GetName());
        return loaded;
    }

    private static void EnsureExactIdentity(AssemblyName requested, AssemblyName actual)
    {
        if (!HasExactIdentity(requested, actual))
            throw new InvalidDataException($"dependency assembly identity mismatch: requested {requested.FullName}; resolved {actual.FullName}");
    }

    private static bool HasExactIdentity(AssemblyName requested, AssemblyName actual)
    {
        var requestedToken = requested.GetPublicKeyToken() ?? [];
        var actualToken = actual.GetPublicKeyToken() ?? [];
        return StringComparer.OrdinalIgnoreCase.Equals(requested.Name, actual.Name) &&
            Equals(requested.Version, actual.Version) &&
            StringComparer.OrdinalIgnoreCase.Equals(requested.CultureName ?? string.Empty, actual.CultureName ?? string.Empty) &&
            requestedToken.AsSpan().SequenceEqual(actualToken);
    }
}
