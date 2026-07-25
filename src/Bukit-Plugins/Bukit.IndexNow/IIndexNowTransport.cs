namespace Bukit.IndexNow;

public interface IIndexNowTransport
{
    Task<IndexNowPageResponse> GetPageAsync(Uri url, CancellationToken cancellationToken);

    Task<IndexNowSubmitResponse> SubmitAsync(
        IndexNowSubmissionPayload payload,
        CancellationToken cancellationToken);
}

public interface IIndexNowRetryDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

internal sealed class IndexNowRetryDelay : IIndexNowRetryDelay
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}
