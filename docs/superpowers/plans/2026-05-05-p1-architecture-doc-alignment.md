# P1 Architecture Doc Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 对齐三份架构文档，使结论与当前 Bukit 源码现实一致。

**Architecture:** 采用最小侵入策略，仅修正文档中与源码冲突的架构判断，保留原有章节结构和阅读路径。重点统一“内容正文加载模型、路由抽象进度、仓库边界”三类结论。

**Tech Stack:** Markdown 文档、C# 源码事实核对、grep 回归检查

---

### Task 1: 修正架构评审文档

**Files:**
- Modify: `e:/Github/Bukit/guide/dev/architecture-review.md`

- [ ] **Step 1: 更新评审范围与边界表述**
- [ ] **Step 2: 将“正文全量内存高风险”改为“已引入 BodyStore 延迟模型，关注超大规模场景”**
- [ ] **Step 3: 将“路由仍偏约定优先”改为“collections 已落地 + 默认兼容层仍存在”**
- [ ] **Step 4: 移除或弱化对当前仓库不存在模块的现状判断**

### Task 2: 同步架构总览与维护入口

**Files:**
- Modify: `e:/Github/Bukit/guide/dev/architecture.md`
- Modify: `e:/Github/Bukit/guide/dev/maintainer-entrypoints.md`

- [ ] **Step 1: 在 architecture.md 中补充与 review 一致的结论口径**
- [ ] **Step 2: 在 maintainer-entrypoints.md 中确保入口建议不与新结论冲突**
- [ ] **Step 3: 明确当前仓库边界，避免读者误以为包含 AIBuilding 源码**

### Task 3: 回归验证

**Files:**
- Verify: `e:/Github/Bukit/guide/dev/architecture-review.md`
- Verify: `e:/Github/Bukit/guide/dev/architecture.md`
- Verify: `e:/Github/Bukit/guide/dev/maintainer-entrypoints.md`

- [ ] **Step 1: 运行关键词扫描，确保无已确认失真路径**
- [ ] **Step 2: 运行 diagnostics，确保文档无编辑错误**
- [ ] **Step 3: 生成变更摘要，标注每条结论对应的源码事实**
