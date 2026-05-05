# 贡献指南

感谢你对 Bukit 的关注。

## 快速开始

1. 安装 [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) 或更高版本
2. 克隆仓库并构建：

```bash
git clone <repo-url>
cd Bukit
dotnet build bukit.slnx -c Release
```

3. 运行测试：

```bash
dotnet test bukit.slnx -c Release
```

4. 运行冒烟测试（Windows）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/smoke.ps1
```

新开发者参考：[guide/dev/new-developer-30min.md](guide/dev/new-developer-30min.md)。

## 代码风格

- 项目强制启用 `TreatWarningsAsErrors` 和 `EnforceCodeStyleInBuild`
- 提交前格式化代码：

```bash
dotnet format bukit.slnx --verify-no-changes
```

- C# 代码遵循 [.editorconfig](.editorconfig) 约定
- Markdown、YAML、JSON、Shell、PowerShell 文件使用 UTF-8 + LF 换行

## 架构

按改动类型定位源码入口见：[guide/dev/maintainer-entrypoints.md](guide/dev/maintainer-entrypoints.md)。

核心架构文档：
- [guide/dev/architecture.md](guide/dev/architecture.md) — 模块职责与依赖
- [guide/dev/code-wiki.md](guide/dev/code-wiki.md) — 仓库结构与关键类
- [guide/dev/governance-checklist.md](guide/dev/governance-checklist.md) — 发布前检查清单

## 测试

- 单元测试在 `tests/` 目录，使用 xUnit
- 冒烟测试：`scripts/smoke.ps1` 和 `scripts/smoke.sh`
- 测试策略见：[guide/dev/testing-smoke.md](guide/dev/testing-smoke.md)

## AOT 兼容性

本项目发布为 Native AOT。所有新代码必须 AOT 兼容：
- 避免对受 trim 影响的类型使用反射
- Scriban 变更见 AOT 适配说明：[guide/dev/aot.md](guide/dev/aot.md)
- 运行 `scripts/check-aot-warnings.sh` 验证零 AOT 警告

## Pull Request 流程

1. 若变更影响用户行为，请更新文档
2. 运行 `scripts/check-doc-asset-consistency.ps1` 验证文档一致性
3. 运行完整测试套件和冒烟测试
4. 确保代码格式化通过
5. 创建 PR 前 rebase 到 main 分支

## 许可证

提交贡献即表示你同意相关贡献将以 MIT 许可证授权。
