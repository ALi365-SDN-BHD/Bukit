## 1. 契约与闭包

- [x] 1.1 记录自动启用、Git/洁净输入、CLI-only、完整聚合和失败关闭合同
- [x] 1.2 对最终修改路径生成 verification closure 并解决全部 unmapped
- [x] 1.3 分类 Engine、CLI、文档和 Native AOT 专项命令，串行执行共享 build/cache/manifest 资源

最终 closure 无 unmapped；经授权补充的 Shared、Architecture 与 workflow policy owner tests 均已通过。

## 2. 快照与 Worker 协议

- [x] 2.1 建立一次性内容快照和 AOT source-generated `i18n-variant-result.v1`
- [x] 2.2 让内部 Worker 只构建一个语言并返回可验证、可重建的变体结果
- [x] 2.3 覆盖 schema/config/content hash/语言/path/正文边界及损坏结果失败测试

## 3. CLI Worktree 编排

- [x] 3.1 仅在显式多语言 `bukit build` 中校验 Git HEAD、洁净状态和 tracked inputs
- [x] 3.2 创建同 HEAD detached Worktree，按 `languageJobs` 启动并取消 Worker
- [x] 3.3 只清理本次登记的 Worktree，覆盖失败、取消和残留恢复

## 4. 聚合与输出事务

- [x] 4.1 从 Worker 结果重建现有 `BuildVariantResult` 并复用全部根级 writer
- [x] 4.2 在 sibling staging 完成报告、安全门禁和 manifest 后提交输出
- [x] 4.3 覆盖 Worker/聚合/交换失败时正式输出和 cache 不变

## 5. 文档与验证

- [x] 5.1 更新 i18n 与故障排查文档，删除过时 `docs/worktree.md`
- [x] 5.2 运行完整 Engine 和 CLI 专项项目测试及 CLI docs sync
- [x] 5.3 发布当前 macOS ARM64 Native AOT 并执行真实多语言 Git fixture 构建
- [x] 5.4 完成一次专项复审；Critical/Important 为零后停止扩大审查
- [x] 5.5 严格校验 OpenSpec 和 `git diff --check`，记录未验证的远端跨平台证据
