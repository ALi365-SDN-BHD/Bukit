# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。本变更属于 Core 变更：第 0 节全部完成、并且
用户明确确认作为单独的 Core 任务实施之前，不开始第 1 节及以后的任何事项。

## 0. 准备

- [x] 0.1 用户确认 design.md 的决定 1–7（2026-11-19 逐项确认）；结论已写进 proposal.md 的
      "批准范围"。决定 7 选的是"两种都支持"，和原推荐方案不同，已加入 design.md 决定 13 和
      spec.md 的导航 requirement。
- [ ] 0.2 用户明确授权以单独的 Core 任务实施（AGENTS.md 的 Website/Core 隔离规则）。
- [ ] 0.3 写一个测试确认 `mode: data` 的 Notion 文档里 `file`/`files` 字段会被
      `ContentImageRewritePipeline` 本地化。没有覆盖到的话，把扩展 `ImageLocalizeStage`
      列为本变更的前置事项，再请用户确认。
- [ ] 0.4 用 `python3 scripts/checks/codex-workflow.py closure` 生成验证闭包，确认没有未映射
      的文件，写下本变更的专项测试命令；用 `queue init` 建立任务队列。

## 1. 配置

- [ ] 1.1 `Bukit.Config` 新增 `miniprogram` 配置节（`enabled`、`outputDir`、`maxListItems`、
      `datasets[]`）及校验：名称格式、唯一性、`source` 存在且 `mode` 匹配 `kind`、`sort` 字段、
      `types` 取值。每条 spec 场景至少一条测试。
- [ ] 1.2 `ConfigJsonSchemaGenerator` 和 `schemas/` 下生成的 JSON Schema 同步；public-api-drift
      和 analyzer ratchet 通过。

## 2. 数据集投影

- [ ] 2.1 新的内置插件（默认关闭），收集五类数据：collection（列表 + 详情）、module、dataIndex、
      records、taxonomy、menu，以及自动生成的 site 数据集；只输出声明的字段。
- [ ] 2.1a `kind: menu` 读取 `site.yaml` 的 `menus`（按语言，保持顺序和层级）；测试覆盖
      "Menu from site.yaml""Unknown menu"两个场景，并断言网站自己的导航输出不变。
- [ ] 2.2 可见性过滤（`enabled`、草稿、发布时间、过期、`Indexable`），测试覆盖"在 Notion 里
      取消 enabled"场景。
- [ ] 2.3 确定排序（`sort` 加 `id`）；测试：provider 改变行顺序后，输出和哈希不变。
- [ ] 2.4 专用的 `JsonSerializerContext`：UTF-8 无 BOM、单个 `\n`、字段按声明顺序；测试在 unix
      上断言字节，Windows 结果由 GitHub Actions 的 Windows runner 验证。

## 3. 正文和媒体

- [ ] 3.1 把 WechatSync `ContentProcessor` 里通用的 HTML 清洗规则提取成 internal 共享 helper；
      WechatSync 现有测试不变且通过。
- [ ] 3.2 小程序正文清洗：标签和属性白名单、iframe 转链接、`<details>` 展开、站内链接加
      `data-bukit-id`；每条 spec 场景至少一条测试。
- [ ] 3.3 图片和链接转成绝对 HTTPS 地址；残留的签名 URL 让构建失败；`site.url` 校验。

## 4. 版本目录

- [ ] 4.1 内容哈希命名、一级目录、二级目录、`catalogHash`、确定的 `generatedAt`；测试覆盖
      "一条记录变化""正文变化""重建不变""多语言"四个场景。
- [ ] 4.2 投影计划和构建清单接管 `mp/`；过期文件清理；关闭输出时删除整个目录；与静态文件
      冲突时构建失败。
- [ ] 4.3 增量构建下的哈希与全量构建一致。

## 5. 校验

- [ ] 5.1 `required`、`types` 和 `maxListItems` 校验，诊断码 `miniprogram-dataset-invalid`，
      消息带上数据集、记录 id 和字段。

## 6. 文档

- [ ] 6.1 `guide/user/` 新增小程序数据章节：配置参考、Notion 建模对照表（`data/*.yaml` 和
      `menus` 如何搬到 Notion 数据库）、输出布局、目录格式。
- [ ] 6.2 小程序端同步协议（design.md 那一节），附一份最小的参考实现伪代码；写明 10MB 缓存、
      并发和 404 重拉目录的处理。
- [ ] 6.3 部署说明：ICP 备案的 HTTPS 合法域名、目录禁用缓存、Notion 修改后触发重建的 CI 示例
      （定时、手动；webhook 作为可选方案）。
- [ ] 6.4 CHANGELOG 三语。

## 7. 收尾

- [ ] 7.1 专项测试通过；`openspec validate add-miniprogram-data --strict` 通过。
- [ ] 7.2 一次 delta review（独立子代理）：Critical/Important 修复后复审，Minor 记录不阻塞。
