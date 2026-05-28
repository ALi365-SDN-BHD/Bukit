# 编写外部插件

外部插件让你可以用**任意语言**（Node.js、Python、Go 等）扩展 Bukit，通过简单的 stdin/stdout JSON 协议通信。它们作为子进程运行，完全兼容 Bukit 的 Native AOT 构建。

## 工作原理

```
Bukit 引擎                      你的插件（子进程）
     |                               |
     |--- JSON 请求 → stdin -------→ |  （读取请求）
     |                               |  （处理逻辑）
     |←-- JSON 响应 ← stdout ------ |  （写入响应）
     |                               |
```

Bukit 启动你的插件入口程序，通过 stdin 发送 JSON 请求，再从 stdout 读取 JSON 响应。

## 配置

在 `site.yaml` 中添加 `externalPlugins` 配置段：

```yaml
site:
  externalPlugins:
    my-plugin:
      runtime: process          # 仅支持 "process"
      entry: plugins/my-plugin.js
      hooks: [derive-pages]     # 或 [after-build]，或两者都加
      capabilities: [derive-pages]
      timeoutMs: 5000
```

| 字段 | 说明 |
|------|------|
| `runtime` | 固定为 `"process"` |
| `entry` | 插件可执行文件路径（相对项目根目录） |
| `hooks` | 参与的生命周期钩子：`after-build`、`derive-pages` |
| `capabilities` | 必填：`emit-outputs`（after-build 用）、`derive-pages`（derive-pages 用） |
| `timeoutMs` | 插件超时时间（默认 5000 毫秒） |
| `options` | 可选：传递给插件的自定义键值对 |

## 协议概述

### 请求格式

Bukit 发送的 JSON 对象：

```json
{
  "schemaVersion": "2",
  "hook": "derive-pages",
  "plugin": { "name": "my-plugin", "version": "0.1.0" },
  "site": { "baseUrl": "/", "language": "zh-CN", "title": "我的站点" },
  "projectRoot": "/path/to/project",
  "outputDir": "/path/to/project/dist",
  "derivePages": {
    "routedPages": [
      { "id": "...", "title": "...", "slug": "...", "url": "/...", "collection": "..." }
    ]
  },
  "config": { "options": {} }
}
```

### 响应格式

你的插件必须向 stdout 写入 JSON：

```json
{
  "ok": true,
  "derivedPages": [
    {
      "id": "my-page",
      "title": "我的生成页面",
      "slug": "my-page",
      "url": "/my-page/",
      "outputPath": "my-page/index.html",
      "contentHtml": "<p>生成的内容</p>"
    }
  ],
  "logs": [{ "level": "info", "message": "生成了 1 个页面" }]
}
```

## 钩子：derive-pages

用 derive-pages 根据已有路由生成新页面。插件接收所有已路由的页面信息，返回新的派生页面。

### 后构建钩子（after-build）

```json
// 请求
{
  "hook": "after-build",
  "afterBuild": {
    "outputDir": "/project/dist",
    "routedPages": [...]
  }
}

// 响应
{
  "ok": true,
  "outputs": [
    { "path": "plugin-data.json", "contentType": "application/json", "text": "..." }
  ]
}
```

## 安全模型

- **输出大小限制**：stdout/stderr 默认 1MB（可通过 `maxStdoutBytes`/`maxStderrBytes` 调整）
- **超时保护**：超过 `timeoutMs` 自动终止
- **环境隔离**：仅透传 `BUKIT_*` 变量和 `AllowEnvironment` 白名单中的变量
- **输出路径校验**：插件无法写入输出目录之外

## 完整示例：Node.js Derive-Pages 插件

```javascript
#!/usr/bin/env node
const { stdin, stdout } = require("process");

let raw = "";
stdin.setEncoding("utf-8");
stdin.on("data", chunk => raw += chunk);
stdin.on("end", () => {
    const req = JSON.parse(raw);

    if (req.hook === "handshake") {
        stdout.write(JSON.stringify({
            ok: true,
            supportedHooks: ["derive-pages"],
            negotiatedSchemaVersion: "2"
        }));
        return;
    }

    if (req.hook === "derive-pages") {
        const count = req.derivePages.routedPages.length;
        stdout.write(JSON.stringify({
            ok: true,
            derivedPages: [{
                id: "hello",
                title: `你好（${count} 个页面）`,
                slug: "hello",
                url: "/hello/",
                outputPath: "hello/index.html",
                contentHtml: `<p>从 ${count} 个页面生成。</p>`,
                publishAt: new Date().toISOString()
            }],
            logs: [{ level: "info", message: `从 ${count} 个页面派生了 hello` }]
        }));
    }
});
```

完整可运行的示例见 `examples/plugin-site/plugins/hello-derive.js`。

## 故障排查

| 问题 | 检查 |
|------|------|
| 找不到插件 | 确认 `entry` 路径相对于项目根目录正确 |
| 无输出 | 检查 stderr 错误信息。设置 `logging.level: debug` 可在日志中看到 |
| 超时 | 增大 `timeoutMs` 或简化插件逻辑 |
| 权限拒绝 | 确保插件文件可执行（`chmod +x`） |
| JSON 响应无效 | 手动测试：`echo '{"hook":"handshake"}' | node plugin.js` |
