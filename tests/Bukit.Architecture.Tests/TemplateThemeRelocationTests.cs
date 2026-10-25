using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class TemplateThemeRelocationTests
{
    private static readonly Assembly Rendering = typeof(Bukit.Rendering.SiteModel).Assembly;
    private static readonly Assembly Theme = typeof(Bukit.Theme.ThemeManifestV2).Assembly;
    private static readonly Assembly Engine = typeof(Bukit.Engine.SiteEngine).Assembly;
    private static readonly Assembly Shared = typeof(Bukit.Shared.ILogger).Assembly;
    private static readonly Assembly Config = typeof(Bukit.Config.ConfigValidator).Assembly;

    [Theory]
    [InlineData("Bukit.Rendering.ITemplateRenderer", true)]
    [InlineData("Bukit.Rendering.Scriban.ScribanTemplateLinter", true)]
    [InlineData("Bukit.Rendering.Scriban.TemplateVariableWarning", true)]
    [InlineData("Bukit.Rendering.Scriban.ScribanLayoutDirectiveParser", false)]
    [InlineData("Bukit.Rendering.Scriban.ShortcodeProcessor", false)]
    [InlineData("Bukit.Rendering.Scriban.ScribanSymbolAnalyzer", false)]
    [InlineData("Bukit.Rendering.Scriban.ScribanModelKnownFields", false)]
    [InlineData("Bukit.Rendering.Scriban.ScribanTemplateContextContract", false)]
    public void Rendering_OwnsTemplateTypes(string name, bool isPublic)
    {
        Type? type = Rendering.GetType(name);
        Assert.NotNull(type);
        Assert.Equal(isPublic, type.IsPublic);
    }

    [Theory]
    [InlineData("ThemeTemplateResolver", true)]
    [InlineData("ThemeBootstrapper", true)]
    [InlineData("ThemeBootstrapResult", true)]
    [InlineData("ThemePathResolver", false)]
    [InlineData("ResolvedThemePaths", false)]
    [InlineData("ThemeNameSanitizer", false)]
    [InlineData("ThemeManifestYamlValidator", true)]
    [InlineData("ThemeManifestStrictValidator", false)]
    [InlineData("ThemeDoctor", true)]
    public void Theme_OwnsThemeTypes(string name, bool isPublic)
    {
        Type? type = Theme.GetType("Bukit.Theme." + name);
        Assert.NotNull(type);
        Assert.Equal(isPublic, type.IsPublic);
    }

    [Fact]
    public void FormerOwners_DoNotRetainDefinitions()
    {
        Assert.DoesNotContain(Shared.GetTypes(), t => t.Name is "ScribanLayoutDirectiveParser" or "ShortcodeProcessor");
        Assert.DoesNotContain(Config.GetTypes(), t => t.Name == "ThemeManifestStrictValidator");
        Assert.Null(typeof(Bukit.Config.ConfigValidator).GetMethod("ValidateThemeYaml"));
        string[] moved = ["ITemplateRenderer", "TemplateRendererBase", "ScribanTemplateLinter", "TemplateVariableWarning",
            "ScribanSymbolAnalyzer", "ScribanTemplateContextContract", "ScribanModelKnownFields", "ThemeTemplateResolver",
            "ThemeBootstrapper", "ThemeBootstrapResult", "ThemePathResolver", "ResolvedThemePaths", "ThemeNameSanitizer"];
        Assert.DoesNotContain(Engine.GetTypes(), t => moved.Contains(t.Name, StringComparer.Ordinal));
        Assert.Null(Theme.GetType("Bukit.Theme.ThemeDoctorCommand"));
    }

    [Fact]
    public void Theme_RemainsIndependentOfEngineAndPrimitives()
    {
        Assert.DoesNotContain(Theme.GetReferencedAssemblies(), a => a.Name is "Bukit.Engine" or "Bukit.Engine.Primitives");
        Assert.Equal(["Bukit.Engine", "Bukit.Theme.Tests"], Friends(Theme));
        Assert.Equal(["Bukit.Engine", "Bukit.Engine.Audit", "Bukit.Rendering.Tests"], Friends(Rendering));
        Assert.Contains("Bukit.Theme", Friends(typeof(Bukit.Engine.Abstractions.Content.ContentDocument).Assembly));
    }

    [Fact]
    public void Theme_OnlyDiagnoses_LabsOwnsInternalPrinting()
    {
        Type? doctor = Theme.GetType("Bukit.Theme.ThemeDoctor");
        Assert.NotNull(doctor);
        Assert.NotNull(doctor.GetMethod("Diagnose"));
        Assert.Null(doctor.GetMethod("PrintReport"));
        Assembly labs = typeof(Bukit.Labs.Cli.LabsCliAssemblyMarker).Assembly;
        Type? printer = labs.GetType("Bukit.Labs.Cli.Commands.ThemeDoctorReportPrinter");
        Assert.NotNull(printer);
        Assert.False(printer.IsPublic);
        Assert.NotNull(printer.GetMethod("Print", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
    }

    private static string[] Friends(Assembly assembly) => assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
        .Select(a => a.AssemblyName).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void SharedAndConfig_DoNotOwnTemplateOrManifestImplementation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bukit-core.slnx")))
            root = root.Parent;
        Assert.NotNull(root);

        foreach (string project in new[] { "Bukit.Shared", "Bukit.Config" })
        {
            string projectRoot = Path.Combine(root.FullName, "src", "Bukit-Core", project);
            foreach (string path in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
                         .Where(p => !Path.GetRelativePath(projectRoot, p).Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj")))
            {
                string source = File.ReadAllText(path);
                Assert.DoesNotContain("Scriban", source, StringComparison.Ordinal);
                if (project == "Bukit.Config")
                    Assert.DoesNotContain("theme.yaml", source, StringComparison.Ordinal);
            }
        }
    }
}
