using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04D2B1PluginHostErrorCodeContractTests
{
    private const string TargetTypeName =
        "Bukit.PluginHost.PluginHostErrorCodes";
    private const string VocabularySchema =
        "bukit-plugin-host-error-vocabulary-v1";
    private const string CandidateManifestBlob =
        "7b07d6890562387010b52301e9f8716e9bf10ed1";
    private const string Decision =
        "G-04D2B2 single-type internalization decision: only `Bukit.PluginHost.PluginHostErrorCodes` is narrowed from public to internal in 2.0; the other 103 candidates are not batch-approved.";
    private const string CurrentBaseline =
        "The current public API baseline contains 493 types, including 68 `2.0-candidate` entries.";
    private static readonly string[] StableVocabulary =
    [
        "plugin.unsupportedProtocol",
        "plugin.invalidResponse",
        "plugin.timeout",
        "plugin.executionFailed",
        "plugin.permissionDenied",
        "plugin.outputTooLarge"
    ];
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PluginProtocolClientTests_DoNotConsumeErrorCodeClrType()
    {
        var testSource = File.ReadAllText(Path.Combine(
            RepoRoot,
            "tests",
            "Bukit.PluginHost.Tests",
            "PluginProtocolClientTests.cs"));

        Assert.DoesNotContain(
            TargetTypeName.Split('.').Last(),
            testSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ProtocolVocabularyFixture_PreservesExactSixTermsAndActiveDocs()
    {
        var fixturePath = Path.Combine(
            RepoRoot,
            "tests",
            "fixtures",
            "plugin-contracts",
            "plugin-host-error-vocabulary.v1.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var root = document.RootElement;
        var codes = root.GetProperty("codes")
            .EnumerateArray()
            .Select(code => code.GetString())
            .ToArray();

        Assert.Equal(VocabularySchema, root.GetProperty("schema").GetString());
        Assert.Equal(StableVocabulary, codes);
        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(TargetTypeName, File.ReadAllText(fixturePath), StringComparison.Ordinal);

        var protocol = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "plugins",
            "Bukit 插件协议 v1 规范.md"));
        foreach (string code in StableVocabulary)
        {
            Assert.Contains($"`{code}`", protocol, StringComparison.Ordinal);
        }

        var securityAdr = File.ReadAllText(Path.Combine(
            RepoRoot,
            "docs",
            "plugins",
            "Bukit 插件安全模型 ADR.md"));
        foreach (string code in StableVocabulary.Skip(1))
        {
            Assert.Contains($"`{code}`", securityAdr, StringComparison.Ordinal);
        }

        var protocolClientSource = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.PluginHost",
            "PluginProtocolClient.cs"));
        Assert.DoesNotContain(
            "PluginHostErrorCodes.PermissionDenied",
            protocolClientSource,
            StringComparison.Ordinal);

        var permissionEvaluatorSource = File.ReadAllText(Path.Combine(
            RepoRoot,
            "src",
            "Bukit-Core",
            "Bukit.PluginHost",
            "PluginPermissionEvaluator.cs"));
        Assert.DoesNotContain(
            "plugin.permissionDenied",
            permissionEvaluatorSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PluginHostAssembly_KeepsErrorCodeTypeInternalAndDoesNotExportIt()
    {
        var assembly = typeof(Bukit.PluginHost.PluginConfigLoader).Assembly;
        var type = assembly.GetType(
            TargetTypeName,
            throwOnError: false,
            ignoreCase: false);

        Assert.NotNull(type);
        Assert.False(type.IsPublic);
        Assert.DoesNotContain(
            assembly.GetExportedTypes(),
            exported => exported.FullName == TargetTypeName);
    }

    [Fact]
    public void CurrentBaseline_ContainsFourteenAssemblies493TypesAnd68Candidates()
    {
        using var document = ReadJson(
            "docs",
            "governance",
            "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(493, types.Length);
        Assert.Equal(68, types.Count(entry =>
            entry.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, entry =>
            entry.GetProperty("assembly").GetString() == "Bukit.PluginHost" &&
            entry.GetProperty("name").GetString() == TargetTypeName);
    }

    [Fact]
    public void ClosedManifest_PreservesHistoricalErrorCodeEvidenceAndExactBlob()
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
            target.GetProperty("externalEvidence")
                .GetProperty("searchStatus")
                .GetString());

        var prefix = Encoding.UTF8.GetBytes($"blob {bytes.Length}\0");
        var blobBytes = new byte[prefix.Length + bytes.Length];
        prefix.CopyTo(blobBytes, 0);
        bytes.CopyTo(blobBytes, prefix.Length);

        Assert.Equal(
            CandidateManifestBlob,
            Convert.ToHexStringLower(SHA1.HashData(blobBytes)));
    }

    [Fact]
    public void ActiveGovernance_RecordsExactG04D2B2DecisionAndCurrentBaseline()
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
            "bukit-core-g04d2b2-plugin-host-error-codes-internalization-2026-07-23.zh-CN.md");

        Assert.Contains(Decision, declaration, StringComparison.Ordinal);
        Assert.Contains(Decision, guide, StringComparison.Ordinal);
        Assert.Contains(CurrentBaseline, declaration, StringComparison.Ordinal);
        Assert.Contains(CurrentBaseline, guide, StringComparison.Ordinal);
        Assert.True(File.Exists(ledgerPath), $"Missing G-04D2B2 decision ledger: {ledgerPath}");
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

        throw new DirectoryNotFoundException(
            "Could not locate the Bukit repository root.");
    }
}
