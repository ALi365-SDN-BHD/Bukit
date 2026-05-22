# Bukit Agent Knowledge Base — 完整文件清单

> 所有 AI Agent 相关文件的唯一真源目录：`src/skills/`

## 一、src/skills/ 目录（AI Agent 源码目录）— 36 个文件

```
src/skills/
├── skills-index.yaml              # 知识索引（机器可读，单一真源）
├── skills-index.json              # JSON 版索引（脚本自动生成）
├── plugin.json                    # Claude Code / Copilot 插件清单
│
├── CLAUDE.md                      # Claude Code Agent 完整入口
├── AGENTS.md                      # Codex CLI Agent 完整入口
├── GEMINI.md                      # Gemini CLI Agent 完整入口
├── copilot-instructions.md        # Copilot CLI Agent 完整入口
│
├── README.md                      # 英文使用说明
├── README.zh-CN.md                # 中文使用说明
├── README.ms.md                   # 马来文使用说明
│
├── using-bukit/SKILL.md           # 网关技能（入口路由）
├── bukit-cli-reference/SKILL.md   # CLI 参考
├── bukit-config/SKILL.md          # 配置模型
├── bukit-theme/SKILL.md           # 主题目录
├── bukit-templating/SKILL.md      # Scriban 模板
├── bukit-design-tokens/SKILL.md   # 设计令牌
├── bukit-content-to-template/SKILL.md  # Schema→模板
├── bukit-notion/SKILL.md          # Notion 内容源
├── bukit-routing/SKILL.md         # URL 路由
├── bukit-i18n/SKILL.md            # 多语言
├── bukit-plugins-debug/SKILL.md   # 插件调试
├── bukit-deploy/SKILL.md          # GitHub Pages 部署
├── bukit-clone/SKILL.md           # 网站克隆
├── bukit-seo/SKILL.md             # 传统 SEO
├── bukit-geo/SKILL.md             # GEO（AI 搜索引擎优化）
├── bukit-preview/SKILL.md         # 本地预览
├── bukit-dev/SKILL.md             # HMR 开发服务器
├── bukit-webhook/SKILL.md         # Webhook 自动部署
│
└── scripts/
    ├── validate-skills.sh         # 技能文件验证
    └── generate-index-json.sh     # YAML → JSON 转换
```

## 二、根目录平台约定文件（轻量引用，指向 src/skills/）

各平台要求入口文件在固定位置，根目录放置轻量引用：

| 文件 | 平台 | 内容 |
|------|------|------|
| `CLAUDE.md` | Claude Code | 指向 `src/skills/CLAUDE.md` |
| `AGENTS.md` | Codex CLI | 指向 `src/skills/AGENTS.md` |
| `GEMINI.md` | Gemini CLI | 指向 `src/skills/GEMINI.md` |
| `.github/copilot-instructions.md` | Copilot CLI | 指向 `src/skills/copilot-instructions.md` |

每个引用文件约 10 行，形如：
```
# Bukit Agent Knowledge Base
# Canonical path: src/skills/CLAUDE.md
The full Bukit agent instructions live at `src/skills/CLAUDE.md`.
```

## 三、验证结果

- ✅ `validate-skills.sh`：18 skills valid, 0 plugin.json errors
- ✅ `dotnet build -warnaserror`：0 警告 0 错误
- ✅ `skills-index.yaml`：18 技能 + 12 工作流
- ⚠️ 4 个 SKILL.md 缺少 "Common Errors" 章节（不影响功能，后续优化）
