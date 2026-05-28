# CLI 集成测试 Spec

## Why
ThemeInstallCommand 和 DeployCommand 是 CLI 中两个重要的命令，但当前几乎没有测试覆盖（ThemeInstallCommand 0%，DeployCommand 仅 6 个 dry-run 测试）。它们涉及文件系统操作、tar.gz 解压、路径安全验证等核心逻辑，缺乏测试覆盖存在回归风险，也是 CLI 覆盖率低的主要拖累。

## What Changes
- 为 ThemeInstallCommand 编写集成测试（本地 tar.gz 安装、路径安全、主题名称检测）
- 为 DeployCommand 补充 ArgReader 入口测试和缺失配置场景

## Impact
- Affected specs: coverage-climb-4, coverage-boost-80
- Affected code: 
  - `src/Bukit.Cli/Commands/ThemeInstallCommand.cs`
  - `src/Bukit.Cli/Commands/DeployCommand.cs`
  - `tests/Bukit.Cli.Tests/ThemeInstallCommandTests.cs` (新文件)
  - `tests/Bukit.Cli.Tests/DeployCommandTests.cs` (追加)

## Requirements

### Requirement: ThemeInstallCommand - 本地 tar.gz 安装
系统 SHALL 支持从本地 tar.gz 文件安装主题。

#### Scenario: 从有效 tar.gz 安装主题
- **WHEN** 调用 `ThemeInstallCommand.RunAsync` 参数为有效的本地 tar.gz 路径
- **THEN** 返回 0，主题文件被提取到 themes/<name>/ 目录

#### Scenario: tar.gz 文件不存在
- **WHEN** 文件路径不存在
- **THEN** 返回 2，输出错误信息

#### Scenario: 无效的 tar.gz 文件
- **WHEN** 文件不是有效的 tar.gz
- **THEN** 返回 2，输出 "Invalid archive" 错误

#### Scenario: 已存在主题，未指定 --force
- **WHEN** themes/<name>/ 已存在且未传 --force 参数
- **THEN** 返回 2，提示使用 --force

#### Scenario: 已存在主题，指定 --force
- **WHEN** themes/<name>/ 已存在且传了 --force 参数
- **THEN** 返回 0，覆盖安装

### Requirement: ThemeInstallCommand - 路径安全
系统 SHALL 防止主题安装路径逃逸。

#### Scenario: 路径逃逸检测
- **WHEN** `ResolveThemeDestination` 传入含 `../` 的主题名
- **THEN** 抛出 `InvalidOperationException`

### Requirement: ThemeInstallCommand - 主题名称检测
系统 SHALL 能自动检测提取后目录中的主题名称。

#### Scenario: 通过 theme.yaml 检测
- **WHEN** 提取目录包含有效的 `theme.yaml`
- **THEN** `DetectThemeName` 返回 theme.yaml 中的 name 字段

#### Scenario: 通过单子目录检测
- **WHEN** 提取目录仅含一个子目录且含 layouts/
- **THEN** `DetectThemeName` 返回子目录名

#### Scenario: 无法检测主题名
- **WHEN** 提取目录结构不符合任何已知模式
- **THEN** `DetectThemeName` 返回 null

### Requirement: ThemeInstallCommand - ExtractAndInstallAsync 纯方法
系统 SHALL 能提取 tar.gz 并调用安装。

#### Scenario: 有效 tar.gz 成功提取安装
- **WHEN** 提供有效的 tar.gz 和 themesDir
- **THEN** 返回 0，文件被复制到目标目录

### Requirement: DeployCommand - ArgReader 入口
系统 SHALL 支持通过 ArgReader 调用 DeployCommand。

#### Scenario: 无 config 参数
- **WHEN** `RunAsync(ArgReader)` 传入不存在的 config
- **THEN** 返回 1（配置加载失败）

#### Scenario: 缺失 mode 参数
- **WHEN** `RunAsync(ArgReader)` 未传任何参数
- **THEN** 从当前目录尝试加载 site.yaml

### Requirement: DeployCommand - baseUrl 规范化
系统 SHALL 自动为 baseUrl 添加前缀 `/`。

#### Scenario: 不带前导斜杠的 baseUrl
- **WHEN** baseUrl = "my-repo"
- **THEN** 内部规范化为 "/my-repo"
