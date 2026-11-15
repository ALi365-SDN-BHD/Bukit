# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。提交带 pathspec。

## 0. 准备

- [x] `codex-workflow-policy.v1.json` 的 `bukit-new-command-plan` 覆盖本变更；
      `codex-workflow-self-test.sh` 通过；`closure` 无未映射文件。

## 1. T1 缺陷修复

- [ ] 导航模块链接（单个 `/` 开头）和标签索引 `term.url` 加 `base_url`。
- [ ] 内容派生文本 `html.escape`；`page.content` 不转义。
- [ ] `ListCardPartial` 删除 `country`、`industry`。
- [ ] 测试：子路径导航、外链不变、标签链接、标题转义、无业务字段。

## 2. T2 外观与文字

- [ ] 模板读 `site.params.labels.*`，英文兜底；去掉 “Starter theme” 等 eyebrow。
- [ ] `SiteYaml.yaml` 写英文 `labels`；`StarterSiteScaffold` 对简体中文写中文。
- [ ] 样式：简洁内容型、深色模式、窄屏折叠菜单；保留两行颜色变量。
- [ ] 测试：缺省英文、中文站点构建后显示 `首页`、`zh-TW` 为英文、样式契约、
      无外部资源、菜单按钮 `hidden`。
- [ ] CHANGELOG（三语）、quick start。

## 3. 收尾

- [ ] 专项测试通过；`openspec validate improve-starter-theme --strict`。
- [ ] 一次 delta review，无 Critical/Important。
