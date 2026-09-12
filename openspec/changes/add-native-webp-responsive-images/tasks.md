## 1. 实施闭包与基线

- [x] 1.1 使用 `scripts/checks/codex-workflow.py closure` 对计划修改的 Engine、测试和文档路径生成 verification closure，核对所有文件均有映射且不包含 CI、发布、历史参考目录或 SRBiz 路径
- [x] 1.2 使用 `scripts/checks/codex-workflow.py classify` 对 Engine 专项测试、文档合同检查和 Native AOT 验证命令分类，确认共享 build/cache/manifest 资源串行执行
- [x] 1.3 记录当前 HEAD、SDK、ImageSharp 版本、相关文件哈希和 `Directory.Build.props` 既有未提交修改，验证实施不会覆盖并发工作

## 2. Core 原生 WebP 编码

- [x] 2.1 在 `Bukit.Engine` 建立供两条现有图片路径复用的内部 ImageSharp WebP 编码入口，验证质量、可选 resize、取消、临时文件清理和完整 WebP 解码测试通过
- [x] 2.2 将主题静态资产的 WebP 转换从外部 `cwebp/ImageMagick` 切换到内部编码入口，验证 PATH 中没有图片命令时 `ImageOptimizerTests` 仍通过且 AVIF 保持失败关闭
- [x] 2.3 验证 `formats: []`、禁用图片优化、大小写和重复 `webp` 的兼容行为，确保不会生成或重复生成 WebP

## 3. 内容媒体 WebP 变体

- [x] 3.1 扩展现有 `image-processing` 计划，为本地 JPEG/PNG 生成整尺寸和小于源宽度的 WebP 候选，验证 JPEG、PNG、透明 PNG、小图和取消场景测试通过
- [x] 3.2 将 WebP、sidecar、格式、宽度、质量、源 SHA-256 和编码器身份接入现有 freshness 与插件输出所有权，验证配置变化只重建受影响产物
- [x] 3.3 扩展过期和孤立产物清理，验证只有具有有效 Bukit 所有权的 WebP 会被覆盖或删除，同名用户文件保持不变且不会被投射
- [x] 3.4 让单个编码或验证失败只移除失败候选及临时文件，验证成功构建不会包含任何不存在或未验证的 WebP URL

## 4. HTML 渐进增强

- [x] 4.1 根据实际成功变体映射为本地内容图片生成单层 `picture` 和 `source type="image/webp"`，验证原始 `img` 与原格式 `src/srcset` 均保留
- [x] 4.2 保留 `sizes`、`alt`、尺寸、lazy loading、decoding、class、id 和其他作者属性，验证 WebP 支持与不支持浏览器均有可用候选
- [x] 4.3 将已有 `picture`、作者 `srcset`、外部 URL、data URL、SVG、GIF、脚本、样式、模板和注释作为不可改写输入，验证重复运行幂等且不产生嵌套 `picture`
- [x] 4.4 验证最终 HTML 字节、增量 manifest、输出计划和发布报告一致；若 after-build 改写晚于清单哈希，则在清单最终化前完成增强或刷新既有记录

## 5. 文档与专项验证

- [x] 5.1 更新用户配置和内置插件文档，明确 WebP 为 Core 原生能力、空列表禁用语义、AVIF 暂不发布及 `<picture>` 回退，并运行对应 config/docs 合同检查
- [x] 5.2 运行 `dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter "FullyQualifiedName~ImageProcessingPluginTests|FullyQualifiedName~ImageOptimizerTests|FullyQualifiedName~SiteEngineMediaLifecycleTests"`，验证全部受影响 Engine 场景通过
- [x] 5.3 使用隔离输出构建受支持的 Native AOT Bukit binary，并在 PATH 不含外部图片工具的 fixture 中验证 WebP 生成、完整解码和 HTML 投射；不得用脚本自测代替真实 binary 运行
- [x] 5.4 执行一次专项代码复审，检查路径边界、输出所有权、manifest 一致性、内存释放和公共配置兼容；Critical/Important 为零后停止扩大审查
- [x] 5.5 运行 `openspec validate add-native-webp-responsive-images --type change --strict --no-interactive` 和 `git diff --check`，记录命令、输入、工具链和结果作为本变更 GREEN 证据

## 6. SRBiz 后续交付边界

- [x] 6.1 形成仅包含 Core binary SHA、配置差异和验收命令的 SRBiz 交接说明，验证本变更未修改 SRBiz、Notion、CI、部署或线上状态
- [x] 6.2 在获得独立授权后，才在 SRBiz 更新 binary 和 `formats: [webp]`，执行隔离非增量构建及 390/768/1440 浏览器验证；代表页面图片传输量未下降至少 30% 时保持禁用
- [ ] 6.3 只有 Core 实施、专项证据、SRBiz 独立验收和所有必需授权均完成后，才同步或归档 OpenSpec；提交、推送、发布和部署继续分别授权
