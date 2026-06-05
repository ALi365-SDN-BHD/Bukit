# Fix CLI Commands Check Regex — Verification Checklist

## Code Changes
- [x] `check-cli-commands.py` 第 96 行正则使用 `(?<!Value)Name:` 排除 `ValueName:` 匹配
- [x] `check-cli-commands.py` 第 116 行正则同样使用 `(?<!Value)Name:` 排除 `ValueName:` 匹配

## Functional Verification
- [x] `python3 src/skills/scripts/check-cli-commands.py` 退出码为 0
- [x] 不再输出 `scope` 或 `theme scope` 作为未文档化命令
- [x] 仍然能正确检测真正的未文档化命令（如未来新增的命令）

## Regression
- [x] `bash src/skills/scripts/validate-skills-strict.sh` 所有 16 项检查通过
- [x] 现有 CLI 命令（如 `build`, `deploy`, `init` 等）仍然被正确识别
