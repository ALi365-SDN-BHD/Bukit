# 按需模板生成 Spec

## Why
clone 和 import 命令对用户实际内容无感知，却无条件输出 page.html、post.html、list.html 等全套模板。这些源自 init 脚手架的习惯，对 clone（从目标网站提取）和 import（从 HTML demo 导入）毫无意义。

## What Changes
- 新增 `TemplateScope` 控制模板输出范围：`full`（全部）、`bare`（仅 base+partials+index）、`none`（不生成 theme）
- clone 默认 `bare`
- import html-demo 默认 `bare`（移除 EnsureFallbackTemplate 保底）
- init 新增 `bare`/`none` 值，默认保持 `minimal`

## Impact
- Affected files: CloneThemeGenerator.cs, CloneContentWriter.cs, CloneCommand.cs, ImportCommand.cs, HtmlDemoImportOptions, ThemeGenerator.cs, InitCommand.cs, BukitCliSpecs.cs
