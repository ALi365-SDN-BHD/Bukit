# Smoke Gold Checks Checklist

## smoke-all.sh
- [x] 脚本存在且可执行
- [x] 构建全部 7 个示例站点
- [x] 验证 sitemap/rss/search.json 输出结构
- [x] 单个失败不中断后续站点
- [x] 最终汇总 passed/failed

## CI 集成
- [x] `quality-gate.sh` 调用 `smoke-all.sh`

## 回归
- [x] `bash -n` 语法正确
- [x] 7/7 本地运行全部通过
