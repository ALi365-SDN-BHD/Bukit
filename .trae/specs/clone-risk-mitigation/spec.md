# Clone 风险缓解 Spec

> 来源：`.trae/documents/bukit-audit-report-202605-28.md` P2-1

## Why

1. **README 措辞过强**："Clone any website's design" 暗示合规风险，实际功能是"提取设计令牌生成主题"
2. **JSON 解析静默失败**：`CloneTokens.FromJson()` 的 `catch` 块静默返回默认值（全 null），AI 生成的 tokens 有误时白屏无提示

## What Changes

- README.md：`"Clone any website's design"` → `"Extract design tokens and scaffold a Bukit theme"`（英文）
- README.zh-CN.md：同步修正中文措辞
- `CloneModels.cs`：`FromJson()` 解析失败时输出警告并返回失败标记，让 CloneCommand 能提前报错退出
- CloneCommand：检查 tokens 加载结果，失败时输出错误信息并返回退出码

## Impact

- Affected specs: 无
- Affected code:
  - `README.md` + `README.zh-CN.md` — 措辞修正
  - `src/Bukit.Cli/Commands/CloneModels.cs` — `FromJson()` 返回 `(CloneTokens?, string? error)`
  - `src/Bukit.Cli/Commands/CloneCommand.cs` — 检查返回值并报错

## MODIFIED Requirements

### Requirement: README 措辞修正

README 中 clone 相关描述 SHALL 改为 "Extract design tokens and scaffold a Bukit-compatible theme"。

#### Scenario: 用户阅读 README

- **WHEN** 用户阅读 README 的 clone 功能描述
- **THEN** 看到 "Extract design tokens and scaffold...", 而非 "Clone any website's design"

### Requirement: FromJson 解析失败时输出警告

`CloneTokens.FromJson()` SHALL 返回 `(CloneTokens?, string?)` 元组。解析失败时 error 非空，CloneCommand SHALL 检测并输出错误后退出。

#### Scenario: AI 生成无效 JSON

- **GIVEN** `tokens.json` 内容是 `{"primary": 123}`（类型错误）或 `{invalid json}`
- **WHEN** `bukit clone --tokens tokens.json` 执行
- **THEN** 输出 `✖ Failed to parse tokens.json: <error details>` 并返回退出码 2

#### Scenario: 正常 JSON 不受影响

- **GIVEN** 合法的 `tokens.json`
- **WHEN** `bukit clone --tokens tokens.json` 执行
- **THEN** 正常生成主题，退出码 0
