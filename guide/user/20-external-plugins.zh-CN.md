# 编写外部插件

## 插件分类与安全级别

Bukit 根据运行时模型和信任边界将插件分为四种类型：

| 插件类型 | 定位 | 安全级别 | 备注 |
|---------|------|---------|------|
| 内置插件 | 引擎内部能力 | 高 | 进程内运行，完全信任 |
| 进程插件 | 本地可信扩展 | 低 | 拥有宿主机完整进程权限，无沙箱隔离。CI 环境默认禁用，使用 `--allow-external-plugins` 启用。 |
| 未来 WASM 插件 | 可分发社区插件 | 中高 | 沙箱隔离，资源受限 |
| Section 插件 | 主题组件级能力 | 中 | 主题范围内能力 |

**进程插件**是目前支持的外部插件运行时。以下章节详细说明其安全模型、配置和使用方式。

## 安全与信任模型

**外部插件作为子进程运行，拥有完整的宿主机进程权限。**这意味着：

- 它们可以读取宿主机文件系统上的**任何文件**，不限于项目目录。
- 它们可以访问网络并建立任意出站连接。
- 它们可以执行任意的子进程和系统命令。
- 插件与宿主机之间**没有沙箱**或容器隔离。

**因此，你必须：**
- 只从**可信来源**安装插件（你认识并信任的作者，或官方 Bukit 插件注册表）。
- 在将插件添加到项目之前审查其源代码。
- 绝不在生产环境中使用来自不可信第三方的插件。

**额外的安全措施：**
- **CI 环境默认禁用外部插件。**要在 CI 中启用，请在命令行传递 `--allow-external-plugins`。
- **通过 `externalPluginPolicy` 控制插件加载：**设置 `site.externalPluginPolicy` 为 `deny`（阻止所有）、`warn`（加载并警告，默认）或 `allow`（静默加载）。无效值会抛出 `ConfigException`，错误码 `BKT-0002`。
- **插件入口路径必须在项目目录内。**绝对路径如 `/usr/bin/some-tool` 会被拒绝，除非插件在配置中显式设置 `allowAbsoluteEntry: true`。
- **stdout/stderr 输出有大小限制**，默认 1MB（可通过 `maxStdoutBytes` / `maxStderrBytes` 配置）。
- **超时保护：**插件超过 `timeoutMs` 会被自动终止。
- **环境隔离：**仅透传 `BUKIT_*` 变量和 `AllowEnvironment` 白名单中的变量给插件子进程。
- **输出路径校验：**插件无法写入配置的输出目录之外。

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
      allowAbsoluteEntry: false # 仅在 entry 为绝对路径时设为 true
```

| 字段 | 说明 |
|------|------|
| `runtime` | 固定为 `"process"` |
| `entry` | 插件可执行文件路径（相对项目根目录） |
| `hooks` | 参与的生命周期钩子：`after-build`、`derive-pages` |
| `capabilities` | 必填：`emit-outputs`（after-build 用）、`derive-pages`（derive-pages 用） |
| `timeoutMs` | 插件超时时间（默认 5000 毫秒） |
| `allowAbsoluteEntry` | 允许 `entry` 使用绝对路径（默认 `false`）。仅在插件二进制文件位于项目外部时需要。 |
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
