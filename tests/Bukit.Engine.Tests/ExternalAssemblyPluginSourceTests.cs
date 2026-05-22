// DESKTOP-REMOVED: ExternalAssemblyPluginSource disabled (AOT-only).
#if false
using Bukit.Engine.Plugins;
using Bukit.Shared;
using Xunit;

namespace Bukit.Engine.Tests;

public sealed class ExternalAssemblyPluginSourceTests
{
    [Fact]
    public void GetPlugins_RegistersResolvingHandlerOnlyOnce()
    {
        using var temp = new TempDir();
        var source = new ExternalAssemblyPluginSource(
            temp.Path,
            new ConsoleLogger(LogLevel.Error),
            new Bukit.Config.SiteConfig { Name = "t", Title = "t" });

        ExternalAssemblyPluginSource.ResetResolvingRegistrationForTests();
        _ = source.GetPlugins().ToList();
        _ = source.GetPlugins().ToList();

        Assert.Equal(1, ExternalAssemblyPluginSource.ResolvingHandlerRegistrationCount);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bukit-tests", Guid.NewGuid().ToString("N"));

        public TempDir()
        {
            Directory.CreateDirectory(Path);
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "plugins"));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
#endif
