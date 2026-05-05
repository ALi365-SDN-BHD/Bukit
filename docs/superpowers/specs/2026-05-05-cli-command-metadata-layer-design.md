# CLI 命令元数据层设计

## 目标

在不引入大型 CLI 框架的前提下，为 `Bukit.Cli` 增加一层薄的命令元数据模型，统一以下能力：

- 命令与子命令定义
- 位置参数与选项参数定义
- 全局 help 与子命令 help 输出
- 常见参数错误与未知命令错误信息
- 参数校验与默认值口径

本次设计的目标不是重写 CLI，而是让现有命令实现继续保留，把分散在入口、帮助输出、参数读取与报错文案中的“结构性信息”收束到同一来源，降低后续维护和文档漂移成本。

## 范围

- 调整 `src/Bukit.Cli` 内部 CLI 结构
- 新增命令元数据模型与注册表
- 新增统一 help 渲染逻辑
- 新增统一参数绑定与错误格式化逻辑
- 渐进迁移现有命令到元数据层
- 修正 help 与实现之间已经出现的轻微漂移

## 非目标

- 不引入 `System.CommandLine`、`Spectre.Console.Cli` 等大型框架
- 不重写 `build`、`doctor`、`preview` 等命令的业务逻辑
- 不一次性把所有 CLI 文档改为自动生成
- 不实现复杂 shell 补全、彩色终端、交互式提示等增强能力
- 不追求完整 GNU 级参数解析特性

## 现状问题

当前 CLI 入口与命令实现能工作，但“命令结构定义”分散在多处：

- `Program.cs` 手写命令分发与未知命令处理
- `HelpPrinter.cs` 手写全局命令与公共参数说明
- `ThemeCommand`、`PluginCommand`、`IntentCommand` 等命令各自维护 `PrintHelp()`
- `BuildCommand`、`PreviewCommand`、`WebhookCommand` 等各自读取参数并自行输出错误信息
- `ArgReader` 只提供 `HasFlag` / `GetOption` / `GetArg`，不能表达参数约束与元信息

这带来几个具体问题：

### 1. 参数定义与 help 容易漂移

- 参数真实支持情况由命令代码决定
- help 文案由单独代码维护
- 文档又是第三处来源
- 例如 `--jobs` 已在文档与 `BuildCommand` 中存在，但全局 help 未列出

### 2. 错误信息风格不统一

- 有的命令直接输出错误并返回 `2`
- 有的命令依赖入口 `catch` 打印异常消息
- 有的命令用 `✖`/`⚠`
- 有的命令只输出英文短句，不附 usage

### 3. 子命令模型不显式

- `theme`、`plugin`、`intent` 通过 `GetArg(1)` 手工解析子命令
- 子命令帮助与未知子命令错误是每个命令自己约定
- 很难形成统一行为

### 4. 参数校验分散

- `--port` 的合法性在 `PreviewCommand` 和 `WebhookCommand` 各自处理
- 布尔互斥参数如 `--clean` / `--no-clean` 由业务命令自己推断
- 缺少统一的“缺参 / 非法值 / 未知参数”路径

## 设计原则

### 1. 薄元数据，厚业务

- 元数据层只负责声明结构、绑定参数、渲染帮助、格式化错误
- 现有命令继续负责业务逻辑与副作用

### 2. 渐进迁移

- 允许旧命令与新命令在一段时间内并存
- 优先迁移参数复杂、最容易漂移的命令

### 3. 单一事实来源

- 命令名、别名、参数名、默认值说明、帮助文案、基础校验规则以元数据为准

### 4. 保持可读性

- 不做过度抽象
- 命令定义应能在单文件中直观看懂

## 推荐方案

采用“命令元数据注册表 + 统一解析/渲染层 + 命令执行适配器”的结构。

### 核心类型

建议新增以下模型：

- `CliCommandSpec`
  - 命令名
  - 简介
  - 别名
  - 位置参数列表
  - 选项列表
  - 子命令列表
  - 执行委托
- `CliArgumentSpec`
  - 名称
  - 是否必填
  - 帮助说明
  - 默认值说明
- `CliOptionSpec`
  - 长选项名，如 `--config`
  - 可选短名，如 `-c`
  - 值类型，如 `flag` / `string` / `int` / `enum`
  - 是否必填
  - 是否可重复
  - 默认值说明
  - 帮助说明
  - 校验器
- `CliCommandRegistry`
  - 注册所有顶层命令
  - 负责命令查找、别名解析与 help 索引
- `CliParseResult`
  - 命中命令
  - 已绑定参数
  - 解析错误列表
- `CliDiagnostic`
  - 诊断级别：error / warning
  - 错误码
  - 面向用户的消息
  - 是否建议附带 usage

### 解析与执行流程

建议把入口流程收束为：

1. `Program.cs` 从 `CliCommandRegistry` 读取顶层命令表
2. 统一解析顶层命令名与子命令链
3. 基于命令元数据解析位置参数和选项
4. 统一执行基础校验
5. 若存在解析错误，则由统一错误渲染器输出错误与 usage
6. 若解析成功，则将结果传给现有命令执行函数
7. 业务异常仍可由入口兜底，但消息格式要统一

### 参数绑定策略

建议不要让业务命令继续直接调用 `GetOption()`/`HasFlag()`，而是增加一层轻量绑定结果：

- `CliBoundCommand`
  - `GetString("--config")`
  - `GetBool("--strict-port")`
  - `GetInt("--port")`
  - `GetArgument(0)`
  - `TryGetEnum<T>()`

这样做的目的不是再造一套复杂 API，而是把“取值 + 基本校验 + 默认值”前移到统一层。

### help 渲染策略

由元数据统一生成：

- 顶层 help：列出命令、简介、公共选项
- 子命令 help：列出 usage、参数、选项、示例
- 错误后 help：只展示当前命令 usage 与相关参数，不回落到整份全局 help

建议输出结构统一为：

- 标题
- Usage
- Commands 或 Arguments
- Options
- Examples

### 错误信息策略

建议引入统一错误类别：

- `unknown-command`
- `unknown-subcommand`
- `missing-argument`
- `missing-option-value`
- `invalid-option-value`
- `unknown-option`
- `conflicting-options`

建议统一行为：

- 语法/参数错误返回 `2`
- 业务验证失败返回 `1`
- 成功返回 `0`

建议统一文案风格：

- 第一行直接说明问题
- 第二行给出修复方向
- 如有必要，附当前命令 usage

示例：

- `Unknown command: deploy`
- `Missing required argument: <dir>`
- `Invalid value for --port: abc`
- `Options --clean and --no-clean cannot be used together`

### 兼容与异常策略

- 入口仍保留总 `try/catch`
- 预期内参数错误不再依赖抛异常
- 业务命令中的文件不存在、网络失败、配置错误等可继续抛异常或返回业务错误
- 入口捕获异常后应统一格式化为单行主消息，必要时补充 inner exception

## 建议的文件拆分

建议在 `src/Bukit.Cli` 下新增一组轻量文件：

- `Cli/Metadata/CliCommandSpec.cs`
- `Cli/Metadata/CliOptionSpec.cs`
- `Cli/Metadata/CliArgumentSpec.cs`
- `Cli/Metadata/CliCommandRegistry.cs`
- `Cli/Parsing/CliParser.cs`
- `Cli/Parsing/CliParseResult.cs`
- `Cli/Rendering/CliHelpRenderer.cs`
- `Cli/Rendering/CliErrorRenderer.cs`
- `Cli/Binding/CliBoundCommand.cs`

现有命令文件保留，但逐步改成：

- `BuildCommand.RunAsync(CliBoundCommand command)`
- `PreviewCommand.RunAsync(CliBoundCommand command)`
- `ThemeCommand.RunAsync(CliBoundCommand command)`

如需降低迁移风险，也可以短期保留一个适配层，把 `CliBoundCommand` 暂时转换为旧 `ArgReader` 视图，待迁移完成后再删除。

## 命令建模建议

### 顶层命令

第一阶段纳入元数据层的顶层命令建议为：

- `build`
- `preview`
- `theme`
- `plugin`

原因：

- `build` 覆盖最多公共选项
- `preview` 覆盖数值参数校验和默认值
- `theme`、`plugin` 覆盖子命令 help 与未知子命令处理

### 第二阶段命令

- `doctor`
- `intent`
- `init`
- `clean`
- `webhook`
- `version`

原因：

- `doctor` 与 `intent` 有较多输出风格，适合在第一阶段模型稳定后迁移
- `webhook` 涉及环境变量与长运行进程，错误与帮助文案更适合在统一模型成熟后接入

## 迁移步骤

### 阶段 1：打基础

- 引入命令元数据模型与注册表
- 新建统一 help 渲染器与错误渲染器
- 保持 `ArgReader` 可用，避免一次性重写所有命令

### 阶段 2：迁移首批命令

- 为 `build`、`preview`、`theme`、`plugin` 建立元数据
- 改写 `Program.cs`，由注册表驱动命令发现与 help
- 将这些命令的 usage/help 从手写文案迁移为元数据生成

### 阶段 3：统一错误路径

- 统一参数错误返回码与文案
- 清理各命令中重复的 `Invalid --port.`、`Unknown ... subcommand`、`Missing ...` 文案

### 阶段 4：迁移剩余命令

- 接入 `doctor`、`intent`、`init`、`clean`、`webhook`、`version`
- 仅保留业务级输出，不再保留独立 help 实现

### 阶段 5：文档对齐

- 根据元数据更新 `guide/dev/cli.md`
- 根据元数据更新 `guide/user/12-命令行参考.md`
- 至少消除已知的 `--jobs` / 全局 help 漂移

## 测试建议

建议新增或补充以下测试，而不是给每个参数都写一份重复测试：

- 顶层 help 输出包含首批元数据命令
- `build --help`、`preview --help`、`theme --help`、`plugin --help` 输出符合预期
- 缺少必填位置参数时返回 `2` 并附 usage
- 非法 option 值时返回 `2`
- 未知子命令时返回 `2`
- 互斥参数冲突时返回 `2`
- 现有业务行为不变，例如 `ThemeCommand use`、`PluginCommand list`、`PreviewCommand --port auto`

测试重点应放在：

- 元数据到 help 的渲染是否正确
- 元数据到参数绑定的行为是否正确
- 迁移后业务命令的可观察行为是否未回归

## 风险与控制

### 1. 迁移期间双轨维护

风险：

- 一部分命令走元数据层，一部分仍走旧路径，容易出现行为差异

控制：

- 在 `Program.cs` 保持统一入口
- 每迁移一个命令，就删除其旧 help 文案分支

### 2. 过度抽象

风险：

- 为了“一次建对”而引入过多泛化接口，反而降低可读性

控制：

- 只支持当前 CLI 已出现的参数形态
- 暂不引入动态命令发现或插件式命令系统

### 3. 文档生成时机过早

风险：

- 在元数据模型尚未稳定前就强推文档自动生成，会拖慢首阶段落地

控制：

- 第一阶段先解决代码内部共源
- 文档自动生成或片段复用放到后续阶段评估

## 验收标准

- 顶层命令与首批子命令可由元数据注册表驱动发现
- `build`、`preview`、`theme`、`plugin` 的 help 不再手写维护
- 参数错误信息和 exit code 统一
- 现有命令业务行为保持不变
- 现有 CLI 文档中的已知漂移项得到修正
