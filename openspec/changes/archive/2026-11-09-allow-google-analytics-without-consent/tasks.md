# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 校验

- [x] `I18nValidator` 删除"Google provider 必须有 consent.google"检查，保留反方向检查。
- [x] `ConfigJsonSchemaGenerator` 的 `allOf` 只保留"有 consent 就要求 Google provider"。

## 2. 测试

- [x] Config：GA、GTM 无 consent 时校验通过且 `Consent` 为 null；有 consent 无 Google provider 仍报错。
- [x] Schema：`allOf` 只剩一个方向。
- [x] Engine：仅 GA 无 consent 时无 `gtag('consent'`、有完整引导且输出确定；仅 GTM 无 consent 时保留 `<noscript>`；GA+GTM 无 consent 时只初始化一次；报告 `googleConsent` 为 null。

## 3. 文档

- [x] 用户指南（19、04、16）、开发文档 `config-site-yaml.md`、中英 CHANGELOG。

## 4. 验证

- [x] `dotnet test tests/Bukit.Config.Tests`、Engine 的 Analytics 测试、`openspec validate --strict`。
