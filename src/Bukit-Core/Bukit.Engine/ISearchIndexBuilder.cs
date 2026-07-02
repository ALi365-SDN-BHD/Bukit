namespace Bukit.Engine;

internal interface ISearchIndexBuilder
{
    void GenerateMergedSearchIndex(string outputDir, IReadOnlyList<BuildVariantResult> results, bool includeDerived);
    void GenerateSearchIndexIndex(string outputDir, IReadOnlyList<BuildVariantResult> results);
}
