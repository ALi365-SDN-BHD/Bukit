# Checklist

- [x] `DiagnosticCode.cs` 第 15-22 行 Route 枚举值不再有对齐用的多余空格
- [x] `RenderDependencyHasherTests.cs` 第 243/263/277 行对象初始化器已拆分为多行
- [x] `ProtocolEchoPlugin/Program.cs` 第 73-74 行链式 `??` 表达式缩进符合 editorconfig
- [x] `dotnet format whitespace --verify-no-changes` 返回 exit code 0
- [x] 所有现有测试套件通过（无回归）
