using System.Text.Json;
using System.Xml.Linq;
using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04CPublicSurfacePilotTests
{
    private const string RemovedTypeName = "Bukit.Engine.RouteInventoryInspectEntry";
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry()
    {
        var engineAssembly = typeof(RouteInventoryValidator).Assembly;

        Assert.Null(engineAssembly.GetType(RemovedTypeName, throwOnError: false, ignoreCase: false));
    }

    [Fact]
    public void ProductVersion_IsTheApprovedTwoPointZeroAlpha()
    {
        var document = XDocument.Load(Path.Combine(RepoRoot, "Directory.Build.props"));
        var versions = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Version")
            .Select(element => element.Value)
            .ToArray();

        Assert.Equal(["2.0.0-alpha.1"], versions);
    }

    [Fact]
    public void CurrentPublicApiBaseline_ContainsOnlyTheApprovedRemoval()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-public-api-baseline.v1.json");
        var root = document.RootElement;
        var types = root.GetProperty("types").EnumerateArray().ToArray();

        Assert.Equal("bukit-core-public-api-baseline-v1", root.GetProperty("schema").GetString());
        Assert.Equal("net10.0", root.GetProperty("targetFramework").GetString());
        Assert.Equal("no-general-clr-sdk", root.GetProperty("sdkPolicy").GetString());
        Assert.Equal(14, root.GetProperty("assemblies").GetArrayLength());
        Assert.Equal(539, types.Length);
        Assert.Equal(135, types.Count(type =>
            type.GetProperty("compatibility").GetString() == "2.0-candidate"));
        Assert.DoesNotContain(types, type =>
            type.GetProperty("assembly").GetString() == "Bukit.Engine" &&
            type.GetProperty("name").GetString() == RemovedTypeName);
    }

    [Fact]
    public void ClosedCandidateManifest_PreservesTheHistoricalPilotEvidence()
    {
        using var document = ReadJson("docs", "governance", "bukit-core-2.0-public-surface-candidates.v1.json");
        var root = document.RootElement;
        var candidates = root.GetProperty("candidates").EnumerateArray().ToArray();
        var target = Assert.Single(candidates, candidate =>
            candidate.GetProperty("fullName").GetString() == RemovedTypeName);

        Assert.Equal(136, root.GetProperty("candidateCount").GetInt32());
        Assert.Equal(136, candidates.Length);
        Assert.Equal("closed", root.GetProperty("declarationState").GetString());
        Assert.Equal("consumer-declaration-pending", target.GetProperty("declarationStatus").GetString());
        Assert.Equal("unknown-until-voluntary-declaration", target.GetProperty("privateConsumerStatus").GetString());
        Assert.Equal("no-public-match-found", target.GetProperty("externalEvidence").GetProperty("searchStatus").GetString());

        var queries = target.GetProperty("externalEvidence").GetProperty("queries").EnumerateArray().ToArray();
        Assert.Equal(2, queries.Length);
        Assert.All(queries, query =>
        {
            Assert.Equal(0, query.GetProperty("returned").GetInt32());
            Assert.False(query.GetProperty("truncated").GetBoolean());
        });
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

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
