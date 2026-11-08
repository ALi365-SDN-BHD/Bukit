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
  - m3 `keep` 下共享的进行中下载把首个调用方的原地址返回给其他等价地址（仅拼写差异）。
  - m4 octet-stream／缺失 Content-Type 时先写完整响应体再识别签名，可提前用前 4 KB 拒绝。
  - m5 JSON Schema 的 onFailure 枚举区分大小写，运行时不区分。
  - m6 非法取值沿用 `ConfigRequiredFieldMissing` 诊断码；空白项被丢弃后报告的下标可能偏移。
  - m7 policy 规则的 `contractConsumerGlobs` 未列出部分实现文件。
  - m8 未逐一测试 SSRF、大小限制、解码失败、不安全缓存、超时、封面字段下的 keep，以及尾点／大写主机。
- [x] 端到端：silkroute 测试站以 `onFailure: keep` + `downloadHosts` 构建，不可达的允许域名图片保留原地址，非允许域名图片不请求、保持原样。
