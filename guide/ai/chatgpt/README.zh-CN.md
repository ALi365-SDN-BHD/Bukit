# ChatGPT + Bukit：对话式建站 Prompt Pack

语言版本：简体中文（当前）| [English](./README.md) | [Bahasa Melayu](./README.ms.md)

本目录提供“可直接复制粘贴”的 Prompt 与说明，用于把建站入口迁移到 ChatGPT：由 AI 产出 `intent.yaml`（优先）或 `site.yaml`，再由 Bukit 做确定性校验与构建。

## 两种用法

### 用法 A：直接在 ChatGPT 对话中使用（最简单）

1. 把 [system_instructions.md](./system_instructions.md) 的内容粘贴到 ChatGPT 的“系统约束”（或当作对话开场的第一条消息）。
2. 选择一种输出路线：
   - 推荐：粘贴 [prompt_intent.md](./prompt_intent.md) 并按问卷填写，生成 `intent.yaml`
   - 仅当你明确要直接写配置：粘贴 [prompt_site_yaml.md](./prompt_site_yaml.md) 生成 `site.yaml`
3. 本地闭环（推荐先跑校验再构建）：

```bash
dotnet run --project src/Bukit.Cli -c Release -- intent validate intent.yaml
dotnet run --project src/Bukit.Cli -c Release -- intent apply intent.yaml --out site.yaml
dotnet run --project src/Bukit.Cli -c Release -- doctor --config site.yaml
dotnet run --project src/Bukit.Cli -c Release -- build --config site.yaml --clean --site-url https://example.com
```

4. 如果 `intent validate` / `doctor` 报错：把错误输出粘贴给 ChatGPT，并使用 [prompt_fix_config.md](./prompt_fix_config.md) 让 AI 只返回修复后的 YAML。

### 用法 B：创建一个「Bukit 官方 GPT」（体验更像产品）

1. GPT Instructions 使用 [system_instructions.md](./system_instructions.md)。
2. GPT Knowledge 建议放入 [knowledge_manifest.md](./knowledge_manifest.md) 列出的文件（字段表、Intent 契约、示例配置、CLI 速查等）。
3. 日常对话中优先让 GPT 输出 `intent.yaml`，再走 `intent validate/apply`。

## 运行前的最低准备

- Notion 内容源：`NOTION_TOKEN` 必须设置为环境变量（不要粘贴到对话里）。
- GitHub Pages 子路径：`site.baseUrl` 必须以 `/` 开头，例如 `/my-repo`；根路径用 `/`。

