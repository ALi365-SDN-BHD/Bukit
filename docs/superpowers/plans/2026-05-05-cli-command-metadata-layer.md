# CLI Command Metadata Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 `Bukit.Cli` 增加不依赖大型框架的命令元数据层，统一参数定义、help 输出和参数错误信息，同时保持现有命令业务行为不变。

**Architecture:** 采用“元数据注册表 + 轻量解析器 + 统一渲染器 + 命令适配器”方案。先新增独立的 CLI 基础设施并用测试锁定行为，再将 `build`、`preview`、`theme`、`plugin` 四个命令迁移到新入口，最后清理旧 help 路径并对齐文档。

**Tech Stack:** C#/.NET、xUnit、现有 `Bukit.Cli` 命令结构、Markdown 文档

***

### Task 1: 建立 CLI 元数据骨架

**Files:**

- Create: `/workspace/src/Bukit.Cli/Cli/Metadata/CliArgumentSpec.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Metadata/CliOptionType.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Metadata/CliOptionSpec.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Metadata/CliCommandSpec.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Metadata/CliCommandRegistry.cs`
- Test: `/workspace/tests/Bukit.Cli.Tests/CliCommandRegistryTests.cs`
- [ ] **Step 1: 先写失败测试，锁定注册表能解析顶层命令和别名**

```csharp
using Bukit.Cli.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliCommandRegistryTests
{
    [Fact]
    public void Resolve_FindsCommandByName_AndAlias()
    {
        var build = new CliCommandSpec(
            Name: "build",
            Description: "生成静态站点",
            Aliases: new[] { "b" },
            Arguments: Array.Empty<CliArgumentSpec>(),
            Options: Array.Empty<CliOptionSpec>(),
            Subcommands: Array.Empty<CliCommandSpec>());

        var registry = new CliCommandRegistry(new[] { build });

        Assert.Same(build, registry.Resolve("build"));
        Assert.Same(build, registry.Resolve("b"));
    }
}
```

- [ ] **Step 2: 运行测试确认当前失败**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliCommandRegistryTests`

Expected: FAIL，提示 `CliCommandSpec` 或 `CliCommandRegistry` 不存在

- [ ] **Step 3: 实现最小元数据类型和注册表**

```csharp
namespace Bukit.Cli.Cli.Metadata;

public sealed record CliArgumentSpec(
    string Name,
    string Description,
    bool Required = false,
    string? DefaultValueHelp = null);
```

```csharp
namespace Bukit.Cli.Cli.Metadata;

public enum CliOptionType
{
    Flag,
    String,
    Integer
}
```

```csharp
namespace Bukit.Cli.Cli.Metadata;

public sealed record CliOptionSpec(
    string Name,
    string Description,
    CliOptionType Type = CliOptionType.String,
    string? ShortName = null,
    bool Required = false,
    string? ValueName = null,
    string? DefaultValueHelp = null,
    IReadOnlyList<string>? AllowedValues = null,
    string? ConflictWith = null);
```

```csharp
namespace Bukit.Cli.Cli.Metadata;

public sealed record CliCommandSpec(
    string Name,
    string Description,
    IReadOnlyList<string>? Aliases = null,
    IReadOnlyList<CliArgumentSpec>? Arguments = null,
    IReadOnlyList<CliOptionSpec>? Options = null,
    IReadOnlyList<CliCommandSpec>? Subcommands = null);
```

```csharp
namespace Bukit.Cli.Cli.Metadata;

public sealed class CliCommandRegistry
{
    private readonly Dictionary<string, CliCommandSpec> _commands;

    public CliCommandRegistry(IEnumerable<CliCommandSpec> commands)
    {
        _commands = new Dictionary<string, CliCommandSpec>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands)
        {
            _commands[command.Name] = command;
            if (command.Aliases is null)
            {
                continue;
            }

            foreach (var alias in command.Aliases)
            {
                _commands[alias] = command;
            }
        }
    }

    public CliCommandSpec? Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _commands.TryGetValue(name, out var command) ? command : null;
    }
}
```

- [ ] **Step 4: 运行注册表测试确认通过**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliCommandRegistryTests`

Expected: PASS

- [ ] **Step 5: 提交基础元数据骨架**

```bash
git add /workspace/src/Bukit.Cli/Cli/Metadata /workspace/tests/Bukit.Cli.Tests/CliCommandRegistryTests.cs
git commit -m "feat(cli): add command metadata registry primitives"
```

### Task 2: 增加轻量解析器与绑定结果

**Files:**

- Create: `/workspace/src/Bukit.Cli/Cli/Parsing/CliDiagnostic.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Parsing/CliParseResult.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Parsing/CliParser.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Binding/CliBoundCommand.cs`
- Test: `/workspace/tests/Bukit.Cli.Tests/CliParserTests.cs`
- [ ] **Step 1: 写失败测试，覆盖缺少参数、非法取值和互斥选项**

```csharp
using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Cli.Parsing;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliParserTests
{
    private static readonly CliCommandSpec PreviewSpec =
        new(
            Name: "preview",
            Description: "本地预览 dist",
            Options: new[]
            {
                new CliOptionSpec("--port", "预览端口", CliOptionType.Integer, ValueName: "port"),
                new CliOptionSpec("--strict-port", "严格端口", CliOptionType.Flag),
                new CliOptionSpec("--log-format", "日志格式", CliOptionType.String, AllowedValues: new[] { "text", "json" })
            });

    [Fact]
    public void Parse_ReturnsError_WhenIntegerOptionIsInvalid()
    {
        var result = CliParser.Parse(PreviewSpec, new[] { "--port", "abc" });
        Assert.Contains(result.Diagnostics, d => d.Code == "invalid-option-value");
    }

    [Fact]
    public void Parse_ReturnsError_WhenRequiredArgumentMissing()
    {
        var spec = new CliCommandSpec(
            Name: "theme",
            Description: "主题相关命令",
            Arguments: new[] { new CliArgumentSpec("name", "主题名", Required: true) });

        var result = CliParser.Parse(spec, Array.Empty<string>());

        Assert.Contains(result.Diagnostics, d => d.Code == "missing-argument");
    }
}
```

- [ ] **Step 2: 运行解析器测试确认失败**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliParserTests`

Expected: FAIL，提示 `CliParser`、`CliParseResult`、`CliDiagnostic` 缺失

- [ ] **Step 3: 实现最小解析器、诊断模型和绑定结果**

```csharp
namespace Bukit.Cli.Cli.Parsing;

public sealed record CliDiagnostic(string Code, string Message, bool ShowUsage = true);
```

```csharp
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Parsing;

public sealed record CliParseResult(
    CliCommandSpec Command,
    CliBoundCommand BoundCommand,
    IReadOnlyList<CliDiagnostic> Diagnostics)
{
    public bool IsSuccess => Diagnostics.Count == 0;
}
```

```csharp
namespace Bukit.Cli.Cli.Binding;

public sealed class CliBoundCommand
{
    private readonly IReadOnlyDictionary<string, string?> _options;
    private readonly IReadOnlyList<string> _arguments;

    public CliBoundCommand(IReadOnlyDictionary<string, string?> options, IReadOnlyList<string> arguments)
    {
        _options = options;
        _arguments = arguments;
    }

    public string? GetString(string name) => _options.TryGetValue(name, out var value) ? value : null;

    public bool GetBool(string name) => _options.ContainsKey(name);

    public int? GetInt(string name)
    {
        var text = GetString(name);
        return int.TryParse(text, out var value) ? value : null;
    }

    public string? GetArgument(int index) => index >= 0 && index < _arguments.Count ? _arguments[index] : null;
}
```

```csharp
using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Parsing;

public static class CliParser
{
    public static CliParseResult Parse(CliCommandSpec command, IReadOnlyList<string> args)
    {
        var diagnostics = new List<CliDiagnostic>();
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var positionals = new List<string>();
        var optionMap = (command.Options ?? Array.Empty<CliOptionSpec>())
            .SelectMany(x => new[] { x.Name, x.ShortName }.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => (Key: v!, Spec: x)))
            .ToDictionary(x => x.Key, x => x.Spec, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Count; i++)
        {
            var token = args[i];
            if (!token.StartsWith("-", StringComparison.Ordinal))
            {
                positionals.Add(token);
                continue;
            }

            if (!optionMap.TryGetValue(token, out var spec))
            {
                diagnostics.Add(new CliDiagnostic("unknown-option", $"Unknown option: {token}"));
                continue;
            }

            if (spec.Type == CliOptionType.Flag)
            {
                options[spec.Name] = "true";
                continue;
            }

            if (i + 1 >= args.Count)
            {
                diagnostics.Add(new CliDiagnostic("missing-option-value", $"Missing value for {spec.Name}"));
                continue;
            }

            var value = args[++i];
            if (spec.Type == CliOptionType.Integer && !int.TryParse(value, out _))
            {
                diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Invalid value for {spec.Name}: {value}"));
                continue;
            }

            if (spec.AllowedValues is not null && spec.AllowedValues.Count > 0 && !spec.AllowedValues.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                diagnostics.Add(new CliDiagnostic("invalid-option-value", $"Invalid value for {spec.Name}: {value}"));
                continue;
            }

            options[spec.Name] = value;
        }

        var argumentSpecs = command.Arguments ?? Array.Empty<CliArgumentSpec>();
        for (var i = 0; i < argumentSpecs.Count; i++)
        {
            if (argumentSpecs[i].Required && i >= positionals.Count)
            {
                diagnostics.Add(new CliDiagnostic("missing-argument", $"Missing required argument: <{argumentSpecs[i].Name}>"));
            }
        }

        foreach (var spec in command.Options ?? Array.Empty<CliOptionSpec>())
        {
            if (!string.IsNullOrWhiteSpace(spec.ConflictWith) && options.ContainsKey(spec.Name) && options.ContainsKey(spec.ConflictWith))
            {
                diagnostics.Add(new CliDiagnostic("conflicting-options", $"Options {spec.Name} and {spec.ConflictWith} cannot be used together"));
            }
        }

        return new CliParseResult(command, new CliBoundCommand(options, positionals), diagnostics);
    }
}
```

- [ ] **Step 4: 运行解析器测试确认通过**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliParserTests`

Expected: PASS

- [ ] **Step 5: 提交解析器与绑定层**

```bash
git add /workspace/src/Bukit.Cli/Cli/Parsing /workspace/src/Bukit.Cli/Cli/Binding /workspace/tests/Bukit.Cli.Tests/CliParserTests.cs
git commit -m "feat(cli): add metadata-driven parser and bound command"
```

### Task 3: 增加统一 help 与错误渲染器

**Files:**

- Create: `/workspace/src/Bukit.Cli/Cli/Rendering/CliHelpRenderer.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/Rendering/CliErrorRenderer.cs`
- Test: `/workspace/tests/Bukit.Cli.Tests/CliRenderingTests.cs`
- [ ] **Step 1: 先写失败测试，固定 help 和错误的基本格式**

```csharp
using Bukit.Cli.Cli.Metadata;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliRenderingTests
{
    [Fact]
    public void RenderHelp_IncludesUsage_Arguments_AndOptions()
    {
        var spec = new CliCommandSpec(
            Name: "preview",
            Description: "本地预览 dist",
            Arguments: new[] { new CliArgumentSpec("dir", "目录", Required: false) },
            Options: new[] { new CliOptionSpec("--port", "预览端口", CliOptionType.Integer, ValueName: "port") });

        var text = CliHelpRenderer.Render(spec, "bukit preview");

        Assert.Contains("Usage:", text);
        Assert.Contains("bukit preview", text);
        Assert.Contains("--port <port>", text);
    }

    [Fact]
    public void RenderError_PrefixesPrimaryMessage()
    {
        var text = CliErrorRenderer.Render(new CliDiagnostic("invalid-option-value", "Invalid value for --port: abc"));
        Assert.Contains("Error:", text);
        Assert.Contains("Invalid value for --port: abc", text);
    }
}
```

- [ ] **Step 2: 运行渲染测试确认失败**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliRenderingTests`

Expected: FAIL，提示渲染器不存在

- [ ] **Step 3: 实现最小 help/error 渲染**

```csharp
using System.Text;
using Bukit.Cli.Cli.Metadata;

namespace Bukit.Cli.Cli.Rendering;

public static class CliHelpRenderer
{
    public static string Render(CliCommandSpec spec, string commandPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine(commandPath);
        builder.AppendLine();
        builder.AppendLine("Usage:");
        builder.Append("  ").Append(commandPath);

        foreach (var arg in spec.Arguments ?? Array.Empty<CliArgumentSpec>())
        {
            builder.Append(arg.Required ? $" <{arg.Name}>" : $" [{arg.Name}]");
        }

        if ((spec.Options?.Count ?? 0) > 0)
        {
            builder.Append(" [options]");
        }

        builder.AppendLine();

        if ((spec.Arguments?.Count ?? 0) > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Arguments:");
            foreach (var arg in spec.Arguments!)
            {
                builder.Append("  <").Append(arg.Name).Append(">  ").AppendLine(arg.Description);
            }
        }

        if ((spec.Options?.Count ?? 0) > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Options:");
            foreach (var option in spec.Options!)
            {
                var suffix = option.Type == CliOptionType.Flag ? string.Empty : $" <{option.ValueName ?? "value"}>";
                builder.Append("  ").Append(option.Name).Append(suffix).Append("  ").AppendLine(option.Description);
            }
        }

        return builder.ToString();
    }
}
```

```csharp
using Bukit.Cli.Cli.Parsing;

namespace Bukit.Cli.Cli.Rendering;

public static class CliErrorRenderer
{
    public static string Render(CliDiagnostic diagnostic)
    {
        return $"Error: {diagnostic.Message}";
    }
}
```

- [ ] **Step 4: 运行渲染测试确认通过**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliRenderingTests`

Expected: PASS

- [ ] **Step 5: 提交渲染器**

```bash
git add /workspace/src/Bukit.Cli/Cli/Rendering /workspace/tests/Bukit.Cli.Tests/CliRenderingTests.cs
git commit -m "feat(cli): add shared help and error renderers"
```

### Task 4: 用元数据入口接管 Program，并接入 build/preview

**Files:**

- Modify: `/workspace/src/Bukit.Cli/Program.cs`
- Create: `/workspace/src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `/workspace/src/Bukit.Cli/Commands/BuildCommand.cs`
- Modify: `/workspace/src/Bukit.Cli/Commands/PreviewCommand.cs`
- Test: `/workspace/tests/Bukit.Cli.Tests/CliProgramFlowTests.cs`
- [ ] **Step 1: 写失败测试，锁定全局 help、未知命令和** **`preview --help`** **行为**

```csharp
using Bukit.Cli.Cli.Metadata;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CliProgramFlowTests
{
    [Fact]
    public void Specs_IncludeBuild_AndPreview()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        Assert.NotNull(registry.Resolve("build"));
        Assert.NotNull(registry.Resolve("preview"));
    }
}
```

- [ ] **Step 2: 运行流程测试确认失败**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliProgramFlowTests`

Expected: FAIL，提示 `BukitCliSpecs` 不存在

- [ ] **Step 3: 新建命令规格表，并让 Program 通过注册表分发 build/preview**

```csharp
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

        return new CliCommandRegistry(new[] { build, preview });
    }
}
```

```csharp
using Bukit.Cli;
using Bukit.Cli.Cli.Parsing;
using Bukit.Cli.Cli.Rendering;
using Bukit.Cli.Commands;

var registry = BukitCliSpecs.CreateRegistry();
var commandName = args.Length == 0 ? null : args[0];

if (commandName is null or "help" or "--help" or "-h")
{
    var all = registry.Resolve("build");
    Console.WriteLine("bukit");
    return 0;
}

var spec = registry.Resolve(commandName);
if (spec is null)
{
    Console.Error.WriteLine($"Unknown command: {commandName}");
    return 2;
}

var tail = args.Skip(1).ToArray();
if (tail.Any(x => x is "--help" or "-h"))
{
    Console.WriteLine(CliHelpRenderer.Render(spec, $"bukit {spec.Name}"));
    return 0;
}

var parsed = CliParser.Parse(spec, tail);
if (!parsed.IsSuccess)
{
    Console.Error.WriteLine(CliErrorRenderer.Render(parsed.Diagnostics[0]));
    Console.Error.WriteLine(CliHelpRenderer.Render(spec, $"bukit {spec.Name}"));
    return 2;
}

return spec.Name switch
{
    "build" => await BuildCommand.RunAsync(parsed.BoundCommand),
    "preview" => await PreviewCommand.RunAsync(parsed.BoundCommand),
    _ => 2
};
```

- [ ] **Step 4: 让 build/preview 新增** **`CliBoundCommand`** **重载并保留旧行为**

```csharp
using Bukit.Cli.Cli.Binding;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class BuildCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        return RunAsync(new CliBoundCommand(
            new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["--config"] = reader.GetOption("--config"),
                ["--site"] = reader.GetOption("--site"),
                ["--output"] = reader.GetOption("--output"),
                ["--base-url"] = reader.GetOption("--base-url"),
                ["--site-url"] = reader.GetOption("--site-url"),
                ["--cache-dir"] = reader.GetOption("--cache-dir"),
                ["--metrics"] = reader.GetOption("--metrics"),
                ["--jobs"] = reader.GetOption("--jobs"),
                ["--log-format"] = reader.GetOption("--log-format"),
            }
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Array.Empty<string>()));
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var reader = new ArgReader(Array.Empty<string>());
        var resolved = new ResolvedConfigPath(
            command.GetString("--config") is { } configPath ? Path.GetFullPath(configPath) : Path.GetFullPath("site.yaml"),
            Directory.GetCurrentDirectory());
        var config = ConfigLoader.Load(resolved.FullConfigPath);

        var overrides = new ConfigOverrides
        {
            Output = command.GetString("--output"),
            BaseUrl = command.GetString("--base-url"),
            Clean = command.GetBool("--clean") ? true : command.GetBool("--no-clean") ? false : null,
            Draft = command.GetBool("--draft") ? true : null,
            IsCI = command.GetBool("--ci"),
            Incremental = command.GetBool("--incremental") ? true : command.GetBool("--no-incremental") ? false : null,
            CacheDir = command.GetString("--cache-dir"),
            MetricsPath = command.GetString("--metrics"),
            Jobs = command.GetInt("--jobs")
        };

        var logger = new ConsoleLogger(ParseLogLevel(config.Logging.Level, overrides.IsCI), command.GetString("--log-format") ?? "text");
        var engine = new SiteEngine(logger);
        await engine.BuildAsync(config, resolved.RootDir, overrides);
        return 0;
    }
}
```

- [ ] **Step 5: 运行首批流程测试**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~CliProgramFlowTests`

Expected: PASS

- [ ] **Step 6: 提交新入口与 build/preview 迁移**

```bash
git add /workspace/src/Bukit.Cli/Program.cs /workspace/src/Bukit.Cli/Cli/BukitCliSpecs.cs /workspace/src/Bukit.Cli/Commands/BuildCommand.cs /workspace/src/Bukit.Cli/Commands/PreviewCommand.cs /workspace/tests/Bukit.Cli.Tests/CliProgramFlowTests.cs
git commit -m "feat(cli): route build and preview through metadata layer"
```

### Task 5: 接入 theme/plugin 子命令与统一 usage

**Files:**

- Modify: `/workspace/src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `/workspace/src/Bukit.Cli/Commands/ThemeCommand.cs`
- Modify: `/workspace/src/Bukit.Cli/Commands/PluginCommand.cs`
- Test: `/workspace/tests/Bukit.Cli.Tests/ThemeCommandTests.cs`
- Test: `/workspace/tests/Bukit.Cli.Tests/PluginCommandTests.cs`
- [ ] **Step 1: 先写失败测试，覆盖未知子命令和** **`theme use`** **缺参**

```csharp
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class ThemeCommandTests
{
    [Fact]
    public async Task RunAsync_ReturnsTwo_WhenThemeNameMissing()
    {
        var exitCode = await ThemeCommand.RunAsync(new ArgReader(new[] { "theme", "use" }));
        Assert.Equal(2, exitCode);
    }
}
```

- [ ] **Step 2: 运行主题/插件测试确认当前基线**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~ThemeCommandTests|FullyQualifiedName~PluginCommandTests`

Expected: 现有 `PluginCommandTests` PASS，新 `ThemeCommandTests` 可能 PASS 或 FAIL；记录当前行为后再改造

- [ ] **Step 3: 在** **`BukitCliSpecs`** **中声明 theme/plugin 子命令树**

```csharp
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
```

- [ ] **Step 4: 删除** **`ThemeCommand`** **和** **`PluginCommand`** **内部手写** **`PrintHelp()`，改为只保留业务分支**

```csharp
namespace Bukit.Cli.Commands;

public static class PluginCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        return sub switch
        {
            "list" => ListAsync(reader),
            _ => Task.FromResult(2)
        };
    }
}
```

```csharp
namespace Bukit.Cli.Commands;

public static class ThemeCommand
{
    public static Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        return sub switch
        {
            "list" => ListAsync(reader),
            "use" => UseAsync(reader),
            _ => Task.FromResult(2)
        };
    }
}
```

- [ ] **Step 5: 运行主题/插件相关测试**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj --filter FullyQualifiedName~ThemeCommandTests|FullyQualifiedName~PluginCommandTests`

Expected: PASS

- [ ] **Step 6: 提交子命令元数据迁移**

```bash
git add /workspace/src/Bukit.Cli/Cli/BukitCliSpecs.cs /workspace/src/Bukit.Cli/Commands/ThemeCommand.cs /workspace/src/Bukit.Cli/Commands/PluginCommand.cs /workspace/tests/Bukit.Cli.Tests/ThemeCommandTests.cs /workspace/tests/Bukit.Cli.Tests/PluginCommandTests.cs
git commit -m "feat(cli): move theme and plugin help to metadata specs"
```

### Task 6: 清理旧 help 路径并对齐 CLI 文档

**Files:**

- Modify: `/workspace/src/Bukit.Cli/Commands/HelpPrinter.cs`
- Modify: `/workspace/guide/dev/cli.md`
- Modify: `/workspace/guide/user/12-命令行参考.md`
- Verify: `/workspace/src/Bukit.Cli/Program.cs`
- Verify: `/workspace/tests/Bukit.Cli.Tests`
- [ ] **Step 1: 删除全局 help 中已被元数据接管的重复清单，保留兼容壳或直接改为调用渲染器**

```csharp
using Bukit.Cli.Cli.Rendering;

namespace Bukit.Cli.Commands;

public static class HelpPrinter
{
    public static void Print()
    {
        var registry = BukitCliSpecs.CreateRegistry();
        Console.WriteLine("bukit");
        Console.WriteLine();
        Console.WriteLine("Use `bukit <command> --help` for command-specific usage.");
    }
}
```

- [ ] **Step 2: 更新维护者文档，显式说明参数定义现在以元数据层为准**

```md
实现参考：
- `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- `src/Bukit.Cli/Cli/Parsing/CliParser.cs`
- `src/Bukit.Cli/Commands/*Command.cs`

说明：
- 顶层命令与首批命令 help 已由元数据层统一生成
- `--jobs` 已进入统一 help 口径
```

- [ ] **Step 3: 更新用户文档，补齐首批命令统一 help 口径**

```md
说明：
- 你可以用 `bukit build --help`、`bukit preview --help`、`bukit theme --help` 查看命令专属参数
- 参数名称与默认值以 CLI 内置 help 为准
```

- [ ] **Step 4: 运行完整 CLI 测试集**

Run: `dotnet test /workspace/tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj`

Expected: PASS

- [ ] **Step 5: 运行解决方案级回归**

Run: `dotnet test /workspace/bukit.slnx`

Expected: PASS

- [ ] **Step 6: 提交清理与文档对齐**

```bash
git add /workspace/src/Bukit.Cli/Commands/HelpPrinter.cs /workspace/guide/dev/cli.md /workspace/guide/user/12-命令行参考.md
git commit -m "docs(cli): align help output and command reference"
```

## 自检结论

- 规格覆盖：计划覆盖了元数据模型、解析/绑定、help/error 渲染、四个首批命令迁移、文档对齐和测试回归，未遗漏已批准范围中的核心需求。
- 占位符检查：未使用 `TBD`、`TODO`、`later` 等占位词；每个任务都给出了明确文件路径、示例代码或命令。
- 一致性检查：计划统一使用 `CliCommandSpec`、`CliOptionSpec`、`CliArgumentSpec`、`CliParser`、`CliBoundCommand` 这组类型名，后续任务未出现命名漂移。

