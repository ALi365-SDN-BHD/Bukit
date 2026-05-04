namespace Bukit.Cli.Commands;

public static class HelpPrinter
{
    public static void Print()
    {
        Console.WriteLine("bukit");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  create <dir>          从零创建站点工程（等价 init）");
        Console.WriteLine("  init <dir>            初始化站点工程骨架");
        Console.WriteLine("  build                 生成静态站点");
        Console.WriteLine("  preview               本地预览 dist");
        Console.WriteLine("  clean                 清理输出与缓存");
        Console.WriteLine("  doctor                环境与配置诊断");
        Console.WriteLine("  plugin                插件相关命令");
        Console.WriteLine("  theme                 主题相关命令");
        Console.WriteLine("  intent                AI Intent 相关命令");
        Console.WriteLine("  webhook               Webhook 触发器");
        Console.WriteLine("  version               版本信息");
        Console.WriteLine();
        Console.WriteLine("Common build options:");
        Console.WriteLine("  --config <path>       默认 site.yaml");
        Console.WriteLine("  --site <name>         使用 sites/<name>.yaml（rootDir 仍为当前目录）");
        Console.WriteLine("  --output <dir>        覆盖 build.output");
        Console.WriteLine("  --base-url <path>     覆盖 site.baseUrl");
        Console.WriteLine("  --site-url <url>      覆盖 site.url（用于 sitemap/rss）");
        Console.WriteLine("  --clean               构建前清理");
        Console.WriteLine("  --no-clean            禁用构建前清理（配合增量构建）");
        Console.WriteLine("  --draft               渲染草稿");
        Console.WriteLine("  --ci                  CI 模式");
        Console.WriteLine("  --incremental         增量构建（默认启用）");
        Console.WriteLine("  --no-incremental      关闭增量构建");
        Console.WriteLine("  --cache-dir <dir>     覆盖缓存目录（默认 <config-dir>/.cache）");
        Console.WriteLine("  --metrics <path>      输出构建指标 JSON（相对路径按 rootDir 解析）");
        Console.WriteLine("  --log-format <text|json>  控制日志格式（默认 text）");
        Console.WriteLine();
        Console.WriteLine("Preview options:");
        Console.WriteLine("  --dir <path>          默认 dist");
        Console.WriteLine("  --host <host>         默认 localhost");
        Console.WriteLine("  --port <port|auto>    默认 4173（auto 自动选择可用端口）");
        Console.WriteLine("  --strict-port         端口占用则直接失败（默认会自动递增重试）");
    }
}
