# Checklist

## Task 1: ThemeInstallCommand 集成测试
- [x] ThemeInstallCommandTests.cs 文件存在
- [x] CreateMinimalTarGz 辅助方法正确创建有效的 tar.gz
- [x] 文件不存在场景测试通过
- [x] 无效 tar.gz 场景测试通过
- [x] 有效安装场景测试通过
- [x] 已存在主题无 --force 场景测试通过
- [x] 已存在主题有 --force 场景测试通过
- [x] 路径逃逸检测测试通过
- [x] 通过 theme.yaml 检测主题名测试通过
- [x] 通过单子目录检测主题名测试通过
- [x] 无法检测主题名测试通过

## Task 2: DeployCommand 追加测试
- [x] ArgReader 无效 config 测试已添加（已知问题：命令抛出 ConfigException 而非返回 1）
- [x] ArgReader 无参数测试已添加（已知问题：命令抛出 ConfigException 而非返回 1）
- [x] ArgReader 基本选项测试已添加（已知问题：--skip-build 仍触发 BuildPlanner marker 检查）

## 验证
- [x] `dotnet build` 0 warnings
- [x] 无格式违规（`dotnet format --verify-no-changes`）
- [x] ThemeInstallCommandTests 20 个测试全部通过
- [ ] DeployCommandTests 3 个 ArgReader 测试需配合命令层修复（ConfigException 处理 + .bukit-output-marker 检查调整）
