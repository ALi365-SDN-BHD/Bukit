using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2APluginSecretMaskerInternalizationTests
{
    private const string TargetTypeName =
        "Bukit.PluginHost.PluginSecretMasker";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string Decision =
        "G-04D2A single-type internalization decision: only `Bukit.PluginHost.PluginSecretMasker` is narrowed from public to internal in 2.0; the other 104 candidates are not batch-approved.";
    private const string CurrentBaseline =
        "The current public API baseline contains 444 types, including 5 `2.0-candidate` entries.";
    private const string HistoricalD1CM2Decision =
        "G-04D1C-M2 five-type atomic decision: only the five approved `Bukit.Content.Notion` renderer-extension CLR identities are removed in 2.0; the other 105 candidates are not batch-approved.";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PluginHostAssembly_KeepsMaskerInternalAndDoesNotExportIt()
    {
        var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
        var type = assembly.GetType(TargetTypeName, throwOnError: false, ignoreCase: false);

        Assert.NotNull(type);
        Assert.False(type.IsPublic);
        Assert.DoesNotContain(
            assembly.GetExportedTypes(),
            exported => exported.FullName == TargetTypeName);
    }

    [Fact]
    public void CurrentBaseline_ContainsFourteenAssemblies444TypesAnd5Candidates()
    {
        using var document = ReadJson(
            "docs", "governance", "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(444, types.Length);
        Assert.Equal(5, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, type =>
            type.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            type.GetProperty("name").GetString() == TargetTypeName);
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalMaskerEvidenceAndExactBlob()
    {
        var path = Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-public-surface-candidates.v1.json");
        var bytes = File.ReadAllBytes(path);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();
        var target = Assert.Single(candidates, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("fullName").GetString() == TargetTypeName);

        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        Assert.Equal(
            "consumer-declaration-pending",
            target.GetProperty("declarationStatus").GetString());
        Assert.Equal(
            "unknown-until-voluntary-declaration",
            target.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal(
            "no-public-match-found",
            target.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    [Fact]
    public void ActiveGovernance_RecordsExactG04D2ADecisionAndPreservesHistory()
    {
        var declaration = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "governance",
            "bukit-core-2.0-consumer-declaration.md"));
        var guide = File.ReadAllText(Path.Combine(
            RepoRoot,
            "guide",
            "dev",
            "public-api-governance.md"));
        var ledgerPath = Path.Combine(
            RepoRoot,
            "docs",
            "analysis",
            "bukit-core-g04d2a-plugin-secret-masker-internalization-2026-07-23.zh-CN.md");

        Assert.Contains(Decision, declaration, StringComparison.Ordinal);
        Assert.Contains(Decision, guide, StringComparison.Ordinal);
        Assert.Contains(CurrentBaseline, declaration, StringComparison.Ordinal);
        Assert.Contains(CurrentBaseline, guide, StringComparison.Ordinal);
        Assert.Contains(HistoricalD1CM2Decision, declaration, StringComparison.Ordinal);
        Assert.Contains(HistoricalD1CM2Decision, guide, StringComparison.Ordinal);
        Assert.True(File.Exists(ledgerPath), $"Missing G-04D2A decision ledger: {ledgerPath}");
    }

    private static JsonDocument ReadJson(params string[] relativeSegments)
    {
        var path = Path.Combine([RepoRoot, .. relativeSegments]);
        return JsonDocument.Parse(File.ReadAllText(path));
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

        throw new DirectoryNotFoundException("Could not locate the Bukit repository root.");
    }
}
