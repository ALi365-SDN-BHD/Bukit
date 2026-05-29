# 全命令 CliBoundCommand 迁移实施计划

## 一、目标

将剩余 16 个使用 `ArgReader` 的命令全部迁移到 `CliBoundCommand`，最终删除 `ArgReader.cs` 和 Program.cs 旧路径 switch。

## 二、当前状态

```
                         Program.cs
                        ┌──────────┐
                        │ 旧路径    │ ← 16 个分支（本次目标）
          CliParser─────┤ switch   │
              │          │          │
              ▼          └──────────┘
    SimpleParseResult          ▲
         │                     │
         ▼                     │ fall through (SubcommandParseResult)
    ┌──────────────────┐       │
    │ 8 个已迁移命令     │       │
    │ CliBoundCommand  │       │
    └──────────────────┘       │
                          ┌────┴──────────────┐
                          │ 16 个未迁移命令      │
                          │ RunAsync(ArgReader) │
                          └───────────────────┘
```

迁移完成后：

```
       Program.cs
      ┌──────────────────────┐
      │  CliParser.Parse      │
      │    ├─ Simple → 8 命令 │
      │    └─ Subcmd  → 16命令│
      └──────────────────────┘
               │
               ▼
      CliBoundCommand (统一入口)
               │
               ▼
          ArgReader.cs 删除
       Program.cs 旧路径删除
```

## 三、命令分类与迁移策略

### 类别 A：已部分迁移 (1 个)

**DataCommand** — 已有 `RunAsync(CliBoundCommand)` + `RunAsync(ArgReader)` 双入口。

改动：删除旧的 `RunAsync(ArgReader)`，在 Program.cs 新路径添加 dispatch。

### 类别 B：纯入口模式 (9 个) — ArgReader 只在 RunAsync 入口使用

InitCommand, CleanCommand, CompletionCommand, VersionCommand, VisualCommand, WebhookCommand, DocsCheckCommand, SeoCommand, GeoCommand

模式：

```csharp
// 迁移前
RunAsync(ArgReader reader) {
    var opt1 = reader.GetOption("--opt1");
    var opt2 = reader.GetArg(1);
    // ... 提取完就不再碰 reader
    DoWork(opt1, opt2);
}

// 迁移后
RunAsync(ArgReader reader) {
    var spec = BukitCliSpecs.CreateRegistry().Resolve("cmd");
    return RunAsync(CliBoundCommandFactory.Create(reader, spec));
}
RunAsync(CliBoundCommand command) {
    var opt1 = command.GetString("--opt1");
    var opt2 = command.GetArgument(0);
    DoWork(opt1, opt2);
}
```

改动量：每个命令 \~5 行新增 + \~5 行改写。

### 类别 C：内部透传模式 (4 个) — ArgReader 透传给同一文件的私有方法

ConfigCommand, PluginCommand, TemplateCommand, IntentCommand

模式：

```csharp
// 迁移前
RunAsync(ArgReader reader) {
    var sub = reader.GetArg(1);
    switch(sub) {
        case "x": return DoX(reader);
    }
}
DoX(ArgReader reader) { reader.GetOption(...) }

// 迁移后
RunAsync(ArgReader reader) {
    return RunAsync(CliBoundCommandFactory.Create(reader, spec));
}
RunAsync(CliBoundCommand command) {
    var sub = command.GetArgument(0) ?? "default";
    switch(sub) {
        case "x": return DoX(command);
    }
}
DoX(CliBoundCommand command) { command.GetString(...) }
```

改动量：每个命令 \~10-15 行（需要改写内部方法签名）。

### 类别 D：深度透传链 (1 个) — ArgReader 传递到多个子命令文件

**ThemeCommand** + 5 个子命令文件：

```
ThemeCommand.RunAsync(ArgReader)
  ├─ ThemeWizardCommand.RunAsync(ArgReader)  ← 需要迁移
  │    └─ → ThemeCommand.SetThemeAsync(name, reader, ...)  ← 需要去 reader
  ├─ ThemePackCommand.RunAsync(ArgReader)     ← 需要迁移
  ├─ ThemeInstallCommand.RunAsync(ArgReader)  ← 需要迁移
  │    └─ → ThemeRegistryCommand.ResolveAsync(name, reader) ← 需要迁移
  └─ ThemeRegistryCommand.SearchAsync(ArgReader)  ← 需要迁移
```

这是唯一一个需要修改**多个文件**的命令。每个子命令文件遵循与类别 B/C 相同的改造模式。

改动量：约 6 个文件，每个 \~5-20 行。

## 四、实施步骤

### 阶段 1：扩大新路径适配器 (13 个 easy/medium 命令)

对以下命令执行标准迁移（与已完成的 build/deploy/doctor 完全一致的步骤）：

| 优先级 | 命令                                                                             | 文件数 | 子命令数 |
| --- | ------------------------------------------------------------------------------ | --- | ---- |
| P1  | InitCommand, CleanCommand, CompletionCommand, VersionCommand, DocsCheckCommand | 5   | 0    |
| P1  | VisualCommand, WebhookCommand, SeoCommand, GeoCommand                          | 4   | 1-2  |
| P2  | ConfigCommand, PluginCommand                                                   | 2   | 2/1  |
| P2  | TemplateCommand, IntentCommand                                                 | 2   | 7/3  |

每个命令的迁移操作（3 步）：

1. 将 `RunAsync(ArgReader)` 的方法体提取到新的 `RunAsync(CliBoundCommand)` 中
2. `RunAsync(ArgReader)` 改为 `CliBoundCommandFactory.Create(reader, spec)` + 委托调用
3. 内部方法签名 `ArgReader` → `CliBoundCommand`（如适用）

### 阶段 2：Theme 生态迁移 (hard)

按文件逐个迁移：

1. **ThemeCommand.cs**: 母命令适配，13 个子命令路由
2. **ThemeWizardCommand.cs**: 接受 CliBoundCommand 替代 ArgReader
3. **ThemePackCommand.cs**: 接受 CliBoundCommand 替代 ArgReader
4. **ThemeInstallCommand.cs**: 接受 CliBoundCommand 替代 ArgReader
5. **ThemeRegistryCommand.cs**: SearchAsync/ResolveAsync 接受 CliBoundCommand

### 阶段 3：DataCommand 收尾

删除旧的 `RunAsync(ArgReader)` 重载（新路径已不需要）。

### 阶段 4：Program.cs 改造

当所有命令都有 `RunAsync(CliBoundCommand)` 入口后：

```csharp
// 改造后的 Program.cs（伪代码）
var parsed = CliParser.Parse(spec, tail);
if (!parsed.IsSuccess) { ... return 2; }

var resolved = parsed switch
{
    SimpleParseResult s => DispatchSimple(spec.Name, s.BoundCommand),
    SubcommandParseResult c => DispatchSubcommand(spec.Name, c),
    _ => null
};
if (resolved.HasValue) return resolved.Value;

// 不再有回退旧路径！（或仅保留 UnknownCommand 作为兜底）
```

### 阶段 5：ArgReader 删除

最终检查：

* `grep -r "ArgReader" src/` → 只出现在 ArgReader.cs 自身 → **可安全删除**

* 删除 `/Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/ArgReader.cs`

* 删除 `ConfigPathResolver.Resolve(ArgReader)` 重载（如果没有调用者）

## 五、风险与缓解

| 风险                     | 等级 | 缓解措施                                            |
| ---------------------- | -- | ----------------------------------------------- |
| ThemeCommand 链路长（6 文件） | 中  | 每个子文件独立迁移，每步骤编译验证                               |
| SeoCommand 选项多（10+ 个）  | 低  | 仅入口提取，内部不变                                      |
| 子命令路由逻辑丢失              | 低  | `command.GetArgument(0)` 等价于 `reader.GetArg(1)` |
| 命令行行为回归                | 低  | 已有 \~770 CLI 测试覆盖                               |

## 六、预计改动规模

| 阶段            | 文件数       | 行数变化                                        |
| ------------- | --------- | ------------------------------------------- |
| 1 (easy)      | 9 文件      | \~+45 / -45                                 |
| 1 (medium)    | 4 文件      | \~+60 / -60                                 |
| 2 (Theme)     | 6 文件      | \~+60 / -60                                 |
| 3 (Data)      | 1 文件      | -5                                          |
| 4 (Program)   | 1 文件      | -16 / +30                                   |
| 5 (ArgReader) | 2 文件      | **删除** ArgReader.cs + ConfigPathResolver 重载 |
| **总计**        | **23 文件** | **\~+200 / -200**                           |

## 七、不可行性说明

以下命令的 `RunAsync(ArgReader)` 入口在迁移后仍需保留，直到阶段 4 完成旧的 Program.cs switch 被删除：

* 无。阶段 1-3 完成后所有命令都有 `RunAsync(CliBoundCommand)` 入口。阶段 4 的新路径 dispatch 使用 CliBoundCommand，ArgReader 只在 `RunAsync(ArgReader)` 适配器内部被创建和消费，这些适配器作为 CliBoundCommandFactory 的调用方是 ArgReader 的最后一处引用。阶段 5 删除所有适配器 + ArgReader.cs。

