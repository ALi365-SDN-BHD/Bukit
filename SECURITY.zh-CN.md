# 安全策略

## 支持的版本

| 版本   | 支持状态          |
|--------|-------------------|
| 1.0.x  | :white_check_mark: |

## 报告漏洞

如果你发现 Bukit 存在安全漏洞，请私下报告。

**请勿公开提交 Issue。** 请将详情发送给维护者。

我们将在 7 天内确认收到报告，并目标在 30 天内提供修复。

## 安全注意事项

### Webhook（`bukit webhook`）

Webhook 服务器接受入站 HTTP 请求并触发 GitHub `repository_dispatch` 事件。安全使用建议：

- 务必设置 `BUKIT_WEBHOOK_TOKEN` 以验证入站请求
- 生产部署使用 HTTPS（如通过反向代理）
- 内置限流器（每分钟 10 次请求）限制速率
- 完整部署指南见：[guide/dev/webhook.md](guide/dev/webhook.md)

### Notion API Token

Notion 集成 token 属于敏感信息。请将其存储在环境变量或安全凭据存储中：

```bash
export BUKIT_NOTION_TOKEN=secret_xxx
```

切勿将 token 提交到版本控制。

### 外部插件

外部插件以独立进程或 WASM 模块运行。请仅使用来自可信来源的插件。
