using Bukit.Engine.Abstractions.Content;
using System.Diagnostics;
using System.Text.Json;
using Bukit.Shared;
using Bukit.Shared.Notion;
namespace Bukit.Content.Notion;

internal static class NotionRelationResolver
{
    internal static async Task<IReadOnlyDictionary<string, RelationTargetInfo>> ResolveMissingTaxonomyRelationTargetsAsync(
        NotionContentClient client,
        IReadOnlyList<NotionContentSource.PageDraft> drafts,
        IReadOnlyDictionary<string, RelationTargetInfo> existingIndex,
        NotionRelationTargetCache? relationTargetCache,
        int renderConcurrency,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var planStopwatch = Stopwatch.StartNew();
        var maxResolve = 200;
        var candidates = drafts.Select(static d => new NotionRelationResolveCandidate(d.RelationKeys, d.Fields));
        var missing = NotionRelationResolvePlan.BuildMissingIds(candidates, existingIndex, maxResolve);
        planStopwatch.Stop();
        logger?.Info($"event=notion.relation.plan candidates={drafts.Count} missing={missing.Count} max_resolve={maxResolve} plan_ms={planStopwatch.ElapsedMilliseconds}");

        if (missing.Count == 0)
        {
            return existingIndex;
        }

        var concurrency = renderConcurrency is > 0 ? renderConcurrency : 4;
        using var sem = new SemaphoreSlim(concurrency, concurrency);
        var tasks = new Task<RelationTargetInfo?>[missing.Count];
        var resolveStopwatch = Stopwatch.StartNew();
        var cacheHits = 0;
        for (var i = 0; i < missing.Count; i++)
        {
            var pageId = missing[i];
            if (relationTargetCache is not null)
            {
                var cached = await relationTargetCache.TryReadAsync(pageId, cancellationToken);
                if (cached is not null)
                {
                    tasks[i] = Task.FromResult<RelationTargetInfo?>(cached);
                    cacheHits++;
                    continue;
                }
            }

            tasks[i] = ResolveOneAsync(pageId);
        }

        await Task.WhenAll(tasks);
        resolveStopwatch.Stop();

        Dictionary<string, RelationTargetInfo>? merged = null;
        var resolvedCount = 0;
        for (var i = 0; i < tasks.Length; i++)
        {
            var t = await tasks[i];
            if (t is null)
            {
                continue;
            }

            merged ??= new Dictionary<string, RelationTargetInfo>(existingIndex, StringComparer.OrdinalIgnoreCase);
            merged[t.PageId] = t;
            resolvedCount++;
        }

        logger?.Info($"event=notion.relation.resolve requested={missing.Count} resolved={resolvedCount} cache_hits={cacheHits} concurrency={concurrency} resolve_ms={resolveStopwatch.ElapsedMilliseconds}");

        return merged ?? existingIndex;

        async Task<RelationTargetInfo?> ResolveOneAsync(string pageId)
        {
            await sem.WaitAsync(cancellationToken);
            try
            {
                using var doc = await client.GetAsync(NotionApiUrls.Pages(pageId), cancellationToken);
                var page = doc.RootElement;
                var props = page.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object ? p : default;
                var title = NotionContentPropertyParser.ExtractTitle(props) ?? pageId;
                var slug = NotionContentPropertyParser.ExtractSlug(props) ?? NotionContentSource.Slugify(title) ?? pageId.Replace("-", string.Empty, StringComparison.Ordinal);
                var type = NotionContentPropertyParser.ExtractType(props) ?? string.Empty;

                var url = NotionContentSource.GetString(page, "url");
                url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();

                var target = new RelationTargetInfo(pageId, title, slug, type, url);
                if (relationTargetCache is not null)
                {
                    await relationTargetCache.WriteAsync(target, cancellationToken);
                }

                return target;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.Warn($"event=notion.relation.resolve_failed pageId={pageId} message={ex.Message}");
                return null;
            }
            finally
            {
                sem.Release();
            }
        }
    }
}
