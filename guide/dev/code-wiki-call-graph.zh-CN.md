# Bukit / BukitJalil 模块调用关系图

本文档聚焦“模块如何彼此调用”，帮助开发者快速看清从 UI、CLI 到构建引擎的真实边界与关键链路。

## 1. 总体结论

这个仓库不是一个单体系统，而是两条主线协作：

- **Bukit**：负责静态站点构建。
- **BukitJalil**：负责 AI 对话、主题生成、配置落盘、调用 Bukit CLI。

最重要的边界是：

> **BukitJalil 并不直接引用 Bukit 引擎程序集。**
>
> 两者之间的集成协议是：**项目目录文件 + `site.yaml` + 外部 `bukit` CLI 进程**。

## 2. 系统级关系图

```mermaid
flowchart LR
    subgraph Desktop["BukitJalil.Desktop"]
        UI["Blazor UI / AiStudio"]
        Executor["ToolExecutor"]
        ConfigSvc["BukitConfigService"]
        CliSvc["BukitCliService"]
        Store["ProjectStore / LiteDB"]
    end

    subgraph Core["BukitJalil.Core"]
        Orchestrator["ConversationOrchestrator"]
        Tools["ToolRegistry"]
        Providers["LLM Providers"]
        Prompt["PromptBuilder / RAG"]
    end

    subgraph Project["项目目录"]
        SiteYaml["site.yaml"]
        ThemeFiles["themes/*"]
        ContentFiles["content/*"]
        Dist["dist/*"]
    end

    subgraph Bukit["Bukit"]
        Cli["Bukit.Cli"]
        Engine["Bukit.Engine"]
        Config["Bukit.Config"]
        Content["Bukit.Content"]
        Routing["Bukit.Routing"]
        Rendering["Bukit.Rendering"]
        Plugins["Plugins"]
    end

    UI --> Orchestrator
    Orchestrator --> Tools
    Orchestrator --> Providers
    Orchestrator --> Prompt
    UI --> Executor
    Executor --> Store
    Executor --> ConfigSvc
    Executor --> CliSvc
    Store --> SiteYaml
    Executor --> ThemeFiles
    ConfigSvc --> SiteYaml
    CliSvc --> Cli
    Cli --> Config
    Cli --> Engine
    Engine --> Content
    Engine --> Routing
    Engine --> Rendering
    Engine --> Plugins
    Engine --> Dist
```

## 3. BukitJalil 到 Bukit 的真实桥接

### 3.1 关键事实

- BukitJalil 负责维护项目状态和文件。
- Bukit 负责读取这些文件并完成构建。
- 集成点不是类调用，而是“生成文件后启动 CLI”。

### 3.2 桥接链路

```mermaid
sequenceDiagram
    participant User as 用户
    participant UI as AiStudio
    participant Exec as ToolExecutor
    participant Config as BukitConfigService
    participant Store as ProjectStore
    participant CLI as BukitCliService
    participant SG as bukit build

    User->>UI: 发起生成/编译
    UI->>Exec: ExecuteAsync(toolCall)
    Exec->>Config: GenerateSiteYaml(project)
    Config->>Store: WriteProjectFile(site.yaml)
    Exec->>CLI: BuildAsync(projectDir)
    CLI->>SG: 启动外部进程 bukit build
    SG-->>CLI: stdout/stderr/exit code
    CLI-->>Exec: 构建结果
    Exec-->>UI: ToolResult
```

### 3.3 这一层的职责分工

| 模块 | 职责 |
|---|---|
| `AiStudio` | 触发对话、编译、预览等 UI 行为 |
| `ToolExecutor` | 把模型工具调用映射为真实操作 |
| `ProjectStore` | 维护项目目录与文件写入 |
| `BukitConfigService` | 根据项目状态生成 `site.yaml` |
| `BukitCliService` | 调用外部 `bukit build/preview` |

## 4. BukitJalil 内部调用关系

### 4.1 对话与工具调用链

```mermaid
flowchart TD
    A["AiStudio"] --> B["ConversationOrchestrator"]
    B --> C["PromptBuilder / KnowledgeService"]
    B --> D["ProviderRegistry"]
    D --> E["OpenAI-compatible / Anthropic Provider"]
    E --> F["LLM 返回 tool calls"]
    F --> G["AiStudio"]
    G --> H["ToolExecutor"]
    H --> I["ProjectStore"]
    H --> J["BukitConfigService"]
    H --> K["BukitCliService"]
    H --> L["DeployService"]
```

### 4.2 典型动作 1：AI 主题生成并确认落盘

```mermaid
flowchart TD
    A["用户描述需求"] --> B["ConversationOrchestrator.SendAsync"]
    B --> C["LLM 生成 ThemeFiles / tool calls"]
    C --> D["ToolExecutor.ExecuteGenerateTheme"]
    D --> E["生成预览用 HTML/CSS/JS"]
    E --> F["ToolExecutor.ExecuteConfirmDemo"]
    F --> G["写入 themes/default/layouts/*"]
    F --> H["写入 themes/default/assets/*"]
```

### 4.3 典型动作 2：手动编译与预览

```mermaid
flowchart TD
    A["AiStudio.BuildProjectAsync"] --> B["ToolExecutor.ExecuteCompileSite"]
    B --> C["BukitConfigService.WriteToDisk"]
    C --> D["ProjectStore.WriteProjectFile(site.yaml)"]
    B --> E["BukitCliService.BuildAsync"]
    E --> F["外部进程: bukit build"]
    F --> G["dist 输出生成"]
    G --> H["AiStudio.StartPreviewServerAsync"]
    H --> I["BukitCliService.PreviewAsync"]
    I --> J["外部进程: bukit preview"]
```

## 5. Bukit 内部调用关系

### 5.1 build 主链

```mermaid
flowchart TD
    A["Program.cs"] --> B["BuildCommand.RunAsync"]
    B --> C["ConfigPathResolver.Resolve"]
    B --> D["ConfigLoader.Load"]
    B --> E["ConfigOverrides"]
    B --> F["SiteEngine.BuildAsync"]
    F --> G["ConfigApplier.Apply"]
    F --> H["ConfigValidator.Validate"]
    F --> I["ContentProviderFactory.Create"]
    I --> J["MarkdownFolderProvider / NotionContentProvider / CompositeContentProvider"]
    F --> K["I18nOutputMerger"]
    K --> L["BuildVariantAsync"]
    L --> M["DataModuleBuilder"]
    L --> N["RouteGenerator.Generate"]
    L --> O["PluginRunner.RunDerivePages"]
    L --> P["PageRenderDispatcher.RenderPages"]
    P --> Q["ITemplateRenderer / Scriban"]
    L --> R["PluginRunner.RunAfterBuild"]
    F --> S["MetricsWriter / BuildManifest"]
```

### 5.2 单语言变体构建细化

```mermaid
flowchart TD
    A["BuildVariantAsync"] --> B["拆分 dataItems / contentItems"]
    B --> C["DataModuleBuilder.BuildModules"]
    B --> D["RouteGenerator.Generate"]
    D --> E["BuildContext"]
    E --> F["TaxonomyTermsInjector"]
    F --> G["PluginRunner.RunDerivePages"]
    G --> H["renderQueue = routed + derived"]
    H --> I["PageRenderDispatcher.RenderPages"]
    I --> J["RenderSpecialLists"]
    J --> K["复制 assets/static/media"]
    K --> L["PluginRunner.RunAfterBuild"]
    L --> M["保存 manifest / 输出日志"]
```

## 6. 内容、路由、渲染、插件四层协作

### 6.1 内容归一化层

- Markdown / Notion / 多源内容都会被归一化成 `ContentItem`
- `Meta` 供引擎决策
- `Fields` 供模板消费

```mermaid
flowchart LR
    A["Markdown 文件"] --> D["ContentItem"]
    B["Notion 页面"] --> D
    C["sources[] 多源配置"] --> D
    D --> E["Meta"]
    D --> F["Fields"]
```

### 6.2 路由层

```mermaid
flowchart TD
    A["ContentItem"] --> B{"是否存在 route override"}
    B -- 是 --> C["直接使用 url/outputPath/template"]
    B -- 否 --> D{"是否命中 site.permalinks"}
    D -- 是 --> E["BuildFromPermalink"]
    D -- 否 --> F["按 type 使用默认规则"]
    C --> G["RouteInfo"]
    E --> G
    F --> G
```

### 6.3 渲染层

```mermaid
flowchart TD
    A["SiteModel / PageModel / ListPageModel"] --> B["ScribanModelBinder"]
    B --> C["ScribanTemplateRenderer"]
    C --> D["HTML"]
    D --> E["FileWriter / OutputDir"]
```

### 6.4 插件层

```mermaid
flowchart LR
    A["BuildContext"] --> B["DerivePages Plugins"]
    B --> C["DerivedRouted"]
    C --> D["渲染队列"]
    D --> E["AfterBuild Plugins"]
    E --> F["sitemap.xml / rss.xml / search.json / taxonomy.json"]
```

## 7. 模块边界图

### 7.1 Bukit 分层边界

| 层 | 输入 | 输出 | 不应该做的事 |
|---|---|---|---|
| CLI | 命令行参数 | `AppConfig + Overrides` | 不应承载实际构建细节 |
| Config | `site.yaml` | 类型化配置 | 不应负责内容加载 |
| Content | Markdown / Notion / 多源 | `ContentItem[]` | 不应负责路由与模板选择 |
| Routing | `ContentItem` | `RouteInfo` | 不应关心内容来源 |
| Rendering | `SiteModel / PageModel` | HTML | 不应负责内容拉取 |
| Engine | 上述组件与 IO | 输出目录 | 不应退化为巨型业务类 |
| Plugins | `BuildContext` | 派生页 / 构建后产物 | 不应绕过稳定契约随意侵入主流程 |

### 7.2 BukitJalil 分层边界

| 层 | 职责 |
|---|---|
| UI | 人机交互、工作流触发 |
| Core | Provider、Prompt、工具定义、纯逻辑 |
| Desktop Services | 文件、CLI、部署、存储集成 |
| Project Directory | 站点配置、主题、内容、构建产物 |

## 8. 最值得顺着看的调用链

如果你要快速理解系统，建议按以下顺序看：

1. **BukitJalil 视角**
   - `AiStudio` → `ToolExecutor` → `BukitConfigService` → `BukitCliService`
2. **CLI 视角**
   - `Program.cs` → `BuildCommand.RunAsync`
3. **引擎视角**
   - `SiteEngine.BuildAsync` → `BuildVariantAsync`
4. **页面生成视角**
   - `ContentProviderFactory` → `RouteGenerator` → `PageRenderDispatcher`
5. **输出产物视角**
   - `PluginRunner.RunAfterBuild` → sitemap/rss/search/taxonomy

## 9. 常见误解

- 误以为 BukitJalil 直接调用 Bukit 的 C# API
- 误以为 `mode=data` 也会进入普通页面渲染
- 误以为插件可以任意改写主流程内部结构
- 误以为 preview 是渲染引擎的一部分，实际上它是 CLI 附带的本地静态文件服务

## 10. 配套文档

- 仓库总览：[`code-wiki.md`](./code-wiki.md)
- 新开发者阅读路线：[`new-developer-30min.md`](./new-developer-30min.zh-CN.md)
- 架构边界：[`architecture.md`](./architecture.md)
- 插件体系：[`plugins.md`](./plugins.md)

## 11. 一句话总结

这套系统最关键的理解是：**BukitJalil 负责“生成与编排”，Bukit 负责“读取配置并构建站点”，二者通过文件与 CLI 进程解耦连接。**
