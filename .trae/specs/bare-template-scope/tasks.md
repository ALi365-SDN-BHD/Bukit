# Tasks: 按需模板生成

- [x] Task 1: 创建 TemplateScope 枚举 + CloneThemeGenerator/CloneContentWriter 改造
  - 创建 `src/Bukit.Cli/Commands/TemplateScope.cs`，定义 `TemplateScope` enum（Full/Bare/None）和辅助方法
  - 修改 `CloneThemeGenerator.WriteTo()` 新增 `templateScope` 参数，Bare 模式下跳过 page/post/list/pagination/taxonomy-*/search/bukit.templates.yaml
  - 修改 `CloneContentWriter.WriteTo()` 同上

- [x] Task 2: CloneCommand 新增 --template 选项
  - 新增 `--template` 选项解析，默认 "bare"
  - 传递 scope 到 CloneThemeGenerator 和 CloneContentWriter

- [x] Task 3: ImportCommand + import 链路改造
  - `HtmlDemoImportOptions` 新增 `TemplateScope` 属性，默认 "bare"
  - `ImportCommand.HtmlDemoAsync` 新增 `--template` 选项解析
  - `ThemeGenerator.Generate()` 移除 `EnsureFallbackTemplate` 调用
  
- [x] Task 4: InitCommand 新增 bare/none + BukitCliSpecs 注册
  - `InitCommand` SupportedTemplates 新增 "bare", "none"
  - WriteTheme: bare → CloneThemeGenerator(bare); none → 跳过
  - `BukitCliSpecs.cs` clone 和 import html-demo 注册 --template

- [x] Task 5: 验证构建通过
  - `dotnet build src/Bukit.Cli -c Release` 0 errors, 0 warnings
  - `bukit init --template bare` 功能测试：pages/ 目录仅含 index.html
