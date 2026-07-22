using Bukit.Engine;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class G04CPublicSurfacePilotTests
{
    private const string RemovedTypeName = "Bukit.Engine.RouteInventoryInspectEntry";

    [Fact]
    public void EngineAssembly_DoesNotExposeRemovedRouteInventoryInspectEntry()
    {
        var engineAssembly = typeof(RouteInventoryValidator).Assembly;

        Assert.Null(engineAssembly.GetType(RemovedTypeName, throwOnError: false, ignoreCase: false));
    }
}
