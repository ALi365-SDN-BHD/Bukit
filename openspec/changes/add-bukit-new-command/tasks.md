# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。每个任务的提交都带 pathspec：
`git commit -m "…" -- <本任务路径>`；不要用不带路径的 `git commit`。

## 0. 批准与准备

- [x] G0：已批准 **Path A**（2026-11-16）：直接把 `new` 加入 Core，接受
      Labs-first 例外。证据：`design.md` Open Questions 的 G0 记录。
- [ ] 在 `scripts/checks/codex-workflow-policy.v1.json` 增加
      `bukit-new-command-contract` 规则，覆盖本变更、计划、资源、源码、测试、
      文档；跑 `codex-workflow-self-test.sh`。
- [ ] `git status --short` 只含本变更文件；`git diff --cached --quiet` 和
      `git diff --quiet` 都通过。出现其他改动就由所有者提交/stash/确认后再
      开始，不得改动。`guide/dev/cli.md` 同时是 T05 文件，先解决归属；
      BukitJalil 的 `docs/plans/2026-11-12-media-upload-design.md` 不属于本
      变更，跨仓实施前由所有者处理。
- [ ] `codex-workflow.py closure` 无未映射文件。`queue init` 只创建空队列；
      T01–T07 由实施者在开始任务时 `queue acquire` 加入。
- [ ] 基线测试通过并 `cache record`；不改代码的基线取证。

## 1. Core internal scaffold

- [ ] `StarterSiteScaffold` 读取 `Bukit.Cli` 的 StarterTheme/StarterSite
      资源；缺失资源抛错。
- [ ] 目标检查、复制主题/内容/.gitignore、`YamlStream` 写 site.yaml；
      用单个 `created` 列表记录目录和文件，失败倒序回滚；不用
      `FindMissingDirectories`，遇 `null` 父路径停止。
- [ ] 新增 `Resources/StarterSite/SiteYaml.yaml` 示例内容与
      `StarterTheme/LICENSE`、`og-default.png` 资源。
- [ ] `Bukit.Cli.csproj` 的 `EmbeddedResource` 加 `Resources\StarterSite\**`；
      不改 `Bukit.Cli.Shared.csproj`。
- [ ] 测试：资源存在、缺失资源抛错、复制映射、无空文件、先写嵌套文件再
      触发缺失资源的回滚、目标原本为空时仍为空、子目录和父目录回滚；
      回滚清理异常用 `shortcut:` 注释记录限制，不吞异常。

## 2. 主题占位符与转义

- [ ] 删除 `HeaderPartial.html` 的 `{{-- bukit:brand --}}` 行；不在 scaffold
      里做占位符替换。
- [ ] `HeaderPartial.html`、`FooterPartial.html`、`IndexTemplate.html` 对
      `site.title`、`site.description`、`site.params.*` 使用 `html.escape`。
- [ ] 测试：资源不含 `{{--`；标题含 `&`、`<` 时页头/页脚/首页输出转义文本；
      `<title>` 和 JSON-LD 已安全，内容派生字段的既有缺口记录为 Known Debt。

## 3. 命令与 YAML

- [ ] `NewCommand`：`CliArgumentSpec Required: true`、title/language 校验、
      slug、成功输出；用户错误抛 `CommandArgumentException`，不自定义异常。
- [ ] `StarterSiteScaffold`：用 `YamlStream` 修改 `SiteYaml.yaml`；不写
      `site.url`；`content.media.onFailure: keep`。
- [ ] `BukitCliSpecs` / `BukitCliDescriptors` 注册 `new`。
- [ ] 测试：默认可构建、自定义 title/language、空白 `--title`、非法输入、
      非空/文件/断链符号链接目标、断链目标带尾斜杠、`Résumé` slug 与
      BukitJalil 一致、确定性、无 `.git`/`dist`、失败回滚。

## 4. 命令面契约

- [ ] `CoreBoundaryTests` 白名单加入 `new`；`CliProgramFlowTests` 覆盖
      resolve/dispatch。
- [ ] `bukit --help` 和 completion 能看到 `new`。
- [ ] `Bukit.Architecture.Tests` 通过。

## 5. 文档与变更记录

- [ ] README（en / zh-CN / ms）命令表和 quick start；每张表按该文件列数加行。
- [ ] `guide/user/01-quick-start.md`、`guide/user/12-cli-reference.md`、
      `guide/dev/cli.md`、`guide/skills/bukit-cli-reference/SKILL.md`、
      `guide/skills/using-bukit/SKILL.md`。
- [ ] 两个 SKILL.md 把“twelve static commands”改为 thirteen，并更新
      `using-bukit` 的 scaffolding 说明。
- [ ] `guide/dev/cli.md` 的 “BukitJalil consumer contract” 节已在 HEAD
      `8cbe72cd` 提交；在表里追加 `new` 行（退出码 0/2/1、生成文件集合、
      `content.sources`/`content.media.onFailure`；测试 `NewCommandTests`）；
      只追加，不覆盖。若实施时该节被回退或 Bukit 尚未发布 `new`，放到 Part B
      第 1 步和 `MIN_BUKIT_VERSION` 一起改。
- [ ] CHANGELOG（en / zh-CN / ms）。
- [ ] `cli-docs-sync.sh`、`docs-consistency.sh`、`no-core-drift.sh` 通过。

## 6. OpenSpec 与验证

- [ ] `openspec validate add-bukit-new-command --strict --no-interactive`。
- [ ] 运行 closure 输出的 `specialtyTests` 并集；现有 policy 可能要求完整的
      Cli、Config、Rendering、Architecture 测试，按输出执行，不要截断。
- [ ] 如获授权：Native AOT `bukit` 构建并跑一次 `bukit new`。
- [ ] 一次 delta-only 统一审查；Critical/Important 修复后复验。
- [ ] 实施完成后同步 `openspec/specs/cli-scaffold/spec.md` 并归档变更；
      归档以实际证据为前提。
