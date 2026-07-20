using System.Text;
using System.Text.Json;

namespace Bukit.PublicApiDrift;

internal static class BaselineFile
{
    private const string GovernedBaselinePath = "docs/governance/bukit-core-public-api-baseline.v1.json";
    private static readonly UTF8Encoding CanonicalEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        WriteIndented = true
    };

    public static ApiBaseline Load(string path, BaselineValidationMode mode)
    {
        var inputBytes = File.ReadAllBytes(path);
        if (inputBytes.Length >= 3 && inputBytes[0] == 0xef && inputBytes[1] == 0xbb && inputBytes[2] == 0xbf)
            throw new InvalidDataException("baseline must be UTF-8 without a byte-order mark");

        string input;
        try
        {
            input = CanonicalEncoding.GetString(inputBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("baseline must be valid UTF-8 without a byte-order mark", exception);
        }

        using var document = JsonDocument.Parse(input, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        });
        ValidateShape(document.RootElement);

        var baseline = JsonSerializer.Deserialize<ApiBaseline>(input, JsonOptions)
            ?? throw new InvalidDataException("baseline is empty");
        Validate(baseline, mode);

        if (mode == BaselineValidationMode.Committed)
        {
            var expectedBytes = CanonicalEncoding.GetBytes(Serialize(baseline));
            var normalizedInputBytes = CanonicalEncoding.GetBytes(NormalizeLineEndings(input));
            if (!expectedBytes.AsSpan().SequenceEqual(normalizedInputBytes))
                throw new InvalidDataException("committed baseline is not canonical");
        }

        return baseline;
    }

    public static string Serialize(ApiBaseline baseline) =>
        NormalizeLineEndings(JsonSerializer.Serialize(baseline, JsonOptions)) + "\n";

    public static void WriteNew(string path, ApiBaseline baseline, string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        var repository = CanonicalizePath(repositoryRoot);
        var tempRoot = Environment.GetEnvironmentVariable("TMPDIR");
        var temp = CanonicalizePath(string.IsNullOrEmpty(tempRoot) ? "/tmp" : tempRoot);
        var destination = CanonicalizePath(path);
        var governedBaseline = CanonicalizePath(Path.Combine(repository, GovernedBaselinePath));

        if (PathsEqual(destination, governedBaseline))
            throw new InvalidOperationException("snapshot output must not be the governed baseline");
        if (PathEntryExists(Path.GetFullPath(path)))
            throw new IOException("snapshot output must be a new path");
        if (!IsDescendant(destination, repository) && !IsDescendant(destination, temp))
            throw new InvalidOperationException("snapshot output must be inside the repository or system temporary directory");

        // This contains ordinary misuse and links or aliases that exist during validation. It does not
        // defend against an adversarial same-account process racing to replace a validated parent path.
        using var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, CanonicalEncoding);
        writer.Write(Serialize(baseline));
    }

    private static string CanonicalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? throw new InvalidOperationException("path has no root");
        var current = root;
        var relative = fullPath[root.Length..];
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.Length == 0) continue;
            var candidate = Path.Combine(current, segment);
            var target = ResolveLinkTarget(candidate);
            current = target is null ? candidate : Path.GetFullPath(target.FullName);
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(current));
    }

    private static FileSystemInfo? ResolveLinkTarget(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        if (info.LinkTarget is null) return null;
        return info.ResolveLinkTarget(returnFinalTarget: true)
            ?? throw new InvalidOperationException($"cannot resolve symbolic link: {path}");
    }

    private static bool PathEntryExists(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) return true;
        try
        {
            if (new FileInfo(path).LinkTarget is not null) return true;
        }
        catch (IOException)
        {
        }

        try
        {
            return new DirectoryInfo(path).LinkTarget is not null;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool IsDescendant(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return !StringComparerForPaths.Equals(relative, ".") &&
               !StringComparerForPaths.Equals(relative, "..") &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", PathComparison) &&
               !Path.IsPathRooted(relative);
    }

    private static bool PathsEqual(string left, string right) => StringComparerForPaths.Equals(left, right);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static StringComparer StringComparerForPaths =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void Validate(ApiBaseline baseline, BaselineValidationMode mode)
    {
        Require(baseline.Schema, "schema");
        Require(baseline.TargetFramework, "targetFramework");
        Require(baseline.SdkPolicy, "sdkPolicy");
        if (!StringComparer.Ordinal.Equals(baseline.Schema, ApiPolicy.Schema))
            throw new InvalidDataException("unexpected baseline schema");
        if (baseline.SchemaVersion != 1) throw new InvalidDataException("unexpected baseline schemaVersion");
        if (!StringComparer.Ordinal.Equals(baseline.TargetFramework, "net10.0"))
            throw new InvalidDataException("unexpected targetFramework");
        if (!StringComparer.Ordinal.Equals(baseline.SdkPolicy, "no-general-clr-sdk"))
            throw new InvalidDataException("unexpected sdkPolicy");

        if (baseline.Assemblies is null || baseline.Assemblies.Count == 0)
            throw new InvalidDataException("baseline must contain assemblies");
        if (baseline.Types is null) throw new InvalidDataException("baseline types are missing");

        ValidateOrderedUnique(baseline.Assemblies, static item => item.Assembly, "assemblies");
        ValidateOrderedUnique(baseline.Types, static item => $"{item.Assembly}\0{item.Name}", "types");
        var assemblyNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var assembly in baseline.Assemblies)
        {
            Require(assembly.Assembly, "assembly name");
            Require(assembly.Project, "assembly project");
            assemblyNames.Add(assembly.Assembly);
            if (mode == BaselineValidationMode.Committed && !File.Exists(assembly.Project))
                throw new FileNotFoundException("committed baseline project is missing", assembly.Project);
        }

        foreach (var type in baseline.Types)
        {
            Require(type.Assembly, "type assembly");
            Require(type.Name, "type name");
            Require(type.Owner, "type owner");
            Require(type.Classification, "type classification");
            Require(type.Compatibility, "type compatibility");
            Require(type.MigrationHorizon, "type migrationHorizon");
            Require(type.Signature, "type signature");
            if (!assemblyNames.Contains(type.Assembly))
                throw new InvalidDataException($"type {type.Name} references an unknown assembly");
            if (!IsApprovedOrReviewRequired(type.Classification, ApiPolicy.Classifications))
                throw new InvalidDataException($"unknown classification for {type.Name}");
            if (!IsApprovedOrReviewRequired(type.Compatibility, ApiPolicy.Compatibility))
                throw new InvalidDataException($"unknown compatibility for {type.Name}");
            if (type.PublicMembers is null || type.ProtectedMembers is null)
                throw new InvalidDataException($"member lists are missing for {type.Name}");
            ValidateMembers(type.PublicMembers, "publicMembers", type.Name);
            ValidateMembers(type.ProtectedMembers, "protectedMembers", type.Name);

            if (mode == BaselineValidationMode.Committed &&
                (StringComparer.Ordinal.Equals(type.Owner, "unresolved-owner-review") ||
                 StringComparer.Ordinal.Equals(type.Classification, "review-required") ||
                 StringComparer.Ordinal.Equals(type.Compatibility, "review-required") ||
                 StringComparer.Ordinal.Equals(type.MigrationHorizon, "review-required")))
                throw new InvalidDataException($"committed baseline contains unresolved policy metadata for {type.Name}");
        }
    }

    private static void ValidateShape(JsonElement root)
    {
        RequireObject(root, "baseline", "schema", "schemaVersion", "targetFramework", "sdkPolicy", "assemblies", "types");
        foreach (var assembly in root.GetProperty("assemblies").EnumerateArray())
            RequireObject(assembly, "assembly", "assembly", "project");
        foreach (var type in root.GetProperty("types").EnumerateArray())
            RequireObject(type, "type", "assembly", "name", "owner", "classification", "compatibility", "migrationHorizon", "signature", "publicMembers", "protectedMembers");
    }

    private static void RequireObject(JsonElement element, string name, params string[] properties)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{name} must be an object");
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!properties.Contains(property.Name, StringComparer.Ordinal))
                throw new InvalidDataException($"{name} contains an unknown property: {property.Name}");
            if (!found.Add(property.Name)) throw new InvalidDataException($"{name} contains a duplicate property: {property.Name}");
        }
        if (found.Count != properties.Length || properties.Any(property => !found.Contains(property)))
            throw new InvalidDataException($"{name} is missing a required property");
    }

    private static void ValidateOrderedUnique<T>(IReadOnlyList<T> values, Func<T, string> key, string name)
    {
        string? previous = null;
        foreach (var value in values)
        {
            var current = key(value);
            if (previous is not null && StringComparer.Ordinal.Compare(previous, current) >= 0)
                throw new InvalidDataException($"{name} must be sorted and unique");
            previous = current;
        }
    }

    private static void ValidateMembers(IReadOnlyList<string> members, string name, string typeName)
    {
        string? previous = null;
        foreach (var member in members)
        {
            Require(member, $"{name} member");
            if (previous is not null && StringComparer.Ordinal.Compare(previous, member) >= 0)
                throw new InvalidDataException($"{name} for {typeName} must be sorted and unique");
            previous = member;
        }
    }

    private static bool IsApprovedOrReviewRequired(string value, HashSet<string> allowed) =>
        StringComparer.Ordinal.Equals(value, "review-required") || allowed.Contains(value);

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"{name} is required");
    }

    private static string NormalizeLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
