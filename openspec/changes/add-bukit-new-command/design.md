# Design: `bukit new`

## Context

当前事实：

- `src/Bukit-Core/Bukit.Cli/Resources/StarterTheme/` 有 17 个主题资源，
  `theme.yaml` 已经是资源。
- `guide/user/01-quick-start.md` 明确写 Core 没有 scaffold 命令。
- BukitJalil `templates/starter/` 有 25 个文件；17 个来自 StarterTheme，真正
  由它维护的是 8 个站点级文件：`site.yaml`、两篇示例、`data` 示例、分享图、
  `robots.txt`、`LICENSE`、`.gitignore`。
- Labs 的 `InitCommand` 未路由，且生成的是旧配置契约，不能直接提升。
- `HeaderPartial.html` 第 6 行的 `{{-- bukit:brand --}}` **不是 Scriban
  注释**。Scriban 的注释语法是 `{{* ... *}}`；这个占位符在渲染时会被当作
  表达式并抛错。它必须从资源中删除，不能只在复制时替换。
- `Bukit.Cli.Shared` 没有 `InternalsVisibleTo`；把 internal 的 scaffold 放
  在那里，`bukit` 和 `Bukit.Cli.Tests` 都看不到，改成 public 又会进入公共
  API 漂移基线。
- Core 的 YAML 读写使用 `YamlStream` / `RepresentationModel` 或静态上下文，
  没有反射式 `SerializerBuilder`。CLI 是 `PublishAot` 且
  `TreatWarningsAsErrors`，不应为一个 starter 引入反射序列化。

## Decisions

### D1. 只加一个命令，不加向导

命令固定为：

```bash
bukit new <dir> [--title <text>] [--language <tag>]
```

不提供 `--force`、`--notion`、`--theme`、`--template` 或多模板选择。

理由：BukitJalil 的真实 Notion 输入是多个数据库、每个有 content/data 角色和
源名，一个 `--notion <id>` 只能表达单库，仍要回头改 YAML，等于多一个半成品
选项。多模板向导是 Labs 的实验，不是 Core 的稳定契约。

### D2. 只生成一个规范的 Markdown starter

输出对齐当前 BukitJalil starter 的语义，而不是逐字节复制：

- `site.yaml`
- `content/posts/welcome.md`
- `content/pages/about.md`
- `data/features-10.md`
- `themes/starter/**`
- `.gitignore`

不生成 README、`dist/`、`.git/` 或任何 BukitJalil 文件。

### D3. 代码放在 `Bukit.Cli`，不放在 Shared

Path A（默认）下没有任何 Labs 代码调用这些类型，所以：

- `NewCommand`、`StarterSiteScaffold` 都是 `Bukit.Cli` 的 internal 类型。
- `Bukit.Cli.Tests` 已经通过 `InternalsVisibleTo("Bukit.Cli.Tests")` 看到
  `Bukit.Cli` 的 internal；测试不需要 public API。
- `Bukit.Cli.csproj` 已经在嵌入 `Resources\StarterTheme\**`；把
  `Resources\StarterSite\**` 加进同一个 `EmbeddedResource` ItemGroup。
- 资源逻辑名前缀是 `Bukit.Cli.Resources.StarterTheme.` 和
  `Bukit.Cli.Resources.StarterSite.`。
- 不改 `Bukit.Cli.Shared.csproj`，也不新增 public 类型。

Path B（先 Labs）才需要把同一组 internal 代码放到 `Bukit.Labs.Cli`，或给
`Bukit.Cli.Shared` 增加对 Labs 的 `InternalsVisibleTo`；仍然不要 public。

### D4. `site.yaml` 用资源模板 + `YamlStream`

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

### D5. 目标目录与原子写入

- 目标必须不存在，或存在且为空目录。
- 先规范化目标：
  `var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target));`。
  `Path.TrimEndingDirectorySeparator` 不会削掉根目录；这样 `broken/` 和
  `broken` 是同一个路径。
- 拒绝文件、非空目录、目标本身是符号链接/重解析点；**包括指向不存在目标的
  断链**。`DirectoryInfo.LinkTarget` 对断链仍返回目标，所以先检查它，再检查
  `Exists`。不先 trim 时，`broken/` 的 `LinkTarget` 是 `null`，会绕过检查，
  最后在 `Directory.CreateDirectory` 抛 IO 异常，退出码变成 1。
- 不再单独拒绝根目录和用户主目录：它们不可能为空，非空规则已经拒绝；单独
  的 ordinal 比较还会漏掉尾斜杠和大小写差异，属于死分支。
- **只用一个 `created` 列表**，按创建顺序记录目录和文件。建目录 helper 从目标
  向上收集尚不存在的目录，再逐级创建并记录；遇到 `Path.GetDirectoryName`
  返回 `null` 就停止，不能调用 `Directory.Exists(null)`（Windows 上
  `Z:\` 不存在时会死循环）。失败时倒序处理：文件直接删，目录只在为空时删。
  不能 `ClearDirectory(root)`，否则会删掉其他程序在写入期间放进来的文件
  （例如 Finder 的 `.DS_Store`）。
- 目标原本存在且为空时，失败后目录仍在且为空；目标原本不存在时，失败后删除
  本次创建的最深目录及其新建父目录。

### D6. 输入校验

- `title`：`--title` 未提供时用目录名（先
  `Path.TrimEndingDirectorySeparator`；仍为空则 `My Site`）。`--title` 提供
  但 trim 后为空时**拒绝**，不能静默替换。标题 1–200 字符，不能含控制字符。
- `language`：省略时 `en-US`；使用与 `site.languages` 相同的安全规则。
  复用 `SiteConfigValidator.IsSafeLanguageTag`：把它从 `private` 改为
  `internal`，并在 `Bukit.Config/InternalsVisibleTo.cs` 加
  `[assembly: InternalsVisibleTo("bukit")]`。不复制第二份正则。
- `site.name`：标题的 ASCII slug（NFKD、小写、非 `a-z0-9` 转 `-`、去首尾、
  最多 64、空则 `site`），与 BukitJalil 的 `siteSlug` 行为一致。slug 逻辑
  放在 `NewCommand` 私有方法里，不再单独建文件。

### D7. 删除 brand 占位符，不做替换

`{{-- bukit:brand --}}` 不是 Scriban 注释，不能留在资源里。T02 直接删除
`HeaderPartial.html` 第 6 行：

```html
      {{-- bukit:brand --}}
```

复制逻辑不再执行 `Replace("{{-- bukit:brand --}}", "")`。品牌文本由
`theme.params.brand` 分支输出，不需要占位符。

### D8. 命令不做副作用

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

### D9. Core 命令面与治理

Core CLI 的架构测试使用精确白名单；新增 `new` 必须同时修改
`BukitCliSpecs`、`BukitCliDescriptors` 和
`tests/Bukit.Architecture.Tests/CoreBoundaryTests.cs`。

仓库的 2026-06-13 计划要求“新 CLI 命令先进入 Labs”。本变更的默认路径是
**G0 明确批准直接把 `new` 加入 Core**，理由：Labs 的 `InitCommand` 原型已经
存在但不可用，重新做一遍 Labs 命令只会增加一次性代码。若 G0 不批准，按计划
附录 A 先加一个薄的 Labs 证明，再提升到 Core。

### D10. 跨仓 rollout

Bukit 发布包含 `new` 的版本后，BukitJalil：

1. 升级 `BUKIT_VERSION` 和校验和。
2. 用 `BukitRunner` 调用 `bukit new`，再补 Notion `content.sources`。
3. 删除 `templates/starter/`、`scripts/make-starter.sh`、
   `starter.source.json`、哈希测试和打包拷贝。
4. `guide/skills` 同步保持独立，不并入本变更。

## Error Model

- 用户输入、目标目录、标题、语言错误：抛
  `Bukit.Shared.CommandArgumentException`；`Program.cs` 已映射为退出码 2。
  `NewCommand` 不再自己 try/catch，也不定义 `StarterSiteException`。
- 资源缺失（`InvalidOperationException`）和 IO/磁盘错误：沿用全局处理，
  退出码 1。这是安装/环境问题，不是用户参数错误。
- 未预期异常：全局处理，退出码 1。

这样 BukitJalil 可以按退出码区分“改参数/换目录”与“环境/安装故障”。

## Compatibility

- 现有站点、构建、渲染、配置读取不变。
- 新增命令不改变任何旧命令的行为。
- StarterTheme 转义和占位符删除只影响新 scaffold 和测试；已有站点不会被
  重写。
- 生成结构是事实契约；后续修改 `site.yaml` 字段需要兼容说明或迁移。

## Known Debt

- Labs 的 `StarterThemeScaffold.cs` 仍有一份 18 项资源到主题路径的映射。本变更
  不合并它；两份列表可能漂移。记录为后续清理：最好让 Labs 也调用 Core 的
  scaffold，或在 Labs 侧删除重复映射。当前不在范围内。
- StarterTheme 的 `page.title`、`item.title`、`item.summary`、`feature.title`
  等内容派生字段仍直接输出。它们是既有主题问题，不是 `new` 引入的；本变更只
  修站点级字段（`site.title`、`site.description`、`site.params.*`）。内容字段
  的转义需要单独的主题硬化变更。
- 回滚清理时如果 `File.Delete`/`Directory.Delete` 抛错，会覆盖原始异常。发生
  概率很低；代码加 `shortcut:` 注释写明限制，不吞异常。后续 hardening 可以把
  清理错误收集起来，再重抛原始异常。

## Rejected Alternatives

- **只发布 starter tarball**：仍要消费者自己拼 `site.yaml` 和 8 个站点级文件，
  不解决契约漂移。
- **`--notion`**：无法表达多数据库、角色和源名；会留下半成品接口。
- **复用 Labs `InitCommand`**：未接线，配置旧，且会带入六套模板的维护面。
- **反射式 `SerializerBuilder`**：与 AOT 和现有 `YamlStream` 契约不一致；只为
  一个 starter 引入不必要的裁剪风险。
- **保留 `{{-- bukit:brand --}}` 并在复制时替换**：它不是注释，原始资源渲染
  就会抛错；必须在资源层删除。
- **运行时执行 `git init`**：Core 不应替调用方决定仓库、身份和提交边界。

## Open Questions

- G0：**已批准 Path A（2026-11-16）**。用户明确选择直接把 `bukit new`
  加入 Core，并接受 Labs-first 例外。批准证据是本会话的 G0 选择记录。
  实施仍受计划 T00 的工作区前置检查约束。
- 默认语言用 `en-US`（与当前 starter 对齐）还是 `zh-CN`（Core 配置默认）。
  本计划采用 `en-US`；如需改为 `zh-CN`，只改默认值和测试。
- 主题 `LICENSE` 是复制一份 MIT 文本到资源，还是用构建目标嵌入根
  `LICENSE`。本计划采用资源副本，避免跨目录构建逻辑。
