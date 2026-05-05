# GitHub Release Action 多平台发布计划

> **目标**：创建 `.github/workflows/release.yml`，在推送 tag（如 `v1.0.0`）时自动构建 win-x64 / linux-x64 / osx-arm64 三个平台的 Native AOT 发布包，并创建 GitHub Release 附带压缩包。

## Summary

- 目标受众：维护者 / 下游 CI 用户
- 触发条件：推送 `v*` 格式的 tag
- 构建产物：3 个平台的 Native AOT 单文件可执行程序（`.tar.gz` / `.zip`）
- 交付形式：GitHub Release + 版本号注入 + 可下载资产

## Current State Analysis

### 已有资产

| 资产 | 路径 | 说明 |
|---|---|---|
| AOT 配置 | `src/Bukit.Cli/Bukit.Cli.csproj` | `Configuration=AOT` 自动设置 `PublishAot/PublishSingleFile/InvariantGlobalization` |
| 版本号 | `Directory.Build.props` | 固定 `1.0.0`，可通过 `VersionPrefix` 覆盖 |
| SDK | `global.json` | .NET 10.0.100 |
| 已有 workflow | `examples/github-pages-workflow.yml` | 仅限 Pages 部署，不涉及 Release |
| 符号剥离 | csproj `BukitStripSymbols` | 默认 false，linux 下启用需 `llvm-objcopy` |
| 产物清单 | 现有 `README.zh-CN.md` §AOT 发布 | 已文档化 `win-x64` / `linux-x64` 命令 |

### 缺失项

| 缺失项 | 说明 |
|---|---|
| Release workflow | 仓库当前无任何与 Release 相关的 GitHub Actions |
| 多平台矩阵 | 未定义跨平台构建矩阵 |
| OSX 文档 | README 提到 `osx-x64` 但未区分 arm64 |
| 版本号注入 | tag 触发时未将 `VersionPrefix` 注入构建 |

## Proposed Changes

### Step 1: 确定平台矩阵

选择三个主流平台 RID：

| 平台 | RID | 压缩格式 | GitHub Actions runner |
|---|---|---|---|
| Windows x64 | `win-x64` | `.zip` | `windows-latest` |
| Linux x64 | `linux-x64` | `.tar.gz` | `ubuntu-latest` |
| macOS ARM64 | `osx-arm64` | `.tar.gz` | `macos-latest` |

### Step 2: 创建 `.github/workflows/release.yml`

**文件**
- 新建 `e:\Github\Bukit\.github\workflows\release.yml`

**Workflow 结构**

```yaml
name: Release

on:
  push:
    tags: ['v*']
  workflow_dispatch:  # 允许手动触发

permissions:
  contents: write     # 创建 Release 需要

jobs:
  build:
    strategy:
      matrix:
        include:
          - rid: win-x64
            os: windows-latest
            ext: .zip
          - rid: linux-x64
            os: ubuntu-latest
            ext: .tar.gz
          - rid: osx-arm64
            os: macos-latest
            ext: .tar.gz
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0   # 获取 tag 信息

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish Native AOT
        run: >
          dotnet publish src/Bukit.Cli
          -c AOT
          -r ${{ matrix.rid }}
          -o out/bukit-${{ matrix.rid }}
          -p:VersionPrefix=${{ github.ref_name }}
          -p:BukitStripSymbols=true

      - name: Package (Windows)
        if: matrix.rid == 'win-x64'
        shell: pwsh
        run: Compress-Archive -Path out/bukit-${{ matrix.rid }}/bukit.exe -DestinationPath bukit-${{ github.ref_name }}-${{ matrix.rid }}.zip

      - name: Package (Linux/macOS)
        if: matrix.rid != 'win-x64'
        run: tar -czf bukit-${{ github.ref_name }}-${{ matrix.rid }}.tar.gz -C out/bukit-${{ matrix.rid }} bukit

      - uses: actions/upload-artifact@v4
        with:
          name: bukit-${{ matrix.rid }}
          path: bukit-${{ github.ref_name }}-${{ matrix.rid }}${{ matrix.ext }}

  release:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v2
        with:
          files: bukit-*/*
          generate_release_notes: true
```

### Step 3: 版本号注入逻辑

- 在 `dotnet publish` 时传入 `-p:VersionPrefix=${{ github.ref_name }}`
- `${{ github.ref_name }}` 会自动剥离 `v` 前缀（如 tag `v1.2.3` → ref_name `1.2.3`，需在 workflow 中处理）
- 需要在 workflow 中以脚本处理：`VERSION=${GITHUB_REF_NAME#v}` → `-p:VersionPrefix=$VERSION`

**修正后构建命令**：
```bash
# 提取纯净版本号（去掉 v 前缀）
VERSION="${GITHUB_REF_NAME#v}"
dotnet publish src/Bukit.Cli -c AOT -r $RID -o out/bukit-$RID -p:VersionPrefix=$VERSION -p:BukitStripSymbols=true
```

### Step 4: 产物清单

每次 Release 将产生 6 个资产（3 个原件 + 3 个压缩包）：

| 文件名 | 说明 |
|---|---|
| `bukit-v1.0.0-win-x64.zip` | Windows 单文件 |
| `bukit-v1.0.0-linux-x64.tar.gz` | Linux 单文件 |
| `bukit-v1.0.0-osx-arm64.tar.gz` | macOS ARM 单文件 |

### Step 5: 对现有 pages.yml 的兼容性确认

- 新建 `release.yml` 不影响现有的 `pages.yml`（已在 `.github/workflows/` 中）
- 两个 workflow 互不干扰：`release.yml` 由 tag 触发，`pages.yml` 由 push 到 main 触发

## Verification Steps

1. 推送测试 tag：`git tag v1.0.0-test && git push origin v1.0.0-test`
2. 在 GitHub Actions 页面观察 `Release` workflow 状态
3. 确认 3 个平台的 job 均成功
4. 确认 GitHub Release 页面出现下载资产
5. 下载 win-x64 包验证：解压后运行 `bukit version` 应输出正确版本号

## Assumptions & Decisions

- macOS runner 使用 `macos-latest`（当前为 ARM64）；如需 Intel 版需增加 `osx-x64`
- Linux 上启用 `BukitStripSymbols=true`（GitHub runner 自带 `llvm-objcopy`）
- 版本号从 tag 名推导：`vX.Y.Z` → 版本 `X.Y.Z`
- 压缩包不包含整个输出目录，仅包含 `bukit`（或 `bukit.exe`）单文件
- 不做代码签名（Windows Authenticode / macOS notarization），后续可按需添加
