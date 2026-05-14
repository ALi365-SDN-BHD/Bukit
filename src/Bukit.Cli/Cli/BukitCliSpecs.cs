using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli;

public static class BukitCliSpecs
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

        var preview = new CliCommandSpec(
            Name: "preview",
            Description: "本地预览 dist",
            Options: new[]
            {
                new CliOptionSpec("--dir", "预览目录"),
                new CliOptionSpec("--host", "监听地址"),
                new CliOptionSpec("--port", "监听端口", CliOptionType.String, ValueName: "port"),
                new CliOptionSpec("--strict-port", "严格端口模式", CliOptionType.Flag)
            });

        var plugin = new CliCommandSpec(
            Name: "plugin",
            Description: "插件相关命令",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "list",
                    Description: "列出插件",
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    })
            });

        var theme = new CliCommandSpec(
            Name: "theme",
            Description: "主题相关命令",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "create",
                    Description: "创建主题",
                    Arguments: new[] { new CliArgumentSpec("name", "主题名", Required: true) },
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名"),
                        new CliOptionSpec("--from", "源主题名"),
                        new CliOptionSpec("--brand", "品牌名"),
                        new CliOptionSpec("--primary-color", "主色"),
                        new CliOptionSpec("--accent-color", "强调色"),
                        new CliOptionSpec("--use", "创建后切换到该主题", CliOptionType.Flag),
                        new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag)
                    }),
                new CliCommandSpec(
                    Name: "list",
                    Description: "列出可用主题",
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "use",
                    Description: "切换主题",
                    Arguments: new[] { new CliArgumentSpec("name", "主题名", Required: true) },
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    })
            });

        var seo = new CliCommandSpec(
            Name: "seo",
            Description: "SEO 审计命令",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "audit",
                    Description: "读取 seo-report.json 并返回 CI 状态",
                    Options: new[]
                    {
                        new CliOptionSpec("--dir", "构建输出目录"),
                        new CliOptionSpec("--report", "seo-report.json 路径"),
                        new CliOptionSpec("--strict", "warning 也返回失败", CliOptionType.Flag),
                        new CliOptionSpec("--external", "联网检查 canonical、链接和图片", CliOptionType.Flag)
                    }),
                new CliCommandSpec(
                    Name: "diff",
                    Description: "比较两个 seo-report.json 并执行回归预算",
                    Options: new[]
                    {
                        new CliOptionSpec("--baseline", "基线 seo-report.json 路径"),
                        new CliOptionSpec("--current", "当前 seo-report.json 路径"),
                        new CliOptionSpec("--max-new-errors", "允许新增 error 数量", CliOptionType.Integer, ValueName: "n"),
                        new CliOptionSpec("--max-new-warnings", "允许新增 warning 数量", CliOptionType.Integer, ValueName: "n"),
                        new CliOptionSpec("--max-new-issues", "允许新增 issue 总数", CliOptionType.Integer, ValueName: "n"),
                        new CliOptionSpec("--fail-on-new-code", "逗号分隔的新增 issue code 黑名单"),
                        new CliOptionSpec("--fail-on-route-removed", "route 删除时失败", CliOptionType.Flag),
                        new CliOptionSpec("--fail-on-indexable-drop", "indexable route 变成 noindex 时失败", CliOptionType.Flag)
                    })
            });

        var deploy = new CliCommandSpec(
            Name: "deploy",
            Description: "部署静态站点",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--output", "输出目录"),
                new CliOptionSpec("--base-url", "覆盖 site.baseUrl"),
                new CliOptionSpec("--site-url", "覆盖 site.url"),
                new CliOptionSpec("--branch", "目标分支"),
                new CliOptionSpec("--message", "提交信息"),
                new CliOptionSpec("--ci", "CI 模式", CliOptionType.Flag),
                new CliOptionSpec("--dry-run", "仅预览，不实际部署", CliOptionType.Flag),
                new CliOptionSpec("--skip-build", "跳过构建步骤", CliOptionType.Flag)
            });

        return new CliCommandRegistry(new[] { build, deploy, preview, plugin, theme, seo });
    }
}
