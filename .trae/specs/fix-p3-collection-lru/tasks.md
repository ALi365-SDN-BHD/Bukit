# Tasks

## P3-2: ConfigJsonSchemaGenerator collections 字段补全

- [x] Task 1: 添加 `CollectionSchema()` 方法到 ConfigJsonSchemaGenerator
  - 替换第 56 行 `("collections", Obj(("type", "object")))` 为 `("collections", CollectionSchema())`
  - 新增 7 个方法：CollectionSchema / CollectionItemSchema / CollectionPaginationSchema / CollectionOutputSchema / CollectionArchiveDetailSchema / CollectionFilteredListItemSchema / CollectionSchemaFieldItemSchema
  - 每个 collection: required=[permalink, template]，含 8 个属性组
  - 验证：`dotnet build src/Bukit.Config/Bukit.Config.csproj -c Release` ✅

## P3-8: BodyCacheDecorator LRU 淘汰

- [x] Task 2: 实现真实 LRU 淘汰策略
  - 移除 `private readonly ConcurrentQueue<string> _accessOrder`
  - 新增 `_lruLock` (object) + `_lruList` (LinkedList) + `_lruNodes` (ConcurrentDictionary)
  - 缓存命中时在 lock 内将节点移到链表尾部
  - 缓存新增时在 lock 内将 key 加入链表尾部
  - TrimExcess 从链表头部移除最老条目
  - 验证：`dotnet build src/Bukit.Content/Bukit.Content.csproj -c Release` ✅

- [x] Task 3: 新增 LRU 行为测试
  - LruHitRefreshesEvictionOrder: 命中 A 后淘汰的是 B 而非 A
  - LruEvictsLeastRecentlyUsed: A→B→C 淘汰 A
  - 验证：546/546 Content Tests 通过 ✅

- [x] Task 4: 全量回归
  - `dotnet build bukit.slnx -c Release` 0 错误 0 警告 ✅
  - `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release` 1072/1072 ✅
  - `dotnet test tests/Bukit.Content.Tests/Bukit.Content.Tests.csproj -c Release` 546/546 ✅

# Task Dependencies
- Task 1 和 Task 2 可并行执行（互不依赖）
- Task 3 依赖 Task 2
- Task 4 依赖 Task 1、3
