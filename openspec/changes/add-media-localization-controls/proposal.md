# Add media localization controls

## Why

迁移 silkroute.cc（见 sitegen 仓库 `bukit/docs/bukit-core-findings.zh-CN.md` 的 M1–M3）时发现，图片本地化无法按需开启：

1. 任何下载失败都会把图片地址替换成 `defaultImageUrl`，正文 `<img>`/`srcset` 也一样。一次临时网络故障就会把占位图永久写进输出，而构建仍然成功。
2. 只能整站开关下载，不能只下载会过期的 Notion 图片、让其他外链保持原样。
3. Notion 上传文件的 S3 签名地址常返回 `application/octet-stream`，被 Content-Type 白名单拒绝，必然失败。而这类地址约一小时后过期，最需要本地化。

用户已确认先实施这一组（"先做图片本地化"）。

## What Changes

- 新增 `content.media.onFailure: placeholder | keep`，默认 `placeholder`（保持现状）。`keep` 在下载失败时保留原地址，失败仍记录并在构建摘要中报告。空地址、缺下载目录这两种非下载失败情况不受影响。
- 新增 `content.media.downloadHosts` 和 `content.media.skipHosts`（域名列表，支持 `*.` 前缀通配）。不需下载的地址原样保留，不计为失败。
- Content-Type 为 `application/octet-stream`、`binary/octet-stream` 或缺失时，读取文件签名判断真实图片类型；判断出的类型须通过现有签名与解码校验，扩展名按判断结果确定。SVG 和无法识别的内容仍被拒绝。
- 配置读取、严格字段校验、取值校验、JSON Schema、用户指南、公共 API 基线同步更新。

## Impact

- 受影响代码：`Bukit.Config`（`MediaConfig`、`SiteDefaultsApplier.Content`、`ProviderValidators`、`ConfigStrictFieldValidator`、`ConfigJsonSchemaGenerator`）、`Bukit.Engine/ContentProviderFactory`（有效配置规范化）、`Bukit.Content/Media`（`ImageAssetLocalizer`、`ImageContentSignature`）。
- 公共契约：`MediaConfig` 新增三个属性和两个静态规则方法 `TryParseOnFailure`、`TryNormalizeHostPattern`（新增性变更，现有成员不变，公共类型数不变）；公共 API 基线随之更新。
- 跨仓影响：Bukit 主题无需改动。silkroute.cc 站点（sitegen 仓库）可在新版本中开启下载，配置 `onFailure: keep` 与 `downloadHosts`。现有站点不配置新键时输出不变。
- 兼容性：全部默认值保持现有行为；octet-stream 响应过去必失败，现在可能成功——这是有意的行为放宽，且仍需通过签名与解码校验。

## Non-Goals

- 不改变封面字段为空时返回占位图的语义。
- 不按重定向后的域名再次过滤；域名规则只作用于原始地址。
- 不放行 SVG，不新增可下载的图片格式，不仅凭 URL 扩展名放行。
- 不新增 `onFailure: error`（让失败直接中断构建），留待后续需要时再加。
- 不运行全量或发布 gate。
