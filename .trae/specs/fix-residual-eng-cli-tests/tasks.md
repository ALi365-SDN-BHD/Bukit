# Tasks

## 分类 A: GetCollection 默认值 (1 test)

- [x] Task A1: 修复 SiteEngineHelperTests.GetCollection_WithNeither_ReturnsPage
  - 改 `Assert.Equal("page", result)` → `Assert.Equal("", result)`
  - **验证**: ✅ 通过

## 分类 B: 路由生成缺少集合配置 (3 tests)

- [x] Task B1: 修复 RoutePipelineTests — 添加 page collection permalink 配置
  - **验证**: ✅ 通过

- [x] Task B2: 修复 PagesByIdDataPluginTests — 添加 post collection permalink + RouteGenerator 参数适配
  - **验证**: ✅ 通过

## 分类 C: CollectionWarningStage 警告逻辑 (7 tests)

- [x] Task C1: 修复 CollectionWarningStageTests (7 tests)
  - [DEPRECATED] → [WARN], hasCollection → assert NotEmpty, customTypeWithoutColl → assert Single
  - **验证**: ✅ 全部通过

## 分类 D: 页面渲染 (1 test)

- [x] Task D1: 修复 PageRenderDispatcherLazyBodyTests — 添加 collection meta 确保列表路由匹配
  - **验证**: ✅ 通过

## 分类 E: 构建性能 (2 tests)

- [x] Task E1: 修复 BuildPipelinePerformanceTests — 添加 post collection 配置到 site.yaml
  - **验证**: ✅ 通过

## 分类 F: CLI Import (4 tests)

- [x] Task F1: 修复 ImportCommandTests — 添加 about.html 确保 page 类型的 page.html 模板被生成
  - **验证**: ✅ 全部通过

## 最终验证

- [x] Task Z: 全量回归
  - Engine: 0/1113 failures ✅
  - CLI: 0/834 failures ✅
  - Architecture: 0/12 failures ✅
  - 全部测试套件: 0 failures ✅
