# Codex Clone 类迁移清单

## Phase 11 边界

- 本阶段只准备 `Bukit.Clone` 领域库骨架，不迁移 Clone 业务逻辑。
- Labs 中现有 Clone 实现继续作为当前事实来源。
- `Bukit.Clone` 不引用 `experimental/Bukit.Labs.Cli`，也不复制 Labs 命令实现。
- 后续若把 Clone 正式插件化，应先抽领域库，再由外部进程插件承载 CLI 入口。

## 当前 Labs Clone 文件分组

### CLI 编排

- `CloneCommand.cs`
- `CloneCommandOptions.cs`

### 模型

- `CloneModels.cs`
- `CloneInputModels.cs`
- `CloneNavModels.cs`
- `CloneOutputModels.cs`

### 输入与序列化

- `CloneInputLoader.cs`
- `CloneJsonContext.cs`

### 资产与内容输出

- `CloneAssetDownloader.cs`
- `CloneContentAssetHelpers.cs`
- `CloneContentCssWriter.cs`
- `CloneContentWriter.cs`
- `CloneSectionDataWriter.cs`
- `CloneYamlWriter.cs`

### 生成器

- `CloneBehaviorGenerator.cs`
- `CloneFidelityCommonBlocks.cs`
- `CloneFidelityGenerator.cs`
- `CloneFidelityHtmlParser.cs`
- `CloneIndexPageGenerator.cs`
- `CloneLayoutGenerator.cs`
- `CloneResearchWriter.cs`
- `CloneStyleSheetGenerator.cs`
- `CloneThemeGenerator.cs`

### 验证与对比

- `CloneBehaviorVerifyScript.cs`
- `CloneFidelityRunner.cs`
- `CloneScreenshotComparer.cs`
- `CloneVerifier.cs`

## 后续迁移建议

1. 先迁移纯模型与输入 DTO，并保持 source-generated JSON 兼容 Native AOT。
2. 再迁移纯领域生成逻辑，避免夹带 CLI 输出、文件系统副作用或 Labs 命令上下文。
3. 最后把外部资源下载、截图对比、浏览器验证留给插件进程层，通过协议传入显式权限。
4. Core CLI 只通过插件协议调用 Clone 插件，不引用 Clone 插件实现。
