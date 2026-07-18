namespace Bukit.Engine;

internal sealed class DefaultSearchIndexBuilder : ISearchIndexBuilder
{
    public void GenerateMergedSearchIndex(string outputDir, IReadOnlyList<BuildVariantResult> results, bool includeDerived, int maxContentLength)
        => SearchIndexBuilder.GenerateMergedSearchIndex(outputDir, results, includeDerived, maxContentLength);

    public void GenerateSearchIndexIndex(string outputDir, IReadOnlyList<BuildVariantResult> results)
        => SearchIndexBuilder.GenerateSearchIndexIndex(outputDir, results);
}
