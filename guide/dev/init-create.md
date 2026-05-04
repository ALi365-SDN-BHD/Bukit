# init/create（脚手架初始化）

`bukit init <dir>`（也可用同义命令 `create <dir>`）用于创建一个最小可运行站点工程：包含 `site.yaml`、内容目录、以及一个可用主题（layouts/assets/static 与必需模板）。

实现参考：`src/Bukit.Cli/Commands/InitCommand.cs`

相关文档：
- [命令行（CLI）参数参考](./cli.md)
- [配置（site.yaml）字段参考](./config-site-yaml.md)
- [主题开发](./theme.md)
- [doctor](./doctor.md)

## 基本用法

```bash
bukit init my-site
```

同义命令：

```bash
bukit create my-site
```

## 参数

当前脚手架支持两类参数：

- `--provider <markdown|notion>`（默认 markdown）
- `--template <name>`（默认 minimal；当前版本仅写入配置，不影响文件生成）

说明：
- `--provider notion` 会生成 Notion 模式的 `site.yaml`，但 `databaseId` 需要你自行填写
- 本命令不会触碰当前目录以外的文件，只会在目标目录下创建/覆盖脚手架文件

## 生成的目录结构

执行后，目标目录会生成如下结构（省略部分文件）：

```text
<dir>/
  site.yaml
  README.md
  .gitignore
  content/
    hello-world.md
  themes/
    starter/
      assets/
        style.css
      static/
      layouts/
        layouts/
          base.html
        pages/
          index.html
          list.html
          page.html
          post.html
        partials/
          header.html
          footer.html
```

对应关系：
- `site.yaml` 中默认写入 `theme.name: starter`，并保留 `layouts/assets/static` 为默认值（见 [主题开发](./theme.md)）
- `hello-world.md` 默认作为 `type: page` 的内容页渲染（路由规则见 [routing](./routing.md)）
- 主题模板满足 `doctor` 的必需模板清单（见 [doctor](./doctor.md)）

## 生成的关键文件说明

### 1) .gitignore

脚手架默认忽略：
- `dist/`：构建输出目录
- `.bukit/`：历史缓存目录（当前清理/缓存主目录是 `.cache/`，见 [缓存与清理](./cache-clean.md)）

注意：如果你希望默认忽略 `.cache/`，应在新站点的 `.gitignore` 里手动加上（或后续调整脚手架实现）。

### 2) site.yaml

Markdown 模式下的关键字段：
- `content.provider: markdown`
- `content.markdown.dir: content`
- `theme.name: starter`
- `build.output: dist`

Notion 模式下的关键字段：
- `content.provider: notion`
- `content.notion.databaseId: xxxxx`（占位）

字段含义与默认值详见：[config-site-yaml.md](./config-site-yaml.md)。

### 3) starter 主题

starter 主题是“最小可运行主题”，包含：
- `layouts/layouts/base.html`：基础 layout（引用 `site.base_url` 拼接资源）
- `partials/header.html` / `partials/footer.html`
- `pages/page.html` / `pages/post.html` / `pages/index.html` / `pages/list.html`
- `assets/style.css`

主题如何使用 `theme.params`、如何扩展 modules 等高级用法见：[theme.md](./theme.md)。

## 建议的验证流程

在目标目录内：

1. `bukit doctor` 确认配置与模板健全
2. `bukit build --clean` 生成站点
3. `bukit preview --dir dist` 本地预览

## 已知限制与改进点

- `--template` 目前只影响写入配置的 templateName（暂未驱动不同文件模板生成）
- `.gitignore` 目前默认忽略 `.bukit/`，但引擎默认缓存目录为 `.cache/`（见 [缓存与清理](./cache-clean.md)）

