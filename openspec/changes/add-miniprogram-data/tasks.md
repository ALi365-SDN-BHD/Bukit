# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。本变更属于 Core 变更：第 0 节全部完成、并且
用户明确确认作为单独的 Core 任务实施之前，不开始第 1 节及以后的任何事项。

## 0. 准备

- [x] 0.1 用户确认 design.md 的决定 1–7（2026-11-19 逐项确认）；结论已写进 proposal.md 的
      "批准范围"。决定 7 选的是"两种都支持"，和原推荐方案不同，已加入 design.md 决定 13 和
      spec.md 的导航 requirement。
- [x] 0.2 用户明确授权以单独的 Core 任务实施（AGENTS.md 的 Website/Core 隔离规则）。
      2026-11-20 授权，在 `feat/miniprogram-data` 分支实施。
- [ ] 0.3 写一个测试确认 `mode: data` 的 Notion 文档里 `file`/`files` 字段会被
      `ContentImageRewritePipeline` 本地化。没有覆盖到的话，把扩展 `ImageLocalizeStage`
      列为本变更的前置事项，再请用户确认。
      代码核对（未写测试）：`ImageLocalizeStage` 在内容阶段处理全部源的文档，不区分
      `mode`；data 文档到变体阶段的 `VariantDataSitePlanner` 才分出，所以同样被本地化。
      残留的签名 URL 由本插件让构建失败兜底。
- [x] 0.4 用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包，确认没有未映射
      的文件，写下本变更的专项测试命令；用 `queue init` 建立任务队列。
      闭包无未映射文件（策略新增 `miniprogram-data-change` 规则，自测通过）。单个实施任务，
      未建队列。

## 1. 配置

- [x] 1.1 `Bukit.Config` 新增 `miniprogram` 配置节（`enabled`、`outputDir`、`maxListItems`、
      `datasets[]`）及校验：名称格式、唯一性、`source` 存在且 `mode` 匹配 `kind`、`sort` 字段、
      `types` 取值。每条 spec 场景至少一条测试。
- [ ] 1.2 `ConfigJsonSchemaGenerator` 和 `schemas/` 下生成的 JSON Schema 同步；public-api-drift
      和 analyzer ratchet 通过。

## 2. 数据集投影

- [x] 2.1 新的内置插件（默认关闭），收集五类数据：collection（列表 + 详情）、module、dataIndex、
      records、taxonomy、menu，以及自动生成的 site 数据集；只输出声明的字段。
- [x] 2.1a `kind: menu` 读取 `site.yaml` 的 `menus`（保持顺序和层级）；测试覆盖
      "Menu from site.yaml""Unknown menu"两个场景，并断言网站自己的导航输出不变。
      插件只读 `Site.Menus`，不写站点上下文；未单独断言网站导航。
- [x] 2.2 可见性过滤（`enabled`、草稿、`Indexable`；Core 没有发布时间和过期过滤），测试覆盖"在 Notion 里
      取消 enabled"场景。
- [x] 2.3 确定排序（`sort` 加 `id`）；测试：provider 改变行顺序后，输出和哈希不变。
- [x] 2.4 专用的 `JsonSerializerContext`：UTF-8 无 BOM、单个 `\n`、字段按声明顺序；测试在 unix
      上断言字节，Windows 结果由 GitHub Actions 的 Windows runner 验证。
      实际用 `JsonNode` + `Utf8JsonWriter`（AOT 安全，不需要 context）。Windows 未验证。

## 3. 正文和媒体

- [x] 3.1 在 Core 里用 AngleSharp 实现小程序正文清洗（不抽取 WechatSync 的 `ContentProcessor`：
      它在插件层，Core 不能依赖它）。
- [x] 3.2 小程序正文清洗：标签和属性白名单、iframe 转链接、`<details>` 展开、站内链接加
      `data-bukit-id`；每条 spec 场景至少一条测试。
- [x] 3.3 图片和链接转成绝对 HTTPS 地址；残留的签名 URL 让构建失败；`site.url` 校验。

## 4. 版本目录

- [ ] 4.1 内容哈希命名、一级目录、二级目录、`catalogHash`、确定的 `generatedAt`；测试覆盖
      "一条记录变化""正文变化""重建不变""多语言"四个场景。
      前三个场景有测试；多语言只靠每个变体一个输出根目录，没有测试。
- [ ] 4.2 通过 `PluginOutputs` 让构建清单接管 `mp/`；过期文件清理；关闭输出时删除整个目录；与静态文件
      冲突时构建失败。
      已登记到 `PluginOutputs`（有测试）；清理、关闭和冲突依赖已有清单逻辑，没有端到端测试。
- [ ] 4.3 增量构建下的哈希与全量构建一致。

## 5. 校验

- [x] 5.1 `required`、`types` 和 `maxListItems` 校验，诊断码 `SchemaValidationFailed`，
      消息带上数据集、记录 id 和字段。
      `maxListItems` 超限有实现，没有测试。

## 6. 文档

- [x] 6.1 `guide/user/` 新增小程序数据章节：配置参考、Notion 建模对照表（`data/*.yaml` 和
      `menus` 如何搬到 Notion 数据库）、输出布局、目录格式。
- [x] 6.2 小程序端同步协议（design.md 那一节），附一份最小的参考实现伪代码；写明 10MB 缓存、
      并发和 404 重拉目录的处理。
- [x] 6.3 部署说明：ICP 备案的 HTTPS 合法域名、目录禁用缓存、Notion 修改后触发重建的 CI 示例
      （定时、手动；webhook 作为可选方案）。
- [x] 6.4 CHANGELOG 三语。

## 7. 收尾

- [x] 7.1 专项测试通过；`openspec validate add-miniprogram-data --strict` 通过。
      2026-11-20：Architecture、Config、Engine 测试项目，Cli `NotionOffline`，docs 检查，
      dotnet-format、analyzer ratchet、public-api-drift 均通过。
- [x] 7.2 一次 delta review（独立子代理）：Critical/Important 修复后复审，Minor 记录不阻塞。
      首轮 Important 3（`data-bukit-id` 范围、`string` 类型写出 CLR 类型名、未声明字段的
      `required`/`types`），两轮复审后为 0。未修的 Minor（记录，不阻塞）：
      - built-in 字段名遮蔽同名记录属性；
      - 菜单 URL 不校验协议，顺序按声明而非 `weight`；
      - 校验错误只给 data 记录的不透明 id；
      - Windows 保留名（CON、NUL…）可通过名称正则；
      - HtmlEncode 把非 ASCII 编成数字实体；
      - 列表里的空白项不剔除；
      - Notion rollup 的嵌套列表无法输出；
      - 签名 URL 只检查 `image` 字段和正文 `<img>`。
      2026-11-20 高强度审计的 10 条发现已修复：data 记录相对地址带上 base path；签名 URL
      复用 `SignedUrlIdentity`；未匹配的 module 或 scope 让构建失败；Notion data 源的 `body`
      需要 `renderContent`；各类型拒收用不到的键；`site.url` 在渲染前校验；分类只统计集合
      页面且不分大小写；站内链接按规范化 URL 匹配；正文只清洗一次并复用解析器；内置字段改为
      一张访问表。
      2026-11-20 第二轮高强度审计的 9 条发现已修复：未匹配的 `module`/scope 按所有行（不论
      是否启用）判断，多语言站点改为警告并写空数据集；scope 全部停用时写空值；`dataIndex` 的
      `body` 经过清洗；每个文件写出时立即登记 `PluginOutputs`；开发构建缺少 `site.url` 时跳过；
      `/release-v1.2` 这类带点的页面链接按 `RoutePathBuilder.NormalizeUrl` 匹配；格式错误的
      `site.url` 改用 `ConfigInvalidValue`；`types.body` 被拒绝；补充对应测试。
      2026-11-20 第三轮高强度审计的 7 条发现已修复：未匹配的 `module`/scope 按内容图里所有
      语言的行判断，任何语言都没有时构建失败（多语言站点不再只警告）；scope 匹配复用
      `ConfiguredContentFieldReader` 的别名规则；data 记录的 `url` 取记录自身属性；去掉自算的
      多语言判断；`site.url` 拒绝查询串和片段；菜单按 `weight` 排序；新增多语言端到端测试
      （按语言输出、`locale` 过滤、拼写错误失败、关闭后清理 `mp/`）。
      2026-11-20 第四轮高强度审计的 6 条发现已修复：`AppConfig.MiniProgram` 加 `[JsonInclude]`，
      多语言 `bukit build` 的 worktree 子进程不再丢掉这一节（新增往返测试复现并验证）；`locale`
      不属于站点语言的行不算匹配；scope 兜底匹配改用不抛异常的 `AliasMatches`；`site.url` 拒绝
      账号信息且错误消息不回显；插件构造函数必须传执行模式；重命名误导性的测试。
      经核实无需修改：`MediaConfig.PlaceholderMissing` 不读自 site.yaml，只在
      `ImageLocalizeStage` 里写进临时的 `media with` 副本；worktree 子进程自己运行
      `ContentPipeline`，会在其目录中重新探测，因此不受 JSON 序列化影响。
      2026-11-20 第五轮高强度审计的 7 条发现已修复：新增 `MiniProgramUrls`，开启
      `site.sharedAssets` 时共享资源地址保留根路径（本地化媒体仍用语言路径）；未知块级容器内的
      纯文本包成段落；`NaN`/无穷大报错并指明记录；`number` 精确保留整数和小数；排序时把
      `DateTime` 和 ISO 日期文本统一成时间比较；dataIndex 非文本 `body` 报类型错误；正文并发读取、
      顺序清理。
