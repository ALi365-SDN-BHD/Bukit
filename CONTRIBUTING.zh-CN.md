# 贡献指南

Bukit Core 目前采用企业内部优先模式。仓库和许可证仍然公开，外部贡献可能会被评审，但不保证评审、接纳、响应时间、兼容性、支持或发布时间。内部业务优先级优先。见 [Bukit Core 产品定位](docs/governance/bukit-core-product-positioning.md)。

感谢你对 Bukit 的关注。

## 快速开始

1. 安装 [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) 或更高版本
2. 克隆仓库并构建：

```bash
git clone <repo-url>
cd Bukit
dotnet build bukit-core.slnx -c Release
```

3. 运行快速贡献门禁：

```bash
bash scripts/quality-gate.sh Release
```

`scripts/quality-gate.sh` 是 `scripts/gates/ci-fast.sh` 的兼容包装器。它检查文档一致性、活跃 workflow 边界、配置文档契约、CLI 文档同步、skill 元数据、README 同步和 Core CLI 脚本契约。它不是完整发布门禁。

4. 代码变更需运行测试：

```bash
dotnet test bukit-test.slnx -c Release
```

当前开发文档地图见 [guide/dev/README.md](guide/dev/README.md)。

## 代码风格

- 项目强制启用 `TreatWarningsAsErrors` 和 `EnforceCodeStyleInBuild`
- 提交前格式化代码：

```bash
dotnet format bukit-core.slnx --verify-no-changes
```

- C# 代码遵循 [.editorconfig](.editorconfig) 约定
- Markdown、YAML、JSON、Shell、PowerShell 文件使用 UTF-8 + LF 换行

## 架构

主要开发入口见 [guide/dev/README.md](guide/dev/README.md)。

核心架构文档：
- [guide/dev/architecture.md](guide/dev/architecture.md) — 模块职责与依赖
- [guide/dev/release.md](guide/dev/release.md) — CI、测试与发布门禁边界
- [guide/dev/release-checklist.md](guide/dev/release-checklist.md) — 发布专用清单
- [guide/dev/documentation-governance.md](guide/dev/documentation-governance.md) — 文档治理

## 测试

- 单元测试在 `tests/` 目录，使用 xUnit
- Core 测试项目由 `scripts/checks/core-tests.sh` 列出
- 冒烟入口是 `scripts/smoke.sh` 和 `scripts/smoke/core.sh`
- 测试策略见 [guide/dev/testing.md](guide/dev/testing.md)

## AOT 兼容性

本项目发布为 Native AOT。所有新代码必须 AOT 兼容：
- 避免对受 trim 影响的类型使用反射
- Scriban 变更见 AOT 适配说明：[guide/dev/aot.md](guide/dev/aot.md)
- 发布专用 Native AOT 打包使用 `scripts/build/package-native-aot.sh`

## Pull Request 流程

1. 若变更影响用户行为，请更新文档
2. 本地运行 `bash scripts/quality-gate.sh Release`，确保快速文档与契约门禁通过
3. 代码变更先运行目标测试，再在交付前运行 `BUKIT_CI_FULL_SKIP_FAST=1 bash scripts/gates/ci-full.sh Release`
4. 发布产物、Native AOT、冒烟和安全验证属于发布专用检查，仅在变更触及对应表面时运行
5. GitHub Actions 使用 `.github/workflows/ci.yaml` 处理 PR 和分支 push
6. 创建 PR 前 rebase 到 main 分支

## 许可证

提交贡献即表示你同意相关贡献将以 MIT 许可证授权。
