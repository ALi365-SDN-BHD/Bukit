# Intent（意图文件）在仓库中的落地与用法

Intent 是“对外契约”在 CLI 中的落地实现，用于把一个结构化 intent.yaml 转换成可执行的 site.yaml 配置，并在生成前进行校验。

相关专题：
- [ChatGPT Prompt Pack](../ai/chatgpt/README.zh-CN.md)
- [CLI 参考](./cli.md)

实现参考：
- `src/Bukit.Cli/Commands/IntentCommand.cs`
- `src/Bukit.Cli/Intent/*`

## 三个子命令

### 1) init

交互式生成 intent 文件：

```bash
bukit intent init --out intent.yaml
```

会写入指定路径，并提示下一步 validate/apply。

### 2) validate

校验 intent 是否可被正确应用（不会写文件）：

```bash
bukit intent validate intent.yaml
```

rootDir 推断规则：
- 优先使用 `--root-dir <dir>`
- 若传了 `--out <path>` 且输出路径位于 `./sites/` 下：rootDir = 当前目录
- 否则 rootDir = outPath 的目录（或当前目录）

返回码：
- 0：校验通过
- 1：存在错误
- 2：参数缺失等用法错误

### 3) apply

把 intent 转换为 site.yaml：

```bash
bukit intent apply intent.yaml --out site.yaml
```

行为：
- 会输出 warnings/errors（错误会导致返回码 1）
- 成功会写出配置文件，并提示如果写入的是 `sites/<name>.yaml`，构建时应使用 `bukit build --site <name>`

## 与 build/multi-site 的关系

当你把生成结果写入：
- 根目录 `site.yaml`：直接 `bukit build`
- `sites/<name>.yaml`：使用 `bukit build --site <name>`（rootDir 仍为仓库根）

