# 删除旧路径死代码 + ArgReader 删除可行性分析 Spec

## Why
CLI 统一迁移后，Program.cs 旧路径 switch 中存在 7 条已被新路径完全覆盖的**不可达死代码**。这些分支源于迁移前的新旧双路径架构，现在新路径对 SimpleParseResult 命令先执行并 return，旧路径中对应的 case 永远不会被命中。清除后可简化调度逻辑。同时评估 ArgReader.cs 是否可删除。

## What Changes
- 从 Program.cs 旧路径 switch 中删除 7 条死分支：build、clone、deploy、dev、doctor、preview、lint
- 保留 16 条仍活跃的分支（均为子命令命令，走 ArgReader）
- ArgReader.cs **不能删除**——29 个源文件 / 62 处引用，其中包括所有子命令命令的 `RunAsync(ArgReader)` 入口

## Impact
- Affected specs: `cli-unify-migration`、`dev-command-migration`
- Affected code: `Program.cs` 仅 1 个文件

## 死代码论证

### 执行路径分析

```
用户输入 bukit <command>
    ↓
L7: reader = new ArgReader(args)
L8: command = reader.Command
L24: spec = registry.Resolve(command)
    ↓
L26: if (spec is not null)  ← 所有 21 个命令都在 BukitCliSpecs 中定义
    ↓
L35: parsed = CliParser.Parse(spec, tail)
    ↓
L36: if (!parsed.IsSuccess) → 输出错误, return 2  ← 解析失败直接退出
    ↓
L43: if (parsed is SimpleParseResult simple)  ← 8 个无子命令的命令
    ↓
L45-54: switch dispatch → return resolved.Value  ← 成功执行并 return
    ↓
L61: }  ← 以下代码仅在 SimpleParseResult 未被匹配或 spec 为 null 时可达
    ↓
L63-89: 旧路径 switch  ← 这是当前所处位置
```

### 死分支证明

对以下 7 个命令，旧路径的分支**永远不可达**：

| 命令 | Spec 定义 | 子命令 | CliParser 结果 | 新路径 dispatch | 证明 |
|------|----------|--------|---------------|----------------|------|
| build | ✅ L9-29 | 无 | SimpleParseResult | L47 匹配并 return | 死 |
| clone | ✅ L72-94 | 无 | SimpleParseResult | L48 匹配并 return | 死 |
| deploy | ✅ L301-316 | 无 | SimpleParseResult | L49 匹配并 return | 死 |
| dev | ✅ L44-55 | 无 | SimpleParseResult | L50 匹配并 return | 死 |
| doctor | ✅ L415-423 | 无 | SimpleParseResult | L51 匹配并 return | 死 |
| preview | ✅ L31-42 | 无 | SimpleParseResult | L52 匹配并 return | 死 |
| lint | ✅ L383-390 | 无 | SimpleParseResult | L53 匹配并 return | 死 |

反证法：要使旧路径的 `"build" => BuildCommand.RunAsync(reader)` 被执行，需要同时满足：
1. spec 不为 null（L24 通过，因为 build 在 BukitCliSpecs 中）
2. 无 `--help`/`-h`（L29 不触发）
3. CliParser.Parse 成功（L36 不触发，否则 L40 return 2）
4. parsed 不是 SimpleParseResult（L43 不触发，但 build 无子命令，Parse 永远返回 SimpleParseResult）

第 4 条在逻辑上不可能成立 → 旧路径的 "build" 分支是不可达死代码。其余 6 个命令同理。

### 活跃分支（仍需要保留）

以下 16 个分支仍然活跃，因为对应的命令有子命令，CliParser 返回 SubcommandParseResult，L43 不匹配，fall through 到旧路径：

`create`/`init`/`clean`/`completion`/`config`/`plugin`/`seo`/`geo`/`data`/`docs`/`theme`/`template`/`intent`/`visual`/`webhook`/`version`

## ArgReader 删除可行性评估（步骤 4）

**结论：不可行。** ArgReader.cs 仍有 62 处引用分布在 29 个源文件中。

引用分布：
- Program.cs：创建 ArgReader 实例（入口点）
- CliBoundCommandFactory.cs：接受 ArgReader 参数（核心适配器）
- ConfigPathResolver.Resolve(ArgReader)：仍被子命令命令使用
- 已迁移命令的 RunAsync(ArgReader)：虽为死代码但保留作为回退兼容
- 未迁移子命令命令的 RunAsync(ArgReader)：**主入口**，无法删除

**删除 ArgReader 的前置条件**：所有 16 个子命令命令迁移到 CliBoundCommand 后，ArgReader 仅剩 0 处必要引用。此为一个独立的、范围较大的迁移任务。

## ADDED Requirements

### Requirement 1: Program.cs 旧路径死代码清理
系统 SHALL 从 Program.cs 的旧路径 switch 中删除 7 条被新路径完全覆盖的死分支。

#### Scenario: 7 条死分支被删除
- **WHEN** 代码审查 Program.cs 的旧路径 switch
- **THEN** 不再包含 `"build"`、`"clone"`、`"deploy"`、`"dev"`、`"doctor"`、`"preview"`、`"lint"` 的 case

#### Scenario: 活跃分支保持不变
- **WHEN** `bukit theme create --name my-theme` 被执行
- **THEN** 旧路径的 `"theme" => await ThemeCommand.RunAsync(reader)` 分支仍正常触发
