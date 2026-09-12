## Context

Bukit 当前有两条图片处理路径。`AssetSourceWorkspace` 在渲染前调用 `ImageOptimizer`，为主题静态 JPEG/PNG 生成依赖外部命令的整尺寸 WebP/AVIF；`image-processing` 插件在渲染后为输出目录和本地化内容媒体生成保持原扩展名的尺寸变体。插件的 HTML transform 早于 after-build 执行，会依据命名规则推断原格式 `srcset`，而不是消费实际成功产物。

Core 已直接使用 ImageSharp 3.1.12，并包含 WebP 编码和完整解码验证能力。AVIF 没有同等的已批准验证链，现有实现因此失败关闭。最终实现必须让变体生成、HTML 引用和输出清单基于同一份成功结果，不能扩大为第二套图片系统。

## Goals / Non-Goals

**Goals:**

- 复用现有配置和 `image-processing` 生命周期，为主题静态资产提供可移植整尺寸 WebP，并为本地化内容图片提供整尺寸及响应式 WebP。
- 保证最终 HTML 只引用验证成功的 WebP，同时保留现有原格式图片和属性。
- 复用现有所有权、freshness、取消、临时文件和路径安全约束。
- 使普通 .NET 与 Native AOT 在没有外部图片工具时行为一致。

**Non-Goals:**

- 不自动改写任意主题静态 HTML 图片；主题作者仍可显式消费静态 WebP。
- 不实现 AVIF，不改变内容下载格式，也不删除原图片。
- 不增加公共配置、插件协议、JavaScript、外部服务或新 NuGet 包。
- 不在本变更中更新 SRBiz binary、站点配置、CI 或部署。

## Decisions

### 1. 使用现有 ImageSharp WebP 编码器

建立一个 Engine 内部、非公共的 WebP 编码入口，供 `ImageOptimizer` 和 `ImageProcessingPlugin` 复用。它负责可选 resize、质量设置、临时文件、取消和 `ImageContentValidator` 验证。

选择该方案是因为 ImageSharp 已被 Core 直接使用并支持 WebP 编码/解码，可消除主机 PATH 差异。保留两套独立编码实现会重复失败处理；继续调用 `cwebp/ImageMagick` 会让 Native AOT 发布物依赖机器环境。

### 2. 区分主题静态资产与内容媒体

- 主题静态 JPEG/PNG：保持现有 `ImageOptimizer` 时机，仅把 WebP 转换改为内置编码；不自动修改模板 HTML。
- 本地化内容 JPEG/PNG：在 `image-processing` 中生成原尺寸 WebP 和小于源宽度的配置尺寸 WebP，并自动增强页面 HTML。

这保留当前职责边界，避免扫描和改写所有主题标记，同时覆盖 SRBiz 的主要图片流量来源。

### 3. 最终 HTML 由成功变体映射驱动

after-build 图片阶段先生成并验证变体，再建立以原图片公共路径为键的成功候选映射。随后在同一插件阶段对当前构建拥有的 HTML 文件执行一次幂等增强：只有映射中存在候选时才加入 WebP `source`。

现有预渲染 transform 继续负责原格式 `srcset` 和 `decoding`，但必须把已有 `picture` 作为不可改写区域。WebP 增强在原格式 `img` 外包裹单层 `picture`，并复制其 `sizes` 到 `source`。

如果验证发现 HTML 写入发生在渲染哈希或增量清单最终化之后，实施必须把最终字节重新纳入现有 manifest 记录，或把增强前移到清单最终化之前；不得接受清单与最终文件不一致。

未选择“渲染前推断 WebP URL”，因为部分失败会产生坏链接；未选择让模板直接拼接路径，因为内容 HTML 与多个主题都会重复实现相同合同。

### 4. 使用明确、可清理的文件身份

内容媒体使用：

- 整尺寸：`name.webp`
- 尺寸变体：`name-{width}w.webp`
- freshness：各 WebP 旁置现有后缀 sidecar

freshness schema 升级并记录源路径、源 SHA-256、目标宽度、质量、`webp` 格式和编码器身份。只有带有当前有效所有权的文件可覆盖或删除；同名用户文件导致该候选跳过并记录稳定诊断。

### 5. 保持渐进增强和局部失败

WebP 是可选增强。单个候选失败时删除临时产物并从映射移除；原格式 `<img>` 仍可用。插件失败策略沿用现有站点政策，但任何成功构建都不得包含不存在的 WebP 引用。

AVIF 继续失败关闭。它需要独立 OpenSpec 选择可固定版本、可审计、Native AOT 兼容且能完整验证的编码/解码链，不能只做签名检查。

## Risks / Trade-offs

- [after-build HTML 改写可能使增量 manifest 过期] → 增加最终字节/manifest 一致性集成测试，并在清单最终化前完成或显式刷新记录。
- [正向格式增加发布产物体积和构建 CPU] → 仅显式启用，复用 freshness，禁止放大；SRBiz 启用前要求代表页面传输量至少下降 30%。
- [透明 PNG 或色彩配置产生视觉差异] → 增加透明 PNG、JPEG 和代表真实图片对比测试，始终保留原格式回退。
- [自动包装产生嵌套 picture 或改写作者 srcset] → 将已有 `picture`、脚本、样式、模板和注释作为不可改写块，覆盖幂等测试。
- [文件名碰撞或清理删除用户文件] → 所有覆盖和清理继续要求有效 Bukit 所有权与 freshness，未拥有文件只跳过。
- [一次加载多尺寸图片增加峰值内存] → 首版保持逐图片、逐候选串行释放；只有测量证明需要时再优化并发或复用解码缓冲。

## Migration Plan

1. 在 Bukit Core 完成实现、专项测试、Native AOT 验证和文档更新；不改变默认启用状态。
2. 发布新的 Bukit Core binary。发布、提交和推送均需单独授权。
3. 在 SRBiz 独立变更中更新 binary，并把 `formats: []` 改为 `formats: [webp]`。
4. 使用隔离输出和缓存执行干净非增量构建，验证 `<picture>`、MIME、404、布局和 390/768/1440 浏览器候选。
5. 只有代表页面图片传输量不增加且目标下降至少 30% 时才部署。

回滚时恢复 SRBiz `formats: []` 和前一版 Bukit binary；原格式图片始终保留，因此不需要内容迁移或删除线上资源。
