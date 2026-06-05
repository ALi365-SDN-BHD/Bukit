using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli;

public static partial class BukitCliSpecs
{
    private static CliCommandSpec CreateInitSpec()
        => new(
            Name: "init",
            Description: "初始化新站点",
            Aliases: new[] { "create" },
            Arguments: new[]
            {
                new CliArgumentSpec("dir", "目标目录")
            },
            Options: new[]
            {
                new CliOptionSpec("--provider", "内容源 (markdown|notion)"),
                new CliOptionSpec("--template", "模板名 (minimal|blog|docs|landing|portfolio|bare|none)")
            });

    private static CliCommandSpec CreateRouteSpec()
        => new(
            Name: "route",
            Description: "查看路由信息",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--json", "JSON 格式输出", CliOptionType.Flag),
                new CliOptionSpec("--collection", "按 collection 过滤")
            },
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "inspect",
                    Description: "列出所有路由",
                    Options: new[]
                    {
                        new CliOptionSpec("--json", "JSON 格式输出", CliOptionType.Flag),
                        new CliOptionSpec("--collection", "按 collection 过滤")
                    })
            });

    private static CliCommandSpec CreateDataSpec()
        => new(
            Name: "data",
            Description: "查看数据模块信息",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--module", "模块名"),
                new CliOptionSpec("--format", "输出格式 (json)")
            },
            Subcommands: new[]
            {
                new CliCommandSpec(Name: "inspect", Description: "列出所有数据模块", Options: new[] { new CliOptionSpec("--module", "模块名") }),
                new CliCommandSpec(Name: "dump", Description: "导出数据模块", Options: new[] { new CliOptionSpec("--format", "输出格式 (json)") })
            });

    private static CliCommandSpec CreateDocsSpec()
        => new(
            Name: "docs",
            Description: "文档一致性检查",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "check",
                    Description: "检查 README/guide/skills 之间的一致性",
                    Options: new[]
                    {
                        new CliOptionSpec("--cli", "检查 CLI 命令覆盖率", CliOptionType.Flag),
                        new CliOptionSpec("--config-fields", "检查 site.yaml 字段引用", CliOptionType.Flag),
                        new CliOptionSpec("--file-refs", "检查文件路径引用", CliOptionType.Flag),
                        new CliOptionSpec("--examples", "检查 README 示例可解析性", CliOptionType.Flag),
                        new CliOptionSpec("--skills", "检查 Skill-CLI 一致性", CliOptionType.Flag)
                    })
            });
}
