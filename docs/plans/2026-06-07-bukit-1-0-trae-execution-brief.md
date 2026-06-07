# Bukit 1.0 Trae 执行任务书

> 面向：Trae / Claude Code / Codex / 其他代码代理
>
> 用途：将 “Bukit 1.0 可信稳定化计划” 拆成可执行、可验证、可 review 的任务包。

## 1. 执行目标

你的任务是执行 Bukit 1.0 的可信稳定化工作。第一优先级不是增加新功能，而是建立“任何人都敢用于正式网站”的信任。

本任务书默认：

- `BukitJalil` 不在 Bukit 1.0 范围内
- Bukit 核心面必须先达到完整 GA 标准
- 允许在 1.0 前做一次集中 cleanup，但不能私自决定 breaking cleanup 的边界

## 2. 硬性规则

1. 不要自行改变 Bukit 1.0 的目标、边界或优先级。
2. 第一优先级是修复信任缺口，不是继续扩展功能。
3. 遇到以下高风险面时，不可自行做产品决策，只能标注并给出建议：
   - 配置契约
   - 内容模型
   - 路由行为
   - 主题接口
   - 插件协议
   - 向后兼容
   - 安全边界
4. 每个任务都必须包含：
   - 现状分析
   - 根因归类
   - 最小修复
   - 回归保护
   - 实际验证结果
5. 不允许通过以下方式“伪修复”：
   - 删除 smoke / doctor / security 检查
   - 放宽规则来掩盖资产漂移
   - 依赖 undocumented behavior
   - 改写目标以避免处理根因

## 3. 输出格式

每个任务完成后，必须按以下格式汇报：

```md
## 任务结果
- 是否完成
- 改了什么
- 哪些文件受影响
- 哪些属于实现修复，哪些属于契约澄清

## 根因
- 问题真实原因
- 为什么之前没有被测试拦住

## 验证
- 运行了哪些命令
- 实际结果是什么

## 风险
- 还剩哪些边界未覆盖
- 哪些点需要人工拍板

## 下一步建议
- 建议继续哪个任务包
- 是否需要先人工 review
```

## 4. 执行顺序

按波次推进，不要跳步。

### Wave 1：修复仓库自一致性

目标：先把官方资产、测试、治理规则跑顺，解决最直接伤害信任的问题。

#### 任务包 1：修复 `smoke.sh` 当前失败链条

**目标**

让 `bash scripts/smoke.sh Release` 在当前仓库通过，并且修复过程中不靠放宽规则过关。

**重点检查**

- starter/example 与 doctor 规则是否一致
- `bukit.templates.yaml` 是否完整
- starter 模板上下文声明是否与当前实现一致
- 示例站点中是否存在未知插件配置
- theme/static 目录预期是否与文档、doctor、smoke 一致

**要求**

1. 先复现失败
2. 列出失败项与根因
3. 用最小改动修复
4. 补回归测试或 fixture
5. 重新运行：
   - `bash scripts/smoke.sh Release`
   - `dotnet test bukit.slnx -c Release --no-restore`

**禁止**

- 删除 smoke 检查
- 通过降低 doctor 严格度掩盖问题
- 用 undocumented behavior 规避问题

#### 任务包 2：统一 starter / example / doctor / docs 契约

**目标**

消除官方示例、starter theme、doctor 输出、README/guide、skills 之间的契约漂移。

**要求**

1. 找出当前官方推荐路径与实际仓库行为不一致的点
2. 优先修官方资产，不优先改文案掩盖实现问题
3. 若必须改文档，必须同步修改示例与测试
4. 输出 drift 清单，说明每个漂移点的处理方式

**验证**

- `bash scripts/smoke.sh Release`
- 必要时运行：
  - `dotnet run --project src/Bukit.Cli -c Release -- docs check`
  - 其他与 docs consistency 相关脚本

#### 任务包 3：把 Wave 1 修复固化为回归

**目标**

把 Wave 1 修掉的问题补成自动化保护，避免以后再次漂移。

**要求**

1. 为 starter/example/doctor/template contract 增加测试或 fixture 覆盖
2. 优先补最靠近公开契约的测试，不只补内部实现单测
3. 明确每个新增测试防止哪类回归

**验证**

- `dotnet test bukit.slnx -c Release --no-restore`
- `bash scripts/smoke.sh Release`

### Wave 2：冻结核心 1.0 契约

目标：把“已经能跑”的行为正式收敛成可承诺、可回归、可版本化的 1.0 契约。

#### 任务包 4：冻结 `site.yaml` 配置契约

**范围**

- 字段语义
- 默认值
- override precedence
- 校验行为
- deprecation 策略

**要求**

1. 找出当前实现、文档、示例中的不一致
2. 输出建议冻结面
3. 对必须清理的 breaking point 单独列出
4. 如有代码修改，必须补契约测试

**注意**

可以实现一致性修复，但不要自行决定重大 breaking cleanup，只能标注并提出建议。

#### 任务包 5：冻结内容模型与 schema 行为

**范围**

- `ContentItem` 对外行为
- reserved meta keys
- field normalization
- schema validation 语义
- Markdown / Notion / composite source 对齐

**要求**

补齐契约测试，明确 Meta 与 Fields 边界。

#### 任务包 6：冻结路由契约

**范围**

- 路由优先级
- list / taxonomy / pagination / archive 派生规则
- outputPath 编码
- 冲突处理语义

**要求**

1. 给出当前优先级矩阵
2. 补 golden-style route inventory 测试
3. 明确哪些 fallback 不再允许依赖

#### 任务包 7：主题接口版本化

**范围**

- `theme.yaml`
- `extends`
- `min_engine_version`
- `bukit.templates.yaml`
- required templates
- template accepts / capabilities
- remote theme lock 行为

**要求**

定义 1.0 主题接口边界，并用测试保护。

#### 任务包 8：插件接口版本化

**范围**

- built-in plugin lifecycle
- source-generated plugin contract
- external protocol plugin schema
- handshake/version negotiation
- capabilities
- env isolation
- output manifest tracking
- failure semantics

**要求**

明确 1.0 插件接口边界，并区分核心插件机制与更开放的插件生态。

### Wave 3：发布级信任验收

目标：把 1.0 从“设计上稳定”变成“可发布、可回滚、可审计、可复现”。

#### 任务包 9：可复现构建与审计产物定型

**范围**

- `.bukit/` 报告文件
- route / assets / security / build reports
- clean vs incremental 一致性
- repeated build 稳定性
- 远程主题 lock
- plugin stale cleanup

**要求**

让构建结果达到可复现、可审计、可比较、可回滚的 1.0 标准。

#### 任务包 10：错误信息与诊断码补齐

**目标**

让所有 GA-locked 用户路径具备稳定诊断码、明确定位信息和修复提示。

**要求**

1. 盘点仍无稳定 diagnostic code 的关键异常路径
2. 补齐 Config / Content / Render / Plugin / Build 关键用户面
3. 统一输出格式

#### 任务包 11：安全回归升级为 release gate

**范围**

- route/output safety
- theme path safety
- plugin env/output/entry safety
- SSRF/media/remote fetch safety
- sensitive file leakage
- dangerous URL output

**要求**

把安全边界整理成正式 1.0 发布门槛，并补齐自动化验证。

#### 任务包 12：发布前兼容与迁移文档

**必须输出**

- 核心 GA 面
- 限制支持面
- experimental 面
- breaking cleanup 清单
- 升级迁移指南
- 回滚与审计说明

## 5. 每个任务的执行流程

每个任务包都按以下顺序执行：

1. 先读相关代码、fixture、文档、脚本
2. 复现问题或确认当前行为
3. 定位根因
4. 做最小实现修复
5. 补测试或回归保护
6. 跑验证命令
7. 按规定格式输出结果

## 6. 标准验证命令

除非任务明确不需要，否则优先使用这些验证入口：

```bash
dotnet test bukit.slnx -c Release --no-restore
bash scripts/smoke.sh Release
bash scripts/smoke-all.sh Release
bash scripts/security-regression.sh Release
dotnet run --project src/Bukit.Cli -c Release -- docs check
```

如任务涉及构建一致性、增量构建、报告产物，请补充对应命令并记录实际结果。

## 7. 第一条建议直接执行的任务

如果没有额外指示，先执行：

### 任务包 1：修复 `bash scripts/smoke.sh Release` 失败链条

执行要求：

1. 先完整复现失败
2. 对失败项做根因分类
3. 做最小修复
4. 补自动化回归
5. 重新运行：

```bash
bash scripts/smoke.sh Release
dotnet test bukit.slnx -c Release --no-restore
```

注意：

- 不允许删除 smoke 检查
- 不允许通过放宽 doctor 规则掩盖问题
- 遇到可能影响 1.0 公开契约的 breaking 风险时，只能标注，不可私自决定

## 8. 人工 review gate

以下情况必须等待人工 review，而不是继续自动推进：

- 需要改变公开配置字段语义
- 需要改变路由优先级或 fallback 语义
- 需要改变主题或插件的版本承诺
- 需要引入 breaking cleanup
- 需要降低安全边界或测试门槛

