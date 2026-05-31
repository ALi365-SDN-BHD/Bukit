# 修复 Section 模板缓存同长度文件修改不失效 Spec

## Why

`ScribanTemplateRenderer` 的模板缓存使用 `(LastWriteTimeUtc, Length)` 作为文件变更签名。当 section 模板文件被修改但内容长度不变时（例如 `<h1>...</h1>` 改为 `<h2>...</h2>`），若文件系统时间戳精度不足以区分两次写入（如某些 CI 环境），缓存不会失效，导致渲染输出使用旧模板。

测试 `RenderPage_WithSection_TemplateModifiedBetweenRenders_SeesUpdatedContent` 在 Windows CI 上稳定失败，本地可能因时间戳精度不同而通过。

## What Changes

- 为 `FileSignature` 和 `SectionFileSignature` 增加内容哈希字段，确保内容变更始终可检测
- 两个签名类型合并为一个统一的 `TemplateFileSignature`，消除重复代码
- 哈希仅在缓存未命中时计算（即读取文件时），不影响缓存命中路径性能

## Impact

- Affected specs: 无
- Affected code:
  - `src/Bukit.Rendering/Scriban/ScribanTemplateRenderer.cs` — `FileSignature`、`SectionFileSignature`、`GetCachedTemplate`、`TryGetCachedSectionTemplate`
  - `tests/Bukit.Rendering.Tests/ScribanTemplateRendererTests.cs` — 验证测试通过

---

## MODIFIED Requirements

### Requirement: 模板缓存签名包含内容哈希
`FileSignature` 和 `SectionFileSignature` SHALL 包含文件内容的 SHA256 哈希（前 8 字节），与 `LastWriteTimeUtc` 和 `Length` 共同构成缓存失效判断依据。

#### Scenario: 同长度不同内容被检测
- **GIVEN** section 模板 `hero.html` 内容为 `<h1>{{ section.props.title }}</h1>`（34 字节）
- **WHEN** 模板被修改为 `<h2>{{ section.props.title }}</h2>`（34 字节）
- **THEN** 缓存签名不匹配，模板被重新读取
- **AND** 渲染输出使用新模板内容

#### Scenario: 未修改内容命中缓存
- **GIVEN** section 模板已缓存
- **WHEN** 再次渲染同一模板且文件未修改
- **THEN** 签名匹配，返回缓存的模板对象

#### Scenario: 长度变化内容变化被检测
- **GIVEN** section 模板内容为 50 字节
- **WHEN** 模板被修改为 100 字节的完全不同的内容
- **THEN** `Length` 字段变化导致签名不匹配，模板被重新读取

---

### Requirement: 统一 FileSignature 和 SectionFileSignature
两个功能相同但独立的 `readonly record struct` SHALL 合并为一个 `TemplateFileSignature`，消除重复定义。

#### Scenario: 主模板缓存和 section 模板缓存使用同一签名类型
- **WHEN** `GetCachedTemplate` 和 `TryGetCachedSectionTemplate` 创建签名时
- **THEN** 均使用 `TemplateFileSignature` 类型

---

### Requirement: 现有测试通过
`RenderPage_WithSection_TemplateModifiedBetweenRenders_SeesUpdatedContent` SHALL 在所有平台（Windows、Linux、macOS）通过。

#### Scenario: Windows CI 通过
- **GIVEN** Windows CI 环境运行测试
- **WHEN** 运行 `dotnet test Bukit.Rendering.Tests`
- **THEN** `RenderPage_WithSection_TemplateModifiedBetweenRenders_SeesUpdatedContent` 通过
