# P2 Governance Checklist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新增维护治理清单文档并接入开发者导航，支持长期执行。

**Architecture:** 在 `guide/dev` 新增一份可执行清单，内容分为正文基线、路由治理、文档一致性检查三部分；通过 README 三语导航暴露入口。

**Tech Stack:** Markdown、grep 回归检查、文档诊断

---

### Task 1: 编写治理清单文档

**Files:**
- Create: `e:/Github/Bukit/guide/dev/governance-checklist.md`

- [ ] **Step 1: 写入治理目标与适用范围**
- [ ] **Step 2: 写入正文读取与缓存基线执行步骤**
- [ ] **Step 3: 写入 collections 与兼容层治理检查步骤**
- [ ] **Step 4: 写入文档-资产一致性检查步骤与维护节奏**

### Task 2: 更新三语开发者导航

**Files:**
- Modify: `e:/Github/Bukit/guide/dev/README.md`
- Modify: `e:/Github/Bukit/guide/dev/README.zh-CN.md`
- Modify: `e:/Github/Bukit/guide/dev/README.ms.md`

- [ ] **Step 1: 新增治理清单导航链接**
- [ ] **Step 2: 保持三语导航语义一致**

### Task 3: 回归验证

**Files:**
- Verify: `e:/Github/Bukit/guide/dev/governance-checklist.md`
- Verify: `e:/Github/Bukit/guide/dev/README.md`
- Verify: `e:/Github/Bukit/guide/dev/README.zh-CN.md`
- Verify: `e:/Github/Bukit/guide/dev/README.ms.md`

- [ ] **Step 1: 关键词扫描，确认无失真路径复发**
- [ ] **Step 2: 诊断检查，确认新增/修改文档无错误**
