# responsive-next-gen-images Specification

## Purpose
为启用图片优化的 Bukit 站点提供可移植、可验证且具有原格式回退的 WebP 响应式图片交付合同，同时保证失败、增量构建和用户自有文件场景不会产生坏链接或数据破坏。

## Requirements

### Requirement: 配置必须保持显式启用和向后兼容

系统 SHALL 仅在 `theme.images.enabled` 为 `true` 且 `theme.images.formats` 包含 `webp` 时生成和投射 WebP。空格式列表、未配置图片优化或显式禁用图片优化 MUST 保持既有非 WebP 输出行为。

#### Scenario: 显式启用 WebP
- **WHEN** 站点启用图片优化且格式列表包含大小写任意的 `webp`
- **THEN** 系统生成并投射 WebP，重复的格式值不产生重复产物

#### Scenario: 空格式列表
- **WHEN** 站点配置 `formats: []`
- **THEN** 系统不生成、不投射也不清理 WebP，既有原格式响应式行为保持不变

### Requirement: 系统必须生成经过验证的 WebP 变体

系统 SHALL 为符合条件的本地 JPEG/PNG 生成原尺寸 WebP，并为每个小于源图片宽度的已配置正整数宽度生成 WebP 尺寸变体。系统 MUST NOT 放大图片，且每个发布的 WebP MUST 在发布前通过完整解码验证。

#### Scenario: 大图生成原尺寸和响应式变体
- **WHEN** 一个 1600 像素宽的本地 JPEG/PNG 遇到 `[640, 960, 1280]` 尺寸配置
- **THEN** 系统生成原尺寸 WebP 以及 640、960、1280 像素宽的 WebP 变体

#### Scenario: 小图不放大
- **WHEN** 源图片宽度小于所有配置尺寸
- **THEN** 系统仅生成原尺寸 WebP，不生成任何放大的尺寸变体

### Requirement: HTML 必须提供 WebP 与原格式回退

系统 SHALL 仅为成功生成 WebP 变体的本地内容图片输出 `picture` 和 `source type="image/webp"`，并 MUST 保留原始 `img`、原格式 `src`、原格式 `srcset`、`sizes`、`alt`、`width`、`height`、`loading`、`decoding`、`class`、`id` 及其他作者属性作为回退。

#### Scenario: 浏览器支持 WebP
- **WHEN** 浏览器支持 `image/webp` 且页面图片具有已验证 WebP 变体
- **THEN** 最终 HTML 允许浏览器从 WebP `srcset` 选择适合视口的候选

#### Scenario: 浏览器不支持 WebP
- **WHEN** 浏览器忽略 `source type="image/webp"`
- **THEN** 原始 `img` 及其原格式候选仍可完整显示相同内容

### Requirement: 作者提供的图片标记必须被保护

系统 MUST 保持已有 `picture` 和作者提供的 `srcset` 不变，并 MUST 忽略外部 HTTP(S) 图片、data URL、SVG、GIF、脚本、样式、模板和注释中的伪图片标记。

#### Scenario: 已有 picture
- **WHEN** 输入 HTML 已包含作者提供的 `picture` 或 `source`
- **THEN** 系统不嵌套新的 `picture`，也不改写作者候选

#### Scenario: 非目标图片
- **WHEN** 图片来自外部来源或格式不在本能力范围内
- **THEN** 最终 HTML 和对应资源保持不变

### Requirement: HTML 引用必须由成功产物驱动

系统 MUST 只在最终 HTML 中引用真实存在、验证成功且由当前构建拥有的 WebP 文件。生成或验证失败 MUST 保留可用的原格式回退、记录稳定诊断并移除临时文件，不得输出推断但不存在的 URL。

#### Scenario: 单个 WebP 生成失败
- **WHEN** 某个图片或尺寸的 WebP 编码或验证失败
- **THEN** 最终页面不引用失败候选，原格式图片继续可用且其他成功图片不受影响

#### Scenario: 全部 WebP 生成失败
- **WHEN** 页面没有任何成功的 WebP 候选
- **THEN** 页面不增加 WebP `source`，并保持原格式图片行为

### Requirement: 所有权和增量构建必须覆盖格式身份

系统 SHALL 将源文件身份、源文件 SHA-256、目标宽度、格式、质量和编码器身份纳入 WebP freshness，并 SHALL 将 WebP 及其 sidecar 纳入现有插件输出所有权。配置或源文件变化 MUST 只使受影响产物失效。

#### Scenario: 质量或格式配置变化
- **WHEN** `quality`、`formats`、`sizes` 或源文件内容发生变化
- **THEN** 系统重建或清理由 Bukit 拥有的受影响 WebP，并保留无关的有效产物

#### Scenario: 同名用户文件
- **WHEN** 目标路径已存在但没有有效 Bukit 所有权记录
- **THEN** 系统不覆盖、不删除且不在 HTML 中宣称拥有该文件

### Requirement: WebP 生成必须可移植且不依赖外部工具

系统 SHALL 在普通 .NET 和受支持的 Native AOT Bukit 运行时中使用随 Core 发布的能力生成 WebP，并 MUST NOT 要求构建主机安装 `cwebp`、ImageMagick 或其他 PATH 工具。

#### Scenario: 构建主机没有图片转换命令
- **WHEN** PATH 中不存在 `cwebp`、`magick` 和 `convert`
- **THEN** 启用 WebP 的有效站点仍能完成 WebP 生成和验证

### Requirement: AVIF 必须保持失败关闭

在没有批准的编码器和完整解码验证链之前，系统 MUST NOT 发布或投射 AVIF；配置中出现 `avif` 时 MUST 提供稳定诊断而不是生成无法验证的文件。

#### Scenario: 请求 AVIF 但验证能力不可用
- **WHEN** 格式列表包含 `avif` 且运行时没有批准的完整 AVIF 验证链
- **THEN** 系统不发布、不投射 AVIF，并保持原格式和已支持 WebP 可用
