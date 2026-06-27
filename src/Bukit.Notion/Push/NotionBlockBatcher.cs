using Bukit.Notion.Client;

namespace Bukit.Notion.Push;

internal static class NotionBlockBatcher
{
    internal const int MaximumChildrenPerRequest = 100;

    public static IEnumerable<IReadOnlyList<NotionBlock>> Batch(IReadOnlyList<NotionBlock> blocks)
    {
        for (int offset = 0; offset < blocks.Count; offset += MaximumChildrenPerRequest)
        {
            int count = Math.Min(MaximumChildrenPerRequest, blocks.Count - offset);
            var batch = new NotionBlock[count];
            for (int index = 0; index < count; index++)
            {
                batch[index] = blocks[offset + index];
            }

            yield return batch;
        }
    }
}
