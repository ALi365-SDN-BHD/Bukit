using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli;

public static class BukitCliThemeSpecs
{
    internal static readonly CliCommandSpec ThemeCreateSpec = new(
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
            new CliOptionSpec("--use", "���建后切换到该主题", CliOptionType.Flag),
            new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag)
        });

    internal static readonly CliCommandSpec ThemeListSpec = new(
        Name: "list",
        Description: "列出可用主题",
        Options: new[]
        {
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeUseSpec = new(
        Name: "use",
        Description: "切换主题",
        Arguments: new[] { new CliArgumentSpec("name", "主题名", Required: true) },
        Options: new[]
        {
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeInfoSpec = new(
        Name: "info",
        Description: "查看主题详细信息",
        Arguments: new[] { new CliArgumentSpec("name", "主题名") },
        Options: new[]
        {
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeParamsSpec = new(
        Name: "params",
        Description: "列出主题可定制参数",
        Arguments: new[] { new CliArgumentSpec("name", "主题名") },
        Options: new[]
        {
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemePreviewSpec = new(
        Name: "preview",
        Description: "显示主题预览元数据",
        Arguments: new[] { new CliArgumentSpec("name", "主题名") },
        Options: new[]
        {
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeWizardSpec = new(
        Name: "wizard",
        Description: "交互式主题创建向导",
        Arguments: new[] { new CliArgumentSpec("name", "主题名") },
        Options: new[]
        {
            new CliOptionSpec("--preset", "预设风格 (blog|docs|landing|minimal|portfolio)"),
            new CliOptionSpec("--template", "模板范围 (full|bare|none)，默认 full", CliOptionType.String, ValueName: "scope"),
            new CliOptionSpec("--use", "创建后切换", CliOptionType.Flag),
            new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag),
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemePackSpec = new(
        Name: "pack",
        Description: "打包主题为 tar.gz",
        Arguments: new[] { new CliArgumentSpec("name", "主题名") },
        Options: new[]
        {
            new CliOptionSpec("--output", "输出路径"),
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeInstallSpec = new(
        Name: "install",
        Description: "安装主题 (本地/URL/注册表)",
        Arguments: new[] { new CliArgumentSpec("source", "路径或 URL") },
        Options: new[]
        {
            new CliOptionSpec("--registry", "注册表中的主题名"),
            new CliOptionSpec("--registry-url", "注册表 URL"),
            new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag),
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeSearchSpec = new(
        Name: "search",
        Description: "搜索社区主题注册表",
        Arguments: new[] { new CliArgumentSpec("query", "搜索关键词") },
        Options: new[]
        {
            new CliOptionSpec("--refresh", "强制刷新缓存", CliOptionType.Flag),
            new CliOptionSpec("--registry-url", "注册表 URL"),
            new CliOptionSpec("--config", "配置文件路径"),
            new CliOptionSpec("--site", "多站点名")
        });

    internal static readonly CliCommandSpec ThemeDoctorSpec = new(
        Name: "doctor",
        Description: "诊断组件化主题");

    internal static readonly CliCommandSpec ThemeListComponentsSpec = new(
        Name: "list-components",
        Description: "列出主题组件");

    internal static readonly CliCommandSpec ThemeExportCatalogSpec = new(
        Name: "export-catalog",
        Description: "导出主题目录");
}
