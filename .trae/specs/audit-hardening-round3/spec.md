# 深度审计修复 Round 3 Spec

## Why
前一阶段完成了 `pages/post.html`/`pages/list.html` 缺失和 Import 测试修复（2 处 bug），但深度审计发现 26 项残留问题，涵盖架构依赖违规、旧默认 `"page"` 测试口径、import 生成模板缺失、Doctor helper 测试迁移四大方向。需要系统性修复以减少技术债务，使代码库与配置驱动模板模型一致。

## What Changes

### 阶段一：架构依赖违规（3 项）
- **RouteCommand.cs**: 移除 `using Bukit.Content` 和 `using Bukit.Routing`，通过 Bukit.Engine 封装层间接调用
- **DataCommand.cs**: 移除 `using Bukit.Content`，通过 Bukit.Engine 封装层间接调用

### 阶段二：import 生成模板缺失（2 项严重 + 2 项中等）
- **ThemeGenerator.cs**: 添加 `WriteThemeYaml()` 方法，根据实际发现的页面类型生成 `theme.yaml`
- **SiteConfigGenerator.cs**: 改为感知页面类型，仅生成实际存在页面类型的集合配置
- **统一命名**: `article.html` → `post.html`（与 clone/starter 生态一致）
- **PostList slug 动态匹配**: `listTemplate` 不硬编码 `insights.html`

### 阶段三：源码 "page" 硬编码（8 项）
- **ContentExtractor.cs**: `Type = "Page"` → 使用 collection 名
- **HtmlDemoImporter.cs**: `"page"` 回退 → 使用合理默认值
- **ImportModels.cs**: `Type = "Page"`, `Template = "page"` → 更新默认值
- **CloneYamlWriter.cs**: `defaultType: "page"` → 更新配置约定
- **PaginationPlugin.cs / ArchivePlugin.cs / TaxonomyPageCreator.cs**: `["type"] = "page"` → 更新派生页面 type

### 阶段四：Doctor helper 逻辑迁移（3 项高 + 3 项中）
- **RouteInventoryValidator.cs**: 提取 `"detail"` 为命名常量，添加文档
- **ThemeTemplateResolver.cs**: 文档化 `"home"` 为唯一固定角色
- **ScribanTemplateLinter.cs / ScribanModelKnownFields.cs**: 移除旧变量名 `"post"`, `"page_item"` 或标记为 legacy
- **DoctorManifestChecker.cs**: `CheckUnreferencedTemplates` 集成清单 `accepts`

### 阶段五：测试口径对齐（~30 文件）
- **ContentItemExtensionsTests.cs**: L54 断言修复
- **SeoIndexBuilderTests.cs / RouteGeneratorTests.cs / SiteEngineIntegrationTests.cs** 等: 添加 `collection` 字段或使用非 legacy type 名

## Impact
- Affected specs: audit-hardening-round2, collection-primary-model
- Affected code: src/Bukit.Cli/Commands/RouteCommand.cs, DataCommand.cs; src/Bukit.Importing/ (ThemeGenerator, SiteConfigGenerator, HtmlDemoImporter, ContentExtractor, ImportModels); src/Bukit.Engine/ (RouteInventoryValidator, ScribanTemplateLinter, ScribanModelKnownFields, ThemeTemplateResolver, PaginationPlugin, ArchivePlugin, TaxonomyPageCreator); tests/ (~30 files)
- **BREAKING**: Import 生成的模板文件名从 `article.html` 改为 `post.html`（仅影响 import 生成的新项目）

## ADDED Requirements

### Requirement: Import 生成 theme.yaml
Import 流程 SHALL 在生成模板文件时同步生成 `theme.yaml`，声明与实际发现页面类型匹配的模板角色。`home` 设置为 `required: true`，其他角色默认 `required: false`。

#### Scenario: 单页导入（仅 Home）
- **WHEN** import 仅有一个 index.html（Home）
- **THEN** 生成的 theme.yaml 仅包含 `templates.home: { template: pages/index.html, required: true }`

#### Scenario: 多类型页面导入
- **WHEN** import 有 Home + Page + PostList + PostDetail 类型
- **THEN** theme.yaml 包含 home, page, post, list 角色，各自对应正确的模板文件

### Requirement: SiteConfig 按页面类型生成集合
SiteConfigGenerator SHALL 仅生成存在对应页面类型的集合配置，而非无条件生成 page/post/company/service 四个集合。

#### Scenario: 单页导入
- **WHEN** import 只有一个 Home 页面
- **THEN** site.yaml 不包含 post/company/service 集合配置

#### Scenario: 有 PostList 页面
- **WHEN** import 有一个 PostList 页面（如 blog.html）
- **THEN** site.yaml 的 post 集合 `listTemplate` 指向实际生成的模板文件名（如 `pages/blog.html`）

### Requirement: CLI 架构隔离
Bukit.Cli 中的 RouteCommand 和 DataCommand SHALL NOT 直接使用 Bukit.Content 或 Bukit.Routing 命名空间的类型，应通过 Bukit.Engine 封装层间接调用。

## MODIFIED Requirements

### Requirement: Import 模板命名统一 (was: article.html)
Import 生成的 post detail 模板 SHALL 使用 `post.html`（而非 `article.html`），与 clone/starter 生态一致。

### Requirement: 派生页面 type 字段 (was: `type = "page"`)
PaginationPlugin、ArchivePlugin、TaxonomyPageCreator 生成的派生页面 SHALL 使用与 collection 体系一致的 type/collection 元数据。

## REMOVED Requirements

### Requirement: Scriban 旧变量名 `post`, `page_item` 作为已知根
**Reason**: 这些是旧固定角色模板体系的遗留命名，配置驱动模型中不再有特殊地位
**Migration**: 移除或标记为 deprecated legacy aliases
