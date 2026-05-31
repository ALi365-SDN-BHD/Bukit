# Checklist

- [ ] `TemplateFileSignature` 包含 `LastWriteTimeUtc`、`Length`、`ContentHash` 三个字段
- [ ] 旧的 `FileSignature` 和 `SectionFileSignature` 已被删除
- [ ] `CachedTemplate` 和 `CachedSectionTemplate` 均使用 `TemplateFileSignature`
- [ ] `GetCachedTemplate` 创建签名时包含内容哈希
- [ ] `TryGetCachedSectionTemplate` 创建签名时包含内容哈希
- [ ] 内容哈希使用 SHA256 前 8 字节（`long`）
- [ ] `RenderPage_WithSection_TemplateModifiedBetweenRenders_SeesUpdatedContent` 测试通过
- [ ] 所有 Rendering 测试套件无新增失败
- [ ] 无编译警告
