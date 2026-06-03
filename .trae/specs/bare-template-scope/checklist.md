# Checklist: 按需模板生成

- [x] `TemplateScope.cs` 枚举定义正确（Full/Bare/None），辅助方法 `ShouldWritePageTemplates` 正确
- [x] `CloneThemeGenerator.WriteTo()` Bare 模式下不输出 page/post/list/pagination/taxonomy-*/search/bukit.templates.yaml
- [x] `CloneContentWriter.WriteTo()` Bare 模式下不输出上述模板
- [x] `CloneCommand` --template 选项默认 "bare"，正确传递 scope
- [x] `HtmlDemoImportOptions` 包含 TemplateScope 属性，默认 "bare"
- [x] `ImportCommand` --template 选项解析正确
- [x] `ThemeGenerator.Generate()` 不再调用 EnsureFallbackTemplate
- [x] `InitCommand` 支持 --template bare / none
- [x] `BukitCliSpecs.cs` clone 和 import html-demo 注册了 --template 选项
- [x] 构建通过 (0 errors, 0 warnings)，bare 模式功能验证通过
