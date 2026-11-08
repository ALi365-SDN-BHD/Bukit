# Tasks

唯一实施清单。只有取得实际证据的事项才勾选。

## 1. 配置

- [x] `SeoSchemaConfig.BreadcrumbHome`、`BreadcrumbParent`；YAML 读取、严格字段白名单、校验、JSON Schema。

## 2. 引擎

- [x] `BreadcrumbDescriptorResolver` 支持首页节点与分类上级；新增 `BreadcrumbTaxonomyParentResolver`。
- [x] `SeoContributor` 在开启时把两个字段加入增量哈希。

## 3. 测试

- [x] Config：默认值、读取、空白分类法、Schema 契约。
- [x] Engine：首页节点（有/无首页路由）、按文档分类顺序取上级、回退到分类页顺序、分页条目、其他分类法不受影响、增量哈希。

## 4. 文档与契约

- [x] 用户指南、中英马 CHANGELOG、公共 API 基线、closure 策略。

## 5. 验证

- [x] Config、Engine 专项测试，API 漂移、棘轮、格式检查，`openspec validate --strict`，AOT 二进制编译与冒烟。
