using Bukit.Cli.Shared.Cli.Metadata;

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

        var dev = new CliCommandSpec(
            Name: "dev",
            Description: "LiveReload 实时预览开发服务器",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--host", "监听地址"),
                new CliOptionSpec("--port", "监听端口", CliOptionType.Integer, ValueName: "port"),
                new CliOptionSpec("--output", "覆盖构建输出目录"),
                new CliOptionSpec("--no-watch", "禁用文件监控，仅作为静态服务器", CliOptionType.Flag),
                new CliOptionSpec("--allow-lan", "允许开发服务器监听非本机地址", CliOptionType.Flag),
                new CliOptionSpec("--public", "--allow-lan 的别名", CliOptionType.Flag)
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

        var seo = new CliCommandSpec(
            Name: "seo",
            Description: "SEO 构建质量门禁",
            Options: new[]
            {
                new CliOptionSpec("--dir", "构建输出目录"),
                new CliOptionSpec("--report", "SEO 报告路径"),
                new CliOptionSpec("--strict", "将 warning 视为失败", CliOptionType.Flag),
                new CliOptionSpec("--external", "检查外部链接和媒体 URL", CliOptionType.Flag),
                new CliOptionSpec("--routes", "SEO route map 本地路径"),
                new CliOptionSpec("--observations", "逗号分隔的本地 observation JSON 路径"),
                new CliOptionSpec("--rules", "SEO insights 规则本地路径"),
                new CliOptionSpec("--out", "SEO insights 报告输出路径"),
                new CliOptionSpec("--strict-join", "join 缺口时返回失败", CliOptionType.Flag)
            },
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "audit",
                    Description: "检查 .bukit/seo-report.json",
                    Options: new[]
                    {
                        new CliOptionSpec("--dir", "构建输出目录"),
                        new CliOptionSpec("--report", "SEO 报告路径"),
                        new CliOptionSpec("--strict", "将 warning 视为失败", CliOptionType.Flag),
                        new CliOptionSpec("--external", "检查外部链接和媒体 URL", CliOptionType.Flag)
                    }),
                new CliCommandSpec(
                    Name: "diff",
                    Description: "比较两份 SEO 报告",
                    Options: DiffOptions()),
                new CliCommandSpec(
                    Name: "insights",
                    Description: "从本地 observation JSON 生成 SEO insights 报告",
                    Options: InsightsOptions())
            });

        var geo = new CliCommandSpec(
            Name: "geo",
            Description: "GEO / llms.txt 质量门禁",
            Options: new[]
            {
                new CliOptionSpec("--dir", "构建输出目录")
            },
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "audit",
                    Description: "检查 .bukit/geo-report.json",
                    Options: new[]
                    {
                        new CliOptionSpec("--dir", "构建输出目录")
                    })
            });

        var publish = new CliCommandSpec(
            Name: "publish",
            Description: "发布质量门禁",
            Options: new[]
            {
                new CliOptionSpec("--dir", "构建输出目录"),
                new CliOptionSpec("--report", "发布审计报告路径"),
                new CliOptionSpec("--strict", "将 warning 视为失败", CliOptionType.Flag),
                new CliOptionSpec("--external", "检查外部链接和媒体 URL", CliOptionType.Flag)
            },
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "audit",
                    Description: "检查 .bukit/publish-audit-report.json",
                    Options: new[]
                    {
                        new CliOptionSpec("--dir", "构建输出目录"),
                        new CliOptionSpec("--report", "发布审计报告路径"),
                        new CliOptionSpec("--strict", "将 warning 视为失败", CliOptionType.Flag),
                        new CliOptionSpec("--external", "检查外部链接和媒体 URL", CliOptionType.Flag)
                    }),
                new CliCommandSpec(
                    Name: "diff",
                    Description: "比较两份发布审计报告",
                    Options: DiffOptions())
            });

        var deploy = new CliCommandSpec(
            Name: "deploy",
            Description: "部署到 GitHub Pages（provider: github-pages）",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--dry-run", "只输出部署计划，不执行部署", CliOptionType.Flag),
                new CliOptionSpec("--skip-build", "跳过部署前构建", CliOptionType.Flag),
                new CliOptionSpec("--base-url", "覆盖 site.baseUrl"),
                new CliOptionSpec("--site-url", "覆盖 site.url"),
                new CliOptionSpec("--output", "覆盖构建输出目录"),
                new CliOptionSpec("--branch", "GitHub Pages 目标分支"),
                new CliOptionSpec("--message", "部署提交消息"),
                new CliOptionSpec("--ci", "CI 模式", CliOptionType.Flag),
                new CliOptionSpec("--force", "允许 non-fast-forward 时强制覆盖远端分支", CliOptionType.Flag)
            });

        return new CliCommandRegistry(new[] { build, doctor, config, preview, dev, clean, version, completion, seo, geo, publish, deploy });
    }

    private static CliOptionSpec[] DiffOptions() =>
    [
        new CliOptionSpec("--baseline", "diff 基线报告路径"),
        new CliOptionSpec("--current", "diff 当前报告路径"),
        new CliOptionSpec("--max-new-errors", "允许新增 error 数", CliOptionType.Integer, ValueName: "n"),
        new CliOptionSpec("--max-new-warnings", "允许新增 warning 数", CliOptionType.Integer, ValueName: "n"),
        new CliOptionSpec("--max-new-issues", "允许新增 issue 数", CliOptionType.Integer, ValueName: "n"),
        new CliOptionSpec("--fail-on-new-code", "指定新增 issue code 时失败"),
        new CliOptionSpec("--fail-on-route-removed", "路由移除时失败", CliOptionType.Flag),
        new CliOptionSpec("--fail-on-indexable-drop", "可索引页面变为不可索引时失败", CliOptionType.Flag)
    ];

    private static CliOptionSpec[] InsightsOptions() =>
    [
        new CliOptionSpec("--dir", "构建输出目录", DefaultValueHelp: "dist"),
        new CliOptionSpec("--routes", "SEO route map 本地路径", DefaultValueHelp: "<dir>/.bukit/seo-route-map.json"),
        new CliOptionSpec("--observations", "逗号分隔的 1-10 个本地 observation JSON 路径", Required: true),
        new CliOptionSpec("--rules", "SEO insights 规则本地路径", Required: true),
        new CliOptionSpec("--out", "SEO insights 报告输出路径", DefaultValueHelp: "<dir>/.bukit/seo-insights-report.json"),
        new CliOptionSpec("--strict-join", "unmatched 或 ambiguous 行存在时返回 1", CliOptionType.Flag)
    ];
}
