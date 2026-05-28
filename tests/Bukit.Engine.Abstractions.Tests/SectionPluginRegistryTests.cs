using Bukit.Engine.Abstractions.Plugins;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class SectionPluginRegistryTests
{
    [Fact]
    public void Register_NewPlugin_Succeeds()
    {
        var plugin = new TestSectionPlugin("test", SectionHook.BeforeRender);
        SectionPluginRegistry.Register("test-plugin", plugin);
        Assert.True(SectionPluginRegistry.TryResolve("test-plugin", out var resolved));
        Assert.Same(plugin, resolved);
    }

    [Fact]
    public void Register_Duplicate_Throws()
    {
        var plugin1 = new TestSectionPlugin("p1", SectionHook.BeforeRender);
        var plugin2 = new TestSectionPlugin("p2", SectionHook.BeforeRender);
        SectionPluginRegistry.Register("dup-test", plugin1);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            SectionPluginRegistry.Register("dup-test", plugin2));
        Assert.Contains("dup-test", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryResolve_NotFound_ReturnsFalse()
    {
        Assert.False(SectionPluginRegistry.TryResolve("nonexistent", out var plugin));
        Assert.Null(plugin);
    }

    [Fact]
    public void GetAll_ReturnsRegisteredPlugins()
    {
        var plugin = new TestSectionPlugin("getall", SectionHook.BeforeRender);
        SectionPluginRegistry.Register("getall-test", plugin);

        var all = SectionPluginRegistry.GetAll();
        Assert.Contains("getall-test", all.Keys);
    }

    private sealed class TestSectionPlugin : ISectionPlugin
    {
        private readonly string _name;

        public TestSectionPlugin(string name, SectionHook hook)
        {
            _name = name;
            SupportedHook = hook;
        }

        public SectionHook SupportedHook { get; }

        public Task ExecuteAsync(SectionContext context, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public override string ToString() => _name;
    }
}
