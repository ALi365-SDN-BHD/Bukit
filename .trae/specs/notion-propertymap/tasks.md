# Tasks

## Task 1: 新增 NotionPropertyMap 配置模型 ✅
- [x] 1.1 在 `NotionConfig` 中新增 `PropertyMap` 属性 (`NotionPropertyMapConfig?`)
- [x] 1.2 创建 `NotionPropertyMapConfig` record
- [x] 1.3 `dotnet build` 通过

## Task 2: 传递 propertyMap 到 NotionContentProvider ✅
- [x] 2.1 `NotionProviderOptions` 新增 `PropertyMap` 字段
- [x] 2.2 `ContentProviderFactory` 映射 `config.Notion.PropertyMap → options.PropertyMap`
- [x] 2.3 传递到提取和提升链

## Task 3: 修改 NotionPropertyParser 提取方法 ✅
- [x] 3.1-3.4 ExtractTitle/Slug/Type/PublishAt 接受 propertyMap 参数
- [x] 3.5-3.6 build + test 通过

## Task 4: 提升链使用 propertyMap 字段名 ✅
- [x] 4.1-4.5 language/i18nKey/summary/collection 使用 mapped 名称
- [x] 4.6 test 通过

## Task 5: Doctor --notion-schema 检查 ✅
- [x] 5.1-5.5 `CheckNotionSchemaAsync` 实现完整
- [x] 5.6 build 通过

## Task 6: 单元测试 ✅
- [x] 6.1-6.4 NotionPropertyMapTests (10 tests)
- [x] 6.5 test 全部通过

## Task 7: 验证整体正确性 ✅
- [x] 7.1 build 0 警告 0 错误
- [x] 7.2 format 通过
- [x] 7.3 517 Content + 1028 Engine + 730 Cli tests pass
- [x] 7.4 checklist 全部通过
