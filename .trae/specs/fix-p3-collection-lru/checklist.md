# Checklist

## P3-2: Collection Schema
- [x] `ConfigJsonSchemaGenerator.Generate()` 中 `site.collections` 使用 `CollectionSchema()` 替代空 `Obj(("type", "object"))`
- [x] collection 必填字段 `permalink` 和 `template` 在 schema 的 required 数组中
- [x] `pagination` 子对象含 enabled、pageSize、urlPattern、firstPageUsesListRoute
- [x] `output` 子对象含 rss、sitemap、archive、feedPath、feedTitle、feedDescription、archiveDetail
- [x] `filteredLists` 数组项含 field(required)、value(required)、listRoute(required)、listTemplate
- [x] `schema` 数组项含 name(required)、type、label、format、enum、min、max、required、default
- [x] `schemaFailMode` 为 enum: off/warn/strict

## P3-8: LRU 淘汰
- [x] `ConcurrentQueue<string> _accessOrder` 已移除
- [x] 使用 `LinkedList<string>` + `ConcurrentDictionary<string, LinkedListNode<string>>` + lock 实现 LRU
- [x] 缓存命中时将 key 移到链表尾部
- [x] 淘汰时从链表头部移除最久未访问条目
- [x] 新增测试：命中 key A 后淘汰的是 key B 而非 key A

## 回归
- [x] `dotnet build bukit.slnx -c Release` 0 错误 0 警告
- [x] Content Tests 全部通过（546/546）
- [x] Engine Tests 全部通过（1072/1072）
