// DESKTOP-REMOVED: [BukitPlugin] attribute is no longer used (AOT-only, no source generation).
// Plugins should use the external protocol (process) instead.
#if false
namespace Bukit.Engine.Abstractions.Plugins;

[System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class BukitPluginAttribute : System.Attribute
{
}
#endif
