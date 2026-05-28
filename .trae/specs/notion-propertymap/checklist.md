# Notion PropertyMap Checklist

## Config 模型
- [x] `NotionPropertyMapConfig` record 存在，含 title/slug/type/publishAt/language/i18nKey/summary/collection (all `string?`)
- [x] `NotionConfig.PropertyMap` 属性存在
- [x] `NotionProviderOptions.PropertyMap` 属性存在
- [x] `ContentProviderFactory` 正确映射 config → options

## 提取方法使用 propertyMap
- [x] `ExtractTitle` 使用 `map.Title ?? "Title"`
- [x] `ExtractSlug` 使用 `map.Slug ?? "Slug"`
- [x] `ExtractType` 使用 `map.Type ?? "Type"`
- [x] `ExtractPublishAt` 使用 `map.PublishAt ?? "PublishAt"`，`"Date"` 作为二级回退
- [x] 不提供 propertyMap 时行为不变

## Meta 提升使用 mapped 字段名
- [x] `language` 提升使用 mapped 名称（默认 `"language"`）
- [x] `i18nKey` 提升使用 mapped 名称（默认 `"i18n_key"`，二级 `"i18nkey"`）
- [x] `summary` 提升使用 mapped 名称（默认 `"summary"`）
- [x] `collection` 提升使用 mapped 名称（默认 `"collection"`）

## Doctor --notion-schema
- [x] `bukit doctor --notion-schema` 可执行
- [x] 缺失字段报告 `NOT FOUND`
- [x] 类型不匹配报告 `type mismatch`
- [x] 异常时不会崩溃

## 测试覆盖
- [x] propertyMap 覆盖默认字段名测试 (10 tests)
- [x] 不提供 propertyMap 回退测试
- [x] meta 提升链使用映射后字段名测试
- [x] Config 反序列化测试 (已有 Config 测试)

## 回归验证
- [x] `dotnet build bukit.slnx -c Release` 0 警告 0 错误
- [x] `dotnet format bukit.slnx --verify-no-changes` 通过
- [x] 全部 Bukit.Content.Tests 通过 (517, +10)
- [x] 全部 Bukit.Cli.Tests 通过 (730/733, 3 failures pre-existing DeployCommand)
- [x] 全部 Bukit.Engine.Tests 通过 (1028)
- [x] 不破坏现有 CLI
- [x] 不破坏 examples/notion-site 构建
