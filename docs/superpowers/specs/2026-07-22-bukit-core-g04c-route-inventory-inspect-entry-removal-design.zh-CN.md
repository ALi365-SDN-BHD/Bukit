# Bukit Core G-04C `RouteInventoryInspectEntry` 单类型删除试点设计

日期：2026-07-22

状态：`design-approved / written-spec-review-pending / implementation-not-started`

基线：`main@88a31b5eba2e52219ec3d1a107b703acdf9a3467`

目标集成分支：`2.0`

独立任务分支：`codex/g04c-route-inventory-inspect-entry-removal`

## 1. 目标与决策

本任务是 G-04C 的首个且唯一单类型试点。经明确授权，选择完整删除
`Bukit.Engine.RouteInventoryInspectEntry`，而不是改为 `internal` 或在没有消费者
证据时增加 `Obsolete` 过渡层。

试点目的不是追求 public 类型数量下降，而是验证 Bukit 能否对一个满足严格资格条件
的实现型公共类型，完整执行 2.0 breaking-change 治理链：消费者证据、版本隔离、
失败测试、源码删除、公共 API deliberate approval、baseline 更新、兼容性说明、AOT
验证和独立复审。

成功标准是：只删除这一项无运行时职责的 CLR 类型，路由业务行为与所有受支持产品
契约保持不变；任何额外公共面、schema、协议、配置、持久化格式或运行时行为变化都
是阻断性 scope drift。

## 2. 当前资格证据

### 2.1 公开治理已收敛

GitHub、`origin/main` 与本地 `main` 均为
`88a31b5eba2e52219ec3d1a107b703acdf9a3467`。G-04B3 已将声明生命周期收敛为：

- `declarationState = closed`；
- `feedbackChannel.state = closed`；
- `eligibleAfterRelease = v1.0.10`；
- Issue #60 已关闭；
- 136 项认证搜索与独立证据复审已经完成。

关闭只证明 G-04C 可以被单独决策，不证明私人、未索引或未声明消费者不存在。

### 2.2 类型资格

当前活动仓库对 `RouteInventoryInspectEntry` 的源码检索只命中其定义及治理／审计文档；
没有 Core、Labs、官方插件或测试引用。定义位于
`src/Bukit-Core/Bukit.Engine/RouteInventoryValidator.cs`，但
`RouteInventoryValidator` 的生产逻辑完全使用其私有嵌套类型
`RouteInventoryEntry`，不构造、返回、继承或反射目标类型。

现有证据同时确认：

- 无 protected members；
- 无 public/protected 签名传播；
- 无 serializer、source-generated context、reflection、AOT、schema 或持久化注册；
- 无活动使用文档；
- 认证公开搜索为 `no-public-match-found`；
- 已声明的 CLI／配置／主题／进程消费者没有候选级 CLR 引用。

基线验证结果：Architecture Tests 99/99 通过，文档一致性通过，真实 public API drift
check 通过且 build 为 0 warnings / 0 errors。

## 3. 分支与版本隔离

本地 `2.0` 集成分支从公开收敛后的 `main@88a31b5e` 创建；独立任务分支再从 `2.0`
创建。两者当前都没有 push。

实施时先用独立提交把 `Directory.Build.props` 的产品版本从 `1.0.10` 改为
`2.0.0-alpha.1`，建立可由源码验证的 2.0 开发线。删除提交必须位于该版本提交之后。

该任务只能合并回 `2.0`：

- 禁止合并回 1.x `main`；
- 禁止把删除提交 cherry-pick 到 1.x；
- 禁止在没有显式授权时 push `2.0` 或任务分支；
- 未来若 `main` 被明确切换为 2.0 开发线，须另行完成版本治理决策，不能由本任务
  隐含完成。

## 4. 源码与公共面变化

唯一生产代码变化是删除以下顶层定义：

```csharp
public sealed record RouteInventoryInspectEntry(
    string Url,
    string OutputPath,
    string Template,
    string? Collection,
    string? Type,
    string? Language,
    string RouteSource);
```

不修改同文件中的 `RouteInventoryValidator`、私有 `RouteInventoryEntry`、路由生成、
安全校验、冲突检测、模板选择或内容加载逻辑。不得用重命名、移动、internal 替身或
新 facade 代替删除。

删除后的编译程序集不得再通过大小写敏感完整名称
`Bukit.Engine.RouteInventoryInspectEntry` 解析该类型。

## 5. 公共 API baseline 与 deliberate approval

当前受治理 baseline 有 540 个类型，其中 136 个为 `2.0-candidate`。删除源码但尚未
更新 baseline 时，真实 drift check 必须只报告
`Bukit.Engine::Bukit.Engine.RouteInventoryInspectEntry` 的 breaking removal；出现任何
第二项 drift 都必须停止。

随后使用现行 snapshot 工具写入全新临时文件。受控 baseline 更新必须满足：

- 类型总数 `540 -> 539`；
- `2.0-candidate` 数量 `136 -> 135`；
- 只删除目标类型及其已有 public members；
- assembly mapping、schema、target framework、SDK policy 和其余 539 项完全不变；
- 更新后 public API self-test 与真实 check 都退出 0。

用户对方案 1 的明确批准构成本试点的 deliberate removal decision，但不免除精确 diff、
测试和独立复审。

## 6. 关闭 manifest 与后续 disposition

`docs/governance/bukit-core-2.0-public-surface-candidates.v1.json` 是已经关闭的 136 项
声明窗口快照。它保留窗口结束时的候选 identity、认证搜索和私人消费者未知状态，
本任务不删除或重写其中的目标候选，也不修改另外 135 项。

删除结果通过三层记录表达：

1. 当前 public API baseline 不再包含目标类型；
2. 新增
   `docs/analysis/bukit-core-g04c-route-inventory-inspect-entry-removal-2026-07-22.zh-CN.md`
   作为单类型决定、迁移影响、测试证据和复审结论台账；
3. 活动消费者声明与 public API governance guide 链接该台账，并明确关闭 manifest
   是历史 cohort，不是删除后的当前公共面枚举。

活动声明中关于该类型“未批准 G-04C”的旧句子必须更新为已由本独立决策进入 2.0
删除试点；同时明确另外 135 项没有被批量批准。

不为本试点增加新的 JSON schema、manifest 字段或任意新状态枚举。

## 7. 兼容性与迁移说明

这是明确的 2.0 binary/source breaking change。虽然没有观察到真实消费者，但无法证明
私人消费者为零。

该类型没有运行时职责或替代 API。若外部源码直接构造它，迁移方式是删除该引用并
使用消费者自己的数据结构；不得推荐内部 `RouteInventoryEntry`，也不得把
`RouteInventoryValidator` 私有实现提升为新公共契约。

若实施期间出现真实消费者证据，立即停止删除，恢复目标类型和旧 baseline，另立
obsolete/deprecation 设计；不得在本任务中临时增加 facade 或兼容 shim。

## 8. 测试设计

### 8.1 TDD 与架构守卫

在 `tests/Bukit.Architecture.Tests/` 新增单一职责测试文件。第一条测试先断言 Engine
程序集无法解析完整类型名；它在删除前必须因类型仍存在而 RED，删除后转为 GREEN。

最终架构测试还必须验证：

- `Directory.Build.props` 的版本精确为 `2.0.0-alpha.1`；
- 当前 public API baseline 不包含目标类型；
- 关闭 manifest 仍恰好保留一份历史候选与原搜索证据；
- 活动声明和 G-04C 台账明确表达“该类型已单独决策、其余 135 项未获批”。

这些断言防止以后重新暴露同名类型、误删历史证据或把单类型试点扩散为批量授权。

### 8.2 定向验证

至少运行：

- `Bukit.Architecture.Tests`；
- `Bukit.Engine.Tests`；
- Core、Labs 和官方插件 Release 编译；
- public API drift self-test；
- 删除后、baseline 更新前的精确 breaking diagnostic；
- baseline 更新后的真实 drift check；
- 当前主机 `osx-arm64` 的真实 Native AOT publish/package smoke；
- 每个实现子任务的 `post-change-focused.sh`；
- 父任务唯一一次 `post-change-targeted.sh`，其 `--base` 固定为
  `88a31b5eba2e52219ec3d1a107b703acdf9a3467`，并显式枚举实施计划批准的全部变更路径；
- 独立只读 task review 和最终 aggregate diff review。

不运行 full、release、`test-all`、`smoke-all` 或整仓库测试。

## 9. 失败、环境与回滚策略

以下任一条件阻断试点：

- 出现新的直接 CLR、反射、序列化、继承或签名消费者；
- drift snapshot 包含目标之外的变化；
- Engine 行为测试出现回归；
- Core、Labs、官方插件或 AOT 无法编译；
- 变更无法保持在 2.0 版本线；
- 独立复审出现未关闭 Critical 或 Important finding。

环境、权限或基础设施失败只按实际阻塞报告，不授权修改 TLS、NuGet、测试、CI、发布
或其他无关代码。

版本初始化和单类型删除使用不同提交。若删除需要回滚，只回退删除、baseline、测试和
治理台账提交；`2.0.0-alpha.1` 版本线可继续保留。回滚不得改变 1.x `main`。

## 10. 明确非目标

- 不处理另外 135 个候选；
- 不修改 schema、插件协议、配置或持久化格式；
- 不修改路由行为、asset URL、输出路径或全局路径工具；
- 不创建替代 facade、公共 DTO 或 SDK package；
- 不更新或重新运行 136 项 GitHub 搜索，除非出现新消费者证据；
- 不关闭、重开或评论 Issue #60；
- 不 push 或发布任何 2.0 artifact；
- 不把本试点解释为 G-04C 批量授权。
