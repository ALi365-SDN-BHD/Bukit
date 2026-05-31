# Tasks

- [x] Task 1: 修复 `DiagnosticCode.cs` 第 15-22 行 Route 枚举值的多余对齐空格
  - [x] 1.1 删除第 15-22 行 `=` 前的多余空格，使每个枚举值格式统一为 `<Name> = <Value>`（单空格分隔）

- [x] Task 2: 修复 `RenderDependencyHasherTests.cs` 第 243/263/277 行的长行对象初始化器
  - [x] 2.1 将 3 处 `new CollectionConfig { Permalink = ..., Template = ..., ... }` 拆分为多行，每行缩进 24 空格（6 级 × 4）

- [x] Task 3: 修复 `ProtocolEchoPlugin/Program.cs` 第 73-74 行的链式 `??` 换行
  - [x] 3.1 将链式 `??` 表达式重新格式化，使缩进符合 editorconfig 规则

- [x] Task 4: 验证修复
  - [x] 4.1 运行 `dotnet format whitespace --verify-no-changes` 确认 0 错误
  - [x] 4.2 运行项目测试套件确认无回归

# Task Dependencies
- Task 1, Task 2, Task 3 相互独立，可并行执行
- Task 4 依赖 Task 1, Task 2, Task 3 全部完成
