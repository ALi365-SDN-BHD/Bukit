# BKT-FS-001 FollowSymlinks 安全强化 Spec

> 来源：审计报告 `.trae/documents/bukit-audit-report-2026-05-30-chatgpt-03.md` BKT-FS-001
> 当前状态：默认禁用已安全，但开启时缺少 realpath 边界检查、强警告、CI 禁止、Doctor 检查

## Why

`build.followSymlinks: true` 允许资源复制时跟随符号链接。但当前实现仅靠输出路径词法校验，不等于 realpath 边界校验。恶意 symlink 可指向项目外敏感路径（如 `/etc/passwd`），一旦跟随会导致信息泄露或输出目录污染。需要在开启此选项时增加多层防护。

## What Changes

- **DirectoryCopy.Sync()**: 当 `FollowSymlinks=true` 时，对每个被跟随的 symlink 做 realpath 边界校验，拒绝指向 sourceDir 外的目标
- **ConfigLoader**: 加载时若 `FollowSymlinks=true`，通过 logger 输出强警告
- **ConfigOverrides**: CI 模式下自动 `FollowSymlinks=false`
- **DoctorCommand**: 新增 symlink 安全检查项

## Impact

- Affected specs: 无（新增功能）
- Affected code:
  - `src/Bukit.Engine/DirectoryCopy.cs` — 添加 realpath 校验
  - `src/Bukit.Config/ConfigLoader.cs` — 添加警告日志
  - `src/Bukit.Config/ConfigOverrides.cs` — CI 强制禁用
  - `src/Bukit.Cli/Commands/DoctorCommand.cs` — 新增检查项

## ADDED Requirements

### Requirement: Symlink Realpath Boundary Validation
当 `FollowSymlinks=true` 时，系统 SHALL 在跟随 symlink 之前解析其真实路径，并拒绝指向复制源目录之外的符号链接。

#### Scenario: Symlink points inside source dir
- **GIVEN** `build.followSymlinks: true`
- **AND** a symlink `assets/link.png` → `assets/real.png` (within source)
- **WHEN** DirectoryCopy.Sync() encounters the symlink
- **THEN** the file is followed and copied normally

#### Scenario: Symlink points outside source dir
- **GIVEN** `build.followSymlinks: true`
- **AND** a symlink `assets/evil` → `/etc/passwd` (outside source)
- **WHEN** DirectoryCopy.Sync() encounters the symlink
- **THEN** the symlink is skipped with a warning log

#### Scenario: Symlink with relative traversal
- **GIVEN** `build.followSymlinks: true`
- **AND** a symlink `assets/link` → `../../secrets/key.pem` (outside source after resolve)
- **WHEN** DirectoryCopy.Sync() encounters the symlink
- **THEN** the symlink is skipped with a warning log

#### Scenario: Symlink target is another symlink
- **GIVEN** `build.followSymlinks: true`
- **AND** a symlink chain `a → b → /outside/file` (chain resolves outside source)
- **WHEN** DirectoryCopy.Sync() encounters the symlink
- **THEN** the final resolved realpath is checked and the symlink is skipped with a warning

### Requirement: FollowSymlinks Enable Warning
当用户配置 `build.followSymlinks: true` 时，系统 SHALL 在配置加载阶段通过 logger 输出强警告。

#### Scenario: FollowSymlinks enabled in config
- **GIVEN** `site.yaml` contains `build.followSymlinks: true`
- **WHEN** ConfigLoader loads the config
- **THEN** a warning is logged: "build.followSymlinks is enabled. Symlinks may point outside the project directory. Ensure all symlinks are trusted."

### Requirement: CI FollowSymlinks Forced Deny
当运行在 CI 环境（`IsCI=true`）时，系统 SHALL 强制将 `FollowSymlinks` 设为 `false`。

#### Scenario: CI build with FollowSymlinks true
- **GIVEN** CI environment (`IsCI=true`)
- **AND** config has `build.followSymlinks: true`
- **WHEN** ConfigApplier.Apply() processes overrides
- **THEN** `FollowSymlinks` is forced to `false`

#### Scenario: Local build with FollowSymlinks true
- **GIVEN** local environment (not CI)
- **AND** config has `build.followSymlinks: true`
- **WHEN** ConfigApplier.Apply() processes overrides
- **THEN** `FollowSymlinks` remains `true` (only warning is emitted)

### Requirement: Doctor FollowSymlinks Safety Check
Doctor 命令 SHALL 检查 followSymlinks 配置并提供安全建议。

#### Scenario: Doctor with FollowSymlinks enabled
- **GIVEN** `build.followSymlinks: true`
- **WHEN** `bukit doctor` runs
- **THEN** a warning diagnostic is emitted: "build.followSymlinks is enabled. Ensure all symlinks are within the project directory and trusted. Consider disabling in CI environments."
