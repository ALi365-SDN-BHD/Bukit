# `bukit new` 初始网站命令：实施方案与计划书

> **执行方式：** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to
> implement this plan task-by-task. 每个任务单独验证、单独提交；未获得授权不
> commit、push、发布或运行 full/release gate。
>
> **Goal:** 在 Bukit Core 增加
> `bukit new <dir> [--title <text>] [--language <tag>]`，生成一个可立即
> `config check` / `doctor` / `build` 的 Markdown starter，并让 BukitJalil
> 能在升级内置 Bukit 后删除自己的 starter 副本。
>
> **Architecture:** StarterTheme 资源继续放在
> `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/`；新增
> `Resources/StarterSite/` 放站点级示例内容和 `SiteYaml.yaml`。scaffold 代码
> 放在 `Bukit.Cli` 的 internal 类型里；`Bukit.Cli.csproj` 嵌入两组资源。
> `site.yaml` 从资源模板加载，用 `YamlStream` 设置标题/语言后保存。命令不
> 执行 git、构建或网络。
>
> **Tech Stack:** .NET 10 / C#、项目内 `CliCommandSpec`、YamlDotNet
> `YamlStream` / RepresentationModel、xUnit、OpenSpec、Bash 检查脚本。
>
> **状态：** G0 已批准 Path A（2026-11-16）；T01–T07 已执行，OpenSpec 已归档为
> `2026-11-16-add-bukit-new-command`。本文件保留原实施定义和验证证据。
> **修订：** v6（2026-11-15，吸收 v5 复核：暂存区自动拦截 + 所有提交带
> pathspec、BukitJalil consumer contract 追加 `new` 行、历史工作区快照移入
> 附录 C；v5 已吸收 v4 复核，v4 已吸收 v3 复核 #1–#3，v3 已吸收 v2 复核
> #1–#8，v2 已吸收审计 #1–#17，#18 记为已知债务）。
> **依据：** `openspec/changes/archive/2026-11-16-add-bukit-new-command/`
> （proposal、design、specs、tasks）。任务清单以归档的 `tasks.md` 为唯一
> 勾选来源。
> **代码基线：** v2.2.3。正文用文件和符号定位，不依赖行号。
> **范围：** Bukit Core 的 CLI、资源、测试、文档、白名单和 OpenSpec。
> BukitJalil 的迁移单列为 Part B，需另行授权。

---

## 0. 先读这一段

1. **G0 已批准 Path A（2026-11-16）。** 本计划直接把 `new` 加入 Core；仓库
   2026-06-13 计划的 Labs-first 要求已按“Labs 已有 `InitCommand` 原型但不可用”
   记录为例外。如果后续改变决定，按附录 A 先加一个薄的 Labs 证明，再回到 T03。
2. **只做一个命令、一个 starter。** 不做六套模板、不做 `--notion`、不做
   `--force`。Notion 多库编排由 BukitJalil 在生成后补 `content.sources`。
3. **代码放 `Bukit.Cli`，不放 `Bukit.Cli.Shared`。** Path A 没有 Labs 调用；
   `Bukit.Cli.Tests` 已有 `InternalsVisibleTo`。改成 public 会进公共 API 基线。
4. **YAML 用资源模板 + `YamlStream`。** 不用反射式 `SerializerBuilder`；
   CLI 是 AOT，模板也更容易审查。
5. **错误码分开。** 用户输入/目标错误用 `CommandArgumentException`，Program
   映射为 2；资源缺失/IO 失败走全局处理，返回 1。不要自定义异常。
6. **单写者与干净工作区。** 同一时刻只有一个实施者写仓库。T00 建队列；只读的
   调用方检索、测试清单可以并行。开始 T00 前先 `git status --short`：只能出现
   本变更的文件；其他改动由所有者提交、stash 或确认后再开始。任何不属于本
   变更的改动都不要碰；先确认是谁在写，否则 closure 会混入 release/CI 规则。
   T05 修改的文档（`guide/dev/cli.md`、`guide/skills/bukit-cli-reference/SKILL.md`、
   `guide/user/12-cli-reference.md` 等）如果已有别人的改动，先解决归属。每个
   任务的提交都必须带 pathspec：`git commit -m "…" -- <本任务路径>`；不带路径
   的 `git commit` 会把暂存区里别人的文件一起提交。
7. **不跑 full/release gate，但要跑 closure 的输出。** 现有 policy 在改动
   `Bukit.Cli`/`Bukit.Config`/测试文件时可能要求完整的
   `Bukit.Cli.Tests`、`Bukit.Config.Tests`、`Bukit.Rendering.Tests`、
   `Bukit.Architecture.Tests`；这些是专项检查，照跑。
   `scripts/test-all.sh`、`scripts/smoke-all.sh`、release gate 仍需另行授权。
8. **BukitJalil 不在本仓库改。** Part B 是发布后的独立迁移清单，不阻塞本
   计划；在 Bukit 发布前 BukitJalil 继续用 `make-starter.sh`。

## 1. 目标、非目标与验收

### 1.1 目标

- `bukit new my-site` 生成一个无手工修改即可构建的 Markdown 站点。
- 生成物与当前 BukitJalil starter 语义一致：posts、pages、data、starter 主题、
  `og-default.png`、`robots.txt`、`LICENSE`。
- 生成命令不依赖网络、git、Notion token 或用户环境。
- BukitJalil 在升级到含 `new` 的 Bukit 后，可以删除 25 个模板文件、183 行
  `make-starter.sh`、`starter.source.json` 和 199 行哈希测试。

### 1.2 非目标

- 不支持 `--notion`、多数据库、content/data 角色编排。
- 不实现 `bukit init`、多模板向导、主题注册表、远程主题或主题安装。
- 不执行 `git init`、提交、构建、部署或网络请求。
- 不引入反射式 YAML serializer；不把 scaffold 变成 public API。
- 不修改 BukitJalil 的站点创建、权限闸门、构建和发布流程。
- 不解决 `guide/skills` 的同步；它是另一条链。

### 1.3 验收标准

- `bukit new` 在 macOS、Linux、Windows 上行为一致；Windows 的符号链接测试
  按平台跳过。
- 生成站点通过 `config check`、`doctor`、`build`。
- 目标非空、目标是文件、目标是断链符号链接、标题/语言非法时，命令失败且不
  留下半成品。
- `CoreBoundaryTests` 白名单、CLI 文档同步、OpenSpec 校验通过。
- BukitJalil 迁移清单可执行；在 Bukit 发布前不删除其副本。

## 2. 现状与问题

`guide/user/01-quick-start.md` 明确写 Core 没有 scaffold 命令。官方
StarterTheme 只有 17 个嵌入资源，没有“资源 → 可构建站点”的出口。

BukitJalil `templates/starter/` 共 25 个文件：

- 17 个来自 StarterTheme；
- 8 个站点级 glue：`site.yaml`、`content/posts/welcome.md`、
  `content/pages/about.md`、`data/features-10.md`、`og-default.png`、
  `robots.txt`、`LICENSE`、`.gitignore`。

`scripts/make-starter.sh` 183 行，`packages/server/test/template.test.ts` 199
行，`templates/starter.source.json` 34 行。当前来源是 v2.1.0，而内置 Bukit
是 2.2.3；两者已经可能出现语义漂移。

Labs 的 `InitCommand`（600 行）未在 `Program.cs` 路由，且生成旧契约
（`archive` 对象、`site.url: https://example.com`、硬编码 `zh-CN`、无
`html.escape`）。不能直接提升。

`HeaderPartial.html` 第 6 行的 `{{-- bukit:brand --}}` 不是 Scriban 注释。
Scriban 注释是 `{{* ... *}}`；这个占位符在渲染时会被当作表达式并抛错。它必须
从资源中删除，不能只在复制时替换。

Core 的 `HeaderPartial.html`、`FooterPartial.html`、`IndexTemplate.html`
对 `site.title` / `site.params.*` 没有 `html.escape`；BukitJalil 的脚本额外
补了这一步。`new` 必须先修资源，否则会把转义问题带回来。

`Bukit.Cli.Shared` 没有 `InternalsVisibleTo`。把 internal scaffold 放在那里，
`bukit` 和 `Bukit.Cli.Tests` 都看不到；改成 public 会进入 public API 漂移
基线。Path A 正确的位置是 `Bukit.Cli`。

## 3. 设计决策

### A1. 命令面

```bash
bukit new <dir> [--title <text>] [--language <tag>]
```

- `<dir>` 是必填位置参数，`CliArgumentSpec` 设 `Required: true`。
- `--title` 省略时用目录名（先 `Path.TrimEndingDirectorySeparator`；仍为空
  则用 `My Site`）；提供了但 trim 后为空则拒绝，不能静默替换。
- `--language` 省略时 `en-US`。
- 不提供 `--force`；目标必须不存在或为空。
- 不提供 `--notion`；调用方生成后写 `content.sources`。

### A2. 只生成一个 starter

一个规范的 Markdown starter，不引入多模板选择。输出对齐当前 BukitJalil
starter 的语义，不要求逐字节相同。

### A3. 代码位置与资源

Path A：

- `Bukit.Cli/Commands/NewCommand.cs`：命令、title/language 校验、slug。
- `Bukit.Cli/Commands/New/StarterSiteScaffold.cs`：目标检查、资源读取、
  `YamlStream` 写 site.yaml、复制、回滚。全部 internal。
- `Bukit.Cli.csproj` 的 `EmbeddedResource` ItemGroup 增加
  `Resources\StarterSite\**`；`Resources\StarterTheme\**` 已经在里面。
- 资源逻辑名前缀：`Bukit.Cli.Resources.StarterTheme.` 和
  `Bukit.Cli.Resources.StarterSite.`。
- 不改 `Bukit.Cli.Shared.csproj`；不新增 public 类型。

Path B（附录 A）才需要把同一组 internal 代码放到 `Bukit.Labs.Cli`，或给
`Bukit.Cli.Shared` 增加对 Labs 的 `InternalsVisibleTo`；仍然不要 public。

### A4. `site.yaml` 用资源模板 + `YamlStream`

新增 `Resources/StarterSite/SiteYaml.yaml`，作为完整、可审查的 starter 配置
模板。生成时：

1. 读取嵌入的模板文本。
2. `new YamlStream().Load(new StringReader(text))`。
3. 取根 `YamlMappingNode`，设置：
   - `site.name`：slug；
   - `site.title`：`YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted }`；
   - `site.languages`：只含一个 scalar 的 `YamlSequenceNode`；
   - `theme.params.brand`、`theme.params.footer_text`：双引号标题。
4. `stream.Save(writer, assignAnchors: false)`。

不使用 `SerializerBuilder`，不拼字符串，不引入反射序列化。模板已经包含：

- `site.baseUrl: /`
- `site.seo.defaultImage: /assets/og-default.png`
- `site.collections.post.output.archive: true`（布尔）
- `content.media.onFailure: keep`
- `build: { output: dist, clean: false }`
- `theme.name: starter`
- 不写 `site.url`

`YamlStream` 输出通常是块状 sequence（`languages:\n  - zh-CN`），小的 mapping
可能变成 flow style，文件末尾可能多一行 `...`。这些都是合法 YAML；模板里的
注释只给维护者看，`YamlStream.Save` 不会保留它们。规范和测试只断言**解析后的
值**，不匹配文本样式；“可审查”指的是仓库里的模板本身，不是生成后的文件。

### A5. 目标与原子写入

- 目标必须不存在，或存在且为空目录。
- 先规范化目标：
  `var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));`。
  这样 `broken/` 和 `broken` 得到同一个路径；根目录 `/` 不会被削掉。
- 拒绝文件、非空目录、目标本身是符号链接/重解析点；**包括指向不存在目标的
  断链**。先检查 `new DirectoryInfo(root).LinkTarget is not null`，再检查
  `Exists`。不先 trim 的话，`broken/` 的 `LinkTarget` 是 `null`，会绕过检查，
  最后在 `Directory.CreateDirectory` 抛 IO 异常，退出码变成 1。
- 不再单独拒绝根目录和用户主目录：它们不可能为空，非空规则已经拒绝；单独
  的 ordinal 比较还会漏掉尾斜杠和大小写差异，属于死分支。
- **只用一个 `created` 列表**，按创建顺序记录目录和文件。建目录 helper 从目标
  向上收集尚不存在的目录，再逐级创建并记录；遇到 `Path.GetDirectoryName`
  返回 `null` 就停止。失败时倒序处理：文件直接删，目录只在为空时删。不能
  `ClearDirectory(root)`，否则会删掉其他程序在写入期间放进来的文件（例如
  Finder 的 `.DS_Store`）。
- 不要用 `FindMissingDirectories`：Windows 上 `Path.GetDirectoryName("Z:\\")`
  返回 `null`，`Directory.Exists(null)` 为 false，循环会死循环。
- 目标原本存在且为空时，失败后目录仍在且为空；目标原本不存在时，失败后删除
  本次创建的最深目录及其新建父目录。

### A6. 输入校验

- 标题：`--title` 未提供时用目录名（先
  `Path.TrimEndingDirectorySeparator`；仍为空则 `My Site`）；提供了但 trim
  后为空时拒绝。标题 1–200 字符，不能含控制字符。
- 语言：复用 `SiteConfigValidator.IsSafeLanguageTag`。把它从 `private` 改为
  `internal`，并在 `Bukit.Config/InternalsVisibleTo.cs` 加
  `[assembly: InternalsVisibleTo("bukit")]`。不复制第二份正则。
- `site.name`：ASCII slug（NFKD、小写、非 `a-z0-9` 转 `-`、去首尾、截 64、
  空则 `site`），规则与 BukitJalil 的 `siteSlug` 一致。slug 逻辑放
  `NewCommand` 私有方法，不单独建文件。

### A7. 删除 brand 占位符，不做替换

`{{-- bukit:brand --}}` 不是 Scriban 注释，不能留在资源里。T02 直接删除
`HeaderPartial.html` 第 6 行：

```html
      {{-- bukit:brand --}}
```

复制逻辑不再执行 `Replace("{{-- bukit:brand --}}", "")`。品牌文本由
`theme.params.brand` 分支输出，不需要占位符。

### A8. 命令不做副作用

不执行 `git init`、不提交、不构建、不部署、不联网、不读取
`NOTION_TOKEN`。成功时打印绝对路径和下一步命令，路径加引号，避免
`~/My Sites/x` 粘进 shell 失败：

```text
Created: /abs/path
Next:
  cd "/abs/path"
  bukit config check
  bukit doctor
  bukit build
  bukit preview --dir dist
```

### A9. Core 命令面与治理

需要同时改：

- `Bukit.Cli/Cli/BukitCliSpecs.cs`
- `Bukit.Cli/Cli/BukitCliDescriptors.cs`
- `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs`
- `tests/Bukit.Cli.Tests/CliProgramFlowTests.cs`

Core 命令白名单是精确数组，新增 `new` 必须显式修改，不能只靠 registry。

### A10. 跨仓 rollout

BukitJalil 迁移在 Bukit 发布后进行，见第 9 节 Part B。`guide/skills` 同步不在
本变更范围内。

## 4. 命令契约

| 项 | 规则 |
|---|---|
| 用法 | `bukit new <dir> [--title <text>] [--language <tag>]` |
| `<dir>` | 必填；不存在或空目录；不能是文件、非空目录、符号链接/重解析点（含断链） |
| `--title` | 可选；默认目录名；trim 后 1–200 字符；禁止控制字符；提供但空白时拒绝 |
| `--language` | 可选；默认 `en-US`；安全语言标签 |
| 成功输出 | 绝对路径 + 下一步命令；退出 0 |
| 用户输入/目标错误 | 抛 `CommandArgumentException`，Program 映射为退出 2 |
| 资源缺失/IO/磁盘错误 | 全局处理，退出 1 |
| 未预期异常 | 全局处理，退出 1 |

生成文件：

```text
site.yaml
.gitignore
content/posts/welcome.md
content/pages/about.md
data/features-10.md
themes/starter/theme.yaml
themes/starter/LICENSE
themes/starter/assets/style.css
themes/starter/assets/og-default.png
themes/starter/static/robots.txt
themes/starter/layouts/layouts/base.html
themes/starter/layouts/partials/{seo,header,footer,list-card,pagination-nav}.html
themes/starter/layouts/pages/{index,list,page,post,pagination,taxonomy-index,taxonomy-term,search}.html
themes/starter/layouts/bukit.templates.yaml
```

`site.yaml` 的语义见 `openspec/changes/add-bukit-new-command/design.md` D4。
生成物不包含 `site.url`、`dist/`、`.git/` 或 BukitJalil 文件。

## 5. 组件与文件

| 文件 | 动作 | 职责 |
|---|---|---|
| `src/Bukit-Core/Bukit.Cli/Commands/NewCommand.cs` | 新增 | 参数、title/language 校验、slug、成功输出 |
| `src/Bukit-Core/Bukit.Cli/Commands/New/StarterSiteScaffold.cs` | 新增 | 资源读取、目标检查、YAML、复制、回滚 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/SiteYaml.yaml` | 新增 | site.yaml 模板 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/WelcomePost.md` | 新增 | 示例文章 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/AboutPage.md` | 新增 | 示例页面 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/FeatureData.md` | 新增 | 示例 data |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/LICENSE` | 新增 | 主题 MIT 许可 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/og-default.png` | 新增 | 1200×630 分享图 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/HeaderPartial.html` | 修改 | 删除占位符 + 转义 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/FooterPartial.html` | 修改 | 转义 |
| `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/IndexTemplate.html` | 修改 | 转义 |
| `src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj` | 修改 | 嵌入 StarterSite 资源 |
| `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs` | 修改 | 注册 `new` |
| `src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs` | 修改 | 绑定 handler |
| `src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs` | 修改 | 语言校验改 internal |
| `src/Bukit-Core/Bukit.Config/InternalsVisibleTo.cs` | 修改 | 对 `bukit` 可见 |
| `tests/Bukit.Cli.Tests/NewCommandTests.cs` | 新增 | 命令行为 |
| `tests/Bukit.Cli.Tests/StarterSiteScaffoldTests.cs` | 新增 | 资源/复制/回滚 |
| `tests/Bukit.Rendering.Tests/StarterThemeTemplateResourceTests.cs` | 修改 | 占位符/转义测试 |
| `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs` | 修改 | 白名单 |
| `tests/Bukit.Cli.Tests/CliProgramFlowTests.cs` | 修改 | resolve/dispatch |
| `README.md` / `README.zh-CN.md` / `README.ms.md` | 修改 | 命令表 + quick start |
| `guide/user/01-quick-start.md` | 修改 | 用 `new` 起步 |
| `guide/user/12-cli-reference.md` | 修改 | 命令行/参数 |
| `guide/dev/cli.md` | 修改 | 命令行/参数 |
| `guide/skills/bukit-cli-reference/SKILL.md` | 修改 | 命令行/参数 + 命令数量 |
| `guide/skills/using-bukit/SKILL.md` | 修改 | 命令清单 + scaffolding 说明 |
| `CHANGELOG.md` / `.zh-CN.md` / `.ms.md` | 修改 | 变更记录 |
| `scripts/checks/codex-workflow-policy.v1.json` | 修改 | 新规则 |

## 6. 任务

每个任务结束跑一次引用的测试并单独提交（需授权）。源码路径以
`src/Bukit-Core/` 为根，测试以 `tests/` 为根。

### T00：批准、闭包、基线与队列

**Files:**
- Modify: `scripts/checks/codex-workflow-policy.v1.json`
- Create: `docs/plans/2026-11-15-bukit-new-command-implementation-plan.zh-CN.md`
- Create: `openspec/changes/add-bukit-new-command/**`

**Step 0：工作区前置检查**

```bash
git status --short
git diff --cached --quiet || {
  echo "staged changes exist; owner must commit or stash them before T00" >&2
  git diff --cached --stat >&2
  exit 1
}
git diff --quiet || {
  echo "unstaged tracked changes exist; owner must commit or stash them before T00" >&2
  git diff --stat >&2
  exit 1
}
```

Expected：`git status --short` 只出现本变更的文件；两条 `git diff --quiet`
检查都通过（暂存区为空、已跟踪文件没有未暂存改动）。任何其他改动都停止，
由所有者提交、stash 或确认后再开始。历史工作区快照只放在附录 C，不要拿旧
清单判断当前状态。

**Step 1：复核 G0 记录**

`design.md` 的 Open Questions 已记录 **G0 approved: Path A（2026-11-16）**：
直接把 `new` 加入 Core，接受 Labs-first 例外。T00 只复核这条记录；如果实际
决定变成 Path B，更新记录并执行附录 A。

**Step 2：加 closure 规则**

在 `pathRules` 末尾加一条：

```json
{
  "id": "bukit-new-command-contract",
  "matches": [
    "openspec/changes/add-bukit-new-command/*",
    "openspec/changes/archive/*-add-bukit-new-command/*",
    "openspec/specs/cli-scaffold/spec.md",
    "docs/plans/2026-11-15-bukit-new-command-implementation-plan.zh-CN.md",
    "src/Bukit-Core/Bukit.Cli/Commands/NewCommand.cs",
    "src/Bukit-Core/Bukit.Cli/Commands/New/*.cs",
    "src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/*",
    "src/Bukit-Core/Bukit.Cli/Resources/StarterSite/*",
    "src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs",
    "src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs",
    "src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj",
    "src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs",
    "src/Bukit-Core/Bukit.Config/InternalsVisibleTo.cs",
    "tests/Bukit.Cli.Tests/NewCommandTests.cs",
    "tests/Bukit.Cli.Tests/StarterSiteScaffoldTests.cs",
    "tests/Bukit.Cli.Tests/CliProgramFlowTests.cs",
    "tests/Bukit.Rendering.Tests/StarterThemeTemplateResourceTests.cs",
    "tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs",
    "README.md",
    "README.zh-CN.md",
    "README.ms.md",
    "guide/user/01-quick-start.md",
    "guide/user/12-cli-reference.md",
    "guide/dev/cli.md",
    "guide/skills/bukit-cli-reference/SKILL.md",
    "guide/skills/using-bukit/SKILL.md",
    "CHANGELOG.md",
    "CHANGELOG.zh-CN.md",
    "CHANGELOG.ms.md"
  ],
  "specialtyTests": [
    "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release --filter \"FullyQualifiedName~NewCommand|FullyQualifiedName~StarterSite|FullyQualifiedName~CliProgramFlow\"",
    "dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release --filter \"FullyQualifiedName~StarterThemeTemplateResource\"",
    "dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --filter \"FullyQualifiedName~CoreBoundary\"",
    "dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release --filter \"FullyQualifiedName~StarterThemeScaffold\"",
    "bash scripts/checks/cli-docs-sync.sh",
    "bash scripts/checks/docs-consistency.sh",
    "bash scripts/checks/docs/no-core-drift.sh",
    "bash scripts/checks/dotnet-format.sh",
    "bash scripts/checks/public-api-drift.sh check",
    "bash scripts/checks/code-analysis-ratchet.sh check",
    "openspec validate cli-scaffold --type spec --strict --no-interactive"
  ],
  "contractConsumerGlobs": [
    "src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs",
    "src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs",
    "src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj",
    "src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs"
  ],
  "publicContract": false,
  "resource": "dotnet-serial"
}
```

`scripts/checks/codex-workflow-policy.v1.json` 本身已由现有 `codex-workflow`
规则覆盖，不要在新规则里重复列它。

**Step 3：验证规则与闭包**

```bash
bash scripts/checks/codex-workflow-self-test.sh

# T00 记录变更前的基线；后续 closure 都用这个 BASE，不要用会移动的 HEAD。
BASE=$(git rev-parse HEAD)
printf '%s\n' "$BASE" > /tmp/bukit-new-command-base
changed=()
while IFS= read -r f; do changed+=("$f"); done < <(
  git diff --name-only "$BASE"
  git ls-files --others --exclude-standard
)
args=()
for f in "${changed[@]}"; do args+=(--changed "$f"); done
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json "${args[@]}"
```

Expected: self-test 通过；`unmappedFiles` 为空；`specialtyTests` **至少包含**
上表，并集可能因为现有 `bukit-cli-source`/`bukit-config`/`bukit-rendering-tests`
等规则更大；按实际输出执行。不要用 `git diff --name-only HEAD | while ...`：
管道里的 `while` 在子 shell 执行，`args` 出来是空的。

**Step 4：基线**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CliProgramFlow|FullyQualifiedName~CleanCommand"
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release \
  --filter "FullyQualifiedName~StarterThemeTemplateResource"
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CoreBoundary"
dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~StarterThemeScaffold"
```

用 `codex-workflow.py cache record` 记录 GREEN 基线。

**Step 5：队列**

```bash
python3 scripts/checks/codex-workflow.py queue init \
  --state /tmp/bukit-new-command-queue.json
```

`queue init` 只创建空队列；T01–T07 由实施者在开始任务时用
`queue acquire --task Txx` 加入并取得写者槽。

**Step 6：提交**

```bash
git add docs/plans/2026-11-15-bukit-new-command-implementation-plan.zh-CN.md \
  openspec/changes/add-bukit-new-command \
  scripts/checks/codex-workflow-policy.v1.json
git commit -m "docs: plan bukit new command" -- \
  docs/plans/2026-11-15-bukit-new-command-implementation-plan.zh-CN.md \
  openspec/changes/add-bukit-new-command \
  scripts/checks/codex-workflow-policy.v1.json
```

### T01：Core internal scaffold

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/New/StarterSiteScaffold.cs`
- Create: `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/SiteYaml.yaml`
- Create: `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/WelcomePost.md`
- Create: `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/AboutPage.md`
- Create: `src/Bukit-Core/Bukit.Cli/Resources/StarterSite/FeatureData.md`
- Create: `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/LICENSE`
- Create: `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/og-default.png`
- Modify: `src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj`
- Test: `tests/Bukit.Cli.Tests/StarterSiteScaffoldTests.cs`

**Step 1：写失败测试**

覆盖：

```csharp
[Fact]
public void Resources_LoadTextAndBinary()
{
    Assert.Contains("<!DOCTYPE html>", StarterSiteScaffold.GetThemeText("BaseLayout.html"));
    Assert.NotEmpty(StarterSiteScaffold.GetThemeBytes("og-default.png"));
    Assert.Throws<InvalidOperationException>(
        () => StarterSiteScaffold.GetThemeText("missing.html"));
}

[Fact]
public void Scaffold_CopiesThemeContentAndGitignore()
{
    var target = Path.Combine(_root, "site");
    StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US");
    Assert.True(File.Exists(Path.Combine(target, "themes/starter/theme.yaml")));
    Assert.True(File.Exists(Path.Combine(target, "themes/starter/LICENSE")));
    Assert.True(File.Exists(Path.Combine(target, "themes/starter/assets/og-default.png")));
    Assert.True(File.Exists(Path.Combine(target, "themes/starter/static/robots.txt")));
    Assert.True(File.Exists(Path.Combine(target, "content/posts/welcome.md")));
    Assert.True(File.Exists(Path.Combine(target, "site.yaml")));
}

[Fact]
public void Scaffold_RollsBackWhenResourceMissing()
{
    var target = Path.Combine(_root, "rollback");
    var entries = new[]
    {
        // 先写一个嵌套文件，确保 layouts/partials 等子目录也被创建；
        // 再遇到缺失资源，回滚必须把这些子目录一起清掉。
        new StarterFile("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
        new StarterFile("missing.html", "missing.txt")
    };
    Assert.Throws<InvalidOperationException>(
        () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US", entries));
    Assert.False(Directory.Exists(target));
}

[Fact]
public void Scaffold_ExistingEmptyTargetStaysEmptyOnFailure()
{
    var target = Path.Combine(_root, "empty");
    Directory.CreateDirectory(target);
    var entries = new[]
    {
        new StarterFile("HeaderPartial.html", "themes/starter/layouts/partials/header.html"),
        new StarterFile("missing.html", "missing.txt")
    };
    Assert.Throws<InvalidOperationException>(
        () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US", entries));
    Assert.True(Directory.Exists(target));
    Assert.Empty(Directory.EnumerateFileSystemEntries(target));
}

[Fact]
public void Scaffold_RefusesNonEmptyTarget()
{
    var target = Path.Combine(_root, "non-empty");
    Directory.CreateDirectory(target);
    File.WriteAllText(Path.Combine(target, "keep.txt"), "keep");
    Assert.Throws<CommandArgumentException>(
        () => StarterSiteScaffold.Create(target, "My Site", "my-site", "en-US"));
    Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "keep.txt")));
}
```

**Step 2：跑测试确认失败**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~StarterSiteScaffold"
```

Expected: 编译失败或测试失败，因为类型/资源不存在。

**Step 3：实现 scaffold**

`StarterSiteScaffold` 是 `Bukit.Cli` 的 internal 静态类：

```csharp
internal static class StarterSiteScaffold
{
    private const string ThemePrefix = "Bukit.Cli.Resources.StarterTheme.";
    private const string SitePrefix = "Bukit.Cli.Resources.StarterSite.";

    internal static StarterSiteResult Create(
        string target, string title, string slug, string language)
        => Create(target, title, slug, language, DefaultEntries);

    internal static StarterSiteResult Create(
        string target, string title, string slug, string language,
        IReadOnlyList<StarterFile> entries)
    {
        // Trim the trailing separator so "broken/" is checked as "broken";
        // Path.TrimEndingDirectorySeparator keeps a filesystem root unchanged.
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));
        ValidateTarget(root);
        var created = new List<CreatedEntry>();

        try
        {
            WriteText(root, "site.yaml", BuildSiteYaml(title, slug, language), created);
            foreach (var entry in entries)
            {
                if (entry.Binary)
                {
                    WriteBytes(root, entry.Destination,
                        entry.Site ? GetSiteBytes(entry.Resource) : GetThemeBytes(entry.Resource),
                        created);
                }
                else
                {
                    WriteText(root, entry.Destination,
                        entry.Site ? GetSiteText(entry.Resource) : GetThemeText(entry.Resource),
                        created);
                }
            }
            WriteText(root, ".gitignore", "dist/\n.bukit/\n.cache/\n", created);
            WriteText(root, "themes/starter/static/robots.txt",
                "User-agent: *\nAllow: /\n", created);
            return new StarterSiteResult(root);
        }
        catch
        {
            // shortcut: a cleanup failure can mask the original exception; low
            // probability. Do not swallow cleanup errors; a later hardening pass
            // can collect them and rethrow the original.
            for (var i = created.Count - 1; i >= 0; i--)
            {
                var item = created[i];
                if (item.IsDirectory)
                {
                    if (Directory.Exists(item.Path) &&
                        !Directory.EnumerateFileSystemEntries(item.Path).Any())
                    {
                        Directory.Delete(item.Path);
                    }
                }
                else if (File.Exists(item.Path))
                {
                    File.Delete(item.Path);
                }
            }
            throw;
        }
    }

    private static void WriteText(
        string root, string relative, string content, List<CreatedEntry> created)
    {
        var path = ResolvePath(root, relative);
        CreateDirectoryTree(Path.GetDirectoryName(path)!, created);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        created.Add(new CreatedEntry(path, IsDirectory: false));
    }

    private static void WriteBytes(
        string root, string relative, byte[] content, List<CreatedEntry> created)
    {
        var path = ResolvePath(root, relative);
        CreateDirectoryTree(Path.GetDirectoryName(path)!, created);
        File.WriteAllBytes(path, content);
        created.Add(new CreatedEntry(path, IsDirectory: false));
    }

    private static string ResolvePath(string root, string relative)
    {
        var path = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"starter path escapes the target: {relative}");
        return path;
    }

    private static void CreateDirectoryTree(string directory, List<CreatedEntry> created)
    {
        var full = Path.GetFullPath(directory);
        var missing = new Stack<string>();
        // current can become null at a drive root such as Z:\ on Windows; stop there,
        // never call Directory.Exists(null).
        for (var current = full; current is not null && !Directory.Exists(current);
             current = Path.GetDirectoryName(current))
        {
            missing.Push(current);
        }
        while (missing.Count > 0)
        {
            var path = missing.Pop();
            Directory.CreateDirectory(path);
            created.Add(new CreatedEntry(path, IsDirectory: true));
        }
    }

    private sealed record CreatedEntry(string Path, bool IsDirectory);
}
```

资源读取方法（internal，测试可见）：

```csharp
internal static string GetThemeText(string name) => ReadText(ThemePrefix + name);
internal static byte[] GetThemeBytes(string name) => ReadBytes(ThemePrefix + name);
internal static string GetSiteText(string name) => ReadText(SitePrefix + name);
internal static byte[] GetSiteBytes(string name) => ReadBytes(SitePrefix + name);
```

`ReadText` / `ReadBytes` 从 `typeof(StarterSiteScaffold).Assembly` 取资源，找不到
时抛 `InvalidOperationException($"... resource '{name}'")`，不能返回空串。

`StarterFile` 是 internal record：

```csharp
internal sealed record StarterFile(
    string Resource, string Destination, bool Binary = false, bool Site = false);
```

`Destination` 一律相对**站点根目录**：主题文件写成
`themes/starter/...`，内容写成 `content/...`，data 写成 `data/...`。不要用
相对主题目录的 `layouts/partials/header.html`。

默认 entries：19 个主题资源（17 个原有资源 + `LICENSE` +
`og-default.png`）用 `Site: false`；`WelcomePost.md`、`AboutPage.md`、
`FeatureData.md` 用 `Site: true`。`og-default.png` 是 `Binary: true` 的
主题资源。

| Resource | Destination（相对站点根） |
|---|---|
| `BaseLayout.html` | `themes/starter/layouts/layouts/base.html` |
| `SeoPartial.html` | `themes/starter/layouts/partials/seo.html` |
| `HeaderPartial.html` | `themes/starter/layouts/partials/header.html` |
| `FooterPartial.html` | `themes/starter/layouts/partials/footer.html` |
| `ListCardPartial.html` | `themes/starter/layouts/partials/list-card.html` |
| `PaginationNavPartial.html` | `themes/starter/layouts/partials/pagination-nav.html` |
| `PageTemplate.html` | `themes/starter/layouts/pages/page.html` |
| `PostTemplate.html` | `themes/starter/layouts/pages/post.html` |
| `IndexTemplate.html` | `themes/starter/layouts/pages/index.html` |
| `ListTemplate.html` | `themes/starter/layouts/pages/list.html` |
| `PaginationTemplate.html` | `themes/starter/layouts/pages/pagination.html` |
| `TaxonomyIndexTemplate.html` | `themes/starter/layouts/pages/taxonomy-index.html` |
| `TaxonomyTermTemplate.html` | `themes/starter/layouts/pages/taxonomy-term.html` |
| `SearchTemplate.html` | `themes/starter/layouts/pages/search.html` |
| `TemplateCapabilities.yaml` | `themes/starter/layouts/bukit.templates.yaml` |
| `ThemeYaml.yaml` | `themes/starter/theme.yaml` |
| `StyleCss.css` | `themes/starter/assets/style.css` |
| `LICENSE` | `themes/starter/LICENSE` |
| `og-default.png` | `themes/starter/assets/og-default.png` |
| `WelcomePost.md`（Site） | `content/posts/welcome.md` |
| `AboutPage.md`（Site） | `content/pages/about.md` |
| `FeatureData.md`（Site） | `data/features-10.md` |

```csharp
internal sealed record StarterSiteResult(string Root);
```

`ValidateTarget`：

```csharp
private static void ValidateTarget(string root)
{
    var info = new DirectoryInfo(root);
    if (info.LinkTarget is not null ||
        (info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0))
        throw new CommandArgumentException($"{root} is a symbolic link or reparse point.");
    if (File.Exists(root))
        throw new CommandArgumentException($"{root} is a file.");
    if (Directory.Exists(root) &&
        Directory.EnumerateFileSystemEntries(root).Any())
        throw new CommandArgumentException($"{root} must be empty.");
}
```

这里不再有根目录/主目录特判：非空规则已经拒绝；断链靠 `LinkTarget` 先检查。

`WriteText` / `WriteBytes` 通过 `ResolvePath` 做目标包含检查，通过
`CreateDirectoryTree` 创建并记录父目录，文本用 UTF-8 无 BOM 写入；每个目录和
文件都进入同一个 `created` 列表。`CreateDirectoryTree` 遇到 `null` 父路径就
停止，不调用 `Directory.Exists(null)`。

`BuildSiteYaml` 用 `YamlStream`：

```csharp
private static string BuildSiteYaml(string title, string slug, string language)
{
    var text = GetSiteText("SiteYaml.yaml");
    var stream = new YamlStream();
    stream.Load(new StringReader(text));
    var root = (YamlMappingNode)stream.Documents[0].RootNode;
    var site = (YamlMappingNode)root.Children[new YamlScalarNode("site")];
    site.Children[new YamlScalarNode("name")] = new YamlScalarNode(slug);
    site.Children[new YamlScalarNode("title")] =
        new YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted };
    site.Children[new YamlScalarNode("languages")] =
        new YamlSequenceNode(new YamlScalarNode(language));
    var theme = (YamlMappingNode)root.Children[new YamlScalarNode("theme")];
    var parameters = (YamlMappingNode)theme.Children[new YamlScalarNode("params")];
    parameters.Children[new YamlScalarNode("brand")] =
        new YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted };
    parameters.Children[new YamlScalarNode("footer_text")] =
        new YamlScalarNode(title) { Style = ScalarStyle.DoubleQuoted };
    using var writer = new StringWriter();
    stream.Save(writer, assignAnchors: false);
    return writer.ToString();
}
```

**Step 4：资源**

- `SiteYaml.yaml`：完整 starter 配置，块状 `languages` 也可以；包含
  `content.media.onFailure: keep`、`archive: true`、不写 `site.url`。
- `WelcomePost.md`、`AboutPage.md`、`FeatureData.md` 对齐 BukitJalil 当前
  starter；`welcome.md` 使用 `publishAt` 和相对链接
  `../../../../pages/about/`。
- `LICENSE` 复制根 `LICENSE` 的 MIT 文本。
- `og-default.png` 使用 BukitJalil 当前的 1200×630 PNG（约 3.1 KB），不要
  运行时生成。
- `Bukit.Cli.csproj` 增加：

```xml
<EmbeddedResource Include="Resources\StarterSite\**" />
```

现有 `Resources\StarterTheme\**` 已经有；默认逻辑名是
`Bukit.Cli.Resources.StarterTheme.*` 和 `Bukit.Cli.Resources.StarterSite.*`。
不要改 `Bukit.Cli.Shared.csproj`。

**Step 5：跑测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~StarterSiteScaffold"
```

Expected: PASS。

**Step 6：提交**

```bash
git add src/Bukit-Core/Bukit.Cli/Commands/New/StarterSiteScaffold.cs \
  src/Bukit-Core/Bukit.Cli/Resources/StarterSite \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/LICENSE \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/og-default.png \
  src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  tests/Bukit.Cli.Tests/StarterSiteScaffoldTests.cs
git commit -m "feat: add core starter site scaffold" -- \
  src/Bukit-Core/Bukit.Cli/Commands/New/StarterSiteScaffold.cs \
  src/Bukit-Core/Bukit.Cli/Resources/StarterSite \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/LICENSE \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/og-default.png \
  src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj \
  tests/Bukit.Cli.Tests/StarterSiteScaffoldTests.cs
```

T01 的 `git add` 必须包含 `Bukit.Cli.csproj`；否则干净检出上资源没有嵌入，
测试是红的。

### T02：删除占位符并修复 StarterTheme 转义

**Files:**
- Modify: `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/HeaderPartial.html`
- Modify: `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/FooterPartial.html`
- Modify: `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/IndexTemplate.html`
- Test: `tests/Bukit.Rendering.Tests/StarterThemeTemplateResourceTests.cs`

**Step 1：写失败测试**

在 `StarterThemeTemplateResourceTests` 增加：

```csharp
[Fact]
public void StarterTheme_DoesNotContainLegacyBrandPlaceholder()
{
    var source = Path.Combine(
        FindRepositoryRoot(),
        "src", "Bukit-Core", "Bukit.Cli", "Resources", "StarterTheme",
        "HeaderPartial.html");
    var header = File.ReadAllText(source);
    Assert.DoesNotContain("{{--", header, StringComparison.Ordinal);
}

[Fact]
public void StarterHeaderFooterAndIndex_EscapeSiteSettings()
{
    // copy Header/Footer/Index resources into _layoutsDir
    var renderer = new ScribanTemplateRenderer(_layoutsDir);
    var site = new SiteModel
    {
        Name = "starter-test",
        Title = "A & B",
        Description = "About & more",
        BaseUrl = "",
        Language = "en",
        Params = new Dictionary<string, object>
        {
            ["brand"] = "A & B",
            ["footer_text"] = "A & B",
            ["latest_heading"] = "Latest & best"
        }
    };
    // render header/footer/index with PageModel
    // Assert.Contains("A &amp; B", ...);
    // Assert.Contains("About &amp; more", index);
    // Assert.Contains("Latest &amp; best", index);
}
```

首页的 `<title>` 已经由 `BaseLayout.html` 的 `html.escape` 覆盖；JSON-LD 由
`Utf8JsonWriter` 输出，`&` 会转义。`page.title` / `item.title` 等内容字段仍
未转义，这是既有主题问题，不在本变更范围；记录到 Known Debt。

**Step 2：跑测试确认失败**

```bash
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release \
  --filter "FullyQualifiedName~StarterThemeTemplateResource"
```

Expected: 失败；占位符测试和转义测试都是红的。

**Step 3：修改资源**

- `HeaderPartial.html`：删除第 6 行
  `{{-- bukit:brand --}}`；`site.params.brand`、`site.title` 加
  `| html.escape`。
- `FooterPartial.html`：`site.params.footer_text`、`site.title` 加
  `| html.escape`。
- `IndexTemplate.html`：`site.title`、`site.description`、
  `site.params.latest_heading` 加 `| html.escape`。

不要保留“复制时替换占位符”的步骤；资源本身必须能直接渲染。

**Step 4：跑测试**

```bash
dotnet test tests/Bukit.Rendering.Tests/Bukit.Rendering.Tests.csproj -c Release \
  --filter "FullyQualifiedName~StarterThemeTemplateResource"
```

Expected: PASS。

**Step 5：提交**

```bash
git add src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/HeaderPartial.html \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/FooterPartial.html \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/IndexTemplate.html \
  tests/Bukit.Rendering.Tests/StarterThemeTemplateResourceTests.cs
git commit -m "fix: remove brand placeholder and escape starter theme settings" -- \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/HeaderPartial.html \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/FooterPartial.html \
  src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/IndexTemplate.html \
  tests/Bukit.Rendering.Tests/StarterThemeTemplateResourceTests.cs
```

### T03：`NewCommand`

**Files:**
- Create: `src/Bukit-Core/Bukit.Cli/Commands/NewCommand.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs`
- Modify: `src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs`
- Modify: `src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs`
- Modify: `src/Bukit-Core/Bukit.Config/InternalsVisibleTo.cs`
- Test: `tests/Bukit.Cli.Tests/NewCommandTests.cs`

**Step 1：写失败测试**

至少覆盖：

```csharp
[Fact]
public async Task RunAsync_CreatesBuildableSite()
{
    var target = Path.Combine(_root, "my-site");
    var code = await NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target]));
    Assert.Equal(0, code);
    var config = ConfigLoader.Load(Path.Combine(target, "site.yaml"));
    Assert.Equal(["en-US"], config.Site.Languages);
    Assert.Null(config.Site.Url);
    var build = await BuildCommand.RunInProcessAsync(CliTestHelper.CreateCommand("build",
        ["--config", Path.Combine(target, "site.yaml"), "--output", Path.Combine(target, "dist")]));
    Assert.Equal(0, build);
    Assert.True(File.Exists(Path.Combine(target, "dist/index.html")));
}

[Fact]
public async Task RunAsync_BlankTitleIsRejected()
{
    var target = Path.Combine(_root, "blank");
    await Assert.ThrowsAsync<CommandArgumentException>(() =>
        NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target, "--title", " "])));
    Assert.False(Directory.Exists(target));
}

[Fact]
public async Task RunAsync_WhitespaceTargetIsRejected()
{
    await Assert.ThrowsAsync<CommandArgumentException>(() =>
        NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [" "])));
}

[Fact]
public async Task RunAsync_InvalidLanguageLeavesNoDirectory() { ... }

[Fact]
public async Task RunAsync_InvalidTitleLeavesNoDirectory() { ... }

[Fact]
public async Task RunAsync_RefusesFileTarget() { ... }

[Fact]
public async Task RunAsync_RefusesNonEmptyTarget() { ... }

[Fact]
public async Task RunAsync_RefusesBrokenSymlinkTarget() { ... } // 非 Windows

[Fact]
public async Task RunAsync_RefusesBrokenSymlinkTargetWithTrailingSlash()
{
    if (OperatingSystem.IsWindows()) return;
    var link = Path.Combine(_root, "broken");
    Directory.CreateSymbolicLink(link, Path.Combine(_root, "missing"));
    var target = link + Path.DirectorySeparatorChar;
    await Assert.ThrowsAsync<CommandArgumentException>(() =>
        NewCommand.RunAsync(CliTestHelper.CreateCommand("new", [target])));
    Assert.False(Directory.Exists(link));
}

[Fact]
public async Task RunAsync_DefaultTitleFromDirectory() { ... }

[Fact]
public async Task RunAsync_DiacriticsSlugMatchesBukitJalil()
{
    var target = Path.Combine(_root, "diacritics");
    var code = await NewCommand.RunAsync(CliTestHelper.CreateCommand(
        "new", [target, "--title", "Résumé"]));
    Assert.Equal(0, code);
    var config = ConfigLoader.Load(Path.Combine(target, "site.yaml"));
    Assert.Equal("re-sume", config.Site.Name);
}

[Fact]
public async Task RunAsync_NoGitAndNoDist() { ... }

[Fact]
public async Task RunAsync_IsDeterministic() { ... }
```

已对照 BukitJalil 的 `siteSlug`：两边都是 NFKD 后小写、非 `a-z0-9` 转 `-`，
`Résumé` 得到 `re-sume`，不是 `resume`。测试固定这个等价性。

`RunAsync_CreatesBuildableSite` 用 `--config <target>/site.yaml` 调
`BuildCommand.RunInProcessAsync`；`ConfigPathResolver` 会把 `rootDir` 定为
配置所在目录，主题路径相对 `resolved.RootDir` 解析，不依赖当前 cwd。

用户输入错误在直接调用时抛 `CommandArgumentException`；退出码 2 由
`Program.cs` 现有映射保证，不要在命令里 try/catch 成 2。

**Step 2：跑测试确认失败**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~NewCommand"
```

Expected: 编译失败，`NewCommand` 不存在。

**Step 3：实现命令**

`BukitCliSpecs`：

```csharp
var newSite = new CliCommandSpec(
    Name: "new",
    Description: "生成初始网站",
    Arguments: new[] { new CliArgumentSpec("dir", "目标目录", Required: true) },
    Options: new[]
    {
        new CliOptionSpec("--title", "站点标题", ValueName: "text"),
        new CliOptionSpec("--language", "主语言（BCP 47）", ValueName: "tag")
    });
```

把 `newSite` 放进 registry 数组，位置在 `clean` 之后、`version` 之前。

`BukitCliDescriptors`：

```csharp
new(registry.Commands.First(c => c.Name == "new"), NewCommand.RunAsync),
```

`NewCommand.RunAsync`：

```csharp
public static Task<int> RunAsync(CliBoundCommand command)
{
    var targetArg = command.GetArgument(0)!;
    if (string.IsNullOrWhiteSpace(targetArg))
        throw new CommandArgumentException("new requires a target directory.");
    var target = Path.GetFullPath(targetArg);
    var titleOption = command.GetString("--title");
    var defaultTitle = Path.GetFileName(Path.TrimEndingDirectorySeparator(target));
    var title = titleOption is null ? defaultTitle : titleOption.Trim();
    if (titleOption is not null && title.Length == 0)
        throw new CommandArgumentException("--title must not be blank.");
    if (title.Length == 0) title = "My Site";
    if (title.Length is < 1 or > 200 || title.Any(char.IsControl))
        throw new CommandArgumentException(
            "--title must be 1-200 characters and contain no control characters.");

    var language = (command.GetString("--language") ?? "en-US").Trim();
    if (!SiteConfigValidator.IsSafeLanguageTag(language))
        throw new CommandArgumentException(
            "--language must use safe alphanumeric subtags separated by hyphens.");

    var root = StarterSiteScaffold.Create(target, title, Slug(title), language).Root;
    Console.WriteLine($"Created: {root}");
    Console.WriteLine("Next:");
    Console.WriteLine($"  cd \"{root}\"");
    Console.WriteLine("  bukit config check");
    Console.WriteLine("  bukit doctor");
    Console.WriteLine("  bukit build");
    Console.WriteLine("  bukit preview --dir dist");
    return Task.FromResult(0);
}

private static string Slug(string title)
{
    var slug = title.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
    slug = Regex.Replace(slug, "[^a-z0-9]+", "-").Trim('-');
    return slug.Length == 0 ? "site" : slug[..Math.Min(slug.Length, 64)].TrimEnd('-');
}
```

`Slug` 也可以不用 `Regex`，用循环；保持与 BukitJalil `siteSlug` 等价即可。
不要单独建 `SiteSlug`/`StarterSiteValues`/`StarterSiteYaml` 文件。

**Step 4：跑测试**

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~NewCommand"
```

Expected: PASS。若 build smoke 超过测试预算，把它拆到
`NewCommandBuildTests` 并加超时；不要删掉 build 覆盖。

**Step 5：提交**

```bash
git add src/Bukit-Core/Bukit.Cli/Commands/NewCommand.cs \
  src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs \
  src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs \
  src/Bukit-Core/Bukit.Config/InternalsVisibleTo.cs \
  tests/Bukit.Cli.Tests/NewCommandTests.cs
git commit -m "feat: add bukit new command" -- \
  src/Bukit-Core/Bukit.Cli/Commands/NewCommand.cs \
  src/Bukit-Core/Bukit.Cli/Cli/BukitCliSpecs.cs \
  src/Bukit-Core/Bukit.Cli/Cli/BukitCliDescriptors.cs \
  src/Bukit-Core/Bukit.Config/SiteConfigValidator.cs \
  src/Bukit-Core/Bukit.Config/InternalsVisibleTo.cs \
  tests/Bukit.Cli.Tests/NewCommandTests.cs
```

### T04：Core 命令面契约

**Files:**
- Modify: `tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs`
- Modify: `tests/Bukit.Cli.Tests/CliProgramFlowTests.cs`

**Step 1：先改测试**

- `CoreCliCommands_MatchStableWhitelist` 的数组加入 `"new"`，位置与 registry
  一致（`clean` 之后、`version` 之前）。
- `CliProgramFlowTests` 增加：

```csharp
[Fact]
public void Specs_And_Descriptors_IncludeNew()
{
    Assert.NotNull(BukitCliSpecs.CreateRegistry().Resolve("new"));
    Assert.NotNull(BukitCliDescriptors.ResolveDescriptor(
        BukitCliDescriptors.CreateDescriptors(), "new"));
}
```

**Step 2：跑测试确认失败**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CoreBoundary"
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CliProgramFlow"
```

Expected: 白名单失败；descriptor 测试失败。

**Step 3：修正**

按 T03 的 registry 顺序更新白名单；确认 `bukit --help` 的 HelpPrinter 自动
显示 `new`，completion 自动包含它。若 completion 有静态列表，同步更新。

**Step 4：跑测试**

```bash
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CoreBoundary"
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release \
  --filter "FullyQualifiedName~CliProgramFlow"
```

Expected: PASS。

**Step 5：提交**

```bash
git add tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs \
  tests/Bukit.Cli.Tests/CliProgramFlowTests.cs
git commit -m "test: lock bukit new command surface" -- \
  tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs \
  tests/Bukit.Cli.Tests/CliProgramFlowTests.cs
```

### T05：文档与 CHANGELOG

**Files:**
- Modify: `README.md`、`README.zh-CN.md`、`README.ms.md`
- Modify: `guide/user/01-quick-start.md`
- Modify: `guide/user/12-cli-reference.md`
- Modify: `guide/dev/cli.md`
- Modify: `guide/skills/bukit-cli-reference/SKILL.md`
- Modify: `guide/skills/using-bukit/SKILL.md`
- Modify: `CHANGELOG.md`、`CHANGELOG.zh-CN.md`、`CHANGELOG.ms.md`

**Step 1：README quick start**

把现有命令块改为：

```bash
bukit new my-site
cd my-site
bukit config check
bukit doctor
bukit build --clean
bukit dev
```

**Step 2：每张表按该文件的列数加行**

不要用一条通用的三列表行复制到所有文件：

- `README.md` 命令表是 **2 列**（Command / Purpose）：

```markdown
| `new` | Create an initial site. |
```

- `README.zh-CN.md` 2 列：

```markdown
| `new` | 创建初始网站。 |
```

- `README.ms.md` 2 列：

```markdown
| `new` | Cipta tapak permulaan. |
```

- `guide/user/12-cli-reference.md` 是 **3 列**（Command / Purpose /
  Parameters）：

```markdown
| `new` | Create an initial site. | `<dir>`, `--title`, `--language` |
```

- `guide/dev/cli.md` 是 **2 列**（Command / Parameters）：

```markdown
| `new` | `<dir>`, `--title`, `--language` |
```

- `guide/skills/bukit-cli-reference/SKILL.md`：
  - “Command map”是 **3 列**（Command / Subcommands / Purpose）：

```markdown
| `new` | — | Create an initial site. |
```

  - “Options by command”是 **2 列**（Command / Options）：

```markdown
| `new` | `<dir>` (required), `--title`, `--language` |
```

把 “Bukit Core registers exactly twelve static commands” 改为
“thirteen static commands”。

- `guide/skills/using-bukit/SKILL.md`：
  - “Use only the twelve Core commands” 改为 “thirteen”，命令列表加 `new`；
  - “There is no `init`, `new`, `theme`, …” 去掉 `new`；
  - “Core has no scaffolding command” 改为指向 `bukit new`。
- `guide/dev/cli.md` 的 “BukitJalil consumer contract” 节已在 HEAD
  `8cbe72cd` 提交；T05 直接在该表**追加**一行（只追加，不覆盖这一节）：

  ```markdown
  | `bukit new` exit codes 0/2/1; generated file set; generated `site.yaml` keys `content.sources`/`content.media.onFailure` | `NewCommandTests` |
  ```

  如果实施时该节被回退，或 Bukit 尚未发布含 `new` 的版本，把这一行放到 Part B
  第 1 步，和 `MIN_BUKIT_VERSION` 一起改。

**Step 3：quick start**

把“Bukit Core does not currently expose a stable scaffold command.”改为：

```markdown
Run `bukit new my-site` to create a buildable starter. It writes
`site.yaml`, Markdown content, and a local `themes/starter/`; it does not run
git, build, or make network requests.
```

保留 Minimal Layout 作为“生成物结构”的参考，并说明手动创建仍可用。

**Step 4：CHANGELOG**

三个文件各加一条，英文示例：

```markdown
- **`bukit new` creates a buildable starter site**: `bukit new <dir>
  [--title <text>] [--language <tag>]` writes `site.yaml`, Markdown content,
  and the embedded starter theme without running git, a build, or a network
  request. The generated site passes `config check`, `doctor`, and `build`.
```

中文/马来文保持同义，不扩写。

**Step 5：跑检查**

```bash
bash scripts/checks/cli-docs-sync.sh
bash scripts/checks/docs-consistency.sh
bash scripts/checks/docs/no-core-drift.sh
bash scripts/checks/readme-sync.sh
bash guide/skills/scripts/validate-skills-strict.sh
```

Expected: 全部 PASS。README 和两个 SKILL.md 的现有 policy 规则也会在 closure
里要求这些检查。

**Step 6：提交**

T05 修改的文档在本计划外可能已有改动（尤其 `guide/dev/cli.md`、
`guide/skills/bukit-cli-reference/SKILL.md`、`guide/user/12-cli-reference.md`）；
T00 前置检查必须先解决归属。提交前对每个文件跑 `git diff -- <file>`，确认
只有本变更的 `new` 行；如果还有别人的改动，停止，不要 `git add` 整个文件。

```bash
git add README.md README.zh-CN.md README.ms.md \
  guide/user/01-quick-start.md guide/user/12-cli-reference.md \
  guide/dev/cli.md guide/skills/bukit-cli-reference/SKILL.md \
  guide/skills/using-bukit/SKILL.md \
  CHANGELOG.md CHANGELOG.zh-CN.md CHANGELOG.ms.md
git commit -m "docs: document bukit new" -- \
  README.md README.zh-CN.md README.ms.md \
  guide/user/01-quick-start.md guide/user/12-cli-reference.md \
  guide/dev/cli.md guide/skills/bukit-cli-reference/SKILL.md \
  guide/skills/using-bukit/SKILL.md \
  CHANGELOG.md CHANGELOG.zh-CN.md CHANGELOG.ms.md
```

### T06：OpenSpec 校验与规范同步准备

**Files:**
- Validate: `openspec/changes/add-bukit-new-command/**`
- Later sync: `openspec/specs/cli-scaffold/spec.md`

**Step 1：校验**

```bash
openspec validate cli-scaffold --type spec --strict --no-interactive
```

Expected: valid。

**Step 2：规范同步**

只有在实现和验收证据齐全、用户批准后，才把
`openspec/changes/add-bukit-new-command/specs/cli-scaffold/spec.md` 同步到
`openspec/specs/cli-scaffold/spec.md` 并归档变更。不要把“计划完成”当成
“规范已实现”。

**Step 3：提交**

规范同步单独提交，提交信息。若变更已经归档，把第二个路径换成实际的
`openspec/changes/archive/<date>-add-bukit-new-command`；不要用不带 pathspec
的 `git commit`：

```bash
git add openspec/specs/cli-scaffold/spec.md openspec/changes/add-bukit-new-command
git commit -m "spec: accept bukit new scaffold contract" -- \
  openspec/specs/cli-scaffold/spec.md \
  openspec/changes/add-bukit-new-command
```

### T07：最终验证与审查

**Step 1：运行 closure 的 specialtyTests 并集**

closure 会把所有匹配规则的 `specialtyTests` 取并集；现有 `bukit-cli-source`、
`bukit-config`、`bukit-rendering-tests`、`bukit-architecture-tests` 等规则可能
要求**完整**的 Cli / Config / Rendering / Architecture 测试项目。按 closure
的输出逐条运行，不要因为“看起来太大”而截断。第 7 节的表只是阅读参考；不要
在这里复制第二份命令清单。

Expected: PASS。若缓存条件完全匹配，复用 `cache check` 的 GREEN 证据；不要
为了刷新证据重跑未变化的 fixture。

**Step 2：Native AOT（需授权）**

```bash
bash scripts/build/native-aot.sh <version> osx-arm64 <output-root> Release
<output-root>/bukit new /tmp/bukit-new-smoke
<output-root>/bukit config check --config /tmp/bukit-new-smoke/site.yaml
<output-root>/bukit build --config /tmp/bukit-new-smoke/site.yaml
```

Expected: 二进制包含 `new`，资源可读，生成站点可检查/构建。若不授权 AOT，
至少用 `dotnet run --project src/Bukit-Core/Bukit.Cli -- new ...` 跑一次
端到端；并在最终报告里明确 AOT 未验证。

**Step 3：审查**

- 生成 `review-scope`，只审查本变更的增量。
- 只对 Critical/Important 回到实施；Minor 记录不阻塞。
- 不重复历史审查，不新增未排期的审计。

**Step 4：记录**

```bash
python3 scripts/checks/codex-workflow.py metrics add \
  --state /tmp/bukit-new-command-queue.json \
  --task T07 --phase test --duration-ms <n> \
  --cache-status <hit|miss|none> --status completed
python3 scripts/checks/codex-workflow.py metrics report \
  --state /tmp/bukit-new-command-queue.json
```

## 7. 测试矩阵

下表只用于阅读和审查；**执行清单以 closure 输出的 `specialtyTests` 并集为
唯一来源**。现有规则可能要求完整的 Cli / Config / Rendering / Architecture
测试项目；这是 policy 的要求，不是可以省略的“整项目测试”。不要在 T07 或其他
文档再复制一份可执行命令列表。

| 行 | 项目 | 过滤 / 命令 |
|---|---|---|
| N1 | `Bukit.Cli.Tests` | `FullyQualifiedName~NewCommand` |
| N2 | `Bukit.Cli.Tests` | `FullyQualifiedName~StarterSiteScaffold` |
| N3 | `Bukit.Cli.Tests` | `FullyQualifiedName~CliProgramFlow` |
| N4 | `Bukit.Rendering.Tests` | `FullyQualifiedName~StarterThemeTemplateResource` |
| N5 | `Bukit.Architecture.Tests` | `FullyQualifiedName~CoreBoundary` |
| N6 | `Bukit.Labs.Cli.Tests` | `FullyQualifiedName~StarterThemeScaffold`（Shared 里的主题资源被 Labs 读取） |
| N7 | 脚本 | `bash scripts/checks/cli-docs-sync.sh`；`bash scripts/checks/docs-consistency.sh`；`bash scripts/checks/docs/no-core-drift.sh`；`bash scripts/checks/readme-sync.sh`；`bash guide/skills/scripts/validate-skills-strict.sh` |
| N8 | 脚本 | `bash scripts/checks/dotnet-format.sh`；`public-api-drift.sh check`；`code-analysis-ratchet.sh check` |
| N9 | OpenSpec | `openspec validate cli-scaffold --type spec --strict --no-interactive` |
| N10 | Native AOT / CLI | `bash scripts/build/native-aot.sh ...`，然后 `bukit new` + `config check` + `build` |

只跑这些专项；不跑 full/release gate。N10 需授权。

## 8. 验证闭包

T00 加规则后，先确认 `git status --short` 只有本变更的文件；无关改动由所有者
提交、stash 或确认后再生成闭包。然后按 `BASE`（T00 记录、变更前的提交）到
当前工作树的实际 changed files 生成闭包：

```bash
BASE=${BASE:-$(cat /tmp/bukit-new-command-base)}
changed=()
while IFS= read -r f; do changed+=("$f"); done < <(
  git diff --name-only "$BASE"
  git ls-files --others --exclude-standard
)
args=()
for f in "${changed[@]}"; do args+=(--changed "$f"); done
python3 scripts/checks/codex-workflow.py closure \
  --policy scripts/checks/codex-workflow-policy.v1.json "${args[@]}"
```

每个任务提交后也要用同一个 `BASE` 重新生成闭包；不要用 `HEAD`，否则提交后
列表会变空。

要求：

- `unmappedFiles` 为空；
- `specialtyTests` 至少包含 N1–N9；closure 的并集可能更大（例如完整的
  `Bukit.Cli.Tests`、`Bukit.Config.Tests`、`Bukit.Rendering.Tests`、
  `Bukit.Architecture.Tests`），按实际输出执行；
- `contractConsumers` 包含 `BukitCliSpecs.cs`、`BukitCliDescriptors.cs`、
  `Bukit.Cli.csproj`、`SiteConfigValidator.cs`（以及实际引用
  `NewCommand`/`StarterSiteScaffold` 的源码）；
- `publicContractFiles` 为空（本变更不新增 public API；`SiteConfigValidator`
  仍是 internal）。

分类：

```bash
python3 scripts/checks/codex-workflow.py classify \
  --policy scripts/checks/codex-workflow-policy.v1.json \
  --path src/Bukit-Core/Bukit.Cli/Commands/NewCommand.cs \
  --test-command "dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release"
```

`dotnet test` 为 `dotnet-serial`；文档检查为 `static-parallel`；Native AOT
按现有策略单独运行。不并发写同一 fixture、锁文件或构建清单。

## 9. Part B：BukitJalil 迁移（发布后，另行授权）

本仓库不改 BukitJalil；以下是交接清单：

1. 把 `packages/server/src/bukit/install.ts` 的 `BUKIT_VERSION` 和三个
   校验和升到包含 `new` 的 Bukit 版本；`MIN_BUKIT_VERSION` 同步。
   - 如果 `new` 还没出现在 Bukit `guide/dev/cli.md` 的
     “BukitJalil consumer contract” 表，追加一行：

     ```markdown
     | `bukit new` exit codes 0/2/1; generated file set; generated `site.yaml` keys `content.sources`/`content.media.onFailure` | `NewCommandTests` |
     ```

     和 `MIN_BUKIT_VERSION` 同一次发布，只追加，不覆盖该节。
2. 在 `packages/server/src/sites/service.ts` 的 `create` 中：
   - 用现有 `BukitRunner` 调用 `bukit new <target> --title <name>
     --language <language>`，走最小环境和超时；
   - 保留现有的目标目录、空目录、权限和失败回滚检查；
   - 用 `yaml` 文档只替换 `content.sources`（Notion 源）和
     `content.media.onFailure`（如果仍需要）；标题/语言/主题参数已由
     `new` 写入。
3. 删除：
   - `templates/starter/`（25 个文件）
   - `templates/starter.source.json`
   - `scripts/make-starter.sh`
   - `packages/server/test/template.test.ts` 中的生成器/哈希断言（保留一个
     小的集成测试）
   - `packages/server/src/sites/template.ts` 中的 `copyStarter`、`copyTree`、
     `siteSlug`、`fillSiteYaml` 大部分逻辑（保留 Notion patch）
4. 更新引用：`packages/server/src/paths.ts`、`main.ts` 的 `templateDir`、
   `packages/desktop/scripts/package.mjs` 的 `templates/starter` 拷贝、
   `e2e` 和各测试的 `STARTER` 路径。
5. 测试：
   - 新建 Markdown 网站：`new` → `config check` → 首次构建；
   - 新建 Notion 网站：`new` → patch sources → 带 token 的 check/build；
   - `new` 失败（非空、权限、资源损坏）不留下半成品；
   - 与旧 `templates/starter` 生成结果对比关键文件（site.yaml 语义、主题
     文件集合、示例内容）；
   - 桌面打包后 `BUKITJALIL_RESOURCES` 下不再需要 `templates/starter`。
6. `guide/skills` 同步保持独立；不要把它并入 `new` 的迁移。
7. 回滚：保留上一个 Bukit pin 和旧 `copyStarter` 分支一个发布周期；若
   `new` 在生产出现阻塞，回退 pin 和站点创建实现，不影响已生成站点。

## 10. 里程碑与时间

| 阶段 | 内容 | 估算 |
|---|---|---|
| M0 | G0、规则、基线、队列 | 0.5 天 |
| M1 | T01–T02 资源层、YAML、占位符与转义 | 1 天 |
| M2 | T03 命令 | 1 天 |
| M3 | T04–T06 契约、文档、OpenSpec | 1 天 |
| M4 | T07 最终验证与审查 | 0.5 天 |
| M5 | Bukit 发布 + BukitJalil 迁移 | 独立排期 |

估算不是承诺；以实际测试和审查为准。

## 11. 风险与回滚

| 风险 | 影响 | 缓解 |
|---|---|---|
| Core 命令面审批失败 | 计划阻塞 | G0 先决；不批准则走附录 A |
| 生成结构成为事实契约 | 后续改动需兼容 | 在 OpenSpec/spec 固化；变更时给迁移说明 |
| 资源嵌入/AOT 读取失败 | 发布版命令不可用 | N10 在 Native AOT 上跑 `new`；资源缺失测试 |
| Windows 符号链接测试不稳定 | CI 偶发失败 | 仅在支持平台跑；命令本身用重解析点检查 |
| BukitJalil 不能立即升级 | 副本继续存在 | 保持 `make-starter.sh` 直到 pin 升级；不提前删 |
| 生成站点与旧 starter 语义不同 | BukitJalil 测试失败 | 用语义断言而非逐字节；迁移时对比关键文件 |
| Labs 资源映射与 Core 重复 | 两份列表漂移 | 记录为已知债务；后续让 Labs 调用 Core 或删副本 |
| 工作区混入其他写者的 release/CI 改动 | closure 污染、单写者冲突 | T00 `git status --short` 前置；无关改动由所有者提交/stash/确认 |
| 暂存区有别人的文件 | 提交会把它们一起带走 | T00 `git diff --cached --quiet` 自动拦截；所有提交带 pathspec |
| 回滚清理异常覆盖原始异常 | 错误信息变化 | 代码加 `shortcut:` 注释；不吞异常；后续 hardening 收集清理错误并重抛原始异常 |

回滚：

- 代码：revert `new` 命令、资源、白名单和文档；现有站点不受影响。
- 发布：不发布含 `new` 的版本，或回退 tag。
- BukitJalil：保持旧 pin 和 `copyStarter`，不删除模板副本。

## 12. 验收清单

- [x] G0 已记录：Path A（2026-11-16）。
- [ ] T00 前 `git status --short` 只含本变更文件，`git diff --cached --quiet`
      和 `git diff --quiet` 通过。
- [ ] T00–T06 每个提交都带 pathspec，未带入暂存区里的其他文件。
- [ ] `closure` 无未映射文件。
- [ ] `bukit new` 生成站点通过 `config check` / `doctor` / `build`。
- [ ] 非空、文件、断链符号链接（含尾斜杠形式）、非法标题/语言均失败且无半成品。
- [ ] `--title " "` 被拒绝，不会被替换成 `My Site`。
- [ ] `A & B` 在页头/页脚/首页输出 `A &amp; B`。
- [ ] 生成物无 `.git`、无 `dist`、无 `site.url`。
- [ ] `HeaderPartial.html` 不含 `{{--`。
- [ ] `CoreBoundaryTests` 白名单包含 `new`。
- [ ] CLI 文档同步、docs-consistency、OpenSpec 校验通过。
- [ ] 格式、public API、ratchet、Labs 主题测试通过。
- [ ] 一次增量审查无 Critical/Important。
- [ ] BukitJalil 迁移清单已交接（不在本仓库执行）。

## 附录 A：Path B（先 Labs）

若 G0 不批准直接进 Core：

1. 在 `src/Bukit-Labs/Bukit.Labs.Cli/Commands/New/StarterSiteScaffold.cs` 放
   同一组 internal 代码（或给 `Bukit.Cli.Shared` 增加对
   `Bukit.Labs.Cli` 的 `InternalsVisibleTo`）；不要 public。
2. 在 `Bukit.Labs.Cli/Program.cs` 的 switch 加 `"new"`。
3. 加 `tests/Bukit.Labs.Cli.Tests/NewCommandTests.cs`，覆盖默认生成、非空
   目标、断链符号链接、非法语言、失败回滚。
4. 跑 `dotnet test tests/Bukit.Labs.Cli.Tests/Bukit.Labs.Cli.Tests.csproj`。
5. 在 `design.md` 记录 Labs 证据，再执行 T03–T07 的 Core 提升。
6. 不修复/不搬移 600 行的 `InitCommand` 多模板向导；它是 Labs 历史原型，
   不是本变更的基础。
7. Path B 的重复代码在 Core 提升后删除，或让 Labs 直接调用 Core 的
   internal scaffold；不要把 public API 作为跨程序集桥梁。

## 附录 B：关键代码检查点

- `StarterSiteScaffold` 在 `Bukit.Cli` internal；`Bukit.Cli.Tests` 可见。
- 目标先 `Path.TrimEndingDirectorySeparator(Path.GetFullPath(target))`，再查
  符号链接；根目录不会被削掉。
- 资源逻辑名是 `Bukit.Cli.Resources.StarterTheme.*` 和
  `Bukit.Cli.Resources.StarterSite.*`。
- `Bukit.Cli.csproj` 嵌入 `Resources\StarterSite\**`；不改 Shared csproj。
- `StarterSiteScaffold.GetThemeText` / `GetSiteText` 必须抛错，不能返回空串。
- `BuildSiteYaml` 用 `YamlStream`，不得用 `SerializerBuilder`。
- `NewCommand` 不得调用 `git`、`BuildCommand` 或网络客户端。
- 用户输入/目标错误用 `CommandArgumentException`；资源/IO 错误让它冒泡。
- 生成 `site.yaml` 不得含 `site.url`。
- 主题资源必须含 `LICENSE` 和 `og-default.png`；缺失时命令失败。
- `HeaderPartial.html` 不得含 `{{--`。
- 转义修复必须在 Header/Footer/Index 三处都有测试。
- 回滚只删本次创建的 `created` 目录/文件；不得 `ClearDirectory(root)`，
  也不要用 `FindMissingDirectories` 的 null 循环。
- 回滚清理异常用 `shortcut:` 注释记录“可能覆盖原始异常”的限制；不要吞异常。
- 白名单必须精确匹配 registry 顺序。
- 下一步输出用 `cd "{root}"`，路径加引号。

## 附录 C：审计处置摘要

| 审计项 | 处置 |
|---|---|
| #1 占位符不是注释 | 接受；T02 删除 Header 第 6 行；scaffold 不再替换；design/proposal 改词 |
| #2 internal 类型不可见 | 接受；移到 `Bukit.Cli` internal；改 `Bukit.Cli.csproj`，不动 Shared |
| #3 退出码 | 接受；用户错误 `CommandArgumentException` → 2；资源/IO → 1；删自定义异常和 try/catch |
| #4 回滚误删 | 接受；记录 created files/dirs，按相反顺序删除；不用 `ClearDirectory` |
| #5 空白 `--title` | 接受；提供但空白时拒绝；只有省略时才用目录名/默认值 |
| #6 断链符号链接 | 接受；先查 `DirectoryInfo.LinkTarget`；加测试 |
| #7 反射 serializer | 接受；改用 `SiteYaml.yaml` + `YamlStream` |
| #8 回滚/缺失资源不可测 | 接受；加 internal 入口列表重载；测试注入缺失资源 |
| #9 closure 命令 | 接受；进程替换 + `git ls-files --others`；避免管道子 shell |
| #10 T01 漏 csproj | 接受；`git add` 加 `Bukit.Cli.csproj` |
| #11 验证列表矛盾 | **部分更正**：上一轮“整项目测试违反 AGENTS.md”判断错误；现有 policy 在改 Cli/Config/测试时要求完整项目测试。改为按 closure 并集执行；补 Labs、format、ratchet、no-core-drift 仍成立 |
| #12 文件太碎 | 接受；合并到 `NewCommand.cs` + `StarterSiteScaffold.cs`；删自定义异常/值对象/YAML 文件 |
| #13 根/主目录特判 | 接受；删除；非空规则足够 |
| #14 文档表形状 | 接受；按每个文件的列数分别给行 |
| #15 YAML 文本样式 | 接受；规范改为断言解析值 |
| #16 `cd` 未加引号 | 接受；输出 `cd "{root}"` |
| #17 queue 措辞 | 接受；`queue init` 只建空队列，任务由 acquire 加入 |
| #18 主题映射重复 | 接受但延后；记录为 Known Debt，不在本变更合并 |

### v2 复核追加

| 复核项 | 处置 |
|---|---|
| 回滚没有记录子目录 | 接受；改为单个 `created` 列表记录目录和文件；`WriteText`/`CreateDirectoryTree` 逐级记录；测试先写嵌套文件再触发缺失资源 |
| `FindMissingDirectories` 在 Windows 死循环 | 接受；删除该方法；`CreateDirectoryTree` 遇到 `null` 父路径停止，不调用 `Directory.Exists(null)` |
| “只跑 N1–N9、不跑整个项目”与 policy 冲突 | 接受更正；按 closure 的 `specialtyTests` 并集执行，完整项目测试可能被要求 |
| 无关未跟踪文件导致 closure 失败 | 已解决：`62fa0876` 已把该分析文件纳入版本库；删除特殊处理，保留通用工作区检查 |
| 漏 `guide/skills/using-bukit/SKILL.md` | 接受；T05/文件表/规则加入；改 twelve → thirteen，更新 scaffolding 说明 |
| YamlStream 丢注释、加 `...` | 接受；文档写明模板注释只给维护者看，输出末尾可能有 `...`，测试只解析 |
| `Destination` 基准不一致 | 接受；统一相对站点根目录，主题条目写 `themes/starter/...` |
| 新规则重复列 policy 文件 | 接受；删除；现有 `codex-workflow` 规则已覆盖 |

### v3 复核追加

| 复核项 | 处置 |
|---|---|
| 尾斜杠断链绕过检查 | 接受；`Create` 开头用 `Path.TrimEndingDirectorySeparator(Path.GetFullPath(target))` 规范化；加 `broken/` 测试，断言 `CommandArgumentException`（退出 2）而不是 IO 异常（退出 1） |
| 工作区混入 release/CI 改动 | 接受；T00 增加 `git status --short` 前置条件；无关改动由所有者提交/stash/确认；不碰这三个文件 |
| 回滚清理异常覆盖原始异常 | 接受但低优先级；代码加 `shortcut:` 注释；不吞异常；后续 hardening 收集清理错误并重抛原始异常 |

### v4 复核追加

| 复核项 | 处置 |
|---|---|
| 两个仓库都有本变更以外的改动 | 接受；T00 Step 0 增加 v4 实际快照（`guide/dev/cli.md`、`tests/...Consumers/BukitJalilContractTests.cs`、`NotionOfflineSnapshotCommandTests.cs`、`ProgramEntryPointTests.cs`）和 release 提交提醒；由所有者处理，不得覆盖；`guide/dev/cli.md` 与 T05 冲突，先解决归属。BukitJalil 的 `docs/plans/2026-11-12-media-upload-design.md` 同样未提交，未触碰 |
| `docs/analysis/...` 已纳入版本库 | 接受；删除未跟踪文件特殊规则；保留通用“工作区只含本变更文件”检查 |
| Slug 多余的 `.Replace('_', '-')` | 接受；删除；后续正则 `[^a-z0-9]+` 已覆盖下划线，和 BukitJalil `siteSlug` 逐步一致 |

### v5 复核追加

| 复核项 | 处置 |
|---|---|
| 暂存区别人的文件会被提交带走 | 接受；T00 Step 0 增加 `git diff --cached --quiet` 和 `git diff --quiet` 自动拦截；T00–T06 所有 `git commit` 都改成 `git commit -m "…" -- <本任务路径>` |
| BukitJalil consumer contract 表缺 `new` 行 | 接受；T05 写明在该节已提交后只追加一行（退出码 0/2/1、生成文件集合、`content.sources`/`content.media.onFailure`，测试 `NewCommandTests`）；如果该节未提交或 Bukit 尚未发布 `new`，放到 Part B 第 1 步和 `MIN_BUKIT_VERSION` 一起改 |
| Step 0 写死历史快照 | 接受；Step 0 只保留通用规则和检查命令；v3/v4 快照移到附录 C “工作区历史快照” |

### 工作区历史快照（只作排查，不要当当前清单）

v3 复核时曾出现的 release 相关改动：

```text
 M .github/workflows/release.yaml
 M guide/dev/release.md
 M tests/Bukit.Architecture.Tests/ReleaseWorkflowContractTests.cs
```

v4 复核时 Bukit 工作区出现的暂存改动：

```text
M  guide/dev/cli.md
A  tests/Bukit.Cli.Tests/Consumers/BukitJalilContractTests.cs
M  tests/Bukit.Cli.Tests/NotionOfflineSnapshotCommandTests.cs
M  tests/Bukit.Cli.Tests/ProgramEntryPointTests.cs
```

v4 复核时 HEAD 新增的发布相关提交：`041e6161`、`b7a25223`。
