# 统一共享资源路径条目的规范化与校验

## Why

`site.sharedAssets.paths` 的规范与实现存在四处不一致，且其中两处会影响实际配置：

- 规范写“去掉开头的 `/`”，实现却把 `/assets/` 当绝对路径拒绝。
- `\assets\` 在 macOS/Linux 上被接受，在 Windows 上被 `Path.IsPathRooted` 拒绝。
- 空白条目在读取时被静默丢弃，`paths: [assets/, ""]` 能通过校验。
- `./fonts/` 通过校验后规范化为 `./fonts/`，永远匹配不到输出文件。

这些是配置校验和跨平台一致性问题，不改变共用资源的运行时语义。

## What Changes

- 明确条目是相对输出目录的前缀；前导 `/` 或 `\` 是绝对路径，在所有平台上都拒绝，不再“去掉”。
- 显式拒绝盘符和 UNC 前缀，替换依赖平台的 `Path.IsPathRooted`。
- 空白条目改为带索引的校验错误，不再静默丢弃。
- 规范化时丢弃 `.` 段和空段，`./fonts/` 归一化为 `fonts/`，`assets//x` 归一化为 `assets/x/`。
- 更新 `i18n-shared-assets` 规范的规范化要求与场景，补充 `SharedAssetsPath` 单元测试和配置校验测试。
- 同步 CHANGELOG 与用户/开发文档中的路径规则措辞。

## Impact

- 受影响代码：`Bukit.Config` 的 `SharedAssetsPath`、`SiteDefaultsApplier`、`ConfigValidator`。
- 受影响测试：`Bukit.Config.Tests`（规范化单元测试、校验 theory）。
- 不修改 Engine 运行时、图片处理、SEO 或 Worktree 行为。
- 已有合法配置（`assets/`、`fonts/`、`assets/css/`）行为不变；此前在 POSIX 上被接受的 `\assets\` 现在统一报错。
