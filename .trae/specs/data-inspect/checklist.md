# Data 模块调试 Checklist

## DataCommand
- [x] `bukit data inspect` 可执行
- [x] `bukit data inspect --module hero` 显示详情
- [x] `bukit data dump --format json` 输出合法 JSON
- [x] CLI 注册无冲突

## Doctor 数据检查
- [x] `bukit doctor` 输出 data modules 段
- [x] 0 模块时输出 `(none)`

## 回归验证
- [x] `dotnet build` 0 警告 0 错误
- [x] 全部测试通过
- [x] `dotnet format` 通过
