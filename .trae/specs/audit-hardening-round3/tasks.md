# Tasks

## 阶段一：架构依赖违规

- [ ] Task 1: 修复 RouteCommand.cs 架构违规
  - [ ] 移除 `using Bukit.Content;` 和 `using Bukit.Routing;`
  - [ ] 重构 `MetaHelpers.IsDataItem` 调用为 Bukit.Engine 封装方法（或通过 Engine 公开的 API 实现等效逻辑）
  - [ ] 重构 `RouteGenerator.GenerateWithSource` 调用为 Bukit.Engine 封装
  - [ ] 重构 `DefaultContentProviderFactory`/`ContentPipeline` 调用为 Engine 层封装
  - **验证**: `dotnet test tests/Bukit.Architecture.Tests --filter "Cli_MustNotDirectlyDependOn_Content_Rendering_Routing"` 通过

- [ ] Task 2: 修复 DataCommand.cs 架构违规
  - [ ] 移除 `using Bukit.Content;`
  - [ ] 重构 `MetaHelpers.IsDataItem` 和 `ContentPipeline` 调用为 Engine 封装
  - **验证**: `dotnet test tests/Bukit.Architecture.Tests --filter "Cli_MustNotDirectlyDependOn_Content_Rendering_Routing"` 通过

## 阶段二：import 生成模板缺失

- [ ] Task 3: ThemeGenerator 添加 theme.yaml 生成
  - [ ] 在 `ThemeGenerator.Generate()` 中，根据实际 `pages` 的 PageType 构建模板角色列表
  - [ ] 实现 `WriteThemeYaml()` 方法：home 设 `required: true`，其他默认 `required: false`
  - [ ] PageType.Home → role "home", PageType.Page → role "page", PageType.PostList → role "list", PageType.PostDetail → role "post"
  - **验证**: 重新编译; 用 import 测试验证生成的 theme.yaml 存在且正确

- [ ] Task 4: SiteConfigGenerator 按页面类型生成集合
  - [ ] 修改 `Generate()` 方法接收 `List<DiscoveredPage>` 参数
  - [ ] 仅当存在对应 PageType 的页面时才生成集合配置
  - [ ] 用实际生成的模板文件名填充 `template`/`listTemplate`
  - **验证**: 编译通过; import 测试通过

- [ ] Task 5: 统一 Import 模板命名为 post.html
  - [ ] `ThemeGenerator.GetTemplateFileName`: `PageType.PostDetail => "post.html"`（原 `"article.html"`）
  - [ ] `SiteConfigGenerator.Generate`: `template: 'pages/post.html'`（原 `pages/article.html`）
  - [ ] `ContentExtractor.cs` 中 `PostDetail` 模板映射同步更新
  - **验证**: 相关测试中 `article.html` 引用同步更新; import 测试通过

- [ ] Task 6: PostList listTemplate 动态匹配 slug
  - [ ] `SiteConfigGenerator.Generate()` 从实际 PostList 页面提取 slug 作为 `listTemplate`
  - [ ] 如果存在多个 PostList 页面，使用第一个或抛出警告
  - **验证**: import 测试中多集合场景 template 引用与文件一致

## 阶段三：源码 "page" 硬编码

- [ ] Task 7: 修复 ContentExtractor.cs 和 HtmlDemoImporter.cs 回退值
  - [ ] `ContentExtractor.cs:89`: `Type` 使用 collection 名或 "detail" 替代 "Page"
  - [ ] `ContentExtractor.cs:95`: `_ => "page"` → 使用合理的默认模板名
  - [ ] `HtmlDemoImporter.cs:257`: `"page"` 回退 → 使用 "generic"
  - **验证**: 所有不依赖旧值的 import 测试通过

- [ ] Task 8: 修复 ImportModels.cs 默认值
  - [ ] `PageRecord.Type = "Page"` → 使用 convention 或空默认
  - [ ] `PageRecord.Template = "page"` → 使用 convention 或空默认
  - **验证**: import 相关 CLI 测试通过

- [ ] Task 9: 修复 CloneYamlWriter.cs defaultType 约定
  - [ ] 更新 `defaultType: "page"` 为新约定值
  - **验证**: clone 测试通过

- [ ] Task 10: 修复派生页面插件 type 字段
  - [ ] `PaginationPlugin.cs:70`: `["type"] = "page"` → 使用新约定
  - [ ] `ArchivePlugin.cs:98,135,168`: 同上
  - [ ] `TaxonomyPageCreator.cs:119,223`: 同上
  - **验证**: 相关 Engine 测试通过

## 阶段四：Doctor helper 逻辑迁移

- [ ] Task 11: RouteInventoryValidator 文档化 "detail" kind
  - [ ] 提取 `"detail"` 为 `DefaultDetailKind` 常量
  - [ ] 添加 XML 文档注释说明 theme.yaml 需声明 `accepts.kind: detail`
  - **验证**: 编译通过

- [ ] Task 12: ThemeTemplateResolver 文档化 "home" 固定角色
  - [ ] 为 `HomeTemplateKey`、`DefaultHomeTemplate` 常量添加文档注释
  - [ ] `ValidateRequiredTemplates()` 添加文档说明 home 是唯一强制角色
  - **验证**: 编译通过

- [ ] Task 13: Scriban linter 清理旧变量名
  - [ ] `ScribanTemplateLinter.cs:90`: 移除 `"post"` 和 `"page_item"` 或标记为 legacy
  - [ ] `ScribanModelKnownFields.cs:162`: 同步处理
  - **验证**: Scriban linter 测试通过; 现有模板无新增警告

- [ ] Task 14: DoctorManifestChecker 集成 accepts
  - [ ] `CheckUnreferencedTemplates` 中查询主题清单 `accepts` 信息
  - [ ] 通过 `accepts` 匹配的模板也加入 `usedTemplates`
  - **验证**: 不引入过度的未引用警告

## 阶段五：测试口径对齐

- [ ] Task 15: 修复 ContentItemExtensionsTests L54 断言
  - [ ] 更新 `GetCollection_NoCollectionNoType_ReturnsDefault` 断言值
  - **验证**: `dotnet test --filter "GetCollection_NoCollectionNoType_ReturnsDefault"` 通过

- [ ] Task 16: 修复 `["type"] = "page"/"post"` 测试口径 (~30 文件)
  - [ ] 批量搜索替换: 关键测试文件中 `["type"] = "page"` → 添加 `["collection"] = "page"` 并使用非 legacy type 值
  - [ ] 优先修复: SeoIndexBuilderTests, RouteGeneratorTests, SiteEngineIntegrationTests, SeoModelBuilderTests, GeoSeoModelBuilderTests, RssGeneratorTests, RoutePipelineTests, CollectionWarningStageTests, SiteEngineHelperTests, ProtocolEchoPlugin/Program.cs
  - [ ] 注意保留合理用法: ScribanModelBinderTests 中的 `obj["page"]` 是模型绑定键，不需修改
  - [ ] 注意 `ContentField.Type = "list"` 是数据类型，不需修改
  - **验证**: `dotnet test bukit.slnx -m:1 /p:UseSharedCompilation=false` 整体回归通过

## 阶段六：最终验证

- [ ] Task 17: 全量回归测试 + 5 示例 doctor/build
  - [ ] `dotnet test bukit.slnx -m:1 /p:UseSharedCompilation=false` 通过
  - [ ] 5 示例 (starter, blog-site, docs-site, component-theme, plugin-site) doctor 全部通过
  - [ ] 5 示例 build 全部成功
  - **验证**: 测试通过率不低于修复前; 无新增回归

# Task Dependencies
- Task 1, 2 可并行
- Task 3, 4, 5, 6 依赖阶段二的 import 基础设施变更，需关注相互依赖：Task 4 依赖 Task 3 的 theme.yaml 生成; Task 5/6 与 Task 3/4 耦合
- Task 7, 8, 9 可并行
- Task 10 独立
- Task 11, 12, 13, 14 可并行
- Task 15, 16 可并行；Task 16 的后半部分依赖 Task 3-10 完成
- Task 17 依赖所有前置任务
