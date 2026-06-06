using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class SectionDataResolverAccessorTests
{
    [Fact]
    public void Constructor_DefaultPropertiesAreNull()
    {
        var accessor = new SectionDataResolverAccessor();
        Assert.Null(accessor.AllDocuments);
        Assert.Null(accessor.Registry);
    }

    [Fact]
    public void AllDocuments_CanBeSetAndRead()
    {
        var accessor = new SectionDataResolverAccessor();
        var documents = new List<Bukit.Engine.Abstractions.Content.ContentDocument>();
        accessor.AllDocuments = documents;
        Assert.NotNull(accessor.AllDocuments);
    }

    [Fact]
    public void Registry_CanBeSetAndRead()
    {
        var accessor = new SectionDataResolverAccessor();
        accessor.Registry = null!;
        Assert.Null(accessor.Registry);
    }

    [Fact]
    public void ResolveData_ReturnsNull()
    {
        var accessor = new SectionDataResolverAccessor();
        Assert.Null(accessor.ResolveData(null!));
    }
}
