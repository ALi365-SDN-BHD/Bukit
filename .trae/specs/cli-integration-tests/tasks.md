# Tasks

## Task 1: ThemeInstallCommand 集成测试
创建 `tests/Bukit.Cli.Tests/ThemeInstallCommandTests.cs`，覆盖安装流程的核心方法。

### 步骤
- [x] **1.1 辅助方法**: 创建 `CreateMinimalTarGz` 辅助方法，生成测试用的 tar.gz 包（含 theme.yaml、layouts/default.scriban）
- [x] **1.2 RunAsync - 文件不存在**: 传入无效路径，断言返回 2
- [x] **1.3 RunAsync - 无效 tar.gz**: 传入非 tar.gz 文件，断言返回 2 并输出 "Invalid archive"
- [x] **1.4 RunAsync - 有效安装**: 用 CreateMinimalTarGz 创建有效包，安装到临时目录，断言返回 0 且文件被正确提取
- [x] **1.5 RunAsync - 已存在主题无 --force**: 安装两次，第二次返回 2
- [x] **1.6 RunAsync - 已存在主题有 --force**: 安装两次（第二次带 --force），断言返回 0
- [x] **1.7 ResolveThemeDestination - 路径逃逸**: 传入含 `../` 的主题名，断言抛出 InvalidOperationException
- [x] **1.8 DetectThemeName - 通过 theme.yaml**: 创建含 theme.yaml 的目录，断言返回 theme.yaml 中的 name
- [x] **1.9 DetectThemeName - 通过单子目录**: 创建含 layouts/ 的单子目录结构，断言返回子目录名
- [x] **1.10 DetectThemeName - 无法检测**: 创建无特征目录，断言返回 null

## Task 2: DeployCommand 追加测试
在 `tests/Bukit.Cli.Tests/DeployCommandTests.cs` 中追加测试。

### 步骤
- [x] **2.1 RunAsync(ArgReader) - 无效 config**: 传入不存在的 --config 路径（已知问题：命令抛出 ConfigException 而非返回 1）
- [x] **2.2 RunAsync(ArgReader) - 无参数**: 传入空参数数组，从当前目录尝试加载 site.yaml（已知问题：命令抛出 ConfigException 而非返回 1）
- [x] **2.3 RunAsync(ArgReader) - 基本选项**: 传入 --dry-run --skip-build --config（已知问题：--skip-build 仍触发 BuildPlanner marker 检查）

## Task 1 依赖
- Task 1.2 依赖 1.1（需要辅助方法）
- Task 1.4-1.6 依赖 1.1（需要辅助方法）
- Task 1.7-1.10 不需要 1.1（纯方法测试）

## Task 2 依赖
- Task 2.1-2.3 无内部依赖，可并行实现

## 已知问题（需后续修复）
- DeployCommand.RunAsync(ArgReader) 在 config 文件缺失时抛出 ConfigException，而非返回 exit code 1
- BuildPlanner.EnsureOutputDirectoryCanBeCleaned 在 --skip-build 模式下仍检查 .bukit-output-marker，阻止 dry-run 测试通过
