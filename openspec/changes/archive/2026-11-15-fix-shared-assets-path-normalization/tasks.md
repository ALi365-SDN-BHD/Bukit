# Tasks

## 1. 规范化

- [x] `SharedAssetsPath.Normalize` 丢弃 `.` 段与空段，保留 `..` 供校验拒绝。
- [x] `SharedAssetsPath.IsAbsolute` 显式识别前导 `/`、前导 `\`、盘符和 UNC，不依赖 `Path.IsPathRooted`。
- [x] 读取 `paths` 时保留空白条目，由校验器按索引报错。

## 2. 校验与规范

- [x] `ConfigValidator` 对空白条目给出带索引的错误。
- [x] 更新 `openspec/specs/i18n-shared-assets/spec.md` 的规范化要求与场景。

## 3. 测试与文档

- [x] 新增 `SharedAssetsPath` 单元测试：反斜杠、尾斜杠、`./`、重复斜杠、空串、`..`。
- [x] 配置校验测试覆盖 `/assets/`、`\assets\`、`C:\assets`、`//server/share`、`./fonts/`、空白条目。
- [x] 更新 CHANGELOG（en / zh-CN / ms）与用户/开发文档措辞。
