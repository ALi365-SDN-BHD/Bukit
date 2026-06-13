using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli;

public static partial class BukitCliSpecs
{
    public static CliCommandRegistry CreateRegistry()
    {
        var build = new CliCommandSpec(
            Name: "build",
            Description: "生成静态站点",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--output", "输出目录"),
                new CliOptionSpec("--base-url", "覆盖 site.baseUrl"),
                new CliOptionSpec("--site-url", "覆盖 site.url"),
                new CliOptionSpec("--clean", "构建前清理", CliOptionType.Flag, ConflictWith: "--no-clean"),
                new CliOptionSpec("--no-clean", "禁用构建前清理", CliOptionType.Flag, ConflictWith: "--clean"),
                new CliOptionSpec("--draft", "渲染草稿", CliOptionType.Flag),
                new CliOptionSpec("--ci", "CI 模式", CliOptionType.Flag),
                new CliOptionSpec("--incremental", "启用增量构建", CliOptionType.Flag, ConflictWith: "--no-incremental"),
                new CliOptionSpec("--no-incremental", "关闭增量构建", CliOptionType.Flag, ConflictWith: "--incremental"),
                new CliOptionSpec("--cache-dir", "覆盖缓存目录"),
                new CliOptionSpec("--metrics", "输出构建指标"),
                new CliOptionSpec("--jobs", "并行渲染并发度", CliOptionType.Integer, ValueName: "n"),
                new CliOptionSpec("--allow-external-plugins", "在 CI 环境中启用外部协议插件", CliOptionType.Flag),
                new CliOptionSpec("--log-format", "日志格式", CliOptionType.String, AllowedValues: new[] { "text", "json" })
            });

        var doctor = new CliCommandSpec(
            Name: "doctor",
            Description: "诊断站点配置和模板",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--site-url", "覆盖 site.url")
            });

        var config = new CliCommandSpec(
            Name: "config",
            Description: "配置诊断命令",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--site-url", "覆盖 site.url"),
                new CliOptionSpec("--output", "输出 schema 文件路径")
            },
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "check",
                    Description: "验证配置但不构建站点",
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名"),
                        new CliOptionSpec("--site-url", "覆盖 site.url")
                    }),
                new CliCommandSpec(
                    Name: "schema",
                    Description: "生成 site.yaml JSON Schema",
                    Options: new[]
                    {
                        new CliOptionSpec("--output", "输出 schema 文件路径")
                    })
            });

        var preview = new CliCommandSpec(
            Name: "preview",
            Description: "本地预览 dist",
            Options: new[]
            {
                new CliOptionSpec("--dir", "预览目录"),
                new CliOptionSpec("--host", "监听地址"),
                new CliOptionSpec("--port", "监听端口", CliOptionType.String, ValueName: "port"),
                new CliOptionSpec("--strict-port", "严格端口模式", CliOptionType.Flag),
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名")
            });

        var clean = new CliCommandSpec(
            Name: "clean",
            Description: "清理构建输出和缓存",
            Options: new[]
            {
                new CliOptionSpec("--dir", "清理目录"),
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名")
            });

        var version = new CliCommandSpec(
            Name: "version",
            Description: "显示版本信息");

        var completion = new CliCommandSpec(
            Name: "completion",
            Description: "生成 shell 自动补全脚本",
            Arguments: new[] { new CliArgumentSpec("shell", "bash|zsh|fish") });

        return new CliCommandRegistry(new[] { build, doctor, config, preview, clean, version, completion });
    }
}
