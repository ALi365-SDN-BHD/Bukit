namespace Bukit.Engine;

internal sealed class DefaultSearchIndexBuilder : ISearchIndexBuilder
{
    public void GenerateMergedSearchIndex(string outputDir, IReadOnlyList<BuildVariantResult> results, bool includeDerived)
        => SearchIndexBuilder.GenerateMergedSearchIndex(outputDir, results, includeDerived);

    public void GenerateSearchIndexIndex(string outputDir, IReadOnlyList<BuildVariantResult> results)
        => SearchIndexBuilder.GenerateSearchIndexIndex(outputDir, results);
}
