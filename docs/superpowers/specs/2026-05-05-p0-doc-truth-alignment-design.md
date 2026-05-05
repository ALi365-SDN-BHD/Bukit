# P0 文档事实对齐设计

## 目标

将仓库中的高优先级文档修正为与当前 `Bukit` 仓库现实一致，不新增缺失的 CI/workflow 或历史产品线资产。

## 范围

- 修正 `README*` 与 `guide/*` 中不存在的：
  - `src/AIBuilding/*`
  - `aibuilding.slnx`
  - `.github/workflows/pages.yml`
  - `.github/workflows/smoke.yml`
  - `.github/workflows/build.yaml`
  - `tools/ImageSharp`
- 将“仓库已内置工作流”改为“需自行创建或参考示例流程”。
- 保留当前真实存在的 `Bukit`、`examples/starter`、`guide/*`、`tools/scriban`、测试矩阵说明。

## 不做的事

- 不新增 `.github/workflows/*`
- 不恢复 `AIBuilding` 相关源码或解决方案
- 不修改业务代码和测试逻辑

## 方案

### 方案 A：文档对齐现状（采用）

- 优点：风险最低，能最快恢复文档可信度
- 缺点：不会补回 CI 资产

### 方案 B：补回文档所述资产

- 优点：文档改动少
- 缺点：需要凭空设计 workflow，范围超出本次 `P0`

## 具体改动

- `README.md`、`README.zh-CN.md`、`README.ms.md`
  - 去掉“仓库已内置 Pages workflow”表述
  - 中文版去掉不存在的 `AIBuilding` 章节
- `guide/dev/code-wiki.md`
  - 改为仅描述当前 `Bukit` 仓库
  - 删除不存在的 `AIBuilding`、`aibuilding.slnx`、`.github/workflows/*`、`tools/ImageSharp`
- `guide/dev/new-developer-30min.md`
  - 删除 `AIBuilding` 阅读入口
- `guide/dev/maintainer-entrypoints.md`
  - 删除 `AIBuilding` 入口章节
- `guide/dev/publish-deploy.md`
  - 改为说明“可自行创建 Pages workflow”
- `guide/user/13-部署-GitHub-Pages.md`
  - 改为“参考流程”，不再宣称仓库已提供现成 workflow
- `guide/dev/aot.md`
  - 删除或修正 `tools/ImageSharp` 现状描述

## 验收标准

- 上述文档不再引用当前仓库不存在的路径或工作流
- 根级与开发者入口文档对仓库边界描述一致
- 不引入新的构建或诊断错误
