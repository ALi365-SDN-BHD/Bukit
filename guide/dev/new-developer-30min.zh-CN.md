# 新开发者 30 分钟上手路线

本文档面向第一次进入仓库的开发者，目标不是“学会全部功能”，而是在 30 分钟内建立正确心智模型、跑通最小链路，并知道后续该去哪里继续深挖。

## 1. 先记住这三件事

### 1.1 仓库聚焦 Bukit 主线

当前仓库聚焦 **Bukit** 静态站点引擎，不包含 [BukitJalil](https://github.com/ALi365-SDN-BHD/BukitJalil) 相关源码与解决方案。

如果你是第一次进入仓库，**从 Bukit 开始**即可。

### 1.2 最重要的入口不是某个类，而是最短构建链路

先跑通下面四条命令，比一开始通读全部代码更有价值：

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

### 1.3 你真正要建立的是这一条主线

```text
site.yaml
  -> ConfigLoader / ConfigValidator
  -> ContentProvider
  -> RouteGenerator
  -> Scriban Rendering
  -> Plugins
  -> dist 输出
```

## 2. 30 分钟安排

| 时间段 | 目标 | 产出 |
|---|---|---|
| 0–5 分钟 | 建立仓库地图 | 知道核心模块与核心目录 |
| 5–12 分钟 | 跑通示例 | 确认环境与构建链路正常 |
| 12–20 分钟 | 看懂配置与主题 | 知道内容、模板、输出如何对齐 |
| 20–27 分钟 | 串核心代码 | 把 CLI → Engine 主链连起来 |
| 27–30 分钟 | 跑冒烟或关键测试 | 对后续改动建立信心 |

## 3. 第 1 阶段：0–5 分钟建立地图

按下面顺序快速阅读：

1. `README.md`
2. `guide/dev/README.md`
3. `guide/dev/code-wiki.md`

重点只看这些问题：

- 这个仓库是做什么的？
- 哪条主线是核心？
- 哪个目录放源码、示例、文档、测试？

### 3.1 你要得到的结论

- `src/` 里是核心源码
- `examples/starter/` 是最重要的可运行示例
- `guide/dev/` 是维护者文档
- `tests/` 和 `scripts/` 是修改前后的验证入口

## 4. 第 2 阶段：5–12 分钟跑通最小链路

### 4.1 推荐命令

```bash
dotnet build bukit.slnx -c Release
dotnet run --project src/Bukit.Cli -c Release -- doctor --config examples/starter/site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config examples/starter/site.yaml --clean --site-url https://example.com
dotnet run --project src/Bukit.Cli -c Release -- preview --dir examples/starter/dist --port auto
```

### 4.2 你在观察什么

- `doctor` 能否通过
- `build` 是否能生成 `examples/starter/dist`
- `preview` 是否能正常打开页面

### 4.3 如果你在 Windows

也可以直接跑：

```powershell
pwsh ./scripts/smoke.ps1
```

它会顺带覆盖：

- 普通构建
- i18n
- taxonomy
- modules
- intent
- 多站点

## 5. 第 3 阶段：12–20 分钟看懂配置与主题

这一阶段不要先看复杂代码，先把“输入和输出长什么样”搞清楚。

### 5.1 先看示例配置

重点阅读：

- `examples/starter/site.yaml`

先只关注四组字段：

- `site`
- `content`
- `build`
- `theme`

### 5.2 再看目录约定

重点记住：

- 大多数相对路径都相对 `site.yaml`
- 主题通常由 `layouts / assets / static` 构成
- 输出目录通常是 `dist`

### 5.3 再看示例主题

建议直接打开：

- `examples/starter/themes/alt/layouts/layouts/base.html`
- `examples/starter/themes/alt/layouts/pages/page.html`

这一步的目标不是学会 Scriban，而是确认：

- `site.*` 是怎么传给模板的
- `page.*` 是怎么传给模板的
- 页面最终是怎样被拼出来的

## 6. 第 4 阶段：20–27 分钟串核心代码

这一阶段只看主链，不要一开始钻进所有实现细节。

### 6.1 推荐阅读顺序

1. `src/Bukit.Cli/Program.cs`
2. `src/Bukit.Cli/Commands/BuildCommand.cs`
3. `guide/dev/architecture.md`
4. `src/Bukit.Engine/SiteEngine.cs`
5. `src/Bukit.Routing/RouteGenerator.cs`

### 6.2 你应该关注的问题

#### `Program.cs`

- CLI 如何分发命令？
- `build`、`doctor`、`preview` 分别进入哪里？

#### `BuildCommand.cs`

- CLI 参数如何覆盖 `site.yaml`？
- 哪些参数只影响运行时，不直接写回配置？

#### `SiteEngine.cs`

- 主流程从哪里开始？
- 多语言是在哪一层被拆分的？
- 插件和渲染是在什么时候执行的？

#### `RouteGenerator.cs`

- 默认 `post` / `page` 路由规则是什么？
- `route` override 与 `permalinks` 的优先级是什么？

## 7. 第 5 阶段：27–30 分钟用验证校准理解

如果你准备开始改代码，最后 3 分钟至少做一件事：

### 7.1 看一个关键测试

建议从下面里任选其一：

- `tests/Bukit.Cli.Tests/ConfigPathResolverTests.cs`
- `tests/Bukit.Engine.Tests/RouteGeneratorTests.cs`

### 7.2 或直接跑一个小范围测试

```bash
dotnet test tests/Bukit.Cli.Tests/Bukit.Cli.Tests.csproj -c Release
dotnet test tests/Bukit.Engine.Tests/Bukit.Engine.Tests.csproj -c Release --filter RouteGenerator
```

### 7.3 或跑一次 smoke

```powershell
pwsh ./scripts/smoke.ps1
```

目的很简单：

- 确认仓库在你机器上是健康的
- 知道后续改动后该如何回归验证

## 8. 初学者最容易踩的坑

### 8.1 误以为所有文档入口都在仓库内

当前仓库聚焦 `Bukit` 主线。如果你在其他资料中看到 [BukitJalil](https://github.com/ALi365-SDN-BHD/BukitJalil) 相关入口，请以仓库实际目录与 `bukit.slnx` 为准。

### 8.2 误以为相对路径相对当前终端目录

很多配置路径实际上相对 `site.yaml`，不是相对你执行命令时所在目录。

### 8.3 还没跑 `doctor` 就开始猜问题

推荐排障顺序：

1. 先跑 `doctor`
2. 再跑 `build --clean`
3. 再对照 `examples/starter`

### 8.4 把 `mode=data` 当成普通页面

`mode=data` 不生成普通页面路由，而是注入 `site.modules`。

### 8.5 本地正常、部署后 404

优先检查：

- `site.baseUrl`
- 模板资源是否正确使用 `site.base_url`

## 9. 30 分钟之后怎么继续

### 9.1 如果你要改 CLI / 配置

继续看：

- `guide/dev/cli.md`
- `guide/dev/config-site-yaml.md`
- `tests/Bukit.Cli.Tests/*`

### 9.2 如果你要改内容系统

继续看：

- `guide/dev/content.md`
- `src/Bukit.Content/*`
- `tests/Bukit.Content.Tests/*`

### 9.3 如果你要改渲染或主题

继续看：

- `guide/dev/rendering-scriban.md`
- `guide/dev/theme.md`
- `examples/starter/themes/*`
- `tests/Bukit.Rendering.Tests/*`

### 9.4 如果你要改插件或输出

继续看：

- `guide/dev/plugins.md`
- `guide/dev/built-in-plugins.md`
- `src/Bukit.Engine/Plugins/*`
- `tests/Bukit.Engine.Tests/*`

### 9.5 仓库边界提示

当前仓库聚焦 `Bukit` 主线，不包含 [BukitJalil](https://github.com/ALi365-SDN-BHD/BukitJalil) 相关源码与解决方案。

## 10. 一页版 Checklist

- 读 `README.md`
- 读 `guide/dev/README.md`
- 读 `guide/dev/code-wiki.md`
- 跑 `build + doctor + preview`
- 看 `examples/starter/site.yaml`
- 看示例主题 `base.html / page.html`
- 看 `Program.cs`
- 看 `BuildCommand.cs`
- 看 `SiteEngine.cs`
- 看 `RouteGenerator.cs`
- 跑一个测试或 `smoke.ps1`

## 11. 配套文档

- 仓库代码总览：[`code-wiki.md`](./code-wiki.md)
- 模块调用关系图：[`code-wiki-call-graph.md`](./code-wiki-call-graph.md)
- 架构边界：[`architecture.md`](./architecture.md)

## 12. 一句话总结

第一次进入这个仓库时，最有效的方式不是“从头到尾读源码”，而是**先跑通示例，再顺着 CLI → Engine 主链看代码，最后用测试或 smoke 固化理解**。
