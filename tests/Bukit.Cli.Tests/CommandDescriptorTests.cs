using System.IO;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;

namespace Bukit.Cli.Tests;

[Collection("Console")]
public sealed class CommandDescriptorTests
{
    [Fact]
    public void ResolveChild_FindsChildByNameAndAlias_IgnoringCase()
    {
        var build = new CommandDescriptor(CreateSpec("build"));
        var preview = new CommandDescriptor(CreateSpec("preview", aliases: ["p"]));
        var root = new CommandDescriptor(CreateSpec("root"), Children: [build, preview]);

        Assert.Same(build, root.ResolveChild("BUILD"));
        Assert.Same(preview, root.ResolveChild("P"));
    }

    [Fact]
    public void ResolveChild_WithNoChildren_ReturnsNull()
    {
        var descriptor = new CommandDescriptor(CreateSpec("build"));

        Assert.Null(descriptor.ResolveChild("preview"));
    }

    [Fact]
    public async Task DispatchAsync_SimpleParseResult_InvokesHandler()
    {
        CliBoundCommand? handled = null;
        var spec = CreateSpec(
            "build",
            arguments: [new CliArgumentSpec("source", "source")],
            options: [new CliOptionSpec("--output", "output")]);

        var descriptor = new CommandDescriptor(
            spec,
            Handler: command =>
            {
                handled = command;
                return Task.FromResult(17);
            });
        var result = CliParser.Parse(spec, ["--output", "dist", "content"]);

        var exitCode = await descriptor.DispatchAsync(result);

        Assert.Equal(17, exitCode);
        Assert.Same(result.BoundCommand, handled);
        Assert.Equal("dist", handled!.GetString("--output"));
        Assert.Equal("content", handled.GetArgument(0));
    }

    [Fact]
    public async Task DispatchAsync_SimpleParseResult_WithoutHandler_ReturnsUnknownCommand()
    {
        var descriptor = new CommandDescriptor(CreateSpec("build"));
        var stderr = new StringWriter();
        var original = Console.Error;

        try
        {
            Console.SetError(stderr);

            var exitCode = await descriptor.DispatchAsync(
                CliParser.Parse(descriptor.Spec, Array.Empty<string>()));

            Assert.Equal(2, exitCode);
            Assert.Contains("Unknown command: build", stderr.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public async Task DispatchAsync_SubcommandParseResult_PrefersChildHandlerAndMergesArguments()
    {
        CliBoundCommand? handled = null;
        var serve = CreateSpec(
            "serve",
            arguments: [new CliArgumentSpec("directory", "directory")],
            options: [new CliOptionSpec("--port", "port", CliOptionType.Integer)]);
        var rootSpec = CreateSpec("preview", subcommands: [serve]);
        var root = new CommandDescriptor(
            rootSpec,
            Handler: _ => Task.FromResult(1),
            Children:
            [
                new CommandDescriptor(
                    serve,
                    Handler: command =>
                    {
                        handled = command;
                        return Task.FromResult(9);
                    }),
            ]);

        var result = CliParser.Parse(rootSpec, ["serve", "--port", "8080", "dist"]);
        var exitCode = await root.DispatchAsync(result);

        Assert.Equal(9, exitCode);
        Assert.NotNull(handled);
        Assert.Equal(8080, handled!.GetInt("--port"));
        Assert.Equal("serve", handled.GetArgument(0));
        Assert.Equal("dist", handled.GetArgument(1));
    }

    [Fact]
    public async Task DispatchAsync_SubcommandParseResult_FallsBackToParentHandler_WhenChildHasNoHandler()
    {
        CliBoundCommand? handled = null;
        var child = CreateSpec(
            "json",
            arguments: [new CliArgumentSpec("source", "source")],
            options: [new CliOptionSpec("--verbose", "verbose", CliOptionType.Flag)]);
        var rootSpec = CreateSpec("export", subcommands: [child]);
        var root = new CommandDescriptor(
            rootSpec,
            Handler: command =>
            {
                handled = command;
                return Task.FromResult(23);
            },
            Children:
            [
                new CommandDescriptor(child),
            ]);

        var result = CliParser.Parse(rootSpec, ["json", "--verbose", "posts"]);
        var exitCode = await root.DispatchAsync(result);

        Assert.Equal(23, exitCode);
        Assert.NotNull(handled);
        Assert.True(handled!.GetBool("--verbose"));
        Assert.Equal("json", handled.GetArgument(0));
        Assert.Equal("posts", handled.GetArgument(1));
    }

    [Fact]
    public async Task DispatchAsync_SubcommandParseResult_WithoutHandler_ReturnsUnknownCommand()
    {
        var child = CreateSpec("json");
        var rootSpec = CreateSpec("export", subcommands: [child]);
        var root = new CommandDescriptor(rootSpec, Children: [new CommandDescriptor(child)]);
        var stderr = new StringWriter();
        var original = Console.Error;

        try
        {
            Console.SetError(stderr);

            var result = CliParser.Parse(rootSpec, ["json"]);
            var exitCode = await root.DispatchAsync(result);

            Assert.Equal(2, exitCode);
            Assert.Contains("Unknown command: export json", stderr.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    public async Task DispatchAsync_NestedSubcommand_UsesCurrentImmediateChildDispatch()
    {
        CliBoundCommand? handled = null;
        var leaf = CreateSpec(
            "leaf",
            arguments: [new CliArgumentSpec("source", "source")]);
        var group = CreateSpec("group", subcommands: [leaf]);
        var rootSpec = CreateSpec("root", subcommands: [group]);
        var root = new CommandDescriptor(
            rootSpec,
            Children:
            [
                new CommandDescriptor(
                    group,
                    Handler: command =>
                    {
                        handled = command;
                        return Task.FromResult(29);
                    },
                    Children:
                    [
                        new CommandDescriptor(leaf, Handler: _ => Task.FromResult(31)),
                    ]),
            ]);

        var result = CliParser.Parse(rootSpec, ["group", "leaf", "content"]);
        var exitCode = await root.DispatchAsync(result);

        Assert.Equal(29, exitCode);
        Assert.NotNull(handled);
        Assert.Equal("group", handled!.GetArgument(0));
        Assert.Equal("leaf", handled.GetArgument(1));
        Assert.Null(handled.GetArgument(2));
    }

    [Fact]
    public async Task DispatchAsync_UnknownParseResult_ReturnsTwo()
    {
        var descriptor = new CommandDescriptor(CreateSpec("build"));
        var result = new TestParseResult(descriptor.Spec, CreateBoundCommand(), []);

        var exitCode = await descriptor.DispatchAsync(result);

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Flatten_ReturnsSelfAndNestedChildren()
    {
        var grandchild = new CommandDescriptor(CreateSpec("serve"));
        var child = new CommandDescriptor(CreateSpec("preview"), Children: [grandchild]);
        var root = new CommandDescriptor(CreateSpec("build"), Children: [child]);

        var names = root.Flatten().Select(descriptor => descriptor.Spec.Name).ToArray();

        Assert.Equal(["build", "preview", "serve"], names);
    }

    [Fact]
    public void ExtractCommandPaths_IncludesOnlyDescriptorsWithHandlers()
    {
        var serve = new CommandDescriptor(
            CreateSpec("serve"),
            Handler: _ => Task.FromResult(0));
        var preview = new CommandDescriptor(CreateSpec("preview"), Children: [serve]);
        var root = new CommandDescriptor(
            CreateSpec("build"),
            Handler: _ => Task.FromResult(0),
            Children: [preview]);

        var paths = root.ExtractCommandPaths();

        Assert.Equal(["build", "build preview serve"], paths);
    }

    private static CliCommandSpec CreateSpec(
        string name,
        IReadOnlyList<string>? aliases = null,
        IReadOnlyList<CliCommandSpec>? subcommands = null,
        IReadOnlyList<CliArgumentSpec>? arguments = null,
        IReadOnlyList<CliOptionSpec>? options = null)
    {
        return new CliCommandSpec(
            name,
            $"{name} command",
            aliases,
            Arguments: arguments,
            Options: options,
            Subcommands: subcommands);
    }

    private static CliBoundCommand CreateBoundCommand(
        IReadOnlyDictionary<string, string?>? options = null,
        IReadOnlyList<string>? arguments = null)
    {
        return new CliBoundCommand(
            options ?? new Dictionary<string, string?>(),
            arguments ?? []);
    }

    private sealed record TestParseResult(
        CliCommandSpec Command,
        CliBoundCommand BoundCommand,
        IReadOnlyList<CliDiagnostic> Diagnostics)
        : CliParseResult(Command, BoundCommand, Diagnostics);
}
