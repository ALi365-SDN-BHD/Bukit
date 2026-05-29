# 维护治理执行清单（P2）

本清单把架构评审结论转为可执行动作，供维护者按周期执行。目标是持续降低以下风险：

- 超大规模场景下正文读取与缓存失控
- `collections` 主路径与 `post/page` 兼容路径口径漂移
- 文档与仓库资产再次失真

## 1) 正文读取与缓存基线

### 1.1 执行频率

- 每月一次（常规）
- 涉及 `Content` / `Engine` / `Rendering` 改动时，合并前额外执行一次

### 1.2 执行命令

```bash
dotnet build bukit.slnx -c Release
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --no-clean --incremental
```

### 1.3 记录项

- 构建总耗时（clean 与 incremental 各一次）
- 日志中的 `rendered / skipped`
- `.cache/build-manifest*.json` 是否正常更新
- 是否出现异常正文读取路径（例如无必要阶段重复读取正文）

### 1.4 判定规则

- 通过：incremental 相比 clean 有稳定收益，且无异常错误或明显读放大迹象
- 告警：incremental 收益异常下降、日志出现重复重渲染异常、manifest 行为异常

## 2) Collections 与兼容层治理

### 2.1 统一口径

- 主路径：`site.collections`（集合级 permalink/template/list 策略）
- 兼容路径：`post/page` 默认规则（仅兼容用途，不作为长期扩展主模型）

### 2.2 变更前检查

- 是否可以先通过 `collections` 配置达成目标
- 是否会影响现有主题对 `post/page` 的兼容行为
- 是否需要在 `RouteGeneratorTests` 增加对应场景

### 2.3 变更后验证

```bash
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter RouteGenerator
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean
```

### 2.4 判定规则

- 通过：collections 场景与兼容场景都可解释且测试覆盖
- 告警：新增能力仅在代码路径成立但文档/测试无对应说明

## 3) 文档-资产一致性检查

### 3.1 执行频率

- 每月一次
- 发布前一次

### 3.2 快速检查命令

```bash
pwsh ./scripts/check-doc-asset-consistency.ps1
bash ./scripts/check-doc-asset-consistency.sh

# 补充排查（关键词粗筛）
rg -n "\\.github/workflows/smoke\\.yml|\\.github/workflows/build\\.yaml" README*.md guide
```

> 说明：出现结果不一定是错误；若是“需自建/示例”语义可保留，若是“仓库已内置”语义需修正。

### 3.3 路径可达性检查项

- 入口文档里提到的 solution 是否存在
- 入口文档里提到的目录是否存在
- 若文档声明“仓库已内置 workflow”，需核实文件确实存在

### 3.4 判定规则

- 通过：文档描述与仓库现实一致，且入口路径可达
- 告警：出现“文档断言存在，但仓库不存在”的描述

## 4) 月度/季度节奏

### 每月

- 执行第 1 节基线检查
- 执行第 3 节一致性检查
- 将结果记录到维护日志（可放在 `docs/` 专题文档）

### 每季度

- 回顾 collections 与兼容层策略是否需要收敛
- 复核 `architecture-review.md` 评分与优先级是否需更新

## 5) 变更触发器（必须执行清单）

遇到以下变更，必须执行本清单对应章节：

- 修改 `src/Bukit.Content/*`、`src/Bukit.Engine/*`：执行第 1 节
- 修改 `src/Bukit.Routing/*`、路由配置契约：执行第 2 节
- 修改 `README*` 或 `guide/*`：执行第 3 节
