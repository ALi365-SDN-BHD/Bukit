using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D4BValueCoercionTests
{
    private const string TypeName = "Bukit.Shared.ValueCoercion";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ValueCoercion_ExistsButIsInternalAndNotExported()
    {
        Assembly assembly = typeof(Bukit.Shared.ILogger).Assembly;
        Type type = assembly.GetType(
            TypeName,
            throwOnError: true,
            ignoreCase: false)!;

        Assert.True(type.IsNotPublic);
        Assert.DoesNotContain(type, assembly.GetExportedTypes());
    }

    [Fact]
    public void SharedFriendBoundaryAndProductionConsumerSetRemainUnchanged()
    {
        Assembly assembly = typeof(Bukit.Shared.ILogger).Assembly;
        string[] friends = assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                "Bukit.Content",
                "Bukit.Content.Tests",
                "Bukit.Engine",
                "Bukit.Shared.Tests"
            ],
            friends);

        string sourceRoot = Path.Combine(RepoRoot, "src");
        string declarationPath = Path.Combine(
            sourceRoot,
            "Bukit-Core",
            "Bukit.Shared",
            "ValueCoercion.cs");
        var symbol = new Regex(
            @"\bValueCoercion\b",
            RegexOptions.CultureInvariant);
        string[] productionConsumers = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                path,
                declarationPath,
                StringComparison.Ordinal))
            .Where(path => !IsBuildOutput(path))
            .Where(path => symbol.IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(RepoRoot, path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(productionConsumers);
    }

    [Fact]
    public void CurrentBaseline_RecordsFourteenAssemblies462TypesAnd23Candidates()
    {
        using JsonDocument current = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        JsonElement root = current.RootElement;
        JsonElement[] types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(462, types.Length);
        Assert.Equal(23, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() ==
            "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
            entry.GetProperty("name").GetString() == TypeName);
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalCandidateAndExactBlob()
    {
        string path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        byte[] bytes = File.ReadAllBytes(path);
        using JsonDocument document = JsonDocument.Parse(bytes);
        JsonElement root = document.RootElement;
        JsonElement[] candidates =
            root.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);

        JsonElement candidate = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.Shared" &&
            entry.GetProperty("fullName").GetString() == TypeName);
        Assert.Equal(
            "consumer-declaration-pending",
            candidate.GetProperty("declarationStatus").GetString());
        Assert.Equal(
            "unknown-until-voluntary-declaration",
            candidate.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal(
            "no-public-match-found",
            candidate.GetProperty("externalEvidence")
                .GetProperty("searchStatus")
                .GetString());

        byte[] prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    private static JsonDocument ReadJson(params string[] relativeSegments)
    {
        string path = Path.Combine([RepoRoot, .. relativeSegments]);
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static bool IsBuildOutput(string path)
    {
        string separator = Path.DirectorySeparatorChar.ToString();
        return path.Contains(
                   $"{separator}bin{separator}",
                   StringComparison.Ordinal) ||
               path.Contains(
                   $"{separator}obj{separator}",
                   StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "bukit-core.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
