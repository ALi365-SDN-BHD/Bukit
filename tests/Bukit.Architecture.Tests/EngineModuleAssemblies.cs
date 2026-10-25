using System.Reflection;

namespace Bukit.Architecture.Tests;

/// <summary>
/// The assemblies split out of Bukit.Engine. Types keep their original
/// <c>Bukit.Engine.*</c> namespaces, so governance tests that look a type up by
/// full name search the whole family instead of the Engine assembly alone.
/// </summary>
internal static class EngineModuleAssemblies
{
    internal static readonly Assembly Engine = typeof(Bukit.Engine.SiteEngine).Assembly;
    internal static readonly Assembly Primitives = typeof(Bukit.Engine.BuildPathUtils).Assembly;

    internal static readonly Assembly Feeds = typeof(Bukit.Engine.RssGenerator).Assembly;

    internal static readonly Assembly Analytics = Assembly.Load("Bukit.Engine.Analytics");

    internal static readonly Assembly[] All = [Engine, Primitives, Feeds, Analytics];

    internal static Type GetType(string fullName)
    {
        Type[] matches = All
            .Select(assembly => assembly.GetType(fullName, throwOnError: false, ignoreCase: false))
            .OfType<Type>()
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new TypeLoadException($"{fullName} was not found in any Engine module assembly."),
            _ => throw new InvalidOperationException($"{fullName} is defined in more than one Engine module assembly.")
        };
    }

    internal static Type[] GetExportedTypes() =>
        All.SelectMany(assembly => assembly.GetExportedTypes()).ToArray();
}
