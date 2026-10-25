using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Bukit.Architecture.Tests;

public sealed class LibraryConsoleOutputTests
{
    private static readonly HashSet<string> OutputMembers = new(StringComparer.Ordinal)
    {
        "Write", "WriteLine", "Out", "Error", "SetOut", "SetError", "SetIn",
        "ForegroundColor", "BackgroundColor", "ResetColor", "Clear", "Title",
        "CursorLeft", "CursorTop", "CursorVisible", "SetCursorPosition", "OutputEncoding",
        "OpenStandardOutput", "OpenStandardError"
    };

    private static readonly MetadataReference[] References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(p => MetadataReference.CreateFromFile(p)).ToArray();

    [Fact]
    public void CoreLibraries_DoNotWriteDirectlyToConsole()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "bukit-core.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        string core = Path.Combine(root.FullName, "src", "Bukit-Core");
        var violations = new List<string>();
        foreach (string project in Directory.EnumerateDirectories(core).Order(StringComparer.Ordinal))
        {
            var trees = Directory.EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(p => IsLibrarySource(Path.GetRelativePath(core, p)))
                .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p)).ToArray();
            violations.AddRange(FindOutput(trees));
        }
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Theory]
    [InlineData("Console.WriteLine(1);", 1)]
    [InlineData("System.Console.Write(1);", 1)]
    [InlineData("global::System.Console.Error.WriteLine(1);", 1)]
    [InlineData("System.Console\n /* comment */ .WriteLine(1);", 1)]
    [InlineData("Console.ForegroundColor = ConsoleColor.Red; Console.ResetColor();", 2)]
    [InlineData("Console.SetError(System.IO.TextWriter.Null);", 1)]
    [InlineData("// Console.WriteLine(1);\n var s = \"Console.WriteLine(1);\";", 0)]
    [InlineData("var s = @\"Console.WriteLine(1);\";", 0)]
    [InlineData("var s = \"\"\"Console.WriteLine(1);\"\"\";", 0)]
    [InlineData("var s = $\"{Console.Error}\";", 1)]
    [InlineData("var s = $\"\"\"{Console.Error}\"\"\";", 1)]
    [InlineData("var s = $\"{Console.Out.WriteAsync(\"message\")}\";", 1)]
    [InlineData("var s = $\"\"\"{Console.OpenStandardOutput()}\"\"\";", 1)]
    [InlineData("var s = Console.ReadLine();", 0)]
    public void Guard_BindsRealOutputNotText(string body, int count)
    {
        Assert.Equal(count, FindOutput([CSharpSyntaxTree.ParseText("class Test { void M() { " + body + " } }")]).Count);
    }

    [Theory]
    [InlineData("using C = System.Console; class T { void M() { C.WriteLine(1); } }", 1)]
    [InlineData("using S = global::System; class T { void M() { S.Console.WriteLine(1); } }", 1)]
    [InlineData("using static System.Console; class T { void M() { WriteLine(1); } }", 1)]
    [InlineData("class Console { public static void WriteLine(int x) {} } class T { void M() { Console.WriteLine(1); } }", 0)]
    public void Guard_ResolvesAliasesStaticImportsAndUnrelatedTypes(string source, int count)
    {
        Assert.Equal(count, FindOutput([CSharpSyntaxTree.ParseText(source)]).Count);
    }

    [Fact]
    public void Guard_UsesProjectGlobalAliases()
    {
        Assert.Single(FindOutput([
            CSharpSyntaxTree.ParseText("global using C = System.Console;"),
            CSharpSyntaxTree.ParseText("class T { void M() { C.WriteLine(1); } }")
        ]));
    }

    [Fact]
    public void ProcessRunnerLoggerConstructor_IsAdditive()
    {
        Assert.NotNull(typeof(Bukit.PluginHost.SystemProcessRunner).GetConstructor(Type.EmptyTypes));
        Assert.NotNull(typeof(Bukit.PluginHost.SystemProcessRunner).GetConstructor([typeof(Bukit.Shared.ILogger)]));
    }

    [Theory]
    [InlineData("Bukit.Cli/Program.cs", false)]
    [InlineData("Bukit.Cli.Shared/Printer.cs", false)]
    [InlineData("Bukit.Shared/Logger.cs", false)]
    [InlineData("Bukit.Theme/bin/generated.cs", false)]
    [InlineData("Bukit.Theme/obj/generated.cs", false)]
    [InlineData("Bukit.Theme/Doctor.cs", true)]
    [InlineData("Bukit.Shared/Other/Logger.cs", true)]
    [InlineData("Bukit.Shared/OtherLogger.cs", true)]
    [InlineData("Bukit.Cli.Other/Printer.cs", true)]
    public void Guard_ExemptionsAreExact(string path, bool expected) => Assert.Equal(expected, IsLibrarySource(path));

    private static bool IsLibrarySource(string path)
    {
        string[] parts = path.Replace('\\', '/').Split('/');
        return !parts.Any(p => p is "bin" or "obj")
            && parts[0] is not ("Bukit.Cli" or "Bukit.Cli.Shared")
            && string.Join('/', parts) != "Bukit.Shared/Logger.cs";
    }

    private static List<string> FindOutput(SyntaxTree[] trees)
    {
        var compilation = CSharpCompilation.Create("ConsoleGuard", trees.Append(
            CSharpSyntaxTree.ParseText("global using System;")), References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var violations = new List<string>();
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>()
                         .Where(n => OutputMembers.Contains(n.Identifier.ValueText)))
            {
                var info = model.GetSymbolInfo(name);
                var symbols = info.Symbol is { } symbol ? new[] { symbol } : info.CandidateSymbols.ToArray();
                if (symbols.Any(s => s.ContainingType?.ToDisplayString() == "System.Console"))
                    violations.Add($"{tree.FilePath}:{name.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {name}");
            }
        }
        return violations;
    }
}
