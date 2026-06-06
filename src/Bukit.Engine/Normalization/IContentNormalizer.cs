using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine.Normalization;

public interface IContentNormalizer
{
    ContentDocument Normalize(RawContentDocument raw, ContentModelSchema schema);
}
