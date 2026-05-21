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
                new CliOptionSpec("--strict-port", "严格端口模式", CliOptionType.Flag),
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名")
            });

        var dev = new CliCommandSpec(
            Name: "dev",
            Description: "启动 HMR 开发服务器 (文件变更自动重构建 + 浏览器实时刷新)",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--host", "监听地址"),
                new CliOptionSpec("--port", "监听端口", CliOptionType.Integer, ValueName: "port"),
                new CliOptionSpec("--output", "输出目录", CliOptionType.String, ValueName: "dir"),
                new CliOptionSpec("--no-watch", "禁用文件监控", CliOptionType.Flag)
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

        var clone = new CliCommandSpec(
            Name: "clone",
            Description: "从目标网站提取数据生成 Bukit 主题与内容",
            Options: new[]
            {
                new CliOptionSpec("--tokens", "设计令牌 JSON 文件", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--theme", "目标主题名", CliOptionType.String, ValueName: "name"),
                new CliOptionSpec("--layout", "页面布局 JSON 文件", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--page", "页面元数据 JSON 文件", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--sections", "页面区块 JSON 文件", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--behaviors", "交互行为 JSON 文件", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--icons", "SVG 图标 JSON 文件", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--assets", "静态资源 JSON 文件 (自动下载图片)", CliOptionType.String, ValueName: "file"),
                new CliOptionSpec("--brand", "品牌名 (用于导航栏和页脚)"),
                new CliOptionSpec("--use", "创建后切换到该主题", CliOptionType.Flag),
                new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag),
                new CliOptionSpec("--verify", "生成后执行 doctor/build 验证", CliOptionType.Flag),
                new CliOptionSpec("--visual-threshold", "视觉截图 diff 阈值 (0-1)", CliOptionType.String, ValueName: "ratio"),
                new CliOptionSpec("--fail-on-visual-diff", "截图 diff 超过阈值时失败", CliOptionType.Flag),
                new CliOptionSpec("--fidelity", "保真模式：直接迁移 HTML 目录为模板 (值为 HTML 目录路径)", CliOptionType.String, ValueName: "dir"),
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名")
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
                    }),
                new CliCommandSpec(
                    Name: "info",
                    Description: "查看主题详细信息",
                    Arguments: new[] { new CliArgumentSpec("name", "主题名") },
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "params",
                    Description: "列出主题可定制参数",
                    Arguments: new[] { new CliArgumentSpec("name", "主题名") },
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "wizard",
                    Description: "交互式主题创建向导",
                    Arguments: new[] { new CliArgumentSpec("name", "主题名") },
                    Options: new[]
                    {
                        new CliOptionSpec("--preset", "预设风格 (blog|docs|landing|minimal|portfolio)"),
                        new CliOptionSpec("--use", "创建后切换", CliOptionType.Flag),
                        new CliOptionSpec("--force", "覆盖已有主题", CliOptionType.Flag),
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "pack",
                    Description: "打包主题为 tar.gz",
                    Arguments: new[] { new CliArgumentSpec("name", "主题名") },
                    Options: new[]
                    {
                        new CliOptionSpec("--output", "输出路径"),
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
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
                    }),
                new CliCommandSpec(
                    Name: "search",
                    Description: "搜索社区主题注册表",
                    Arguments: new[] { new CliArgumentSpec("query", "搜索关键词") },
                    Options: new[]
                    {
                        new CliOptionSpec("--refresh", "强制刷新缓存", CliOptionType.Flag),
                        new CliOptionSpec("--registry-url", "注册表 URL"),
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    })
            });

        var template = new CliCommandSpec(
            Name: "template",
            Description: "模板级别操作命令",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "create",
                    Description: "交互式创建模板文件",
                    Arguments: new[] { new CliArgumentSpec("path", "模板路径") },
                    Options: new[]
                    {
                        new CliOptionSpec("--force", "覆盖已有模板", CliOptionType.Flag),
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "list",
                    Description: "列出当前主题所有模板",
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "show",
                    Description: "查看模板内容",
                    Arguments: new[] { new CliArgumentSpec("path", "模板路径") },
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "validate",
                    Description: "校验所有模板语法",
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "snippets",
                    Description: "浏览模板/CSS 片段库",
                    Arguments: new[] { new CliArgumentSpec("name", "片段名") },
                    Options: new[]
                    {
                        new CliOptionSpec("--config", "配置文件路径"),
                        new CliOptionSpec("--site", "多站点名")
                    }),
                new CliCommandSpec(
                    Name: "hints",
                    Description: "模板变量智能提示"),
                new CliCommandSpec(
                    Name: "sync",
                    Description: "自动生成 bukit.templates.yaml",
                    Options: new[]
                    {
                        new CliOptionSpec("--force", "覆盖已有文件", CliOptionType.Flag),
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

        var geo = new CliCommandSpec(
            Name: "geo",
            Description: "GEO (生成式引擎优化) 审计",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "audit",
                    Description: "检查 GEO 指标，读取 seo-report.json 并审核 AI 引擎优化表现",
                    Options: new[]
                    {
                        new CliOptionSpec("--dir", "构建输出目录")
                    })
            });

        var version = new CliCommandSpec(
            Name: "version",
            Description: "显示版本信息");

        var intent = new CliCommandSpec(
            Name: "intent",
            Description: "意图驱动的站点创建",
            Subcommands: new[]
            {
                new CliCommandSpec(
                    Name: "init",
                    Description: "交互式生成 intent.yaml",
                    Options: new[]
                    {
                        new CliOptionSpec("--out", "输出路径")
                    }),
                new CliCommandSpec(
                    Name: "apply",
                    Description: "应用 intent.yaml 生成站点"),
                new CliCommandSpec(
                    Name: "validate",
                    Description: "校验 intent.yaml")
            });

        var webhook = new CliCommandSpec(
            Name: "webhook",
            Description: "启动 Notion → GitHub Actions Webhook 服务",
            Options: new[]
            {
                new CliOptionSpec("--host", "监听地址"),
                new CliOptionSpec("--port", "监听端口", CliOptionType.String, ValueName: "port"),
                new CliOptionSpec("--path", "回调路径"),
                new CliOptionSpec("--repo", "GitHub 仓库 (owner/repo)"),
                new CliOptionSpec("--event", "GitHub dispatch event_type")
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

        var doctor = new CliCommandSpec(
            Name: "doctor",
            Description: "诊断站点配置和模板",
            Options: new[]
            {
                new CliOptionSpec("--config", "配置文件路径"),
                new CliOptionSpec("--site", "多站点名"),
                new CliOptionSpec("--site-url", "覆盖 site.url")
            });

        var init = new CliCommandSpec(
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
                new CliOptionSpec("--template", "模板名")
            });

        return new CliCommandRegistry(new[] { build, clone, deploy, dev, preview, plugin, theme, template, seo, geo, version, intent, webhook, clean, doctor, init });
    }
}
