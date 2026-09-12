## Why

Bukit 已能为本地 JPEG/PNG 生成原格式响应式尺寸，但内容图片不会生成或投射 WebP；现有主题静态资源 WebP 转换还依赖构建机上的 `cwebp` 或 ImageMagick。SRBiz 的六张代表图片在质量 80 的本地抽样中由 8.58 MB 降至 1.03 MB，说明在 Core 中提供可验证、跨平台的原生 WebP 交付具有明确收益。

## What Changes

- 当 `theme.images.enabled: true` 且 `theme.images.formats` 包含 `webp` 时，使用已安装的 ImageSharp 为受支持的本地 JPEG/PNG 生成原尺寸和配置宽度的 WebP 变体。
- 仅依据已成功生成并验证的变体增强最终 HTML，以 `<picture>` 和 `type="image/webp"` 提供 WebP，并保留原始 `<img>`、原格式 `srcset` 及所有可访问性和布局属性作为回退。
- 将 WebP 变体纳入现有输出所有权、freshness、增量失效和安全清理合同；编码失败不得产生坏链接或覆盖用户文件。
- 修正文档，使 WebP 的原生支持、空格式列表的禁用语义，以及 AVIF 因缺少批准的完整验证链而暂不发布的边界清晰一致。
- Core 发布后，由 SRBiz 在独立站点变更中把 `formats: []` 改为 `formats: [webp]` 并完成隔离构建和浏览器验收。

## Capabilities

### New Capabilities

- `responsive-next-gen-images`: 定义可移植的 WebP 变体生成、成功产物驱动的 HTML 投射、原格式回退、所有权、增量构建和失败行为。

### Modified Capabilities

无。

## Impact

- Core：主要影响 `Bukit.Engine` 的图片优化、`image-processing` 内置插件、最终 HTML 投射和相关专项测试；复用现有 ImageSharp 3.1.12，不新增包或外部运行时依赖。
- 公共配置：复用现有 `theme.images.enabled/formats/sizes/quality`，不新增字段、不改变默认启用状态；启用 `webp` 的站点会新增 WebP 文件并改变图片 HTML。
- SRBiz：本提案不修改 SRBiz 仓库、Notion 内容、CI、部署或线上状态；站点启用和 Bukit binary 更新需要后续单独授权。
- 非目标：本变更不实现 AVIF，不处理 SVG/GIF/外部 URL/data URL，不删除原图，不增加 JavaScript 图片加载器，也不执行提交、推送、发布或部署。
