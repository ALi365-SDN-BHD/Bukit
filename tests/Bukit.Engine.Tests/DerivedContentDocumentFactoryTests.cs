using Bukit.Engine.Abstractions.Content;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class DerivedContentDocumentFactoryTests
{
    [Fact]
    public void Create_WritesTheBuildLanguage_UnlessTheFieldsSetOne()
    {
        var stamped = DerivedContentDocumentFactory.Create("a", "A", "a", DateTimeOffset.UnixEpoch, new ContentBodyRef(Html: ""), language: " en ");
        var declared = DerivedContentDocumentFactory.Create(
            "b", "B", "b", DateTimeOffset.UnixEpoch, new ContentBodyRef(Html: ""),
            new Dictionary<string, ContentField> { ["language"] = new("text", "zh-CN") },
            language: "en");
        var none = DerivedContentDocumentFactory.Create("c", "C", "c", DateTimeOffset.UnixEpoch, new ContentBodyRef(Html: ""));

        Assert.Equal("en", stamped.Record.Presentation.Language);
        Assert.Equal("zh-CN", declared.Record.Presentation.Language);
        Assert.Equal("und", none.Record.Presentation.Language);
    }
}
