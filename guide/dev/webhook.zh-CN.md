# Webhook（触发器与安全约束）

Webhook 子命令用于把外部事件（例如 Notion webhook）转换为 GitHub `repository_dispatch`，从而触发构建工作流。

它不影响核心引擎能力，属于“部署集成工具”。

实现参考：`src/Bukit.Cli/Commands/WebhookCommand.cs`

## 基本用法

```bash
dotnet run --project src/Bukit.Cli -c Release -- webhook --repo owner/repo --port 8787 --path /webhook/notion --event bukit_notion
```

启动后会监听：
- `http://<host>:<port><path>`（只接受 POST）

## 参数

| 参数 | 默认值 | 说明 |
|---|---|---|
| `--host <host>` | `localhost` | 监听地址 |
| `--port <port>` | `8787` | 监听端口 |
| `--path <path>` | `/webhook/notion` | 请求路径 |
| `--repo <owner/repo>` | - | GitHub 仓库（也可用环境变量） |
| `--event <event_type>` | `bukit_notion` | repository_dispatch 的 event_type |

## 环境变量（必需）

| 变量 | 作用 |
|---|---|
| `BUKIT_WEBHOOK_TOKEN` | 入站鉴权 token |
| `BUKIT_GITHUB_TOKEN`（或 `GITHUB_TOKEN`） | 调用 GitHub API 的 token |
| `BUKIT_GITHUB_REPO` | 可选：替代 `--repo`（格式 `owner/repo`） |

## 安全约束与鉴权

入站请求必须满足：
- 方法：POST
- 路径：完全匹配 `--path`
- 请求头：`X-Sitegen-Token` 必须等于 `BUKIT_WEBHOOK_TOKEN`

否则返回：
- 405（非 POST）
- 404（路径不匹配）
- 401（token 不匹配）

## 触发行为

满足鉴权后，Webhook 会向 GitHub API 发送：
- `POST https://api.github.com/repos/<owner/repo>/dispatches`
- payload：`{ event_type, client_payload: { source: "bukit-webhook" } }`

并返回 202。
