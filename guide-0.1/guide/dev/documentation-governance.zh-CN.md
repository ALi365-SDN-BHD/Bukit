# 文档治理规则

## 目录职责

| 目录 | 职责 |
|---|---|
| `README.*` | 公开项目入口页 |
| `guide/user/*` | 面向用户的操作手册 |
| `guide/dev/*` | 维护者与贡献者参考 |
| `guide/ai/*` | 面向人类的 AI Prompt 包 |
| `src/skills/*` | AI Agent 知识层 |
| `docs/*` | 产品方案、审计报告、治理记录、长篇分析 |

## 规则

1. **README 必须保持简洁。** 它是项目入口，不是完整手册。
2. **完整 CLI 参考归属 `guide/user` 或 `guide/dev`。** 不在 README 中复制。
3. **完整配置 schema 归属 `guide/dev`。** 不在 README 或 `guide/user` 中复制。
4. **Skills 文档不得在 README 或 guide 中重复。** `src/skills/*` 是 Agent 知识的唯一来源。
5. **所有根 README 语言版本必须保持相同的章节顺序。**
6. **所有 guide README 语言版本应保持相同的信息层级。**
7. **密钥绝不出现在文档示例中。** 始终使用占位名如 `NOTION_TOKEN` 或 `YOUR_KEY`。
8. **Notion token 必须始终写为 `NOTION_TOKEN`。** 绝不展示真实 token 值。

## 语言 Fallback 规则

当本地化文档不存在时：

- **中文 (zh-CN)**：除非链接到非中文材料，否则不需要 fallback 说明
- **英文 (en)**："Currently available in [language] only"
- **马来文 (ms)**："Pada masa ini hanya tersedia dalam bahasa [language]"

使用一致的措辞。不在导航标题中使用临时性标注如 "(Chinese)"。

## 交叉引用原则

- `guide/user` 可引用 `guide/dev` 获取权威字段/契约细节
- `guide/dev` 可引用 `docs/` 获取产品级上下文
- `guide/ai` 应引用 `guide/user` 和 `guide/dev` 说明校验流程
- `src/skills` 绝不应引用临时或暂定文档
