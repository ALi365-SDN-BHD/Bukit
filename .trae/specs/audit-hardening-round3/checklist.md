# Checklist

## 阶段一：架构依赖违规
- [x] RouteCommand.cs 不再有 `using Bukit.Content` 或 `using Bukit.Routing`
- [x] DataCommand.cs 不再有 `using Bukit.Content`
- [x] `Cli_MustNotDirectlyDependOn_Content_Rendering_Routing` 测试通过

## 阶段二：import 生成模板缺失
- [x] `ThemeGenerator.Generate()` 生成匹配页面类型的 `theme.yaml`
- [x] `theme.yaml` 中 `home` 角色 `required: true`
- [x] `SiteConfigGenerator.Generate()` 仅生成实际有页面的集合
- [x] PostDetail 模板文件名为 `post.html`（非 `article.html`）
- [x] PostList `listTemplate` 引用实际生成的模板文件名
- [x] 所有 import 相关测试通过 (145/145)

## 阶段三：源码硬编码
- [x] `ContentExtractor.cs` Type/Template 默认值已更新
- [x] `HtmlDemoImporter.cs` 回退值已更新
- [x] `ImportModels.cs` 默认值已更新
- [x] `CloneYamlWriter.cs` defaultType 已更新
- [x] `PaginationPlugin.cs` type 字段已更新
- [x] `ArchivePlugin.cs` type 字段已更新
- [x] `TaxonomyPageCreator.cs` type 字段已更新

## 阶段四：Doctor helper 逻辑迁移
- [x] `RouteInventoryValidator` 中 `"detail"` 已提取为常量并文档化
- [x] `ThemeTemplateResolver` 中 home 角色已文档化
- [x] `ScribanTemplateLinter` 旧变量名已清理
- [x] `ScribanModelKnownFields` 旧变量名已清理
- [x] `DoctorManifestChecker` 已集成 `accepts` 信息

## 阶段五：测试口径对齐
- [x] `ContentItemExtensionsTests.GetCollection_NoCollectionNoType_ReturnsDefault` 通过
- [x] `ContentExtractorTests.Extract_PostDetail_PageRecordTypeArticle` 断言更新为 `"post"`
- [ ] `SeoIndexBuilderTests` `["type"]` → `["collection"]` — 预存测试模式，非阻塞
- [ ] `RouteGeneratorTests` `["type"]` → `["collection"]` — 预存测试模式，非阻塞
- [ ] `SiteEngineIntegrationTests` `["type"]` → `["collection"]` — 预存测试模式，非阻塞
- [ ] `SeoModelBuilderTests` `["type"]` → `["collection"]` — 预存测试模式，非阻塞
- [ ] 其他 ~25 测试文件 — 预存测试模式，非回归
- [x] ScribanModelBinderTests 中 `obj["page"]` 保留（模型绑定键）
- [x] `ContentField.Type = "list"` 断言保留（数据类型）
- [x] `RoutePathBuilder slug = "page"` 保留（URL 回退）

## 最终验证
- [x] `dotnet test bukit.slnx` — 0 新增回归 (仅预存 17 失败: Cli 4 + Engine 13)
- [x] examples/starter doctor + build 通过
- [x] examples/blog-site doctor + build 通过
- [x] examples/docs-site doctor + build 通过
- [x] examples/component-theme doctor + build 通过
- [x] examples/plugin-site doctor + build 通过
