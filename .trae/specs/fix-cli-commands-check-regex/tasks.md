# Tasks

## Implementation Order

- [x] Task 1: 修复 `check-cli-commands.py` 中的正则表达式
  - [x] 1.1 修改第 96 行正则：`re.search(r'Name:\s*"([^"]+)"', stripped)` → 添加负向后顾 `(?<!Value)` 排除 `ValueName:`
  - [x] 1.2 修改第 116 行正则（主题命令解析）：同样添加 `(?<!Value)` 负向后顾
  - **验证**: `python3 src/skills/scripts/check-cli-commands.py` 退出码为 0，不再报告 `scope` / `theme scope`

- [x] Task 2: 运行 quality-gate.sh 验证修复
  - [x] 2.1 执行 `bash src/skills/scripts/validate-skills-strict.sh` 确认 Check 12 通过
  - **验证**: 所有 16 项检查通过，无 Skills strict validation 错误

# Task Dependencies
- Task 2 依赖 Task 1
