# Tasks

## Task 1: smoke-all.sh 脚本 ✅
- [x] 1.1 创建 `scripts/smoke-all.sh`
- [x] 1.2 遍历 7 个 `examples/*/site.yaml`
- [x] 1.3 对每个站点执行 build + sitemap/rss/search.json 输出检查
- [x] 1.4 本地 7/7 全部通过

## Task 2: CI 集成 ✅
- [x] 2.1 `scripts/quality-gate.sh` 追加 `smoke-all.sh` 调用

## Task 3: 验证 ✅
- [x] 3.1 本地运行全部通过
- [x] 3.2 脚本语法 `bash -n` 通过
