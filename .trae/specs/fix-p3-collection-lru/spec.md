# P3-2 + P3-8 修复 Spec

## Why

**P3-2**: `ConfigJsonSchemaGenerator` 中 `site.collections` 的 JSON Schema 仅为无约束的 `{"type": "object"}`，IDE 对 collection 内部字段（permalink、template、listRoute、pagination、output、filteredLists、schema）无智能提示和校验。

**P3-8**: `BodyCacheDecorator` 的淘汰策略使用 `ConcurrentQueue` FIFO——缓存命中时不将 key 重新入队，导致高频访问条目因"入队早"而被淘汰，而非按"最近最少使用"淘汰。

P3-4（architecture.md VariantBuildPipeline）已修复，无需处理。

## What Changes

### P3-2: ConfigJsonSchemaGenerator
- 将 `("collections", Obj(("type", "object")))` 替换为完整的 `CollectionSchema()` 方法
- 定义每个 collection 的 JSON Schema：permalink (string, required)、template (string, required)、listRoute、listTemplate、schemaFailMode (enum)、pagination、output、filteredLists、schema

### P3-8: BodyCacheDecorator
- 替换 `ConcurrentQueue<string> _accessOrder` 为 `LinkedList<string>` + `ConcurrentDictionary<string, LinkedListNode<string>>` + `lock` 的 LRU 实现
- 缓存命中（TryGetValue 成功）时，将 key 移到链表尾部
- 缓存新增时，key 加入链表尾部；超量时从链表头部淘汰

## Impact
- Affected files:
  - P3-2: `src/Bukit.Config/ConfigJsonSchemaGenerator.cs`
  - P3-8: `src/Bukit.Content/BodyCacheDecorator.cs`
  - P3-8: `tests/Bukit.Content.Tests/BodyCacheDecoratorTests.cs`（新增 LRU 行为测试）

## ADDED Requirements

### Requirement: Collection JSON Schema 完整定义
ConfigJsonSchemaGenerator SHALL 为 `site.collections` 中每个 collection 提供完整的字段校验 schema。

#### Scenario: collection 必填字段
- **WHEN** collection 定义缺少 `permalink` 或 `template`
- **THEN** JSON Schema validator 应报告错误

#### Scenario: collection 可选字段提示
- **WHEN** 用户在 IDE 中编辑 collection
- **THEN** 应提示 listRoute、listTemplate、schemaFailMode、pagination、output、filteredLists、schema 字段

### Requirement: BodyCacheDecorator 真实 LRU 淘汰
BodyCacheDecorator SHALL 实现 LRU（最近最少使用）淘汰策略：缓存命中时更新 key 的访问时间，淘汰时移除最久未访问的条目。

#### Scenario: 命中刷新访问顺序
- **GIVEN** 缓存中有 key A（先入）、key B（后入）
- **WHEN** key A 被命中
- **THEN** key A 的淘汰优先级低于 key B（因为 A 最近被访问过）

#### Scenario: LRU 淘汰最久未访问的条目
- **GIVEN** 缓存已满（达到 maxEntries），且 key A 最近被访问过、key B 从未被访问
- **WHEN** 新 key C 触发淘汰
- **THEN** 淘汰 key B 而非 key A
