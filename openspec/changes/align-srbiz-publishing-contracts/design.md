## Context

跨仓基线：Bukit fde3d99680ff4960121e01181ce25d2721bc30f4；SRBiz-bukit 3eae138ada94b4e3b58eed3fbd74b8c0da45e542。用户已批准本变更；当前 Core 代码与 Native AOT runtime 是只读输入。唯一写者、机器闭包、fixture/lock/manifest 资源串行；保留所有无关工作区变化。

## Goals / Non-Goals

目标是使关系导航和交付证据可核验、可复放、失败关闭。非目标：Core 重构、通用发布框架、永久关系身份系统、反向 JSON 补写、SEO 排名承诺、上线操作或通知。

## Decisions

1. 同一 related-entity.html 使用一个解析器。当前有效 pages 中限定目标集合与当前语言；slug 存在时用集合+content_model.slug 找唯一目标。仅内部路径或本站 canonical URL 可作为 slug 缺失时的 fallback；与存在的 slug 冲突时隐藏。Notion URL 和任意外部 URL 不作为身份，存在有效 slug 时忽略。标题只来自解析后的当前页面，不能作为匹配条件。
2. 资讯的企业关系去重后全量输出；企业页按 collection 识别。先处理企业显式 RelatedContent，再按目标 URL 排序添加由资讯 RelatedCompanies 推导的反向关系，总上限六个唯一有效资讯目标。无结果不输出 section 或占位文案；使用“报道对象/相关报道”，不表达合作、客户或信用背书。诊断 HTML 注释仅固定原因码与数量，验收报告只定位源页，不泄露目标标题、slug、URL、ID；不二次剥离注释。
3. VerifiedAt 独立来自业务核验字段，不退回 LastEditedTime/UpdatedAt，不映射 reviewedAt；覆盖有效、空、时区日期。JSON 关系保留源数据方向；无效关系隐藏只影响 HTML 导航。
4. 一致性检查复用现有 fail-closed completed/security/publish/trust 合同，保留旧 checker 接口和错误规则。核对身份、标题、路由、HTML/JSON/search，资讯/企业正文排除 chrome；Markdown 按支持的文本/链接语义比较，图片存在性与复制哈希校验。HTML 合法反向关系和 search snippets 差异允许。等字节媒体改名报告而不改写。仅空企业列表使用 noindexWhenEmpty；非空企业总列表/马来西亚列表曾被交付补写 noindex 的两处差异必须在独立部署审阅说明中标记。
5. 单个 Python 工具三个入口：check --build-root … --report …；prepare --site-root … --baseline-repo … --baseline-commit … --out …；verify-online --candidate … --base-url …。prepare 输出目录必须全新；固定隔离源码/配置/模板、已锁 runtime、不可变 Git 基线。凭据仅环境变量传入，不复制秘密。业务原始构建不修改；公开 package 与私有 evidence 分离，.bukit 报告不公开。只显式保留现有 CNAME/README/验证文件，并记录来源与哈希；复用 IndexNow prepare helper，不新建/轮换 key。候选清单与哈希封存，篡改即失效。不要求历史 Notion 可重建；固定 fixture 可重放，封存候选可重验。
6. 线上验证只读、有界 HTTPS：新增/保留文件全部比较哈希，删除文件必须404/410；覆盖头部、正文及总体 deadline，拒绝跨主机重定向。首次成功日志不得早于必需检查。无 commit/push/deploy/Notion 写入、通知或远端修复。
7. Core 文档记录实际投影生命周期、marker 保护、有效 manifest/完成状态顺序、slug 路径迁移、draft/删除撤回与 expiry/noindex 仅排除索引的区别。发现新增 Core 缺陷时报告并停止受影响项，不在网站批次偷修 Core。

## Risks / Trade-offs

解析错误必须隐藏而非猜测；过多反向关系仅显示确定顺序的前六条。仅凭构建成功或 HTTP200 不足以验收。隔离候选不等于线上发布成功；Native AOT、配置与 lock 指纹属于证据。缓存只有精确命令、完整输入、工具链和环境前提匹配才复用；共享 fixture 不能并发执行。

## Migration Plan

先落规范和闭包，再模板/fixture，随后交付脚本，最后文档与逐条需求证据。每批仅一次专项复审，Critical/Important 才限定重入；最终只对新差异及跨批交点做一次统一复审。候选准备允许真实 SRBiz 的只读 Notion 取数，不写 Notion，不替代独立上线审批。
