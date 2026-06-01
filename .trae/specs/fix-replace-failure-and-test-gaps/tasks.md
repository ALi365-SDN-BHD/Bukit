# Tasks

- [x] Task 1: GetBlockChildrenIdsAsync 返回 `(bool Success, List<string> Ids)`
  - [x] 改返回类型为 `Task<(bool Success, List<string> Ids)>`
  - [x] 分页循环中途失败 → 返回 `(false, ids)`
  - [x] 全部分页成功 → 返回 `(true, ids)`

- [x] Task 2: DeleteBlockAsync 返回 `(bool Success)`
  - [x] 改返回类型为 `Task<bool>`
  - [x] 检查 `response.IsSuccessStatusCode` → 返回 true/false

- [x] Task 3: PushAsync replace 流程检查失败传播
  - [x] 读取 children 失败 → 标记 `"replace-failed"`，`continue`
  - [x] 任一 block 删除失败 → 标记 `"replace-failed"`，`continue`
  - [x] 全部成功 → 正常 append 新 blocks

- [x] Task 4: 新增测试 `Push_AppendFailed_MarksAppendFailed`
  - [x] mock: PATCH /blocks/{pageId}/children 返回 400
  - [x] 断言: blocksRequested = true
  - [x] 断言: report 包含 `"append-failed"`

- [x] Task 5: 新增测试 `Push_ReplaceFailed_MarksReplaceFailed`
  - [x] mock: GET /blocks/{pageId}/children 返回 500
  - [x] 断言: blocksReadAttempted = true
  - [x] 断言: report 包含 `"replace-failed"`

- [x] Task 6: 运行全量测试验证
  - [x] `dotnet build` 0 errors
  - [x] `dotnet test` 全部通过 (3,306 passed)

# Task Dependencies
- Task 3 depends on Task 1 and Task 2
- Task 4, Task 5 depend on Task 3
- Task 6 depends on Task 4, Task 5
