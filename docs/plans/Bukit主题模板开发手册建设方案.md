# Bukit 主题模板开发手册建设方案

> 状态：需求梳理与实施设计稿  
> 基线仓库：`ALi365-SDN-BHD/Bukit`  
> 基线分支：`main`  
> 基线提交：`62befb40b1b684104c4b6f2d6fec9548c430ad6d`（`analytics`，2026-07-14）  
> 适用范围：Bukit Core 1.0 当前公开主题能力  
> 目标读者：第一次开发 Bukit 主题的人、熟悉静态站点生成器的前端开发者、维护 Bukit Core 的开发者、自动化编码 Agent

---

## 1. 执行结论

Bukit 主题模板开发手册不应被实现为一篇不断膨胀的 Markdown，而应建设为一个“可执行的主题开发知识系统”：

1. **代码与测试是唯一事实源**：手册不得从历史文档复制能力描述后再猜测运行时行为。
2. **一份公共契约，多种消费形式**：人类教程、字段参考、Agent Skill、示例主题和校验脚本共用同一组契约。
3. **先修契约矛盾，再写稳定文档**：当前 `main` 中存在若干“校验器接受、加载器拒绝”“配置声明存在、运行时未消费”等不一致，必须先解决或明确标为不可用。
4. **教程围绕可运行的 Golden Theme 编写**：所有章节都必须能回到一套最小但完整的标准主题，并由 CI 实际构建。
5. **Agent 文档不是人类手册的复制品**：Agent 需要的是确定性流程、输入输出约束、停止条件、校验命令和错误恢复策略。
6. **稳定 Core 与 Labs/规划能力必须完全隔离**：手册不能把历史主题命令、远程主题、市场、可视化生成器或 BukitJalil 的愿景写成当前 Core 能力。

最终交付应包含：

- 面向人的分层主题手册；
- 面向 Agent 的原子化主题开发 Skill；
- `theme.yaml`、模板上下文和模板函数的机器可读契约；
- 一套从最小主题到高级主题的可运行示例；
- 文档—源码—测试漂移检查；
- 主题兼容性、版本和发布规范。

---

## 2. 调研基线与事实优先级

### 2.1 本次分析采用的事实优先级

按仓库自身文档治理要求，主题手册的依据顺序应为：

1. 当前 `src/Bukit-Core` 实现；
2. 当前测试；
3. 当前检查脚本与质量门；
4. 当前 `README`、`guide/user`、`guide/dev`；
5. 历史提交和旧文档，仅用于解释演进，不作为公开契约。

禁止将以下目录当成当前事实源：

- `guide-0.1/`
- `guide-0.2/`
- `scripts-0.1/`
- `scripts-0.2/`

### 2.2 本次分析的主要源码锚点

| 主题 | 事实源 |
|---|---|
| 主题路径与覆盖顺序 | `src/Bukit-Core/Bukit.Engine/ThemePathResolver.cs`、`src/Bukit-Core/Bukit.Rendering/Scriban/FileTemplateLoader.cs` |
| 主题启动与继承 | `src/Bukit-Core/Bukit.Engine/ThemeBootstrapper.cs` |
| `theme.yaml` 加载 | `src/Bukit-Core/Bukit.Theme/ThemeManifestLoader.cs` |
| `theme.yaml` 严格校验 | `src/Bukit-Core/Bukit.Config/ThemeManifestStrictValidator*.cs` |
| 模板角色选择 | `src/Bukit-Core/Bukit.Engine/ThemeTemplateResolver.cs` |
| Scriban 渲染 | `src/Bukit-Core/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs` |
| 模板上下文绑定 | `src/Bukit-Core/Bukit.Rendering/Scriban/ScribanModelBinder.cs` |
| 模板帮助函数 | `src/Bukit-Core/Bukit.Rendering/Scriban/TemplateContextBuilder.cs` |
| 组件与 Section | `src/Bukit-Core/Bukit.Theme/ThemeComponentRegistry.cs`、`SectionSchemaValidator.cs` |
| Token | `src/Bukit-Core/Bukit.Theme/ThemeTokens*.cs` |
| 资源构建 | `src/Bukit-Core/Bukit.Engine/AssetPipeline.cs`、`ScssCompiler.cs`、`ImageOptimizer.cs` |
| CLI | `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs` |
| 主题测试 | `tests/Bukit.Theme.Tests/`、`tests/Bukit.Engine.Tests/` |

### 2.3 当前产品边界

当前主题手册必须说明：

- Bukit Core 是静态站点运行时和 CLI；
- BukitJalil 是上层 AI/控制台愿景，不是当前主题运行时的组成部分；
- Core 当前公开稳定命令包含 `build`、`doctor`、`config`、`preview`、`dev`、`clean`、`version`、`completion`、`seo`、`geo`、`publish`、`deploy`；
- 当前 Core 不应在稳定手册中宣称存在 `theme init`、`theme install`、`theme doctor`、主题市场、远程主题安装等命令；
- 远程主题、市场和控制台工作流应放入单独的 Roadmap/Labs 文档，并显著标注“非当前 Core 契约”。

---

## 3. 当前主题系统的正确心智模型

### 3.1 主题开发不是单纯写 HTML

Bukit 主题由五层契约共同组成：

```text
站点配置 site.yaml
        ↓ 选择主题、覆盖运行参数
主题清单 theme.yaml
        ↓ 声明模板角色、能力、组件、Section、继承、Token
内容与路由模型
        ↓ 生成 page/site/items/pagination/... 模板上下文
Scriban 模板与组件
        ↓ 组合 HTML、调用帮助函数
资源与质量流水线
        ↓ assets/static/tokens/SEO/GEO/publish audit
```

### 3.2 两个配置入口必须严格区分

#### `site.yaml` 的 `theme` 节点

它是**站点使用主题时的运行配置与覆盖层**，当前包含：

- `name`
- `layouts`
- `assets`
- `static`
- `staticTemplate`
- `params`
- `shortcodes`
- `components`
- `scss`
- `images`
- `componentValidation`

#### 主题目录中的 `theme.yaml`

它是**主题包自身的声明清单**，当前根字段包括：

- `name`
- `display_name`
- `version`
- `engine`
- `min_engine_version`
- `description`
- `extends`
- `capabilities`
- `layouts`
- `templates`
- `page_templates`
- `sections`
- `components`
- `assets`
- `tokens`

手册必须反复强调：这两者名称相似但职责不同，字段不能互相搬用。

### 3.3 路径和覆盖顺序

在 `theme.name` 被设置时，默认主题根为：

```text
<site-root>/themes/<theme-name>/
```

模板搜索优先级应以当前运行时为准：

```text
站点级 layouts 覆盖目录
    > 子主题 layouts
    > 父主题 layouts
```

静态文件和资源的覆盖方式：

```text
先复制父主题，再复制子主题
```

因此同名文件由子主题覆盖父主题。

需要单独解释的边界：

- `site.yaml` 中自定义 `theme.layouts/assets/static` 是从站点根解析的运行路径；
- `theme.yaml` 中模板路径是相对主题 `layouts/` 的路径；
- `theme.yaml.assets` 中资源路径相对主题根做安全校验；
- 模板 include/layout 路径不得逃出允许的 layouts 根；
- 主题名不得包含路径分隔符、`..`、控制字符或 Windows 设备保留名。

---

## 4. 现状差距与风险清单

### 4.1 文档结构差距

当前 `guide/user/08-themes-templates.md` 只承担概览角色，缺少完整开发闭环：

- 没有从零可运行主题；
- 没有完整 `theme.yaml` 字段说明；
- 没有模板选择算法；
- 没有模板对象的精确可用性矩阵；
- 没有组件、Section、Token、SCSS、图片、继承的完整说明；
- 没有 Agent 确定性工作流；
- 没有质量门、错误码、负例和兼容策略；
- 现有 Skill 篇幅过短，不能独立驱动 Agent 开发主题。

### 4.2 P0：必须先解决或冻结的公开契约矛盾

| 编号 | 问题 | 当前表现 | 风险 | 建议 |
|---|---|---|---|---|
| C-001 | `sections.plugin` 口径不一致 | 严格校验器接受、模型和启动器读取，但 manifest loader 明确拒绝 | 用户配置通过前置校验后，运行时加载失败 | 统一 loader/validator/model/bootstrap；在修复前不得写入稳定手册 |
| C-002 | `sections.data.filters` 类型不一致 | 严格校验接受 mapping 或 sequence；loader 只解析 mapping | 数组配置可能校验通过但运行时丢失 | 选择并固定一种公共类型，或完善联合类型解析与测试 |
| C-003 | `tokens` 自定义路径未形成闭环 | manifest 接受 `tokens` 字符串；资产流水线始终读取默认 `tokens.yaml` | 文档若宣称自定义路径会误导 | 让资产流水线消费 manifest 值，或移除/标记字段 |
| C-004 | `version` 的“semver”含义不准确 | 错误信息称 semver，实际用 `System.Version.TryParse` | `1.0.0-beta.1` 等标准 SemVer 可能失败 | 明确定义版本语法并统一实现、Schema、文档 |
| C-005 | `min_engine_version` 缺少可验证语义 | 字段被解析和校验为字符串，但未确认构建时阻断逻辑 | 主题兼容性声明可能只是装饰 | 实现版本门禁或在能力矩阵中标为 metadata-only |
| C-006 | 模板 linter 与实际 binder 漂移 | linter 字段表与 `ScribanModelBinder` 不完全一致 | `doctor` 可能误报或漏报 | 从同一声明生成 binder 与 linter 字段目录，加入契约测试 |
| C-007 | `page_templates` 公开用途未证实 | loader/validator 有字段，但当前运行时主选择器使用 `templates` | 手册可能描述不存在的路由行为 | 完成调用链审计；无消费方则标记为保留/移除 |
| C-008 | `required_fields` 执行语义未证实 | 字段可解析和校验，但未确认运行时是否强制 | 用户以为字段会阻断构建 | 找出消费点并测试；否则不得宣称强制能力 |
| C-009 | `theme.yaml.assets.css/js` 执行语义未证实 | 可解析、校验，但未确认自动注入 HTML 或参与 copy 选择 | 容易与 `assets/` 目录复制混淆 | 明确它是 metadata、自动注入还是构建清单 |
| C-010 | `layouts` manifest 映射语义不充分 | 可解析/校验，但模板选择主要依赖 `templates` 和文件路径 | 名称与“布局指令”容易混淆 | 明确用途并补调用链测试，必要时重命名或降级为元数据 |

### 4.3 P0/P1：资源流水线风险

| 编号 | 问题 | 当前表现 | 建议 |
|---|---|---|---|
| A-001 | SCSS 会修改主题源目录 | 编译成功后删除原 `.scss`，生成同目录 `.css` | 构建必须改为非破坏性；输入目录只读，输出写入临时/输出目录 |
| A-002 | SCSS 配置字段未完全生效 | `entryPoint`、`outputDir` 存在，但编译器遍历全部 SCSS 并输出相邻 CSS | 在稳定文档前统一配置与行为 |
| A-003 | 图片 `sizes` 未用于生成尺寸 | 优化器只做格式转换，不创建 `-480w` 等尺寸 | 不得宣称完整响应式图片管线；实现尺寸生成或精简配置 |
| A-004 | `image.srcset` 与优化器契约不一致 | helper 生成 `?w=` URL，静态输出并无对应 resize 服务保证 | 明确仅为 URL 字符串辅助，或与静态变体生成统一 |
| A-005 | Token 输出路径可能与主题资产冲突 | Token 生成与 asset copy 并行写 `assets/css/theme-tokens.css` | 把路径设为保留路径并串行化/检测冲突 |
| A-006 | 外部工具依赖与失败模式不透明 | Sass、cwebp/ImageMagick 不存在时只警告并跳过 | 手册必须列前置依赖、日志、CI 策略和预构建方案 |

### 4.4 生态差距

- `bukit-themes` 当前没有可作为规范基准的完整官方主题；
- 核心仓库需要先提供一个受测试保护的 Golden Theme；
- 后续可将同一主题发布到主题仓库，但不能让两个副本独立演进；
- 示例站点、主题包和文档代码块必须由同一源生成或校验。

---

## 5. 需求目标与非目标

### 5.1 总目标

让一名没有阅读 Bukit 源码的开发者，或一个只读取手册和项目文件的 Agent，能够：

1. 判断当前项目使用哪个主题、主题根在哪里；
2. 创建最小有效主题；
3. 正确配置 `site.yaml` 与 `theme.yaml`；
4. 为首页、内容页、列表页、分类页和分页页选择正确模板；
5. 安全使用所有公开模板对象和帮助函数；
6. 开发并复用 layout、partial、component、section；
7. 处理 Token、CSS、JS、图片、静态文件和继承；
8. 正确处理 SEO/GEO、多语言、可访问性和安全边界；
9. 通过标准命令完成开发、诊断、构建和发布审计；
10. 遇到错误时能根据错误码、失败阶段和示例恢复；
11. 不误用 Labs、历史命令或尚未实现的字段；
12. 产出可维护、可升级、可发布的主题包。

### 5.2 非目标

本轮稳定手册不负责：

- 教用户修改 `src/Bukit-Core/`；
- 把 BukitJalil 对话式建站流程写成 Core 主题 API；
- 承诺远程主题、市场、在线安装或商业发布平台；
- 教授所有 HTML/CSS/JavaScript 基础；
- 替代 Scriban 官方语言参考；
- 记录内部扩展点，例如仅供 Core 内部使用的模板上下文 contributor；
- 为尚未通过契约审计的字段编造行为。

---

## 6. 受众模型与阅读路径

### 6.1 受众 A：第一次写主题的人

需要：

- 一套完整可复制的目录；
- 每一步的输入、输出和验证命令；
- 少用术语，先说明“为什么”；
- 常见错误与修复；
- 从最小主题逐渐增加高级功能。

推荐路径：

```text
概念 → 最小主题 → 模板变量 → 列表/分页 → SEO → 资源 → 质量门
```

### 6.2 受众 B：有 SSG/前端经验的开发者

需要：

- 快速映射 Hugo/Jekyll/Eleventy 等概念到 Bukit；
- 完整 manifest 和上下文参考；
- 覆盖优先级、模板匹配、继承、安全和构建语义；
- 迁移指南和差异清单。

推荐路径：

```text
心智模型 → 配置参考 → 模板选择算法 → 上下文矩阵 → 高级能力
```

### 6.3 受众 C：Agent

需要：

- 可枚举的能力状态；
- 原子步骤与停止条件；
- 不允许推断的字段；
- 读取顺序、修改范围、命令顺序；
- 每步可验证的成功标准；
- 机器可读 Schema、函数目录和上下文目录。

推荐路径：

```text
能力矩阵 → 项目探测 → 契约选择 → 最小改动 → 针对性校验 → 报告
```

### 6.4 受众 D：Core 维护者

需要：

- 每个公开字段和模板对象的源码锚点；
- 兼容性规则；
- 变更时必须更新的文档和测试；
- 自动漂移检测；
- 主题版本和引擎版本治理。

---

## 7. 需求拆分

以下需求应建立可追踪编号，文档、测试和任务均引用编号。

### R-000：版本与能力边界

**必须说明**：

- 文档适配的 Core 版本/提交；
- 稳定、实验、内部、规划、移除五种状态；
- Core 与 Labs/BukitJalil 的边界；
- 不兼容变更的迁移入口。

**验收**：每个高级能力都带状态标签，不出现“代码未实现但文档写成可用”的情况。

### R-001：主题开发心智模型

**必须说明**：

- `site.yaml`、`theme.yaml`、内容、路由、模板上下文、Scriban、资源流水线之间的关系；
- 主题为何不是“选一套 HTML”；
- 构建阶段和错误可能发生在哪一层。

**验收**：读者能在不看源码的情况下解释一次页面如何从内容变成 `dist/*.html`。

### R-002：项目目录与路径解析

**必须说明**：

- 推荐树；
- 主题根；
- 自定义目录；
- 用户覆盖目录；
- 父子主题；
- assets/static 输出位置；
- 相对路径基准；
- Windows/Linux 路径安全。

**验收**：给出不少于 8 个路径解析例子和一张覆盖优先级表。

### R-003：`site.yaml` 主题配置

每个字段都要记录：

- 完整路径；
- 类型；
- 是否必填；
- 默认值；
- 允许值；
- 相对路径基准；
- 运行时消费者；
- 是否影响缓存/增量构建；
- 错误码；
- 正例与负例。

**验收**：覆盖 `ThemeConfig` 当前全部公开字段，字段覆盖率 100%。

### R-004：`theme.yaml` 完整规范

**必须包含**：

- 最小合法清单；
- 完整示例；
- 所有根字段；
- 所有嵌套字段；
- 未知字段失败语义；
- 字段大小写行为；
- 路径限制；
- 版本约束；
- 能力声明语义；
- 扩展/保留字段策略。

**验收**：机器 Schema、人工字段表、loader 和 strict validator 由契约测试证明一致。

### R-005：模板角色与选择算法

**必须说明**：

- 唯一固定角色 `home`；
- 首页默认回退 `pages/index.html`；
- `home.required` 的强制规则；
- `accepts.type`、`collection`、`kind`；
- 空条件作为通配条件的含义；
- 显式 route/collection template 与 manifest 匹配的优先级；
- 无匹配时的失败；
- 多个规则同时匹配时的确定性和冲突检查。

**验收**：为首页、普通页、文章页、集合列表、分类索引、分类 term、分页、过滤列表分别给出决策表和测试。

### R-006：Scriban 最小语言集

**必须说明**：

- 输出、条件、循环、变量赋值；
- include；
- layout 指令；
- 过滤器管道；
- null/空字符串判断；
- HTML 转义；
- 原始 HTML 的安全边界；
- 常见语法错误与行号定位。

**验收**：所有语法例子由真实渲染测试执行，而不是只做文本检查。

### R-007：模板上下文参考

需要以 `ScribanModelBinder` 为事实源，记录：

- 顶层对象；
- 适用模板类型；
- 字段类型；
- 是否总存在；
- 何时为 null/空数组；
- 别名；
- 推荐规范名称；
- 示例值；
- 来源字段；
- 安全/转义要求。

至少覆盖：

- `site`
- `page`
- 根级 `seo`
- `pages`
- `items`
- `pagination`
- `collection`
- `taxonomy`
- `filter`
- `page.fields.*.type`
- `page.fields.*.value`
- `page.content_model` / `page.content_record`
- `page.entities`
- `page.provenance`
- `page.trust`
- `page.representations`
- `site.params`
- `site.modules`
- `site.data`
- `site.data_index`
- 明确记录 Analytics 不进入模板上下文，模板中不存在 site.analytics

**验收**：模板上下文目录由测试与 binder 对比，新增/删除字段时 CI 必须失败并提示更新契约。

### R-008：函数与过滤器目录

区分：

1. Scriban 内置能力；
2. Bukit 注册的公开函数；
3. 内部扩展点；
4. 已移除/不可用函数。

当前需要审计并记录：

- `comp.render`
- `render_section`
- `image.srcset`
- `image.img`
- `util.format_date`
- `util.truncate`
- `util.titleize`
- `util.slugify`
- shortcode 调用

每个函数记录：签名、参数顺序、默认值、返回类型、失败方式、转义行为、示例、负例。

### R-009：Layout、include 与 partial

**必须说明**：

- layout 指令必须位于何处；
- 子模板输出如何进入 `content`；
- 最大嵌套深度 10；
- 循环 layout 如何失败；
- include 的查找顺序；
- 覆盖目录、子主题、父主题的优先级；
- include 越界保护；
- 何时用 layout、partial、component、section。

### R-010：Component

**必须说明**：

- `site.yaml.theme.components` 与 `theme.yaml.components` 的差异；
- 注册方式；
- 模板路径；
- props 类型声明当前是否只用于描述或会执行验证；
- `comp.render` 参数形态；
- 父级上下文可见性；
- 组件命名与覆盖；
- 防止组件承担路由/数据查询职责的设计原则。

### R-011：Section、Variant、Schema 与数据绑定

**必须说明**：

- Section 与 Component 的差异；
- `type`、`variant`、`props`、`items` 的运行模型；
- `theme.yaml.sections` 全字段；
- variant 回退；
- `componentValidation: off|warn|strict`；
- Bukit Section Schema 不是完整 JSON Schema；
- 支持的类型：`string`、`number`、`boolean`、`url`、`image`；
- `required`、`maxLength`、未知 prop；
- schema 文件缺失或 JSON 错误的处理；
- `data.source/mode/limit/sort/filters` 的实际语义；
- Agent 如何生成可校验的 Section。

**前置条件**：先解决 C-001、C-002，并完成当前 PageComposer/SectionDataResolver 调用链审计。

### R-012：内容字段、模块和数据源

**必须说明**：

- `page.fields.<key>.value`，而不是直接假设字段是标量；
- `site.modules.<name>`；
- `site.data.<source>`；
- `site.data_index.<source>.<scope>.<key>`；
- 不同源和集合对模板选择的影响；
- 数据为空时的防御式渲染；
- 不允许模板访问的内部路由元数据。

### R-013：列表、分页、分类、过滤和搜索

**必须说明**：

- `pages` 与 `items` 的关系；
- `pagination` 全字段；
- `collection.key`；
- `taxonomy` 全字段；
- `filter` 全字段；
- 列表页 `page` 为什么仍然存在；
- 派生页面的根级别 alias；
- 空列表；
- canonical/prev/next；
- 分页标题；
- 列表内容模式。

### R-014：多语言

**必须说明**：

- `site.language`；
- 多语言构建时模板可见字段；
- `site.base_url`；
- URL 与语言前缀；
- hreflang；
- 模板中的硬编码文本风险；
- Section props 的国际化策略；
- RTL 和语言属性；
- 主题 capability 中 `i18n` 的真实语义。

### R-015：SEO、GEO 与 `<head>` 所有权

**必须说明**：

- `site.seo.renderMode: inject|theme|off`；
- `inject` 时 Core 管理哪些标签；
- `theme` 时主题必须输出什么；
- `off` 的实际含义；
- `page.seo.title` 与 `page.seo.document_title`；
- title 的兼容回退；
- canonical、robots、OG、Twitter、article、alternates、JSON-LD；
- HTML 转义；
- 缺少标准 `<head>` 的诊断；
- SEO/GEO/publish audit 命令；
- 主题不能重复注入受 Core 管理的标签。

### R-016：资源、静态文件、Token、SCSS 和图片

**必须说明**：

- `assets/` 输出到 `/assets/`；
- `static/` 输出到站点根；
- 父子主题复制顺序；
- dotfile 和 symlink 配置；
- Token 五组字段与 CSS 变量命名；
- Token 固定输出路径；
- 主题 CSS 如何显式引用 Token CSS；
- 外部工具依赖；
- SCSS/图片当前能力限制；
- 不存在 resize 服务时不要把 `?w=` 当成静态尺寸变体；
- CSP、SRI、外链资源和 MIME 安全原则。

**稳定发布前置条件**：先解决 A-001 至 A-005，或者把不一致能力明确标记为实验。

### R-017：主题继承和覆盖

**必须说明**：

- `extends` 仅接受安全主题名；
- 父主题必须存在并包含 `theme.yaml`；
- 当前支持几级继承；
- layout、page template、section、component、asset、static、token 的覆盖矩阵；
- 子主题只改必要文件的实践；
- 父主题升级的兼容风险；
- 循环继承检测；
- 如何测试覆盖是否生效。

### R-018：开发、诊断与故障排查

标准循环：

```bash
bukit config check --config site.yaml
bukit doctor --config site.yaml
bukit dev --config site.yaml
bukit build --config site.yaml --clean
bukit seo audit --dir dist
bukit geo audit --dir dist
bukit publish audit --dir dist
```

需要按阶段组织错误：

1. YAML 解析失败；
2. 配置严格校验失败；
3. 主题启动失败；
4. 模板选择失败；
5. Scriban 解析失败；
6. Scriban 运行时失败；
7. 资源处理失败/跳过；
8. 输出审计失败。

每种错误提供：症状、最小复现、根因、修复、验证。

### R-019：测试、质量、安全和性能

**必须说明**：

- 配置检查；
- doctor；
- clean build；
- HTML 结构检查；
- SEO/GEO/publish audit；
- 可访问性；
- 响应式；
- 跨浏览器；
- 主题继承回归；
- 路径遍历；
- XSS 与转义；
- 外链资源；
- 构建可重复性；
- 增量构建；
- 构建指标；
- 不修改源目录的保证。

### R-020：打包、版本、发布和迁移

**必须定义**：

- 主题包根结构；
- 必须文件；
- 可选文件；
- 版本语义；
- 引擎最低版本；
- changelog；
- license；
- preview 图片；
- 能力声明；
- 发布前验收；
- 父主题依赖；
- 不兼容变更迁移；
- 主题仓库接入规则。

### R-021：Agent 开发协议

Agent 必须：

1. 先读取 `AGENTS.md`；
2. 识别 Core/Labs 边界；
3. 读取 `site.yaml` 并解析主题根；
4. 读取当前 `theme.yaml`；
5. 对照机器能力目录，不自行发明字段；
6. 先运行最小基线检查；
7. 每次只实现一个小范围能力；
8. 每个小步骤后运行最小相关校验；
9. 失败即停止扩展，修复当前步骤；
10. 不在站点主题任务中修改 `src/Bukit-Core/`；
11. 报告修改文件、使用契约、命令结果、遗留假设；
12. 对未验证能力明确写“未使用”，而不是猜测。

---

## 8. 推荐文档信息架构

保留当前 `guide/user`、`guide/dev`、`guide/skills` 的治理边界，不另建一个脱离现有体系的平行文档站。

```text
guide/
├── user/
│   ├── 08-themes-templates.md              # 主题手册入口与阅读地图
│   └── themes/
│       ├── 01-minimal-theme.md
│       ├── 02-mental-model.md
│       ├── 03-directory-paths-precedence.md
│       ├── 04-site-yaml-theme-config.md
│       ├── 05-theme-yaml-manifest.md
│       ├── 06-scriban-basics.md
│       ├── 07-template-context.md
│       ├── 08-template-selection.md
│       ├── 09-layouts-includes-partials.md
│       ├── 10-components.md
│       ├── 11-sections-variants-schema.md
│       ├── 12-data-modules-indexes.md
│       ├── 13-lists-pagination-taxonomy.md
│       ├── 14-i18n.md
│       ├── 15-seo-geo-head.md
│       ├── 16-assets-static-tokens.md
│       ├── 17-scss-images.md
│       ├── 18-theme-inheritance.md
│       ├── 19-development-debugging.md
│       ├── 20-testing-security-performance.md
│       ├── 21-packaging-versioning.md
│       ├── 22-recipes.md
│       └── reference/
│           ├── theme-yaml-fields.md
│           ├── site-yaml-theme-fields.md
│           ├── template-context.md
│           ├── functions-and-filters.md
│           ├── template-selection-matrix.md
│           ├── precedence-matrix.md
│           ├── diagnostics.md
│           └── capability-status.md
├── dev/
│   ├── theme-contract-governance.md
│   ├── theme-rendering-pipeline.md
│   ├── theme-compatibility-policy.md
│   └── theme-doc-generation.md
└── skills/
    └── bukit-theme-development/
        ├── SKILL.md
        ├── capability-matrix.json
        ├── workflow-minimal-theme.md
        ├── workflow-add-template.md
        ├── workflow-add-component.md
        ├── workflow-add-section.md
        ├── workflow-debug-theme.md
        └── workflow-release-theme.md
```

配套新增：

```text
schemas/
├── theme-manifest.schema.json
├── theme-section-schema.schema.json
├── template-context.v1.json
├── template-functions.v1.json
└── theme-capabilities.v1.json

examples/
├── theme-starter/
├── theme-blog/
├── theme-componentized/
├── theme-child/
└── theme-i18n-seo/

scripts/checks/
├── check-theme-contract-drift.*
├── check-theme-guide-examples.*
├── check-template-context-docs.*
└── check-theme-golden-builds.*
```

---

## 9. 每一章的标准写法

为人和 Agent 共读，所有章节使用统一模板。

### 9.1 章节头部元数据

```yaml
id: theme-template-selection
status: stable
applies_to: Bukit Core 1.0
prerequisites:
  - minimal-theme
source_anchors:
  - src/Bukit-Core/Bukit.Engine/ThemeTemplateResolver.cs
verified_by:
  - tests/Bukit.Engine.Tests/ThemeTemplateResolverTests.cs
inputs:
  - theme.yaml
  - content metadata
outputs:
  - resolved template path
```

### 9.2 正文章节顺序

每章固定包含：

1. **这一章解决什么问题**；
2. **一句话心智模型**；
3. **适用/不适用场景**；
4. **前置条件**；
5. **完整可运行示例**；
6. **逐行解释**；
7. **引擎实际执行顺序**；
8. **字段/对象/函数参考表**；
9. **预期输出**；
10. **验证命令**；
11. **常见错误**；
12. **错误示例与修复**；
13. **安全与性能注意事项**；
14. **Agent 执行步骤**；
15. **源码和测试锚点**；
16. **相关章节**。

### 9.3 表格字段规范

字段参考至少包含：

| 列 | 含义 |
|---|---|
| Path | 完整字段路径 |
| Type | 精确类型 |
| Required | 是否必填 |
| Default | 默认值 |
| Allowed | 枚举、格式或范围 |
| Available when | 何时存在/生效 |
| Consumer | 哪个运行时模块读取 |
| Output effect | 对输出的影响 |
| Failure | 错误/警告/静默忽略 |
| Diagnostic | 错误码或日志事件 |
| Example | 最小正例 |
| Status | stable/experimental/internal/planned/removed |

### 9.4 语言规范

- 使用“必须 / 应该 / 可以 / 禁止”表达规范强度；
- 第一次出现术语时给出通俗解释；
- 代码标识保留英文；
- 不使用“通常”“大概”“应该能”等模糊措辞描述运行时；
- 示例不得用 `...` 省略关键结构；
- 示例必须能复制运行；
- 将“当前行为”和“推荐实践”分开；
- 将“稳定能力”和“未来建议”分开；
- 混合命名 alias 只列为兼容项，正文统一使用推荐名称；
- 任何推断必须标为“尚未验证”，并附待办编号。

---

## 10. Golden Theme 设计

手册的所有主线章节应基于一套受 CI 保护的主题，而不是每章重新发明片段。

### 10.1 目录

```text
examples/theme-starter/
├── site.yaml
├── content/
│   ├── index.md
│   ├── about.md
│   └── posts/
│       ├── first.md
│       └── second.md
└── themes/
    └── starter/
        ├── theme.yaml
        ├── tokens.yaml
        ├── layouts/
        │   ├── base.html
        │   ├── pages/
        │   │   ├── index.html
        │   │   ├── page.html
        │   │   ├── post.html
        │   │   ├── list.html
        │   │   ├── taxonomy-index.html
        │   │   └── taxonomy-term.html
        │   ├── partials/
        │   │   ├── head.html
        │   │   ├── header.html
        │   │   └── footer.html
        │   ├── components/
        │   │   └── card.html
        │   └── sections/
        │       └── hero.html
        ├── schemas/
        │   └── hero.schema.json
        ├── assets/
        │   ├── css/site.css
        │   ├── js/site.js
        │   └── images/
        └── static/
            └── favicon.svg
```

### 10.2 主题应逐步演进

为降低认知负担，Golden Theme 应有受测试的里程碑：

1. `step-01-minimal`：首页 + 基础 layout；
2. `step-02-content`：普通内容模板；
3. `step-03-list`：集合、列表和分页；
4. `step-04-taxonomy`：分类；
5. `step-05-component`：组件；
6. `step-06-section`：Section + schema；
7. `step-07-data`：data/modules/data_index；
8. `step-08-i18n-seo`：多语言与 head；
9. `step-09-inheritance`：父子主题；
10. `step-10-release`：完整质量门。

可以用 Git tag、目录快照或文档 patch 表达步骤，但不能维护十套完全独立且会漂移的主题。更推荐一套最终主题 + 每章可重放的 patch/fixture。

### 10.3 Golden Theme 验收

每次提交必须通过：

```bash
bukit config check --config examples/theme-starter/site.yaml
bukit doctor --config examples/theme-starter/site.yaml
bukit build --config examples/theme-starter/site.yaml --clean
bukit seo audit --dir examples/theme-starter/dist --strict
bukit geo audit --dir examples/theme-starter/dist
bukit publish audit --dir examples/theme-starter/dist --strict
```

并断言：

- 关键路由存在；
- 每页只有一个标准 `<title>`；
- canonical 正确；
- asset/static/token 文件存在；
- 父子覆盖符合矩阵；
- HTML 中没有未解析 Scriban；
- 无主题源文件被构建修改；
- Windows/Linux 均可构建；
- 增量构建和 clean build 输出等价。

---

## 11. 模板上下文的机器契约

### 11.1 为什么必须机器化

现在上下文事实分散在 C# model、binder、linter、指南和示例中。人工维护五份列表必然漂移。

建议建立 `template-context.v1.json`，至少包含：

```json
{
  "version": "1",
  "roots": {
    "page": {
      "availableIn": ["content", "list", "derived"],
      "fields": {
        "title": { "type": "string", "nullable": false },
        "fields": {
          "type": "map",
          "valueShape": { "type": "string", "value": "any" }
        },
        "seo": { "type": "seo", "nullable": true }
      }
    }
  },
  "aliases": [
    { "alias": "site.base_path", "canonical": "site.base_url" },
    { "alias": "page.tableOfContents", "canonical": "page.table_of_contents" }
  ]
}
```

### 11.2 生成方式

推荐优先级：

1. 由一个强类型公共描述对象同时驱动 binder、linter、文档生成；
2. 若重构成本暂时过高，则由契约测试反射/渲染已知模型，生成快照并与 JSON 比较；
3. 禁止手工维护 linter 字段表和文档字段表而没有自动比对。

### 11.3 兼容 alias 策略

- 每个字段只能有一个 canonical 名称；
- alias 记录引入版本、弃用版本和删除计划；
- 新文档和 Agent 始终生成 canonical 名称；
- linter 对 alias 给提示而不是错误；
- 重大版本才能删除 alias。

建议 canonical 统一使用 snake_case，保留当前已存在的 camelCase alias 作为兼容项，具体迁移需单独 RFC 决定。

---

## 12. Agent 主题开发 Skill 设计

### 12.1 Skill 不应重复整本手册

`SKILL.md` 只承担：

- 何时触发；
- 必须读取哪些契约；
- 如何判断任务类型；
- 允许修改哪些路径；
- 标准执行顺序；
- 校验和停止条件；
- 最终报告格式。

字段说明、对象参考和函数签名应读取机器契约或人类参考页。

### 12.2 Agent 标准流程

```text
阶段 0：边界确认
  - 读取 AGENTS.md
  - 判断这是站点主题任务还是 Core 缺陷任务
  - 不跨边界修改 Core

阶段 1：项目探测
  - 定位 config
  - 解析 site root
  - 解析 theme.name、layouts、assets、static
  - 读取 theme.yaml 和父主题
  - 读取 capability status

阶段 2：基线验证
  - config check
  - doctor
  - 记录已有失败，不把旧失败误归因于新改动

阶段 3：规划最小改动
  - 明确目标路由/模板角色
  - 明确需要的上下文对象
  - 选择 layout/component/section
  - 列出将修改的精确文件

阶段 4：单步实现
  - 每次只增加一个模板角色或组件
  - 不使用未声明字段
  - 不复制业务硬编码到通用主题

阶段 5：针对性验证
  - config check
  - doctor
  - build
  - 检查目标 HTML/asset/report

阶段 6：完整验收
  - clean build
  - seo/geo/publish audit
  - 检查源目录没有构建副作用

阶段 7：交付报告
  - 修改路径
  - 使用的契约版本
  - 命令与结果
  - 输出路由
  - 已知限制
  - 未使用的实验能力
```

### 12.3 Agent 停止条件

Agent 遇到以下情况必须停止扩展范围：

- 文档字段与 Schema 不一致；
- config check 与 loader 行为矛盾；
- 需要修改 `src/Bukit-Core/` 才能完成站点任务；
- 模板上下文字段不在机器目录中；
- 校验命令失败且原因未解决；
- 需要依赖未安装的外部工具，但用户未允许改变环境；
- 主题继承链缺失或不可信；
- 输出审计出现新增 error。

停止不等于放弃：Agent 应报告证据、影响、建议的 Core 责任路径和最小修复验证方案。

### 12.4 Agent 最终报告格式

```markdown
## Scope
## Contracts Used
## Files Changed
## Runtime Behavior Added
## Validation
## Output Inspected
## Known Limitations
## Core Issues Discovered
```

---

## 13. 实施方案比较

### 方案 A：扩写单篇 `08-themes-templates.md`

**优点**：启动简单。  
**缺点**：文件极长、难导航、参考与教程混杂、Agent 定位低效、变更容易漏改。  
**结论**：不推荐。

### 方案 B：人类手册与 Agent Skill 各写一套

**优点**：两类读者体验可独立优化。  
**缺点**：字段、命令、上下文和例子会重复，极易漂移。  
**结论**：可作为短期过渡，不适合作为最终架构。

### 方案 C：契约驱动的混合体系

**构成**：

- 机器契约：字段、对象、函数、能力状态；
- 人类手册：概念、教程、决策、案例；
- Agent Skill：执行流程与边界；
- Golden Theme：可运行事实；
- CI：防漂移。

**优点**：准确、可维护、可自动校验、人和 Agent 都高效。  
**缺点**：前期需要补契约和测试基础设施。  
**结论**：推荐方案。

---

## 14. 实施批次

### P0：冻结与校正公开契约

#### 工作项

- THEME-CORE-001：统一 `sections.plugin`；
- THEME-CORE-002：统一 `data.filters` 类型；
- THEME-CORE-003：使 `tokens` 路径生效或移除；
- THEME-CORE-004：定义主题版本语义；
- THEME-CORE-005：实现/澄清 `min_engine_version`；
- THEME-CORE-006：审计 `page_templates`；
- THEME-CORE-007：审计 `required_fields`；
- THEME-CORE-008：审计 manifest `assets` 和 `layouts`；
- THEME-CORE-009：统一 binder 与 linter；
- THEME-ASSET-001：SCSS 非破坏性构建；
- THEME-ASSET-002：统一图片优化与 srcset；
- THEME-ASSET-003：解决 Token 输出冲突。

#### 交付物

- `theme-capabilities.v1.json`；
- 公开字段调用链清单；
- 所有矛盾的决策记录；
- 聚焦契约测试。

#### 完成标准

- loader、strict validator、runtime、Schema 和测试对所有公开字段一致；
- 不确定字段被明确标记为 experimental/internal，而不是隐含可用。

### P1：建立 Golden Theme

#### 工作项

- 创建最小站点和主题；
- 覆盖首页、内容页、列表、分页、分类；
- 增加 layout、partial、component、section；
- 增加 Token、asset、static；
- 增加父子主题；
- 建立 golden output/结构断言。

#### 完成标准

- 所有主线命令可重复通过；
- 示例不依赖未发布命令；
- 示例代码块可从真实文件同步。

### P2：编写人类主线手册

先完成 R-000 至 R-009，再完成 R-013、R-015、R-016、R-018，最后补高级章节。

#### 完成标准

- 新手只按主线即可构建完整主题；
- 每章有完整例子、预期结果和故障排查；
- 所有事实带 source anchor 和 test anchor。

### P3：生成参考文档

#### 工作项

- `theme.yaml` JSON Schema；
- template context catalog；
- function catalog；
- capability matrix；
- precedence matrix；
- diagnostics catalog；
- Markdown reference 生成器。

#### 完成标准

- 对公开契约做 100% 覆盖；
- 源码变化而生成物未更新时 CI 失败。

### P4：高级示例和 Recipes

至少包括：

- 企业官网；
- 博客/知识库；
- 多语言站；
- 组件化 landing page；
- 父子主题；
- SEO 由 Core 管理；
- SEO 由主题管理；
- 数据模块；
- 无 JavaScript 主题；
- 外部 CSS 工具链预构建模式。

### P5：Agent Skill

#### 工作项

- 任务路由；
- 项目探测脚本/清单；
- 原子工作流；
- 负面规则；
- 输出模板；
- Agent 评测集。

#### 完成标准

- Agent 仅凭 Skill + 契约 + fixture 可生成通过质量门的主题；
- 不会调用不存在的 CLI；
- 不会在站点任务中改 Core；
- 能识别实验能力并规避。

### P6：文档与示例质量门

#### 检查项

- 文档字段是否存在；
- CLI 命令和参数是否存在；
- YAML 代码块是否可解析；
- Scriban 代码块是否可解析/渲染；
- 示例文件路径是否存在；
- source anchor 是否存在；
- Golden Theme 是否构建；
- 模板上下文是否漂移；
- 中英马版本结构是否对齐；
- Stable 文档是否引用 Labs 能力。

### P7：发布与多语言

中文主线先冻结事实与结构，再从同一契约生成英文/马来文参考；叙事章节允许人工本地化，但代码块和字段表不得独立维护。

---

## 15. 验收矩阵

| 维度 | 验收标准 |
|---|---|
| 准确性 | 每个公开字段、对象、函数均能映射到当前源码和测试 |
| 完整性 | `site.yaml.theme`、`theme.yaml`、模板上下文、帮助函数覆盖率 100% |
| 可运行性 | 所有主线示例可从空目录构建到通过 publish audit |
| 新手体验 | 不阅读源码即可完成最小主题、内容模板和列表模板 |
| Agent 体验 | Agent 能按固定工作流产出，并报告证据和限制 |
| 边界清晰 | Core、Labs、BukitJalil、规划能力不会混写 |
| 可维护性 | 公开契约变化能触发 CI 漂移失败 |
| 可诊断性 | 每类失败都有阶段、症状、根因、修复、验证 |
| 安全性 | 路径、XSS、HTML 转义、外链、symlink、输出清理均有规范与测试 |
| 兼容性 | alias、弃用、主题版本、最低引擎版本有正式政策 |
| 跨平台 | Windows/Linux 路径与外部工具差异有测试或明确限制 |
| 无副作用 | 构建不修改主题源文件；重复构建可复现 |
| SEO/GEO | head 所有权明确，审计命令进入默认交付流程 |
| 可访问性 | 示例满足语义 HTML、键盘、alt、语言属性和基础对比度要求 |

---

## 16. Agent 评测用例

至少建立以下自动/半自动评测：

1. 从空站点创建最小主题；
2. 为指定 collection 增加 detail template；
3. 增加列表和分页；
4. 修复错误的 `page.fields.foo` 访问；
5. 修复重复 `<title>`；
6. 创建可复用 card component；
7. 创建带 schema 的 hero section；
8. 创建父主题和子主题覆盖；
9. 在没有 Sass 的环境中选择安全处理方式；
10. 识别并拒绝不存在的 `bukit theme init`；
11. 识别 `sections.plugin` 契约冲突并不上线该配置；
12. 发现需要改 Core 时停止站点范围并报告；
13. 根据 publish audit 修复新增 error；
14. 不改变业务文案的前提下重构主题结构；
15. 对多语言站点避免硬编码语言文本。

每个评测记录：

- 输入仓库；
- 用户目标；
- 允许修改路径；
- 期望文件；
- 禁止行为；
- 必须运行的命令；
- 最终断言；
- 评分维度。

---

## 17. Definition of Done

“主题模板开发手册完成”必须同时满足：

1. P0 契约问题均已修复、移除或明确降级；
2. Golden Theme 通过全部目标平台的构建和审计；
3. 人类主线章节完整；
4. 参考文档由契约生成或自动比对；
5. Agent Skill 能独立执行主流程；
6. 文档、示例、CLI、Schema、binder、linter 漂移有 CI 门禁；
7. 稳定与实验能力边界无歧义；
8. 至少一轮新手可用性测试和一轮 Agent 评测通过；
9. 中英文（以及需要时的马来文）共享同一代码与字段源；
10. 发布说明包含适用 Core 版本和兼容策略。

---

## 18. 推荐立即执行顺序

不要先安排人员从头写 20 章。正确顺序是：

1. 建立 `theme-public-contract-audit.md`，逐项登记公开字段的 loader、validator、consumer、test、doc；
2. 完成 C-001 至 C-010、A-001 至 A-005 的决策；
3. 创建 Golden Theme 并让当前 CLI 完整构建；
4. 从 Golden Theme 写“最小主题”章节；
5. 建立模板上下文和函数机器目录；
6. 再批量写配置、模板、组件、SEO、资源章节；
7. 最后将确定性步骤压缩为 Agent Skill；
8. 用 CI 锁住契约，避免下一次功能变更再次造成文档漂移。

这一路径可以确保最终手册不是“看起来很详尽”，而是**每一个字段、例子和 Agent 动作都可被当前 `main` 代码验证**。
