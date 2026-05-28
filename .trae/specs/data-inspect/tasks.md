# Tasks

## Task 1: DataCommand 新建 ✅
- [x] 1.1 新建 `DataCommand.cs`（270 行，3 个方法）
- [x] 1.2 `inspect` 子命令 — 按 type 分组输出
- [x] 1.3 `inspect --module <name>` 详情模式
- [x] 1.4 `dump --format json` 子命令（AOT 兼容 Utf8JsonWriter）
- [x] 1.5 CLI 注册 `Program.cs` + `BukitCliSpecs.cs`

## Task 2: Doctor 数据模块检查段 ✅
- [x] 2.1 DoctorCommand 加载内容后调用 `DataCommand.PrintModuleSummary`
- [x] 2.2 异常时输出 `(unavailable — ...)` 不中断 doctor

## Task 3: 验证 ✅
- [x] 3.1 `dotnet build` 0 警告 0 错误
- [x] 3.2 `dotnet format --verify-no-changes` 通过
- [x] 3.3 730 Cli + 1030 Engine tests pass
