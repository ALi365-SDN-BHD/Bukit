# Fix replace-failure-propagation and Test Gaps Spec

## Why
`--update-content replace` 的 `GetBlockChildrenIdsAsync` 和 `DeleteBlockAsync` 没有失败传播。当 Notion API 返回错误时，replace 会退化为 append（旧 blocks 未删除，新 blocks 追加）。这在 rate limit、权限不足、网络异常时会导致页面内容重复叠加。

同时，上轮要求的 2 个核心失败场景测试 (`append-failed` / `replace-failed`) 仍未覆盖。

## What Changes
- **GetBlockChildrenIdsAsync** 返回 `(bool Success, List<string> Ids)` — 分页失败时传播错误
- **DeleteBlockAsync** 返回 `(bool Success)` — 删除失败时传播错误
- **PushAsync replace 流程**检查两者结果，失败时标记 `"replace-failed"`，不继续 append
- 新增测试: `NotionPush_AppendFailed_MarksAppendFailed` — 验证 append 失败报告
- 新增测试: `NotionPush_ReplaceFailed_MarksReplaceFailed` — 验证 replace 失败不 append

## Impact
- Affected specs: notion-seed-push
- Affected code: [NotionSeedPusher.cs](file:///Users/ali/mydev/Git/Github/Bukit/src/Bukit.Cli/Commands/NotionSeedPusher.cs), [ImportCommandTests.cs](file:///Users/ali/mydev/Git/Github/Bukit/tests/Bukit.Cli.Tests/ImportCommandTests.cs)

## ADDED Requirements

### Requirement: Replace content block failure SHALL prevent partial updates
The system SHALL NOT append new blocks when the replace pre-phase (reading existing blocks or deleting existing blocks) fails.

#### Scenario: Read existing blocks fails
- **GIVEN** `--update-content replace` mode
- **AND** Notion API returns 500 or 429 when reading block children
- **WHEN** PushAsync processes the update
- **THEN** the item SHALL be marked as `"replace-failed"` with success=false
- **AND** NO new blocks SHALL be appended to the page

#### Scenario: Delete existing block fails
- **GIVEN** `--update-content replace` mode
- **AND** one of the block DELETE calls returns an error
- **WHEN** PushAsync processes the update
- **THEN** the item SHALL be marked as `"replace-failed"` with success=false
- **AND** NO new blocks SHALL be appended to the page

#### Scenario: Full replace succeeds
- **GIVEN** `--update-content replace` mode
- **AND** all read and delete operations succeed
- **WHEN** PushAsync processes the update
- **THEN** the item SHALL be marked as `"updated"` with success=true
- **AND** new blocks SHALL be appended to the page

### Requirement: Append content block failure SHALL be verified
The system SHALL check the result of the block children append API call and mark the item accordingly.

#### Scenario: Append blocks fails
- **GIVEN** `--update-content append` mode
- **AND** Notion block children PATCH returns an error
- **WHEN** PushAsync processes the update
- **THEN** the item SHALL be marked as `"append-failed"` with success=false

## REMOVED Requirements
None.
