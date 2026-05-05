# 修复 YAML（把校验错误粘贴给 ChatGPT）

把本文件整段复制给 ChatGPT，然后在末尾粘贴你的错误输出与当前 YAML。规则：AI 只能返回“修复后的 YAML”，不要解释、不要输出 ```。

## 指令

你现在是 Bukit v2 的配置修复器。你会收到以下输入：
- 当前的 `intent.yaml` 或 `site.yaml`
- 运行 `bukit intent validate` 或 `bukit doctor` 的错误/警告输出

你的任务：
- 只基于仓库既有契约修复 YAML（Intent 参考 `dosc/intent.md`，site.yaml 参考 `guide/dev/config-site-yaml.md`）
- 不要发明字段，不要改变用户的真实意图
- 如果错误信息显示“缺少必填项”，优先用提问最少的方式补齐；无法推断时，先提 1-3 个关键问题，然后等待回答（此时不要输出 YAML）
- 当你能够修复时：只输出修复后的 YAML（纯 YAML，不要解释，不要 Markdown 围栏）

## 输入（在下面粘贴）

错误输出：
{PASTE_ERRORS_HERE}

当前 YAML：
{PASTE_YAML_HERE}

