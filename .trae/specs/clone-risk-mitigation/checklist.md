# Clone 风险缓解 Checklist

## README 措辞
- [x] `README.md` 中 clone 描述改为 "Extract design tokens..."
- [x] 中文 README 同步修正（无需修改，zh-CN 无 clone 提及）

## FromJson 解析失败
- [x] `FromJson()` 返回 `(CloneTokens?, string?)` 元组
- [x] 解析失败时 error 非空
- [x] `CloneCommand` 检测到错误时输出并返回 exit code 2
- [x] 正常 JSON 不受影响

## 回归验证
- [x] `dotnet build` 0 警告 0 错误
- [x] `dotnet format --verify-no-changes` 通过
- [x] 全部 Clone 相关测试通过
- [x] 不破坏现有 CLI
