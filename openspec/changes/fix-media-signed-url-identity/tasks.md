# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 身份计算

- [x] 新增 `SignedUrlIdentity`：按原顺序解析 query；按方案标志参数识别 SigV4、GCS V4、V2/CloudFront、Notion 主机；只去掉已识别方案的参数，保留其余参数原始拼写；剥离后为空时不留 `?`。
- [x] `NormalizeSourceUrlForKey` 使用剥离后的请求目标，前缀保持 `v3:`；未识别签名方案的 URL 身份逐字节不变；下载路径继续使用原始 `uri`。索引版本不变。

## 2. 查找顺序、失败记忆与重试

- [x] `LocalizeAsync` 查找顺序改为：成功缓存 → 索引 → 按身份找已有文件 → 失败记忆 → 下载。
- [x] `_failedKeys` 改用完整请求目标（`GetComponents(HttpRequestUrl, UriEscaped)`）。
- [x] `DownloadOutcome` 携带 HTTP 状态码；提供不走 `maxRetries` 循环的单次尝试路径。
- [x] 共享下载以 400/401/403 失败、且调用方请求目标不同时：按共享身份串行的重试闸门（同一身份最多一个重试在进行）；轮到时先查成功缓存、再查自身目标的失败记忆，都未命中再单次尝试（受 `timeoutMs` 限制，不进共享 in-flight 表）；成功写入缓存与索引，失败按自身目标记入失败记忆并报告一次；等待与请求响应取消和释放，被取消者不记录失败。
- [x] 下载失败报告只由首个成功写入失败记忆（`TryAdd` 成功）的调用方发出；后续引用的警告和非下载类失败报告保持不变。
- [x] 参数名按转义字符串原样、不区分大小写匹配，不做百分号解码。
- [x] 剥离在转义请求目标字符串上逐段删除，不解码、不重编码、不重排；未删除任何参数时身份逐字节不变（含无签名参数的 Notion 主机 URL）。
- [x] 不可重试的共享失败：请求目标不同的加入者，其自身目标记入失败记忆并报告一次，不额外请求。

## 3. 测试

- [x] 改写 `LocalizeAsync_SamePathDifferentQuery_UsesDistinctStoredFilesWithoutPersistingSecrets`：保留「索引不持久化秘密」断言，改用 `?w=100`/`?w=200`；在任务记录中注明这是对 4de531d9 契约的有意调整。
- [x] 新增：同一 S3 对象不同 SigV4 签名 → 同一文件名，第二次无网络请求。
- [x] 新增：`file.notion.so` 不同 `expirationTimestamp`/`signature` → 同一文件名。
- [x] 新增：不同 `versionId`、不同业务参数 → 不同文件名；参数名大小写变体被剥离。
- [x] 新增：非 Notion 主机上单独的 `signature`、缺少 `X-Amz-Signature` 的 `X-Amz-Expires` → 不剥离。
- [x] 新增：下载请求携带完整原始 query。
- [x] 新增：升级前缓存的未签名图片文件名不变、无请求；签名图片只下载一次。
- [x] 新增：过期先于新签名（403 → 新签名单次尝试成功）；新签名先于过期（过期直接命中，无失败记录）；多个签名在 404 上不额外请求、每个目标各报告一次；失败加入者的 URL 再次出现时不请求、不重复报告；过期 403 后多个新签名只请求一次；同一过期 URL 的两个等待者只请求一次、报告一次；重试等待中取消不记录失败；编码参数名不剥离。
- [x] 保持不变并通过：`OnFailureKeep_SharedFailedDownload_ReturnsEachCallersOwnUrl`（`RequestCount == 1`）、`DistinctHttpRequestTargets…`、`DifferentFragments…`、`StoredFileName_UsesFullSha256Identity`。
- [x] `PagesIndexPlugin` 的图片字段与正文得到同一本地文件名。
- [x] 最终统一审查 I-1：新键未命中时，按旧键（含签名的完整请求目标）查索引与文件身份，可信则复用并记到新键下；`readonly` 缓存中的过期签名网址升级后仍用本地文件。测试 `LocalizeAsync_FileStoredUnderLegacySignedKey_IsReusedWithoutRequest`（去掉修复时失败），Content 667/667。

## 4. 文档与发布

- [x] 用户指南媒体章节：签名参数不影响文件名、支持的签名方案、升级后一次性重新下载。
- [ ] 中英马 CHANGELOG（下一版本，版本号在发布时确认）。
- [ ] 版本号在发布时确认（单独确认后执行；v2.0.12 已发布，不再使用）。

## 5. 验证

- [x] 用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包并解决未映射文件。
- [x] `Bukit.Content` 媒体专项测试、格式检查、`openspec validate fix-media-signed-url-identity --strict`。
- [x] 在 SRBiz-bukit 上连续构建两次（间隔超过 1 小时）：第二次媒体下载数为 0，下载目录无新增文件。证据：silkroutecc 站点副本 readwrite，第一次 20:28 结束（媒体 849 → 920），第二次 21:44 `--no-clean`，媒体 920 → 920、0 页重渲染、97 秒（`/tmp/claude/codex-reports/real-v1/build2.md`）。

证据（2026-11-10）：Content 666、PublicApiDrift、cli-build 通过，审查 0 Critical / 0 Important（`/tmp/claude/codex-reports/review-s23/report.md`）；真实站点 readwrite 第一次构建媒体 849 → 920（`/tmp/claude/codex-reports/real-v1/build1.md`）。版本号按用户决定暂缓；间隔 1 小时的第二次构建已完成，见上。
