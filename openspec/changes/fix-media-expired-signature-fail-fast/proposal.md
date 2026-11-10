# Fail fast on refused expired signed image URLs

## Why

`readonly` Notion 缓存会保留已过期的签名图片网址。本地没有这张图片时，下载得到 HTTP 403；`ImageAssetLocalizer.DownloadCoreAsync` 把它当作普通失败，按 `content.media.maxRetries`（默认 3）再请求 3 次，并在每次之间退避等待。签名过期后的拒绝不可能在重试中成功，这些请求和等待只会拖慢构建。这是 build-time incremental 修复计划最终审查记录的 Minor 遗留项。

## What Changes

- `SignedUrlIdentity` 新增按已识别签名方案读取过期时间：AWS SigV4（`X-Amz-Date` + `X-Amz-Expires`）、GCS V4（`X-Goog-Date` + `X-Goog-Expires`）、AWS SigV2 / GCS V2 / CloudFront（`Expires`，Unix 秒）、Notion 文件代理（`expirationTimestamp`，Unix 毫秒）。识别规则与 `IdentityTarget` 相同；无法识别或无法解析时视为「未知」。
- 下载以 HTTP 400、401 或 403 失败，且请求网址自身的签名按本机时钟已过期时，不再重试：立即记录失败（原因仍为 `HTTP <code>`），警告 `event=media.download_failed` 增加 `signature=expired`。
- 其余失败（未过期或无法判断的签名、未签名网址、5xx、网络错误、超时、空响应）保持现有重试行为。
- 不改变：`onFailure`（`placeholder` / `keep`）结果、失败记忆与报告去重、成功缓存优先于失败记忆、共享下载加入者的单次自有网址重试、旧键（含签名的完整请求目标）文件复用（最终审查 I-1）。

## Impact

- 受影响代码：`Bukit.Content/Media/SignedUrlIdentity.cs`、`Bukit.Content/Media/ImageAssetLocalizer.cs`。
- 受影响测试：新增 `ImageAssetLocalizerTests.ExpiredSignatures.cs`；现有测试全部 `MaxRetries = 0`，行为不变。
- 规格：`media-localization` 新增一条要求。
- 公共契约：无公共 API、配置或索引格式变化。
- 跨仓影响：SRBiz-bukit 使用 `readonly` 缓存且有过期链接时构建变快；输出不变。
- 用户批准范围：实施、分支与 PR；不发布。

## Non-Goals

- 不刷新过期的签名网址，也不改变 Notion 正文缓存策略。
- 不为未识别的签名方案（Azure SAS、阿里云 OSS、腾讯云 COS）判断过期。
- 不改变 404 等其他错误的重试次数。
