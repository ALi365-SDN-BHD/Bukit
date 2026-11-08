# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 0. 准备

- [x] 生成 closure，确认 `unmappedFiles=[]`；新增 policy 规则 `media-localization-controls-contract`，owner self-test `codex-workflow-self-test.sh` exit 0。
- [x] `openspec validate add-media-localization-controls --type change --strict --no-interactive` 通过。

## 1. 配置

- [x] `MediaConfig` 新增 `OnFailure`、`DownloadHosts`、`SkipHosts`，默认值保持现有行为。
- [x] YAML 读取、严格字段白名单、取值校验（onFailure 枚举、域名模式）、JSON Schema。
- [x] `ContentProviderFactory.BuildEffectiveMediaConfig` 规范化（小写、去空白）。

## 2. 本地化

- [x] `RecordFailure` 按 `onFailure` 返回占位图或原地址；空地址、缺目录路径不变。
- [x] 下载前按 `skipHosts`/`downloadHosts` 过滤原始地址，不发请求、不计失败。
- [x] octet-stream／缺失 Content-Type 时按文件签名识别类型，校验通过后按识别类型命名。

## 3. 测试与文档

- [x] Config 测试：读取、默认值、非法 onFailure、非法域名模式、Schema。
- [x] Localizer 测试：keep 与 placeholder、正文 `<img>`/`srcset`、域名过滤、octet-stream PNG/HTML、text/html 仍拒绝。
- [x] 运行专项测试：Config 502/502、Content 617/617、Engine 3125/3125、Architecture 422/422；`cli-docs-sync.sh`、`public-api-drift-self-test.sh` exit 0。
- [x] 公共 API 基线更新（仅 MediaConfig 新增 3 个属性），`public-api-drift.sh check` exit 0。
- [x] 用户指南 04-site-yaml-config、16-parameter-cheatsheet 已更新（仓库仅有英文版）。
- [x] 专项审查无 Critical/Important：独立审查代理只读复核（2026-11-08），确认所有失败路径遵循 onFailure、SSRF 与解码校验未削弱、临时文件均清理、增量构建经本地化 HTML 哈希正确失效。记录的 Minor（不阻塞）：
  - m1（已修复）非 ASCII 域名模式绕过 `CheckHostName`；现先转 punycode 再校验，非法 IDN 配置校验失败。
  - m2（已修复）`MediaHostFilter.NormalizeHost` 改为先小写再 `GetAscii`。修复后 Config 505/505、Content 618/618 通过。
  - m3（已修复）共享下载内部失败返回内部标记，每个等待方按自己的原地址生成结果。
  - m4（已修复）octet-stream／缺失 Content-Type 时先读前 4 KB 识别签名，未识别则不创建文件、不再读取响应体；移除不再使用的 `DetectFileAsync`。m3–m5 修复后 Config 507/507、Content 627/627 通过。
  - m5（已修复）onFailure 的 JSON Schema 改为 enum（保留编辑器补全）加不区分大小写的 pattern，与运行时一致。
  - m6（已修复）新增校验改用 `ConfigInvalidValue`；域名错误改为报出具体取值，不再依赖会因空白项偏移的下标。
  - m7（已修复）`contractConsumerGlobs` 补齐 ProviderValidators、SiteDefaultsApplier.Content、MediaHostFilter、ContentProviderFactory；`codex-workflow-self-test.sh` exit 0，closure `unmappedFiles=[]`。`publicContract` 保持 false：`AppConfig.cs` 已由 `bukit-config` 规则标为公共契约。
  - m8（已修复主要项）新增 keep 下 SSRF 拦截、超过大小上限、octet-stream 签名正确但解码失败、封面字段（keep/placeholder）测试，以及大写／尾点主机匹配测试；不安全缓存与超时仍走同一 `RecordFailure`，未单独测试。修复后 Config 506/506、Content 625/625 通过。
- [x] 端到端：silkroute 测试站以 `onFailure: keep` + `downloadHosts` 构建，不可达的允许域名图片保留原地址，非允许域名图片不请求、保持原样。

## 4. 深度审计修复（2026-11-08）

- [x] `/code-review high` 审计的 10 项全部处理：共享下载改用 `DownloadOutcome` 类型结果（去掉字符串哨兵）；写入改用 `WriteResult`（去掉 -1 哨兵、闭包与不可达分支）；共享下载测试改为等待 `SharedDownloadJoins` 而非固定延时；URL 主机不再重复 IDN 转换，模式转换不再共享静态 `IdnMapping`；`ContentProviderFactory` 撤回重复规范化；Schema 辅助函数去掉 `Regex.Escape`，仅接受字母、数字与 `-`；`MatchesFileAsync` 恢复单次读取，签名长度常量共用；policy 补齐 Schema 生成器与严格字段校验器；域名规则接受精确 IPv4（spec 与指南已同步）。
- [x] 专项测试：Config 509/509、Content 628/628、Engine 3125/3125、Architecture 422/422；`codex-workflow-self-test.sh`、`public-api-drift.sh check` exit 0；closure `unmappedFiles=[]`。
