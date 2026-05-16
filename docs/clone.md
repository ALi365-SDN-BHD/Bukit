最优方案：CLI 命令 + AI Skill 混合架构
5.1 核心思路

Plain Text

┌─────────────────────────────────────────────────────────┐
│                 Bukit Clone 混合架构                       │
│                                                         │
│  ┌─────────────────────┐    ┌─────────────────────────┐ │
│  │    AI Agent Skill   │    │    CLI Command           │ │
│  │   (bukit-clone)     │    │   (bukit clone)          │ │
│  │                     │    │                         │ │
│  │  负责:              │    │  负责:                  │ │
│  │  ├── 浏览器访问     │    │  ├── 从 tokens.json     │ │
│  │  ├── 设计令牌提取   │    │  │   生成完整主题       │ │
│  │  ├── 页面语义识别   │    │  ├── 生成 17 个模板     │ │
│  │  ├── 区块类型判断   │    │  ├── 生成 style.css     │ │
│  │  └── 输出 tokens.json│   │  ├── 更新 site.yaml     │ │
│  │                     │    │  └── 确定性、可重复     │ │
│  └────────┬────────────┘    └───────────▲─────────────┘ │
│           │                             │               │
│           └── tokens.json ──────────────┘               │
│                (中间数据格式)                              │
└─────────────────────────────────────────────────────────┘
分离原则：把"智能判断"留给 AI Skill，把"确定性生成"交给 CLI 命令。

5.2 为什么这是最优解
这个混合方案直接对标了 Bukit 现有的成功模式：


Plain Text

现有模式:                    新增模式:
bukit theme create           bukit clone
  └── CLI: 生成文件          └── CLI: CloneThemeGenerator（从 tokens 生成主题）
  └── Skill: bukit-theme     └── Skill: bukit-clone（提取 tokens + 语义判断）

两者的 CLI 部分都由 StarterThemeScaffold 式的生成器支撑
两者的 Skill 部分都提供 AI 辅助和智能决策
5.3 用户场景分析

Plain Text

场景 A: 有 AI Agent 的用户（如 Claude Code）
────────
/clone-website https://stripe.com --theme stripe

AI Agent 流程:
  1. 浏览器 MCP 打开 stripe.com
  2. 执行 JS 提取设计令牌 → tokens.json
  3. 分析页面区块语义 → layout.json
  4. 调用 CLI: bukit clone --tokens tokens.json --layout layout.json --theme stripe
  5. 验证: bukit build

  ✅ 全自动，5 分钟内完成


场景 B: 无 AI Agent 的用户
────────
# 手动提取设计令牌（或使用任意浏览器工具）
# 编辑 tokens.json:
{
  "colors": { "bg": "#f6f9fc", "primary": "#635bff", ... },
  "typography": { "family": "Inter, sans-serif", ... },
  ...
}

bukit clone --tokens tokens.json --theme my-clone

  ✅ 无需 AI Agent，CLI 独立完成主题生成
  ✅ 可重复，不同用户/不同时间相同 tokens 产出相同主题


场景 C: 仅提取令牌，手动微调
────────
bukit clone --url https://example.com --tokens-only

  产出: tokens.json

# 用户手动编辑 tokens.json
# 然后:
bukit clone --tokens tokens.json --theme my-clone

  ✅ 灵活性最高，用户可以在生成前精确调整设计参数
5.4 架构分层

Plain Text

┌────────────────────────────────────────────────────┐
│                    用户界面层                        │
│                                                    │
│  AI Agent (Claude Code 等)                         │
│    └── /clone-website                         │
│         └── 读取 bukit-clone SKILL.md              │
│              └── 调用 bukit clone CLI              │
│                                                    │
│  终端用户                                           │
│    └── bukit clone --tokens tokens.json --theme X  │
│                                                    │
├────────────────────────────────────────────────────┤
│                    CLI 层 (Bukit.Cli)               │
│                                                    │
│  CloneCommand.cs             ← 命令解析/路由        │
│  CloneThemeGenerator.cs      ← 核心生成逻辑         │
│  CloneTokenExtractor.cs      ← 可选: 从 URL 提取    │
│                                                    │
│  (这就是方案三的实现位置)                             │
├────────────────────────────────────────────────────┤
│                    Skill 层 (SKILL.md)              │
│                                                    │
│  bukit-clone/SKILL.md        ← AI Agent 指令        │
│    ├── Phase 1: 浏览器侦察 → tokens.json           │
│    ├── Phase 2: 调用 bukit clone CLI               │
│    └── Phase 3: 验证                               │
│                                                    │
│  (这是方案三的实现位置)                               │
├────────────────────────────────────────────────────┤
│                    引擎层 (Bukit.Engine)             │
│                                                    │
│  ★ 不需要任何修改！                                  │
│    克隆完全不涉及构建期流程                            │
│                                                    │
└────────────────────────────────────────────────────┘
5.5 具体实现要点
CLI 层（CloneCommand + CloneThemeGenerator）：


C#

// CloneCommand.cs — 对标 ThemeCommand 的模式
public static class CloneCommand
{
    public static async Task<int> RunAsync(ArgReader reader)
    {
        var sub = reader.GetArg(1);
        return sub switch
        {
            null or "" => await CloneAsync(reader),  // 默认：生成主题
            "tokens" => await ExtractTokensAsync(reader),  // bukit clone tokens --url ...
            _ => Unknown(sub)
        };
    }

    private static async Task<int> CloneAsync(ArgReader reader)
    {
        var tokensPath = reader.GetOption("--tokens");
        var layoutPath = reader.GetOption("--layout");
        var themeName = reader.GetOption("--theme") ?? "cloned";
        var brand = reader.GetOption("--brand");
        var use = reader.HasFlag("--use");

        // 1. 加载设计令牌
        var tokens = CloneTokens.LoadFrom(tokensPath);
        var layout = layoutPath is not null
            ? CloneLayoutInfo.LoadFrom(layoutPath)
            : CloneLayoutInfo.Default;

        // 2. 生成主题文件（17 个模板 + CSS + YAML）
        CloneThemeGenerator.WriteTo(rootDir, themeName, tokens, layout, brand);

        // 3. 可选：切换主题
        if (use)
            await ThemeCommand.SetThemeAsync(themeName, ...);

        // 4. 验证（可选 --verify 标志触发）
        return 0;
    }
}
Skill 层（bukit-clone/SKILL.md）：


Markdown

---
name: bukit-clone
description: Clone any website's visual design into a Bukit theme
argument-hint: "<url> [--theme <name>]"
user-invocable: true
---
# Bukit Clone

你将为 $ARGUMENTS 的网站克隆一个 Bukit 主题。

## 设计令牌提取

通过浏览器 MCP 导航到 $ARGUMENTS，执行以下脚本提取设计令牌：

\`\`\`javascript
// 在浏览器控制台/通过 MCP 执行
const tokens = {
  colors: {
    bg: getComputedStyle(document.body).backgroundColor,
    // ... 完整的提取脚本
  }
};
// 保存为 tokens.json
\`\`\`

## 页面布局分析

分析页面 DOM 结构，识别并保存 layout.json：
- nav → header partial
- hero section → index.html hero block
- features section → index.html features block
- footer → footer partial

## 执行生成

提取完 tokens.json 和 layout.json 后，执行：

\`\`\`bash
bukit clone --tokens tokens.json --layout layout.json --theme <name> --use
\`\`\`

## 验证

\`\`\`bash
bukit doctor
bukit build
\`\`\`
六、最终判决

Plain Text

┌────────────────────────────────────────────────────────────────┐
│                                                                 │
│   ★ 推荐方案: CLI 命令 + AI Skill 混合架构                       │
│                                                                 │
│   实现位置:                                                      │
│     • 确定性生成: Bukit.Cli (CloneCommand + CloneThemeGenerator) │
│     • 智能判断:     src/skills/bukit-clone/SKILL.md             │
│                                                                 │
│   ★ 不推荐:                                                     │
│     • 引擎内置:  生命周期冲突（开发期 vs 构建期）                  │
│     • 外部插件:  生命周期冲突 + 协议数据模型不匹配                 │
│     • 纯 AI Skill: 无自动化 + 不可重复 + 依赖 AI Agent           │
│                                                                 │
│   ★ 混合架构的优势:                                              │
│     1. 无 AI Agent 时 → CLI 也能工作 (bukit clone --tokens)     │
│     2. 有 AI Agent 时 → 全自动 (浏览器提取 + CLI 生成)           │
│     3. 对标现有模式 (bukit theme create ↔ bukit-theme skill)    │
│     4. 零引擎侵入 (不需要修改 Bukit.Engine 任何代码)              │
│     5. 可迭代 (Skill 改文件即可, CLI 编译后一致)                  │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
核心洞察：Bukit 的插件系统（IBukitPlugin + PluginRunner）是为构建期内容变换设计的，它的输入是已加载的 ContentItem + BuildContext，输出是派生页面或构建产物。克隆网站到主题是开发期脚手架创建，它的输入是 URL + 浏览器 DOM，输出是 themes/<name>/ 目录。两者的数据模型完全不重叠——强行将克隆塞入插件系统，如同用洗衣机洗盘子：工具本身没问题，但用途不对。

而 bukit theme create + bukit-theme skill 的现有模式证明：CLI 负责确定性生成、Skill 负责智能指引，是 Bukit 已验证成功的设计模式。克隆能力正是这个模式的自然延伸。

