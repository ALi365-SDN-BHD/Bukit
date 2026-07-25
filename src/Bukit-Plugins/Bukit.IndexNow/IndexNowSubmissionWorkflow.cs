using System.Text.Json;

namespace Bukit.IndexNow;

public sealed class IndexNowSubmissionWorkflow
{
    public const string PluginId = "indexnow";
    public const string Version = "0.1.0";
    private const int MaximumBatchSize = 10_000;
    private const int MaximumAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IIndexNowTransport _transport;
    private readonly IIndexNowRetryDelay _delay;
    private readonly IndexNowStateStore _stateStore;

    public IndexNowSubmissionWorkflow(
        IIndexNowTransport? transport = null,
        IIndexNowRetryDelay? delay = null,
        IndexNowStateStore? stateStore = null)
    {
        _transport = transport ?? new IndexNowHttpClient();
        _delay = delay ?? new IndexNowRetryDelay();
        _stateStore = stateStore ?? new IndexNowStateStore();
    }

    public async Task<IndexNowSubmissionResult> RunAsync(
        IndexNowSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<IndexNowDiagnostic>();
        try
        {
            ValidateRequestPaths(request);
            var siteUrl = IndexNowUrlPolicy.ParseSiteUrl(request.SiteUrl.AbsoluteUri);
            var snapshot = await ReadAsync<PublishUrlSnapshotDocument>(request.SnapshotPath, cancellationToken);
            var changeSet = await ReadAsync<PublishUrlChangeSetDocument>(request.ChangeSetPath, cancellationToken);
            ValidateSnapshot(snapshot, siteUrl);
            var changes = ValidateChanges(changeSet, snapshot);

            if (request.DryRun)
            {
                return new IndexNowSubmissionResult(true, changes.Count, 0, 0, diagnostics);
            }

            if (string.IsNullOrWhiteSpace(request.Key))
            {
                throw new InvalidOperationException("INDEXNOW_KEY is required.");
            }

            var statePath = Path.Combine(request.StateDir, "state.json");
            await using var runLock = await _stateStore.AcquireRunLockAsync(
                statePath,
                TimeSpan.FromSeconds(30),
                cancellationToken);
            var state = await _stateStore.LoadAsync(statePath, cancellationToken);
            changes = changes.Concat(state.Pending)
                .GroupBy(IndexNowStateStore.Fingerprint, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(change => change.Url, StringComparer.Ordinal)
                .ThenBy(change => change.Type, StringComparer.Ordinal)
                .ToArray();

            var actionable = changes
                .Where(change => !state.Notified.TryGetValue(change.Url, out var notified) ||
                                 !string.Equals(notified, IndexNowStateStore.Fingerprint(change), StringComparison.Ordinal))
                .ToArray();
            if (actionable.Length == 0)
            {
                return new IndexNowSubmissionResult(true, state.Deployed.Count, 0, state.Pending.Count, diagnostics);
            }

            foreach (var change in actionable)
            {
                try
                {
                    if (!await PreflightAsync(change, cancellationToken))
                    {
                        diagnostics.Add(new IndexNowDiagnostic(
                            "plugin.indexnow.preflightFailed",
                            "error",
                            $"Online deployment verification failed for {change.Url}."));
                    }
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or
                    TaskCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    diagnostics.Add(new IndexNowDiagnostic(
                        "plugin.indexnow.preflightUnavailable",
                        "error",
                        $"Online deployment verification was unavailable for {change.Url}."));
                }
            }

            if (diagnostics.Count > 0)
            {
                return new IndexNowSubmissionResult(false, state.Deployed.Count, 0, state.Pending.Count, diagnostics);
            }

            _ = IndexNowKeyFileWriter.Write(request.OutputRoot, request.Key);
            var deployed = state.Deployed.Concat(actionable.Select(change => change.Url))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            state = state with { Deployed = deployed };
            await _stateStore.SaveAsync(statePath, state, cancellationToken);

            var totalNotified = 0;
            var overallSuccess = true;
            foreach (var batch in actionable.Chunk(MaximumBatchSize))
            {
                var outcome = await SubmitBatchAsync(siteUrl, request.Key, batch, cancellationToken);
                var notified = new Dictionary<string, string>(state.Notified, StringComparer.Ordinal);
                var pending = state.Pending.ToList();
                foreach (var change in batch)
                {
                    pending.RemoveAll(item => IndexNowStateStore.Fingerprint(item) == IndexNowStateStore.Fingerprint(change));
                }

                if (outcome == SubmissionOutcome.Received)
                {
                    foreach (var change in batch)
                    {
                        notified[change.Url] = IndexNowStateStore.Fingerprint(change);
                    }

                    totalNotified += batch.Length;
                }
                else if (outcome == SubmissionOutcome.Pending)
                {
                    pending.AddRange(batch);
                    overallSuccess = false;
                    diagnostics.Add(new IndexNowDiagnostic(
                        "plugin.indexnow.pending",
                        "warning",
                        $"{batch.Length} URL(s) remain pending after a temporary IndexNow failure."));
                }
                else
                {
                    overallSuccess = false;
                    diagnostics.Add(new IndexNowDiagnostic(
                        "plugin.indexnow.terminal",
                        "error",
                        $"IndexNow rejected a batch of {batch.Length} URL(s)."));
                }

                state = state with { Notified = notified, Pending = pending };
                await _stateStore.SaveAsync(statePath, state, cancellationToken);
            }

            return new IndexNowSubmissionResult(
                overallSuccess,
                state.Deployed.Count,
                totalNotified,
                state.Pending.Count,
                diagnostics);
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or JsonException)
        {
            diagnostics.Add(new IndexNowDiagnostic("plugin.indexnow.invalidInput", "error", exception.Message));
            return new IndexNowSubmissionResult(false, 0, 0, 0, diagnostics);
        }
    }

    private async Task<bool> PreflightAsync(IndexNowPendingChange change, CancellationToken cancellationToken)
    {
        var uri = IndexNowUrlPolicy.ParseContentUrl(change.Url);
        var response = await _transport.GetPageAsync(uri, cancellationToken);
        if (change.Type is "added" or "updated")
        {
            return response.StatusCode == 200 &&
                   response.CanonicalUrl is not null &&
                   string.Equals(
                       IndexNowUrlPolicy.ParseContentUrl(response.CanonicalUrl).AbsoluteUri,
                       uri.AbsoluteUri,
                       StringComparison.Ordinal);
        }

        return response.StatusCode is 404 or 410;
    }

    private async Task<SubmissionOutcome> SubmitBatchAsync(
        Uri siteUrl,
        string key,
        IndexNowPendingChange[] changes,
        CancellationToken cancellationToken)
    {
        var payload = new IndexNowSubmissionPayload(
            IndexNowUrlPolicy.AllowedHost,
            key,
            new Uri(siteUrl, key + ".txt").AbsoluteUri,
            changes.Select(change => change.Url).ToArray());
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                var response = await _transport.SubmitAsync(payload, cancellationToken);
                if (response.StatusCode is 200 or 202)
                {
                    return SubmissionOutcome.Received;
                }

                if (response.StatusCode == 429 && attempt < MaximumAttempts)
                {
                    await _delay.DelayAsync(TimeSpan.FromSeconds(attempt), cancellationToken);
                    continue;
                }

                if (response.StatusCode == 429 || response.StatusCode >= 500)
                {
                    return SubmissionOutcome.Pending;
                }

                return SubmissionOutcome.Terminal;
            }
            catch (HttpRequestException)
            {
                return SubmissionOutcome.Pending;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return SubmissionOutcome.Pending;
            }
        }

        return SubmissionOutcome.Pending;
    }

    private static void ValidateRequestPaths(IndexNowSubmissionRequest request)
    {
        var outputRoot = Path.GetFullPath(request.OutputRoot);
        var expectedSnapshot = Path.Combine(outputRoot, ".bukit", "publish-url-snapshot.json");
        if (!string.Equals(Path.GetFullPath(request.SnapshotPath), expectedSnapshot, PathComparison))
        {
            throw new InvalidOperationException("Snapshot must use <output>/.bukit/publish-url-snapshot.json.");
        }

        if (!File.Exists(request.SnapshotPath) || !File.Exists(request.ChangeSetPath))
        {
            throw new InvalidOperationException("Snapshot and change-set files must exist.");
        }
    }

    private static void ValidateSnapshot(PublishUrlSnapshotDocument snapshot, Uri siteUrl)
    {
        if (!string.Equals(snapshot.Schema, "https://bukit.dev/schemas/publish-url-snapshot.v1.json", StringComparison.Ordinal) ||
            !string.Equals(IndexNowUrlPolicy.ParseSiteUrl(snapshot.SiteUrl), siteUrl))
        {
            throw new InvalidOperationException("Snapshot schema or siteUrl is invalid.");
        }

        foreach (var route in snapshot.Routes)
        {
            _ = IndexNowUrlPolicy.ParseContentUrl(route.Url);
        }
    }

    private static IReadOnlyList<IndexNowPendingChange> ValidateChanges(
        PublishUrlChangeSetDocument changeSet,
        PublishUrlSnapshotDocument snapshot)
    {
        var routes = snapshot.Routes.Where(route => route.Indexable)
            .GroupBy(route => route.Url, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var changes = new List<IndexNowPendingChange>();
        foreach (var change in changeSet.Changes)
        {
            if (change.Type is not ("added" or "updated" or "deleted"))
            {
                throw new InvalidOperationException("Change type must be added, updated, or deleted.");
            }

            _ = IndexNowUrlPolicy.ParseContentUrl(change.Url);
            if (change.Type is "added" or "updated")
            {
                if (!routes.ContainsKey(change.Url))
                {
                    throw new InvalidOperationException("Added or updated change must match the candidate snapshot.");
                }
            }

            changes.Add(change);
        }

        return changes.GroupBy(IndexNowStateStore.Fingerprint, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(change => change.Url, StringComparer.Ordinal)
            .ThenBy(change => change.Type, StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidDataException($"IndexNow input '{Path.GetFileName(path)}' is empty.");
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private enum SubmissionOutcome
    {
        Received,
        Pending,
        Terminal
    }
}
