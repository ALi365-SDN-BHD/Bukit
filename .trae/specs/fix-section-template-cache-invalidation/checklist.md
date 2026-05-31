# Checklist

- [x] `TemplateFileSignature` 包含 `LastWriteTimeUtc`、`Length`、`ContentHash` 三个字段
- [x] 旧的 `FileSignature` 和 `SectionFileSignature` 已被删除
- [x] `CachedTemplate` 和 `CachedSectionTemplate` 均使用 `TemplateFileSignature`
- [x] `GetCachedTemplate` 创建签名时包含内容哈希
- [x] `TryGetCachedSectionTemplate` 创建签名时包含内容哈希
- [x] 内容哈希使用 SHA256 前 8 字节（`long`）
- [x] `RenderPage_WithSection_TemplateModifiedBetweenRenders_SeesUpdatedContent` 测试通过
- [x] 所有 Rendering 测试套件无新增失败（136/136）
- [x] 无编译警告（0 warn）
