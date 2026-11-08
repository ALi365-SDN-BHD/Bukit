# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 配置

- [x] `SeoConfig.DefaultAuthor`、`DefaultAuthorType`；YAML 读取、严格字段白名单、校验、JSON Schema。

## 2. 引擎

- [x] `SeoAuthorResolver` 在所有来源都没有作者时回退到默认作者；两个调用点传入配置。
- [x] `SeoContributor` 把两个字段加入增量哈希。

## 3. 测试

- [x] Config：读取、类型大小写、空白作者、只写类型、非法类型、Schema。
- [x] Engine：无作者回退、内容作者优先、GEO 作者优先、JSON-LD author 节点类型、增量哈希变化。

## 4. 文档与契约

- [x] 用户指南、开发文档、中英马 CHANGELOG、公共 API 基线、closure 策略。

## 5. 验证

- [x] Config、Engine 专项测试，API 漂移、棘轮、格式检查，`openspec validate --strict`，AOT 二进制编译。
