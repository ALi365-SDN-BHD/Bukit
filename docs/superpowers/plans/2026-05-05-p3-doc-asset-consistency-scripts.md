# P3 Doc Asset Consistency Scripts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为文档-资产一致性治理新增 `ps1+sh` 检查脚本，并接入治理文档。

**Architecture:** 两个脚本保持规则一致：扫描 `README*.md` 与 `guide/**/*.md` 的失真关键词，结合语义豁免规则过滤误报，再做关键路径存在性检查；默认严格失败（任一错误返回非 0）。

**Tech Stack:** PowerShell、Bash、ripgrep、Markdown

---

### Task 1: 实现 PowerShell 检查脚本

**Files:**
- Create: `e:/Github/Bukit/scripts/check-doc-asset-consistency.ps1`

- [ ] **Step 1: 定义扫描目录、关键词规则、语义豁免关键词与关键路径集合**
- [ ] **Step 2: 实现 Markdown 行级扫描并输出 `ERROR/WARN/OK`**
- [ ] **Step 3: 实现关键路径存在性检查与 `-ExtraPath` 参数**
- [ ] **Step 4: 实现退出码策略（有错误返回 1）**

### Task 2: 实现 Shell 检查脚本

**Files:**
- Create: `e:/Github/Bukit/scripts/check-doc-asset-consistency.sh`

- [ ] **Step 1: 与 ps1 对齐规则常量**
- [ ] **Step 2: 实现扫描与语义豁免逻辑**
- [ ] **Step 3: 实现 `--extra-path` 参数与关键路径检查**
- [ ] **Step 4: 实现退出码策略（有错误返回 1）**

### Task 3: 回填治理文档入口

**Files:**
- Modify: `e:/Github/Bukit/guide/dev/governance-checklist.md`

- [ ] **Step 1: 在快速检查命令中加入 ps1+sh 脚本示例**
- [ ] **Step 2: 保留原有 grep 命令作为补充排查方式**

### Task 4: 执行测试与验证

**Files:**
- Verify: `e:/Github/Bukit/scripts/check-doc-asset-consistency.ps1`
- Verify: `e:/Github/Bukit/scripts/check-doc-asset-consistency.sh`

- [ ] **Step 1: 默认运行两脚本，预期当前仓库返回 0**
- [ ] **Step 2: 传入不存在路径参数，预期返回 1**
- [ ] **Step 3: 对新增/修改文档与脚本做诊断检查**
