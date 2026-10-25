namespace Bukit.Engine.Plugins.BuiltIn;

/// <summary>
/// Resource limits for one data-files load: directory traversal, file bytes,
/// decoded characters, document shape and projected output size.
/// </summary>
internal sealed record DataFileLimits(
    int MaxEntries,
    int MaxDepth,
    long MaxFileSizeBytes,
    long MaxTotalSizeBytes,
    int MaxDocumentNodes,
    int MaxDocumentDepth,
    long MaxScalarChars,
    long MaxProjectedChars,
    int MaxProjectedEntries,
    long MaxDecodedChars)
{
    internal static DataFileLimits Create(
        int maxEntries,
        int maxDepth,
        long maxFileSizeBytes,
        long maxTotalSizeBytes,
        int maxDocumentNodes,
        int maxDocumentDepth,
        long maxProjectedChars,
        int maxProjectedEntries,
        long? maxDecodedChars,
        long? maxScalarChars)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDocumentNodes);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDocumentDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxProjectedChars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxProjectedEntries);
        var resolvedMaxDecodedChars = maxDecodedChars
            ?? Math.Min(maxFileSizeBytes, int.MaxValue);
        var resolvedMaxScalarChars = maxScalarChars ?? resolvedMaxDecodedChars;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolvedMaxDecodedChars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolvedMaxScalarChars);
        if (resolvedMaxScalarChars > resolvedMaxDecodedChars)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxScalarChars),
                "The scalar character limit cannot exceed the decoded character limit.");
        }

        return new DataFileLimits(
            maxEntries,
            maxDepth,
            maxFileSizeBytes,
            maxTotalSizeBytes,
            maxDocumentNodes,
            maxDocumentDepth,
            resolvedMaxScalarChars,
            maxProjectedChars,
            maxProjectedEntries,
            resolvedMaxDecodedChars);
    }
}
