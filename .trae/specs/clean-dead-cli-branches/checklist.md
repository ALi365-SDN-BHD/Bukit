# Checklist

## 死代码删除
- [ ] Program.cs 旧路径不含 `"build"` case
- [ ] Program.cs 旧路径不含 `"clone"` case
- [ ] Program.cs 旧路径不含 `"deploy"` case
- [ ] Program.cs 旧路径不含 `"dev"` case
- [ ] Program.cs 旧路径不含 `"doctor"` case
- [ ] Program.cs 旧路径不含 `"preview"` case
- [ ] Program.cs 旧路径不含 `"lint"` case

## 活跃分支保持
- [ ] `"create"` / `"init"` 分支存在且不变
- [ ] `"clean"` 分支存在且不变
- [ ] `"completion"` 分支存在且不变
- [ ] `"config"` 分支存在且不变
- [ ] `"plugin"` 分支存在且不变
- [ ] `"seo"` / `"geo"` 分支存在且不变
- [ ] `"data"` / `"docs"` 分支存在且不变
- [ ] `"theme"` / `"template"` 分支存在且不变
- [ ] `"intent"` / `"visual"` 分支存在且不变
- [ ] `"webhook"` / `"version"` 分支存在且不变

## 构建和测试
- [ ] dotnet build bukit.slnx -c Release: 0 错误 0 警告
- [ ] dotnet test bukit.slnx -c Release: 全部通过

## ArgReader 删除阻断
- [ ] 文档已记录 ArgReader 无法删除的原因和前置条件
