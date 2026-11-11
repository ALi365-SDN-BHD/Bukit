# Keep media identity stable across re-signed URLs

## Why

媒体缓存键是完整请求地址的 SHA-256（`ImageAssetLocalizer.NormalizeSourceUrlForKey`，`v3:`），包含 `X-Amz-Signature`、`X-Amz-Date` 等签名参数。Notion 托管的图片每次被重新签名，同一个文件就得到新键和新文件名（`<downloadDir>/<sha256>.<ext>`）。silkroute.cc 实测：

- 距上次构建超过约 1 小时，69 篇含 Notion 托管图片的正文会被重新拉取，约 71 张图片（17MB）重新下载，旧文件留在缓存里；媒体缓存已累积 565 个文件，对应 8 次构建。
- 引用这些图片的页面每次都变化，rsync、发布提交和 CDN 缓存每次都要处理这批文件。
- CI 缓存每次增长约 17MB，恢复越来越慢。

签名参数只证明请求有权限，不决定取回哪个对象；对象由路径（Notion 的 S3 路径和 `file.notion.so/f/...` 路径都带固定文件 ID）和其余参数决定。

注意：SRBiz-bukit `v3` 分支的 `site.yaml` 中 `content.media` 没有配置 `downloadHosts`，也就是所有主机的远程图片都会下载。下文的剥离规则因此必须在任意主机上都安全，不能依赖「只下载 Notion 主机」这个前提。

## What Changes

- **按签名方案识别后剥离**，而不是按参数名全局剥离。计算媒体身份时，只有 query 中出现某种签名方案的标志参数，才去掉该方案的签名/过期参数（参数名大小写不敏感）：
  - AWS SigV4：存在 `X-Amz-Signature` 时，去掉 `X-Amz-Algorithm`、`X-Amz-Credential`、`X-Amz-Date`、`X-Amz-Expires`、`X-Amz-SignedHeaders`、`X-Amz-Signature`、`X-Amz-Security-Token`。
  - Google Cloud Storage V4：存在 `X-Goog-Signature` 时，去掉 `X-Goog-Algorithm`、`X-Goog-Credential`、`X-Goog-Date`、`X-Goog-Expires`、`X-Goog-SignedHeaders`、`X-Goog-Signature`。
  - AWS SigV2 / GCS V2 / CloudFront：存在 `Signature`，且同时存在 `AWSAccessKeyId`、`GoogleAccessId` 或 `Key-Pair-Id` 之一时，去掉 `Signature`、`Expires`、`Policy`、`AWSAccessKeyId`、`GoogleAccessId`、`Key-Pair-Id`。
  - Notion 文件代理：只在主机为 `notion.so`、`*.notion.so`、`*.notion.site` 时，去掉 `expirationTimestamp`、`signature`。
  - 其余参数（包括 S3 `versionId`、`response-*`、图片尺寸等业务参数）保持原顺序和原拼写参与身份计算。
- 剥离只作用于身份计算；实际下载请求仍使用原始、带签名的完整 URL。
- **身份前缀保持 `v3:`，索引版本保持 3**。没有识别出签名方案的 URL，身份输入与现在逐字节相同，文件名不变，升级后不会重新下载；只有带签名的 URL（本来每次都在换名）会一次性换到稳定的新名字。旧的签名身份条目在索引里成为不再命中的残留，数量有限，由后续孤儿回收处理。
- **成功缓存优先于失败记忆**：查找顺序改为先查成功缓存、索引和按身份找到的已有文件，再查失败记忆。这样同一对象只要有一个引用成功下载，同一构建中其他引用（包括过期签名）都复用本地文件。
- **失败记忆按完整请求目标记录**（`GetComponents(HttpRequestUrl, UriEscaped)`，主机大小写和 fragment 仍视为相同）。
- **共享下载失败时的加入者**：加入同一共享下载、但完整请求目标不同（签名不同）的调用方，在不可重试的失败（404、类型拒绝、超限、SSRF 阻止、超时等）后，其自身请求目标也写入失败记忆，并在该目标下报告一次失败，因为这些 URL 在 `keep` 下会留在发布的 HTML 中。
- **有限重试**：共享下载以 HTTP 400、401 或 403 失败、且当前调用方的完整请求目标与发起下载的不同时，重试按共享身份串行：同一身份同一时刻最多一个重试在进行。轮到的调用方先重新检查成功缓存，再检查自身请求目标的失败记忆，都未命中才用自己的 URL 单独请求**一次**：受 `timeoutMs` 限制，不走 `maxRetries` 重试循环，不进入共享 in-flight 表；等待和请求都响应取消与 localizer 释放，被取消的调用方不记录失败。这样多个新签名同时等待时，只有第一个真正发请求，其余直接命中缓存；同一个过期 URL 也不会被重复请求。重试带来的额外耗时上限是「实际发出重试的不同请求目标数 × 超时」。
- **报告去重**：下载失败只由第一个把该请求目标写入失败记忆的调用方加入 `Failures` 和构建汇总，并发时同一目标不会报告两次；后续引用照常每次记警告，非下载类失败（如无法删除的不安全缓存文件）保持现有报告方式。
- **参数名匹配**：在 `Uri` 规范化后的转义请求目标上不区分大小写匹配，我们自己不做百分号解码。`Uri` 会把未保留字符的编码（如 `%2D`）还原，这与实际发出的请求一致，所以 `X%2DAmz%2DSignature` 等同于 `X-Amz-Signature`；`Uri` 保留的编码（如 `X-Amz-Signature%26`）不会匹配，因而保留，这是安全的一侧。
- **剥离方式**：在 `GetComponents(HttpRequestUrl, UriEscaped)` 得到的转义字符串上逐段删除，不解码、不重新编码、不重排其余参数；没有删掉任何参数时（包括 Notion 主机上没有签名参数的 URL），身份与现在逐字节相同。成功则写入成功缓存与索引；失败则在自身请求目标下写入失败记忆并报告。实施需要让 `DownloadOutcome` 携带 HTTP 状态码，并为下载提供单次尝试路径。
- 这推翻了 4de531d9 契约的一部分：带签名参数、仅签名不同的 URL 不再视为不同对象。现有测试 `LocalizeAsync_SamePathDifferentQuery_UsesDistinctStoredFilesWithoutPersistingSecrets` 需要改写：「索引不持久化秘密」的断言保留，改用业务参数（`?w=100` / `?w=200`），另加签名版本的反向用例。
- 用户与开发文档、中英马 CHANGELOG 同步；随下一版本发布（v2.0.12 已发布，版本号在发布时确认）。

## Impact

- 受影响代码：`Bukit.Content/Media/ImageAssetLocalizer.cs`（身份计算、查找顺序、失败记忆、单次重试、`DownloadOutcome` 状态码），新增 `Bukit.Content/Media/SignedUrlIdentity.cs`。`PagesIndexPlugin` 自己创建的第二个 localizer 共用同一静态身份函数，需测试覆盖。
- 受影响测试：`ImageAssetLocalizerTests`。`SamePathDifferentQuery…` 按上文改写（这是 Core 定义性测试，改写需在任务中明确记录）；`OnFailureKeep_SharedFailedDownload_ReturnsEachCallersOwnUrl`（只差主机大小写和 fragment，`RequestCount == 1`）必须保持不变；`DistinctHttpRequestTargets…`、`DifferentFragments…`、`StoredFileName_UsesFullSha256Identity` 保持通过。
- 规格：`media-localization` 新增身份要求；修改「Download failure keeps the original URL when configured」中的失败记忆与共享下载描述。
- 公共契约：无公共 API 变化。`<downloadDir>/.media-index.json` 格式与版本不变。
- 跨仓影响：SRBiz-bukit 升级到包含本变更的版本后，首次构建只会把带签名的图片（约 71 张 Notion 图片）下载一次并换成稳定的文件名，未签名图片不受影响；发布仓库出现一次性的小规模改名提交，之后稳定。站点仓库无需改配置。
- 用户批准范围：本提案只批准起草；实施、提交、发布需单独确认。

## Non-Goals

- 不覆盖 Azure SAS（`sv`、`sig`、`se` 等）、阿里云 OSS（`OSSAccessKeyId`、`security-token`）、腾讯云 COS（`q-sign-*`、`q-signature` 等）签名方案。
- 不提供可配置的剥离参数列表。
- 不清理缓存中已经不被引用的文件（孤儿回收另立变更）；升级后的旧文件可手动删除。
- 不改变 Notion 正文缓存的过期重拉策略（`fix-notion-body-cache-contract` 也不改变它）。
- 不对未签名 URL 做 ETag / Last-Modified 重新校验。
