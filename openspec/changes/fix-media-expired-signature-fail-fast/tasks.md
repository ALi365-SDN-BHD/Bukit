# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 实施

- [x] `SignedUrlIdentity.TryGetSignatureExpiry` / `IsSignatureExpired`：按 SigV4、GCS V4、V2/CloudFront、Notion 主机读取过期时间；未知或无法解析返回 false。
- [x] `DownloadCoreAsync`：400/401/403 且签名已过期时不重试，警告带 `signature=expired`；其他失败保持重试。

## 2. 测试

- [x] 过期时间解析：各方案正例；未签名、缺参数、格式错误、负数、溢出、非 Notion 主机返回 false。
- [x] 过期签名 403/401/400 在 `MaxRetries = 3` 下只请求 1 次、报告 1 次；`placeholder` 与 `keep` 结果不变。
- [x] 未过期签名与未签名网址 403、过期签名 503 仍请求 4 次。
- [x] `readonly` 场景：旧键文件复用 0 请求；过期链接 1 次请求；同一构建再次引用不请求、不重复报告。

## 3. 文档

- [x] 中英马 CHANGELOG（Unreleased）。
- [x] 用户指南 `content.media.maxRetries` 说明。

## 4. 验证

- [x] `Bukit.Content.Tests` 专项测试、CRLF 格式检查、`openspec validate fix-media-expired-signature-fail-fast --strict`。证据（2026-11-11）：Content 691/691、Architecture 422/422、docs active-links 与 size-policy 通过；改动的 `.cs` 文件在 LF 下 `dotnet format whitespace` 通过，转成 CRLF 后 `dotnet format style --diagnostics IDE0055` 通过（同一检查能识别 04b02c25 的已知 Windows 问题）。
- [x] PR CI 通过：#117 全部检查通过，2026-11-10 合并（5c0ebd19）。
