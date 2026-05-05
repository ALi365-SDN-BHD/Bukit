# P2 治理清单设计

## 目标

把 P1 的架构结论转化为可执行维护动作，形成长期可复用的治理清单，降低文档口径再次漂移的风险。

## 范围

- 新增维护者治理清单文档：
  - `guide/dev/governance-checklist.md`
- 更新开发者导航入口：
  - `guide/dev/README.md`
  - `guide/dev/README.zh-CN.md`
  - `guide/dev/README.ms.md`

## 非目标

- 不修改业务代码
- 不新增 CI/workflow 资产
- 不实现自动化脚本，仅提供可执行命令与检查步骤

## 设计要点

### 1. 正文读取与缓存基线

- 定义目标指标：读取次数、峰值内存、构建耗时
- 给出统一命令：build/test + 观察项
- 给出“通过/告警”判定规则

### 2. collections 与兼容层治理

- 明确主路径：`site.collections`
- 明确兼容路径：`post/page` 默认规则
- 提供变更检查表：何时改 collections、何时保留兼容层

### 3. 文档-资产一致性检查

- 给出路径可达性手工检查项（solution、目录、workflow、工具目录）
- 给出 grep 级别快速检查命令模板
- 给出维护节奏（每月/每季度）

## 验收标准

- 文档可以单独执行（每个治理项有步骤和命令）
- 与 `architecture-review.md`、`architecture.md`、`maintainer-entrypoints.md` 口径一致
- 开发者三语导航均可看到入口
