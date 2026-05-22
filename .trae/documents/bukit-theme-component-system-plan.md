# Bukit 主题组件化强化实施计划

> 目标：将 Bukit 主题系统从 `layouts + assets + static + partials` 升级为可描述、可组合、可校验、可被 AI Agent 理解的组件化主题协议。

---

## 一、总览：新增/修改文件清单

### 新增项目

| 项目 | 路径 | 说明 |
|------|------|------|
| `Bukit.Theme` | `src/Bukit.Theme/` | 主题组件化核心库（新项目） |
| `Bukit.Theme.Tests` | `tests/Bukit.Theme.Tests/` | 主题组件化测试 |

### 修改项目

| 项目 | 说明 |
|------|------|
| `Bukit.Config` | 扩展 `ThemeConfig`，新增 `ComponentValidation` 等字段 |
| `Bukit.Engine` | 扩展 `BuildPathUtils`，集成 `ThemeComponentRegistry`、`ThemeTokensProcessor`、`PageComposer` 到渲染管线 |
| `Bukit.Rendering` | 新增 `render_section` / `render_component` Scriban 函数 |
| `Bukit.Cli` | 扩展 `ThemeCommand` 子命令（doctor、list-components、export-catalog） |

---

## 二、分步实施计划

### 阶段 1：新项目骨架（Bukit.Theme）

**步骤 1.1：创建 `src/Bukit.Theme/` 项目**
- 创建 `Bukit.Theme.csproj`（net9.0，AOT-friendly，引用 `Bukit.Config`、`Bukit.Shared`）
- 创建 `Bukit.Theme.Tests.csproj`（xUnit，引用 `Bukit.Theme`）

**步骤 1.2：创建强类型模型（Models/）**
- `ThemeManifestV2.cs` — 完整 `theme.yaml` 模型
  - `Name`, `DisplayName`, `Version`, `Engine`, `MinEngineVersion`
  - `Description`, `Extends`
  - `Capabilities` (i18n, seo, geo, darkMode, search, taxonomy 等)
  - `Layouts: Dictionary<string, string>` (default, landing, clean → 文件路径)
  - `PageTemplates: Dictionary<string, ThemePageTemplateDefinition>`
  - `Sections: Dictionary<string, ThemeSectionDefinition>`
  - `Components: Dictionary<string, ThemeComponentDefinition>`
  - `Assets: ThemeAssetsConfig`
  - 全部使用 `record`，Native AOT 友好
- `ThemePageTemplateDefinition.cs` — 页面模板定义
  - `Template`, `Label`, `Accepts` (type + collection), `RequiredFields`
- `ThemeSectionDefinition.cs` — Section 定义
  - `Template`, `Schema` (JSON 路径), `Preview`, `Description`
  - `Variants: Dictionary<string, ThemeVariantDefinition>`
  - `Data: ThemeDataBindingDefinition?`
- `ThemeVariantDefinition.cs` — 变体定义
  - `Template`, `Label`, `Description`
- `ThemeComponentDefinition.cs` — 组件定义
  - `Template`, `Props: Dictionary<string, string>` (prop name → type)
- `ThemeDataBindingDefinition.cs` — 数据绑定
  - `Source`, `Mode`, `Limit`, `Sort`, `Filters: Dictionary<string, object?>`

**步骤 1.3：创建 ThemeTokens 模型**
- `ThemeTokens.cs` — tokens.yaml 模型
  - `Colors`, `Font`, `Radius`, `Spacing`, `Layout`
  - 全部 `Dictionary<string, string>` 或嵌套 `record`
- `ThemeTokensProcessor.cs` — tokens → CSS 处理器
  - 读取 `tokens.yaml` → 生成 `:root { --color-xxx: ... }` CSS

**步骤 1.4：创建 ThemeComponentRegistry**
- `ThemeComponentRegistry.cs`
  - 接收根目录 + theme name，扫描 `sections/`、`components/` 目录
  - 构建 sections/components 索引
  - 支持继承链查找（当前主题 → 父主题）
  - 路径解析：`sections/{name}/{name}.html`，`components/{category}/{name}.html`

### 阶段 2：Section Schema 校验

**步骤 2.1：创建 SectionSchemaValidator**
- `SectionSchemaValidator.cs`
  - 加载 `schema.json` 文件
  - Schema 模型：`SectionSchema`，包含 `Props: Dictionary<string, SchemaPropDefinition>`
  - `SchemaPropDefinition`：`Type`, `Required`, `MaxLength`
  - 支持的 type：`string`, `number`, `boolean`, `url`, `image`
- 校验逻辑：
  - `required` 字段缺失 → 根据 `componentValidation` 配置决定 off/warn/strict
  - `maxLength` 超限
  - `url` 类型基础格式检查
  - `image` 类型基础检查
  - unknown prop → warn

**步骤 2.2：配置扩展**
- 在 `ThemeConfig` 中新增 `ComponentValidation` 字段：
  ```csharp
  public string ComponentValidation { get; init; } = "off";
  // 可选: "off", "warn", "strict"
  ```

### 阶段 3：Page Composer & Section Data Resolver

**步骤 3.1：创建 PageSectionDefinition**
- `PageSectionDefinition.cs`
  - `Type`, `Variant`, `Props`, `Source`, `Filter`, `Limit`, `Sort`

**步骤 3.2：创建 PageComposer**
- `PageComposer.cs`
  - 从 `page.fields.sections` 读取 JSON 数组
  - 反序列化为 `List<PageSectionDefinition>`
  - 每个 section 注入默认 props、data binding
  - 返回 `List<PageSectionDefinition>`（已解析完整）

**步骤 3.3：创建 SectionDataResolver**
- `SectionDataResolver.cs`
  - 接收 section definition + data binding + 所有 pages 数据
  - 根据 `source` 过滤 pages
  - 应用 `filters`、`limit`、`sort`
  - 返回匹配的 pages 列表注入到 `section.items`

### 阶段 4：Scriban 渲染增强

**步骤 4.1：扩展 ScribanTemplateRenderer**
- 新增构造参数：
  - `ThemeComponentRegistry? registry`
  - `SectionSchemaValidator? validator`
  - `SectionDataResolver? dataResolver`
  - `string? componentValidation` (off/warn/strict)
- 新增 Scriban 全局函数 `render_section`：
  ```csharp
  // 用法1: {{ render_section section }}
  // 用法2: {{ render_section section section_data }}
  // section 包含: type, variant, props, items
  Func<object, string> renderSection = (sectionObj) => { ... }
  ```
  - 解析 section 对象（ScriptObject → PageSectionDefinition）
  - 查找 section definition（registry）
  - 判断 variant → 解析 template path
  - 校验 schema
  - 渲染 section template
- 增强现有 `render_component`：
  - 已有 `{{ comp.render name arg1 arg2 arg3 }}`
  - 保持兼容，同时确保 component registry 可用

**步骤 4.2：Section 模板渲染细节**
- Section 模板在 Scriban 中可访问：
  - `{{ section }}` — section 自身（type, variant）
  - `{{ section.props }}` — props 字典
  - `{{ section.items }}` — data resolver 注入的 items
  - `{{ site }}` — 继承父级 site 模型

**步骤 4.3：扩展 ScribanTemplateRendererAdapter**
- 透传新增参数（registry, validator, dataResolver, componentValidation）

**步骤 4.4：扩展 SiteEngine 集成**
- 在 `BuildVariantAsync` 中加载 `ThemeManifestV2`
- 创建 `ThemeComponentRegistry`
- 创建 `SectionDataResolver`
- 传递到 `ScribanTemplateRendererAdapter`

### 阶段 5：Theme Manifest 加载

**步骤 5.1：创建 ThemeManifestLoader**
- `ThemeManifestLoader.cs`
  - `Load(string themeRoot)` → `ThemeManifestV2?`
  - 使用 YamlDotNet StaticSerializer（AOT 友好）
  - 如果 `theme.yaml` 不存在，返回 null（旧主题兼容）
  - 路径解析：layouts/pages/sections/components → 全路径

**步骤 5.2：创建 YamlStaticContext**
- `ThemeManifestYamlStaticContext.cs`
  - 注册所有新类型到 YamlDotNet StaticContext

### 阶段 6：主题继承增强

**步骤 6.1：扩展继承链**
- 当前：只有 `layouts` 通过 `BuildPathUtils` 支持一级继承
- 扩充到：`pages`, `sections`, `components`, `partials`, `tokens`, `assets`
- 在 `ThemeComponentRegistry` 中实现查找链：
  ```
  当前主题 sections/hero/hero.html
  ↓ 不存在 → 父主题 sections/hero/hero.html
  ↓ 不存在 → 报错/warn
  ```

**步骤 6.2：Tokens 深度合并**
- 子主题 tokens 覆盖父主题对应 key
- 子主题没有的 key 从父主题继承
- `ThemeTokens.Merge(ThemeTokens parent)` 方法

### 阶段 7：Design Tokens 处理

**步骤 7.1：ThemeTokensProcessor**
- 读取 `tokens.yaml` → `ThemeTokens` 对象
- 生成 CSS：`dist/assets/css/theme-tokens.css`
- 支持通过 theme.yaml manifest 中的 `tokens` 路径配置
- 构建时自动输出到 `outputDir/assets/css/theme-tokens.css`

**步骤 7.2：Token CSS 变量命名规范**
- `colors.background` → `--color-background: #0f172a`
- `font.sans` → `--font-sans: "Inter, system-ui, sans-serif"`
- `radius.md` → `--radius-md: 16px`
- `spacing.section_y` → `--spacing-section-y: 96px`
- `layout.container` → `--layout-container: 1180px`

### 阶段 8：Theme Doctor CLI

**步骤 8.1：扩展 ThemeCommand**
- 新增子命令：
  - `doctor [theme-name]` — 主题诊断
  - `list-components [theme-name]` — 列出 sections/components
  - `export-catalog [theme-name]` — 导出 theme-catalog.json

**步骤 8.2：创建 ThemeDoctorCommand**
- `ThemeDoctorCommand.cs`
  - 检查 theme.yaml 存在性/schema 有效性
  - 检查 pageTemplates 指针文件存在
  - 检查 sections 模板/schema 存在
  - 检查 schema 必填字段
  - 检查 components 重复名称
  - 检查 variants 有效性
  - 检查 assets/static 存在
  - 检查父主题引用
  - 检查 tokens.yaml 可解析
  - 检测未使用组件
  - 检测模板硬编码业务文案
  - 输出诊断报告

**步骤 8.3：创建 ThemeCatalogWriter**
- `ThemeCatalogWriter.cs`
  - 从 ThemeManifestV2 + ThemeComponentRegistry 生成 JSON
  - 输出到 `.cache/theme-catalog.json` 或 `dist/theme-catalog.json`
  - AI Agent 可读格式

### 阶段 9：构建流程集成

**步骤 9.1：修改 BuildPathUtils**
- 新增 `ResolveThemeSectionsDir`、`ResolveThemeComponentsDir` 等
- 保持向后兼容

**步骤 9.2：修改 SiteEngine.BuildVariantAsync**
- 加载 ThemeManifestV2（如果 theme.yaml 存在）
- 实例化 ThemeComponentRegistry
- 实例化 SectionDataResolver
- 传递到 ScribanTemplateRendererAdapter
- 执行 ThemeTokensProcessor 生成 tokens CSS
- 如果是新主题，执行 PageComposer 集成（可选）

**步骤 9.3：确保旧主题兼容**
- 旧主题无 `theme.yaml` → 跳过所有新增逻辑
- 旧构建流程完全不变

### 阶段 10：示例主题

**步骤 10.1：创建 `examples/component-theme/`**
```
examples/component-theme/
├── site.yaml
├── content/
│   └── index.md
├── themes/
│   └── component-demo/
│       ├── theme.yaml          （完整 manifest）
│       ├── tokens.yaml         （design tokens）
│       ├── layouts/
│       │   └── base.html
│       ├── pages/
│       │   └── home.html
│       ├── sections/
│       │   ├── hero/
│       │   │   ├── hero.html
│       │   │   ├── schema.json
│       │   │   └── preview.json
│       │   └── card-grid/
│       │       ├── card-grid.html
│       │       └── schema.json
│       ├── components/
│       │   └── cards/
│       │       └── insight-card.html
│       ├── partials/
│       │   └── footer.html
│       └── assets/
│           └── css/main.css
```

**步骤 10.2：验证示例可构建**
- `dotnet run --project src/Bukit.Cli -- build examples/component-theme` 成功
- `bukit theme doctor examples/component-theme` 成功
- `bukit theme list-components examples/component-theme` 成功
- `bukit theme export-catalog examples/component-theme` 成功

### 阶段 11：测试

**步骤 11.1：ThemeManifestLoaderTests**
- 正常读取完整 `theme.yaml`
- 缺失 `theme.yaml` 时返回 null
- sections/components/pageTemplates 解析正确
- assets/layouts 解析正确
- extends 字段解析正确

**步骤 11.2：SectionSchemaValidatorTests**
- required 字段缺失检测
- string maxLength 超限检测
- url 类型非法
- unknown prop warn
- validation mode off/warn/strict 行为

**步骤 11.3：ThemeInheritanceTests**
- 当前主题覆盖父主题 section
- 当前主题继承父主题 component
- tokens 深度合并
- 不存在父主题时回退

**步骤 11.4：PageComposerTests**
- 从 JSON 解析 sections
- section variant 解析
- props 合并
- data binding 覆盖

**步骤 11.5：SectionDataResolverTests**
- collection 过滤
- limit 生效
- featured filter 生效
- sort 生效

**步骤 11.6：ThemeCatalogWriterTests**
- 导出 JSON 正确
- sections/components 信息完整

**步骤 11.7：ThemeTokensProcessorTests**
- tokens 解析正确
- CSS 变量生成正确
- 继承合并正确

**步骤 11.8：BuildCompatibilityTests**
- 旧主题无 theme.yaml 仍可构建
- starter 示例仍可构建
- component-theme 示例可构建

### 阶段 12：文档

**步骤 12.1：新增文档**
- `guide/dev/theme-component-system.md` — 主题组件化概述
- `guide/dev/theme-manifest.md` — theme.yaml 字段说明
- `guide/dev/page-composer.md` — Page Composer 使用方式
- `guide/dev/section-schema.md` — Section Schema 写法
- `guide/dev/theme-doctor.md` — Theme Doctor 使用方式
- `guide/dev/design-tokens.md` — Design Tokens 说明

**步骤 12.2：新增 Skill**
- `src/skills/theme-component-system/SKILL.md` — AI Agent 使用指引
- 说明 theme-catalog.json 的读取和使用方式

---

## 三、实施顺序

按优先级分阶段执行：

| 阶段 | 内容 | 依赖 | 预计影响 |
|------|------|------|----------|
| **Phase 1** | 新项目骨架 + 强类型模型 | 无 | 新增文件，无影响 |
| **Phase 2** | ThemeManifestLoader + YamlStaticContext | Phase 1 | 新增文件 |
| **Phase 3** | ThemeComponentRegistry + 继承链 | Phase 2 | 新增文件 |
| **Phase 4** | SectionSchemaValidator | Phase 1 | 新增文件 |
| **Phase 5** | ThemeTokens + ThemeTokensProcessor | Phase 1 | 新增文件 |
| **Phase 6** | PageComposer + SectionDataResolver | Phase 1 | 新增文件 |
| **Phase 7** | Scriban render_section/render_component | Phase 3,4,6 | 修改 Rendering |
| **Phase 8** | SiteEngine + BuildPathUtils 集成 | Phase 7 | 修改 Engine |
| **Phase 9** | ThemeConfig 扩展 (ComponentValidation) | Phase 4 | 修改 Config |
| **Phase 10** | Theme Doctor CLI | Phase 3,4,5 | 修改 CLI |
| **Phase 11** | ThemeCatalogWriter | Phase 3,5 | 新增文件 |
| **Phase 12** | 示例主题 examples/component-theme | Phase 8,10 | 新增文件 |
| **Phase 13** | 测试 | All phases | 新增测试 |
| **Phase 14** | 文档 + Skill | All phases | 新增文档 |
| **Phase 15** | 验证：构建+测试+旧主题兼容 | All phases | 最终验证 |

---

## 四、核心架构图

```
Theme Manifest (theme.yaml)
    ↓
ThemeComponentRegistry (sections/ + components/ 目录扫描)
    ↓
SiteEngine.BuildAsync()
    ├── ThemeTokensProcessor → dist/assets/css/theme-tokens.css
    ├── PageComposer → 解析 page.fields.sections JSON
    │       ↓
    │   SectionDataResolver → 根据 data binding 解析数据
    │       ↓
    │   SectionSchemaValidator → 校验 section props
    │       ↓
    └── ScribanTemplateRendererAdapter
            ↓
        ScribanTemplateRenderer
            ├── {{ render_section section }} → 渲染 section 模板
            ├── {{ render_component name data }} → 渲染 component 模板
            └── 现有 layout/component/shortcode 渲染
```

---

## 五、向后兼容策略

1. **无 theme.yaml 的主题**：完全走旧路径，零影响
2. **有 theme.yaml 的主题**：启用新能力，但旧模板中的 `layouts/` 引用仍然有效
3. **Config 新增字段**：`ComponentValidation` 默认 `"off"`，不产生校验
4. **Scriban 新增函数**：`render_section` 仅在调用时执行，不调用无影响
5. **构建输出不变**：新增能力只增加 `theme-tokens.css` 和可选的 `theme-catalog.json`

---

## 六、关键技术决策

| 决策 | 选择 | 原因 |
|------|------|------|
| 新模型位置 | 新项目 `Bukit.Theme` | 解耦，避免循环依赖 |
| YAML 解析 | YamlDotNet StaticContext | 已有模式，AOT 友好 |
| 模型类型 | C# `record` | 不可变，AOT 友好 |
| Section 数据注入 | `section.items` | Scriban 原生支持遍历 |
| Theme Catalog 格式 | JSON | AI Agent 最易解析 |
| CLI 扩展方式 | ThemeCommand 新增子命令 | 已有模式 |
| Tokens CSS 路径 | `dist/assets/css/theme-tokens.css` | 与现有 assets 结构一致 |
| Validation 策略 | `off/warn/strict` 三态 | 渐进式严格 |
