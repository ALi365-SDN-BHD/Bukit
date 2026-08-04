using Bukit.Engine.Abstractions.Content;
namespace Bukit.Content;

internal sealed class CompositeContentBodyStore : IContentBodyStore, IAsyncDisposable
{
    internal const string TokenPrefix = "__bukit.cbs[";

    private readonly IReadOnlyDictionary<string, IContentBodyStore> _stores;
    private readonly IReadOnlyDictionary<string, IContentBodyStore>? _storesByToken;
    private readonly IReadOnlyList<IContentBodyStore> _allStores;

    public CompositeContentBodyStore(IReadOnlyDictionary<string, IContentBodyStore> stores)
    {
        _stores = stores;
        _storesByToken = null;
        _allStores = stores.Values.ToArray();
    }

    /// <summary>
    /// Token-routed construction: each provider receives a stable opaque route token so
    /// duplicate source keys keep distinct body stores. Public document identity is not
    /// affected; only the internal BodyKey carries the token.
    /// </summary>
    internal CompositeContentBodyStore(IReadOnlyList<(string SourceKey, IContentBodyStore Store)> orderedStores)
    {
        var bySource = new Dictionary<string, IContentBodyStore>(StringComparer.OrdinalIgnoreCase);
        var byToken = new Dictionary<string, IContentBodyStore>(StringComparer.Ordinal);
        for (var i = 0; i < orderedStores.Count; i++)
        {
            bySource[orderedStores[i].SourceKey] = orderedStores[i].Store;
            byToken[MakeToken(i)] = orderedStores[i].Store;
        }

        _stores = bySource;
        _storesByToken = byToken;
        _allStores = orderedStores.Select(entry => entry.Store).ToArray();
    }

    internal static string MakeToken(int providerIndex) => $"{TokenPrefix}{providerIndex}]";

    internal static string PrefixBodyKey(int providerIndex, string? bodyKey)
        => $"{MakeToken(providerIndex)}:{bodyKey ?? string.Empty}";

    public Task<ContentBody> GetAsync(ContentDocument document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(document.Body.Html))
        {
            return Task.FromResult(new ContentBody(document.Body.Html));
        }

        var bodyKey = document.Body.BodyKey;
        IContentBodyStore store;
        string? routedBodyKey;

        if (bodyKey is not null && bodyKey.StartsWith(TokenPrefix, StringComparison.Ordinal) && _storesByToken is not null)
        {
            var separatorIndex = bodyKey.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"Malformed composite body token in '{bodyKey}'.");
            }

            var token = bodyKey[..separatorIndex];
            if (!_storesByToken.TryGetValue(token, out var tokenStore))
            {
                throw new InvalidOperationException($"No content body store registered for token '{token}'.");
            }

            store = tokenStore;
            // Strip the internal token so the child store sees the original BodyKey.
            routedBodyKey = bodyKey[(separatorIndex + 1)..];
        }
        else
        {
            var separatorIndex = document.Id.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"Unable to resolve content body store for document '{document.Id}'.");
            }

            var sourceKey = document.Id[..separatorIndex];
            if (!_stores.TryGetValue(sourceKey, out var sourceStore))
            {
                throw new InvalidOperationException($"No content body store registered for source '{sourceKey}'.");
            }

            store = sourceStore;
            routedBodyKey = bodyKey;
        }

        var routedDocument = document with
        {
            Body = document.Body with { BodyKey = routedBodyKey }
        };

        var sourceId = ContentFieldReader.GetText(document.CustomFields, "sourceId");
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            var sourceKeyPrefix = routedDocument.Id[..Math.Max(routedDocument.Id.IndexOf(':'), 0)];
            var originalBodyKey = routedBodyKey is not null &&
                !string.IsNullOrEmpty(sourceKeyPrefix) &&
                routedBodyKey.StartsWith(sourceKeyPrefix + ":", StringComparison.Ordinal)
                    ? routedBodyKey.Substring(sourceKeyPrefix.Length + 1)
                    : routedBodyKey;
            var sourceDocument = routedDocument with
            {
                Record = routedDocument.Record with { Identity = routedDocument.Record.Identity with { Id = sourceId } },
                Body = routedDocument.Body with { BodyKey = originalBodyKey }
            };
            return store.GetAsync(sourceDocument, cancellationToken);
        }

        return store.GetAsync(routedDocument, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var disposed = new HashSet<IContentBodyStore>(ReferenceEqualityComparer.Instance);
        foreach (var store in _allStores)
        {
            if (!disposed.Add(store))
            {
                continue;
            }

            if (store is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (store is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
