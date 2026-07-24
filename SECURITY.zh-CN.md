# 安全策略

## 支持的版本

| 版本   | 支持状态 |
|--------|----------|
| 2.0.x  | 面向内部使用进行治理；不提供公开支持 SLA |
| 1.x    | 历史版本；不作公开支持承诺 |

## 报告漏洞

如果你发现 Bukit 存在安全漏洞，请私下报告。

**请勿公开提交 Issue。** 请将详情发送给维护者。

欢迎善意的私下报告，并可能会在尽力而为的基础上评审。项目不承诺公开的确认期限、修复期限、支持 SLA 或发布时间表。见 [Bukit Core 产品定位](docs/governance/bukit-core-product-positioning.md)。

## 安全注意事项

### Core 内容与输出边界

当前 Core 安全行为包括：

- 配置式和显式输出清理共用一个受保护 cleaner；项目根、home、文件系统根、`.git`、项目外路径、symlink/reparse target 以及无 marker 的非空目录都会被拒绝；
- 默认生成的搜索 UI 把内容 title 和 snippet 当作文本，不让它们进入 HTML 解释型 sink；
- 默认递归的 content、static、media 和 report inventory 路径不会下降进入目录 symlink 或 reparse point。

这些保证不负责清洗任意主题、自定义脚本或第三方插件输出；`build.followSymlinks: true` 仍只适用于受支持的 copy path。完整行为与排除项见 [Core 安全与可靠性](guide/user/20-core-safety-reliability.md)。

### Core 与 Labs 边界

Bukit Core 不把进程内 hook API 作为稳定扩展边界。扩展行为的安全评审应从外部进程插件路径开始：`Bukit.PluginHost`、`Bukit.Plugin.Abstractions`、项目插件配置以及插件包 `plugin.yaml`。

Labs 功能，包括 webhook 工作流，不属于稳定 Core 命令注册表。请将 Labs 服务视为独立部署表面，不要把它们描述为 Core 运行时保证。当前 Labs webhook 边界见 [guide/labs/webhook.md](guide/labs/webhook.md)。

### 外部插件

外部插件通过 `bukit-plugin-v1` 协议作为独立进程运行。请仅使用来自可信来源的插件，并在启用前验证包清单。

插件安全评审应确认：

- `plugin.yaml` 声明预期的 id、protocol、platforms、entries 和所需权限。
- 运行时入口通过 `Bukit.PluginHost` 选择，并在调用前进行 hash 校验。
- 文件系统、环境变量、超时和输出权限都显式且最小化。
- CI 执行是有意启用的，且不会绕过插件清单或权限检查。
- 报告会屏蔽 secret，避免写入原始 token 值。

当前插件 host 边界见 [guide/dev/plugins.md](guide/dev/plugins.md)。

### Secrets 与 Tokens

不要把 token、API key、webhook shared secret 或部署凭据提交到版本控制。配置文件可以命名所需 secret 来源，但不能嵌入 secret 值。

自动化和部署请使用外部 secret provider，例如 GitHub Actions secrets、部署平台 secret manager，或开发环境中的本地环境管理器。Bukit 从运行时环境读取 provider secret；插件只能接收显式授权的环境权限。

配置契约规则见 [guide/dev/config-site-yaml.md](guide/dev/config-site-yaml.md)，发布/部署边界见 [guide/dev/publish-deploy.md](guide/dev/publish-deploy.md)。
