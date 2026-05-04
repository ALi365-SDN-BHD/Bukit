# GPT Knowledge 建议清单

下面文件建议作为“自定义 GPT”的 Knowledge 上传，以降低幻觉并让模型对齐字段与命令。

## 必选（强约束）

- `dosc/intent.md`：Intent 契约（snake_case）与映射规则
- `guide/dev/config-site-yaml.md`：`site.yaml` 权威字段表（camelCase）
- `guide/user/12-命令行参考.md`：可复制的 CLI 闭环命令与常见参数
- `examples/starter/site.yaml`：最小可运行示例（markdown）
- `examples/starter/site.modules.yaml`：多源 + Modules（mode=data）示例

## 推荐（用于更好问诊与落地）

- `guide/user/14-故障排查.md`：常见报错与修复方法（强烈推荐上传）
- `guide/user/01-快速开始.md`：新手上手路径与目录约定
- `guide/user/06-内容-Notion.md`：Notion 使用与常见坑（token、字段等）
- `guide/user/07-内容-多源-sources.md`：组合源与 mode 语义
- `guide/user/09-Modules-结构化数据.md`：Modules 注入规则与数据形态
- `dosc/ai_guide.md`：AI 落地形态与 Prompt 基线

