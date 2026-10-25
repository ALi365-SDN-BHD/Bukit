using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class BuildContextDataTests
{
    private static readonly BuildDataKey<List<string>> ListKey = new("test_list");
    private static readonly BuildDataKey<string> TextKey = new("__test_text");

    [Fact]
    public void Key_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => new BuildDataKey<string>(" "));
    }

    [Fact]
    public void SetThenGet_ReturnsSameInstance()
    {
        var context = CreateContext();
        var value = new List<string> { "a" };

        context.Set(ListKey, value);

        Assert.Same(value, context.Get(ListKey));
        Assert.True(context.TryGet(ListKey, out var read));
        Assert.Same(value, read);
    }

    [Fact]
    public void Get_Missing_ThrowsAndTryGetReturnsFalse()
    {
        var context = CreateContext();

        Assert.Throws<KeyNotFoundException>(() => context.Get(TextKey));
        Assert.False(context.TryGet(TextKey, out _));
    }

    [Fact]
    public void Names_AreCaseInsensitive()
    {
        var context = CreateContext();
        context.Set(TextKey, "value");

        Assert.Equal("value", context.Get(new BuildDataKey<string>("__TEST_TEXT")));
    }

    [Fact]
    public void TypeMismatch_OnReadOrWrite_Throws()
    {
        var context = CreateContext();
        context.Set(TextKey, "value");
        var conflicting = new BuildDataKey<List<string>>(TextKey.Name);

        Assert.Throws<InvalidOperationException>(() => context.TryGet(conflicting, out _));
        Assert.Throws<InvalidOperationException>(() => context.Set(conflicting, new List<string>()));
        Assert.Equal("value", context.Get(TextKey));
    }

    [Fact]
    public void Remove_DeletesValue()
    {
        var context = CreateContext();
        context.Set(TextKey, "value");

        Assert.True(context.Remove(TextKey));
        Assert.False(context.Remove(TextKey));
        Assert.False(context.TryGet(TextKey, out _));
    }

    [Fact]
    public void GetOrAdd_CreatesOnceUnderConcurrency()
    {
        var context = CreateContext();
        var created = 0;

        var results = Enumerable.Range(0, 64)
            .AsParallel()
            .Select(_ => context.GetOrAdd(ListKey, () =>
            {
                Interlocked.Increment(ref created);
                return new List<string>();
            }))
            .ToArray();

        Assert.Equal(1, created);
        Assert.All(results, result => Assert.Same(results[0], result));
    }

    [Fact]
    public void SnapshotData_IsReadOnlyCopyByName()
    {
        var context = CreateContext();
        context.Set(TextKey, "value");

        var snapshot = context.SnapshotData();
        context.Set(TextKey, "changed");

        Assert.Equal("value", snapshot["__TEST_TEXT"]);
    }

#pragma warning disable CS0618 // The obsolete untyped view must stay in sync with the typed API.
    [Fact]
    public void LegacyDataView_SharesStoreWithTypedApi()
    {
        var context = CreateContext();
        context.Set(TextKey, "typed");
        context.Data["test_list"] = new List<string> { "legacy" };

        Assert.Equal("typed", context.Data["__test_text"]);
        Assert.Equal("legacy", Assert.Single(context.Get(ListKey)));
    }
#pragma warning restore CS0618

    private static BuildContext CreateContext() => new()
    {
        RootDir = "/test",
        OutputDir = "/test/dist",
        BaseUrl = "/",
        LayoutsDir = "/test/layouts",
        RoutedDocuments = [],
        Logger = new ConsoleLogger(LogLevel.Error)
    };
}
