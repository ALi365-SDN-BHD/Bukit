# Bukit Core G-04C `RouteInventoryInspectEntry` 单类型删除关闭台账

日期：2026-07-22

状态：已实施并通过跨边界验证与独立只读复审

基线：`main@88a31b5eba2e52219ec3d1a107b703acdf9a3467`

目标版本线：`2.0.0-alpha.1`

## 1. 决策

G-04C 本次只批准从 Bukit 2.0 CLR 公共面删除
`Bukit.Engine.RouteInventoryInspectEntry`。其余 135 项候选没有获得批量变更授权；
1.x `main` 的 CLR 可见性不受本任务影响。

该类型是未被生产代码消费的实现型 DTO。`RouteInventoryValidator` 的实际业务逻辑使用
私有嵌套 `RouteInventoryEntry`，因此本次删除不改变内容路由生成、模板解析、路径安全、
冲突检测或构建输出。

## 2. 消费者证据边界

仓库语义检索没有发现 Core、Labs、官方插件或测试消费者；G-04B3 的认证公开搜索结果为
`no-public-match-found`。这只能证明已审阅的公开证据没有命中，不能证明私人、未索引或
未自愿声明的消费者不存在。

关闭的 136 项 manifest 保留窗口关闭时的原始 candidate identity、搜索结果和
`unknown-until-voluntary-declaration` 状态。它是历史 cohort，不是删除后的当前公共面
枚举，因此本任务没有删除或重写其中的目标条目。

## 3. 公共面变化

- 产品版本：`1.0.10 -> 2.0.0-alpha.1`；
- 当前 baseline 类型：`540 -> 539`；
- 当前 baseline 的 `2.0-candidate`：`136 -> 135`；
- 删除项：`Bukit.Engine::Bukit.Engine.RouteInventoryInspectEntry`；
- schema、target framework、SDK policy、assembly mapping 和其余 539 项保持不变。

baseline 更新前，真实 drift check 只产生一条诊断：

```text
breaking: Bukit.Engine::Bukit.Engine.RouteInventoryInspectEntry: exported type removed
```

## 4. 兼容性与迁移

这是 Bukit 2.0 的 source/binary breaking change，没有替代 API。若私人消费者直接构造
该记录，应删除引用并使用消费者自己的数据结构；不得引用或要求 Bukit 暴露内部
`RouteInventoryEntry`。

若后续出现新的直接 CLR、反射、序列化、继承、公共签名或 Native AOT 消费证据，
必须重新开启独立兼容性任务。本台账不授权临时 facade、兼容 shim 或另外 135 项变更。

## 5. 验证证据

- 删除前架构测试按预期 RED，删除后同一测试 GREEN；
- `Bukit.Engine.Tests` 与完整 `Bukit.Architecture.Tests` 通过；
- public API drift self-test 及更新后的真实 check 通过；
- Core、Labs 与 `bukit-plugins.slnx` Release 编译通过；
- `osx-arm64` Native AOT 归档构建及 release-artifact smoke 通过；
- 第一次独立只读实施复审未发现未关闭的 Critical 或 Important finding。

环境或基础设施阻塞必须保留为未取得证据，不得记录为通过。父任务的
aggregate targeted gate 和最终 aggregate diff 复审在本关闭提交后执行，并以任务
最终交接记录为准。

## 6. 复审结论

第一次独立只读复审确认 diff 只包含已批准的 2.0 版本线、单类型源码删除、
当前 baseline 精确更新、架构守卫和治理文档。路由行为、配置 schema、插件协议、
持久化格式、asset URL、输出路径、HTTP/TLS 策略及全局路径工具均未改变。

本台账关闭的只是一个单类型试点，不代表 G-04C 批量收窄已经获批或完成。
