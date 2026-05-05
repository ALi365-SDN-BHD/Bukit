# P3 文档资产一致性脚本设计

## 背景

P0/P1/P2 已完成文档口径对齐与治理清单沉淀，但当前检查主要靠人工执行 grep。需要最小自动化脚本，把“文档断言与仓库现实一致”变成可重复执行的质量门禁。

## 目标

- 新增跨平台最小检查脚本（PowerShell + shell）
- 默认严格失败（发现错误即非 0 退出）
- 输出人类可读结果，支持本地与 CI 直接使用
- 回填治理清单文档中的检查命令入口

## 非目标

- 不新增复杂 CLI（如 json 模式、规则配置文件）
- 不改业务代码
- 不实现自动修复

## 设计范围

新增文件：

- `scripts/check-doc-asset-consistency.ps1`
- `scripts/check-doc-asset-consistency.sh`

更新文件：

- `guide/dev/governance-checklist.md`

## 检查规则

### 1) 失真关键词扫描

扫描范围：

- 仓库根 `README*.md`
- `guide/**/*.md`

默认错误关键词：

- `src/AIBuilding`
- `aibuilding.slnx`
- `tools/ImageSharp`
- `.github/workflows/smoke.yml`
- `.github/workflows/build.yaml`

语义豁免（命中则不报错）：

- 行内包含“示例/需自建/参考”语义关键词（中英文）
- 目的：允许文档说明“这是示例或需自建”，禁止“仓库已内置”式错误断言

### 2) 关键路径可达性检查

内置最小存在性检查路径：

- `bukit.slnx`
- `guide/dev`
- `guide/user/13-部署-GitHub-Pages.md`

支持附加检查参数：

- PowerShell：`-ExtraPath`
- shell：`--extra-path`

用于手工或 CI 追加检查“本次改动涉及的新关键路径”。

## 输出与退出码

- 输出前缀：
  - `ERROR:` 规则失败
  - `WARN:` 非致命提示
  - `OK` 检查通过
- 退出码：
  - 有错误：`1`
  - 无错误：`0`

## 验收标准

- 两套脚本在当前仓库默认运行均返回 `0`
- 传入一个不存在的 `extra path` 时返回 `1`
- `governance-checklist.md` 中出现可直接运行的脚本命令示例

## 风险与权衡

- 风险：语义豁免关键词过少可能误报，过多可能漏报
- 对策：先采用最小关键词集合，后续按误报样本迭代
