using Bukit.Cli.Shared.Cli.Metadata;
using Bukit.Cli.Shared.Cli.Parsing;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliContractTests
{
    [Fact]
    public void Registry_CommandTree_RemainsStable()
    {
        var registry = BukitCliSpecs.CreateRegistry();

        var tree = registry.Commands
            .Select(command =>
                $"{command.Name}:{string.Join(",", (command.Subcommands ?? []).Select(subcommand => subcommand.Name))}")
            .ToArray();
        var descriptorCommands = BukitCliDescriptors.CreateDescriptors()
            .Select(descriptor => descriptor.Spec.Name)
            .ToArray();

        Assert.Equal(
            [
                "build:",
                "doctor:",
                "config:check,schema",
                "preview:",
                "dev:",
                "clean:",
                "version:",
                "completion:",
                "seo:audit,diff,insights,question-insights,generative-insights",
                "geo:audit",
                "publish:audit,diff",
                "deploy:",
            ],
            tree);
        Assert.Equal(
            registry.Commands.Select(command => command.Name).OrderBy(name => name, StringComparer.Ordinal),
            descriptorCommands.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Registry_SeoInsights_ExposesRequiredOfflineOptionsWithoutChangingAuditOrDiff()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var seo = registry.Resolve("seo")!;
        var insights = registry.ResolveSubcommand(seo, "insights")!;
        var options = insights.Options!;

        Assert.Equal(["audit", "diff", "insights", "question-insights", "generative-insights"], seo.Subcommands!.Select(command => command.Name));
        Assert.Equal(
            ["--dir", "--report", "--strict", "--external"],
            seo.Options!.Select(option => option.Name));
        Assert.Equal(
            ["--dir", "--routes", "--observations", "--rules", "--out", "--strict-join"],
            options.Select(option => option.Name));
        Assert.True(options.Single(option => option.Name == "--observations").Required);
        Assert.True(options.Single(option => option.Name == "--rules").Required);
        Assert.False(options.Single(option => option.Name == "--routes").Required);
        Assert.Equal("dist", options.Single(option => option.Name == "--dir").DefaultValueHelp);
        Assert.Equal("<dir>/.bukit/seo-route-map.json", options.Single(option => option.Name == "--routes").DefaultValueHelp);
        Assert.Equal("<dir>/.bukit/seo-insights-report.json", options.Single(option => option.Name == "--out").DefaultValueHelp);
        Assert.NotNull(registry.ResolveSubcommand(seo, "audit"));
        Assert.NotNull(registry.ResolveSubcommand(seo, "diff"));
    }

    [Fact]
    public void Registry_SeoQuestionInsights_ExposesRequiredOfflineOptionsWithoutChangingExistingSubcommands()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var seo = registry.Resolve("seo")!;
        var questionInsights = registry.ResolveSubcommand(seo, "question-insights")!;
        var options = questionInsights.Options!;

        Assert.Equal(
            ["--dir", "--routes", "--targets", "--observations", "--rules", "--out", "--strict-join"],
            options.Select(option => option.Name));
        Assert.True(options.Single(option => option.Name == "--targets").Required);
        Assert.True(options.Single(option => option.Name == "--observations").Required);
        Assert.True(options.Single(option => option.Name == "--rules").Required);
        Assert.False(options.Single(option => option.Name == "--routes").Required);
        Assert.Equal("<dir>/.bukit/seo-route-map.json", options.Single(option => option.Name == "--routes").DefaultValueHelp);
        Assert.Equal("<dir>/.bukit/seo-question-insights-report.json", options.Single(option => option.Name == "--out").DefaultValueHelp);
    }

    [Fact]
    public void Registry_SeoGenerativeInsights_ExposesRequiredOfflineOptionsWithoutChangingExistingSubcommands()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        var seo = registry.Resolve("seo")!;
        var generativeInsights = registry.ResolveSubcommand(seo, "generative-insights")!;
        var options = generativeInsights.Options!;

        Assert.Equal(
            ["--dir", "--routes", "--observations", "--rules", "--out", "--strict-join"],
            options.Select(option => option.Name));
        Assert.True(options.Single(option => option.Name == "--observations").Required);
        Assert.True(options.Single(option => option.Name == "--rules").Required);
        Assert.False(options.Single(option => option.Name == "--routes").Required);
        Assert.Equal("<dir>/.bukit/seo-route-map.json", options.Single(option => option.Name == "--routes").DefaultValueHelp);
        Assert.Equal("<dir>/.bukit/generative-citation-report.json", options.Single(option => option.Name == "--out").DefaultValueHelp);
    }

    [Fact]
    public void Parse_BindingContract_RemainsStable()
    {
        var spec = new CliCommandSpec(
            Name: "build",
            Description: "build",
            Arguments:
            [
                new CliArgumentSpec("source", "source"),
            ],
            Options:
            [
                new CliOptionSpec("--output", "output"),
                new CliOptionSpec("--force", "force", CliOptionType.Flag, ShortName: "-f"),
            ]);

        var result = CliParser.Parse(
            spec,
            ["content", "--OUTPUT=dist=preview", "-F", "--unknown=value"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("content", result.BoundCommand.GetArgument(0));
        Assert.Equal("dist=preview", result.BoundCommand.GetString("--output"));
        Assert.Equal("true", result.BoundCommand.GetString("--force"));
        Assert.Null(result.BoundCommand.GetString("--unknown"));
        Assert.Equal(["unknown-option"], result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void TestHelper_BindsImmediateSubcommandOptionsThroughPublicParser()
    {
        var command = CliTestHelper.CreateCommand(
            "config",
            ["config", "check", "--config", "site.yaml"]);

        Assert.Equal("check", command.GetArgument(0));
        Assert.Equal("site.yaml", command.GetString("--config"));
    }

    [Fact]
    public void Parse_NestedSubcommand_PropagatesLeafDiagnosticsToPublicResult()
    {
        var leaf = new CliCommandSpec(
            Name: "leaf",
            Description: "leaf",
            Options:
            [
                new CliOptionSpec("--count", "count", CliOptionType.Integer),
            ]);
        var group = new CliCommandSpec(
            Name: "group",
            Description: "group",
            Subcommands: [leaf]);
        var root = new CliCommandSpec(
            Name: "root",
            Description: "root",
            Subcommands: [group]);

        var result = CliParser.Parse(root, ["group", "leaf", "--count", "invalid"]);

        Assert.Same(root, result.Command);
        Assert.False(result.IsSuccess);
        Assert.Equal("group", result.BoundCommand.GetArgument(0));
        Assert.Equal(["invalid-option-value"], result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }
}
