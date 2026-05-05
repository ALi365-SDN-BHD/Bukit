# 缓存与清理（cache-dir / .cache / clean）

本项目的“缓存”主要服务于增量构建（跳过未变化页面的渲染），而 “clean” 则用于清理输出目录与缓存目录，避免本地/CI 被旧状态污染。

相关文档：
- [增量构建](./incremental-build.md)
- [命令行（CLI）参数参考](./cli.md)

## 缓存目录的权威定义

### 默认缓存目录：`.cache/`

构建引擎默认使用 `<rootDir>/.cache/` 保存增量构建的 manifest 文件：
- 单语言：`build-manifest.json`
- 多语言：`build-manifest.<lang>.json`

`doctor` 也会检查该目录下的 manifest JSON 是否可解析（见 [doctor.md](./doctor.md)）。

### 覆盖缓存目录：`--cache-dir <dir>`

构建时可通过 CLI 覆盖缓存目录（用于 CI 隔离或多工作目录并行构建）：
- 指定后，manifest 会写入该目录下
- 多语言依然会按 `build-manifest.<lang>.json` 分离

## clean 会清理什么

`bukit clean` 的行为：

1. 删除输出目录（由 `--dir` 指定，或由 `--config/--site` 解析 `build.output`）
2. 删除 `<rootDir>/.cache/`（增量 manifest 缓存）
3. 兼容性清理：删除 `<rootDir>/.bukit/`（旧缓存目录/历史遗留）

注意：clean 不会删除你的内容源目录（content/、data/）、主题目录（layouts/themes）或任何配置文件。

## 什么时候需要 clean

- “模板/路由/内容都改了，但输出看起来没变化”：先确认是否启用了增量，再考虑 clean
- “切换语言列表/默认语言后输出不对”：建议 clean，避免不同 variant 的历史产物干扰
- CI 中希望每次构建完全可重现：一般直接 `build --clean` 即可；如出现缓存干扰，再用 `clean`

## 常见问题

1. `build --clean` 与 `clean` 的区别？
   - `build --clean`：只清理输出目录（`build.output`），再执行构建
   - `clean`：清理输出目录 + 清理缓存目录（`.cache` 等）

