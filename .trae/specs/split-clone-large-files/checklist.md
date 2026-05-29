# Checklist

## 文件大小目标
- [x] `src/Bukit.Cli/Commands/CloneCommand.cs` ≤ 250 行（实际 161 行）
- [x] `src/Bukit.Cli/Commands/CloneContentWriter.cs` ≤ 320 行（实际 242 行）
- [x] `src/Bukit.Cli/Commands/CloneThemeGenerator.cs` ≤ 280 行（实际 230 行）
- [x] `src/Bukit.Cli/Commands/CloneFidelityGenerator.cs` ≤ 300 行（实际 238 行）
- [x] `src/Bukit.Cli/Commands/CloneVerifier.cs` ≤ 300 行（实际 271 行）
- [x] `src/Bukit.Cli/Commands/CloneModels.cs` ≤ 200 行（实际 20 行）

## 新增类文件存在且符合单一职责
- [x] 新建 `CloneCommandOptions.cs`（CLI 选项解析）— 100 行
- [x] 新建 `CloneInputLoader.cs`（7 个 LoadXxxAsync）— 156 行
- [x] 新建 `CloneAssetDownloader.cs`（DownloadAssetsAsync + WriteIcons + CountBehaviors）— 94 行
- [x] 新建 `CloneFidelityRunner.cs`（RunAsync + WriteFidelitySiteYaml + TransferAssetsToStatic）— 127 行
- [x] 新建 `CloneSectionDataWriter.cs`（section 数据生成）— 196 行
- [x] 新建 `CloneContentCssWriter.cs`（content CSS 生成）— 50 行
- [x] 新建 `CloneContentAssetHelpers.cs`（asset 与 URL 辅助）— 152 行
- [x] 新建 `CloneLayoutGenerator.cs`（base layout / header / footer）— 174 行
- [x] 新建 `CloneIndexPageGenerator.cs`（index 页面与 section）— 189 行
- [x] 新建 `CloneFidelityHtmlParser.cs`（FidelityPage 提升为顶层 + HTML 解析）— 177 行
- [x] 新建 `CloneFidelityCommonBlocks.cs`（common-block 提取算法）— 158 行
- [x] 新建 `CloneScreenshotComparer.cs`（截图比较 + AffectedSection）— 222 行
- [x] 新建 `CloneInputModels.cs`（输入相关 record 群）— 328 行
- [x] 新建 `CloneNavModels.cs`（导航/section 元信息 record）— 52 行
- [x] 新建 `CloneOutputModels.cs`（icon/asset/summary/behaviors record）— 86 行
- [x] 附加新建 `CloneBehaviorVerifyScript.cs`（Agent B 为满足约束抽出的 JS 常量）— 62 行

## 构建与测试
- [x] `dotnet build bukit.slnx -c Release` 0 warning 0 error
- [x] `dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release` 743 / 743 通过，0 失败
- [x] `Bukit.Cli.Tests/CloneCommandTests.cs` 未做内容修改

## API 与命名空间兼容
- [x] `CloneCommand.RunAsync(ArgReader)` 与 `CloneCommand.RunAsync(CliBoundCommand)` 公共签名未变
- [x] `CloneThemeGenerator.WriteTo(...)` 公共签名未变
- [x] `CloneContentWriter.WriteTo(...)` 公共签名未变
- [x] 所有 model record 仍位于 `Bukit.Cli.Commands` 命名空间
- [x] `Program.cs` 与 `CloneVerifier.cs` 中对 `CloneCommand.*` 的调用无需修改

## 质量门禁
- [x] `scripts/.oversized-baseline.txt` 未新增 clone 相关文件（baseline 仅有 StarterThemeResources.cs）
- [x] 所有 clone 文件 < 332 行，全部远离 600 阈值

## 反射兼容补丁
- [x] `CloneCommand.cs` 末尾保留 `ParseVisualThreshold` / `CountBehaviors` 两个 thin wrapper（私有静态，反射测试可访问）
- [x] `CloneCommandOptions.ParseVisualThreshold` visibility 升为 `internal`
