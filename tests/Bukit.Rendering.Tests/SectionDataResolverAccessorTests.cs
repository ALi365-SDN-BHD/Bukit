using Bukit.Rendering.Scriban;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class SectionDataResolverAccessorTests
{
    [Fact]
    public void Constructor_DefaultPropertiesAreNull()
    {
        var accessor = new SectionDataResolverAccessor();
        Assert.Null(accessor.AllItems);
        Assert.Null(accessor.Registry);
    }

    [Fact]
    public void AllItems_CanBeSetAndRead()
    {
        var accessor = new SectionDataResolverAccessor();
        var items = new List<Bukit.Content.ContentItem>();
        accessor.AllItems = items;
        Assert.NotNull(accessor.AllItems);
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
