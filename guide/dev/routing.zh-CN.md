# 璺敱绯荤粺锛坈ollections 涓昏矾寰勪笌鍏煎瑙勫垯锛?

璺敱绯荤粺璐熻矗鎶?`ContentItem` 鏄犲皠涓?`RouteInfo(url, outputPath, template)`锛屼緵娓叉煋闃舵浣跨敤銆?

瀹炵幇鍙傝€冿細`src/Bukit.Routing/RouteGenerator.cs`

## Collection 椹卞姩璺敱锛堜富妯″瀷锛?

璺敱浼樺厛鐢?`site.collections` 鍐冲畾锛岄泦鍚堥敭閫氬父鏉ヨ嚜 `meta.collection`锛堢己澶辨椂鍥為€€ `meta.type`锛夛細

```yaml
site:
  collections:
    post:
      permalink: /blog/{slug}/
      template: pages/post.html
      listRoute: /blog/
    page:
      permalink: /pages/{slug}/
      template: pages/page.html
      listRoute: /pages/
```

姣忎釜闆嗗悎鏈€灏戦渶瑕侊細

- `permalink`锛堝繀椤诲寘鍚?`{slug}`锛?
- `template`

## Permalink 妯″紡锛堝吋瀹瑰眰锛?

`site.permalinks` 浠嶅彲浣滀负鍏煎杈撳叆锛屼絾鎺ㄨ崘杩佺Щ鍒?`site.collections`銆?

```yaml
site:
  permalinks:
    post: "/{year}/{month}/{slug}/"
    page: "/docs/{slug}/"
```

鏀寔鐨勫崰浣嶇锛?

| 鍗犱綅绗?| 鏉ユ簮 | 绀轰緥 |
|---|---|---|
| `{slug}` | ContentItem.Slug | `my-post` |
| `{title}` | ContentItem.Slug锛堝洖閫€锛?| `my-post` |
| `{year}` | ContentItem.PublishAt 骞达紙4 浣嶏級 | `2025` |
| `{month}` | ContentItem.PublishAt 鏈堬紙2 浣嶏級 | `03` |
| `{day}` | ContentItem.PublishAt 鏃ワ紙2 浣嶏級 | `15` |
| `{type}` | meta.type | `post` |

绀轰緥鏁堟灉锛?

| 閰嶇疆 | slug=`my-post`, 鍙戝竷=2025-03-15 | 鐢熸垚 URL |
|---|---|---|
| `/{year}/{month}/{slug}/` | post 绫诲瀷 | `/2025/03/my-post/` |
| `/{year}/{month}/{day}/{slug}/` | post 绫诲瀷 | `/2025/03/15/my-post/` |
| `/docs/{slug}/` | page 绫诲瀷 | `/docs/my-post/` |

浼樺厛绾э紙浠庨珮鍒颁綆锛夛細
1. 璺敱瑕嗙洊锛圧oute Override锛夆€斺€?meta 涓樉寮忔寚瀹?url/outputPath/template
2. Collection 瑙勫垯 鈥斺€?`site.collections`
3. Permalink 妯″紡 鈥斺€?`site.permalinks`锛堝吋瀹瑰眰锛?
4. 榛樿璺敱瑙勫垯锛堝吋瀹瑰眰锛?

瀹炵幇鍙傝€冿細`RouteGenerator.ExpandPermalinkPattern` / `RouteGenerator.BuildFromPermalink`

## 璺敱瑕嗙洊锛圧oute Override锛?

褰?ContentItem 鐨?Meta 涓瓨鍦ㄤ互涓嬪瓧娈垫椂锛屼細瑕嗙洊榛樿璺敱锛?

1. `route` 鏄犲皠瀵硅薄锛?

```yaml
route:
  url: /custom/
  outputPath: custom/index.html
  template: pages/page.html
```

2. 鎴栬€呭悓绾ф墎骞冲瓧娈碉細

```yaml
url: /custom/
outputPath: custom/index.html
template: pages/page.html
```

瑕嗙洊鐢熸晥鏉′欢锛?
- `url`銆乣outputPath`銆乣template` 涓夎€呴兘闈炵┖鎵嶄細鐢熸晥锛堢己涓€鍒欏洖閫€榛樿璺敱锛?

## Notion 鍐呭濡備綍瑕嗙洊璺敱

Notion 鍐呭閫氳繃鏁版嵁搴撳睘鎬ф槧灏勫埌 `fields`锛屽紩鎿庝細鎶婁互涓嬪瓧娈垫彁鍗囧埌 `meta` 浠ユ敮鎸佽矾鐢辫鐩栵細

- `url`锛堟枃鏈級
- `outputPath`锛堟枃鏈級
- `template`锛堟枃鏈級

濉啓绀轰緥锛?

```
url: /asdfasdf/
outputPath: asdfasdf/index.html
template: pages/page.html
```

娉ㄦ剰锛歂otion 灞炴€у悕浼氳鏍囧噯鍖栵紙蹇界暐澶у皬鍐欍€佺┖鏍笺€佺鍙凤級锛屼緥濡?`Output Path` 浼氳瘑鍒负 `outputpath`銆?
琛ュ厖锛歂otion 鐨?`formula` 瀛楁涔熶細琚В鏋愪负鏂囨湰/鏁板€?甯冨皵/鏃ユ湡锛屽彲鐢ㄤ簬璺敱瑕嗙洊銆?

## outputPath 缂栫爜绛栫暐锛堝鐞嗕腑鏂囦笌绗﹀彿锛?

褰?`outputPath` 鍚腑鏂囨垨绗﹀彿鏃讹紝鍙湪 `site.yaml` 浣跨敤锛?

```yaml
site:
  outputPathEncoding: none|slug|urlencode|sanitize
```

绛栫暐璇存槑锛?
- `none`锛氫笉鍋氫换浣曠紪鐮侊紙榛樿锛?
- `slug`锛氬姣忎釜璺緞娈靛仛 slugify锛堜腑鏂囦細琚浆鎴愮┖锛屾渶缁堝洖閫€涓?`page`锛?
- `urlencode`锛氬姣忎釜璺緞娈靛仛 URL 缂栫爜锛堜繚鐣欎腑鏂囪涔変絾浼氬彉鎴?`%E4%...`锛?
- `sanitize`锛氱┖鏍兼浛鎹负 `-`锛岀Щ闄?`<>:"|?*` 鍜屾帶鍒跺瓧绗︼紝杩炵画 `-` 鍘嬬缉锛屾鏈?`.`/绌烘牸绉婚櫎

寤鸿锛氬鏋滃笇鏈涚ǔ瀹氳法骞冲彴锛屼紭鍏堢敤 `slug`锛涘鏋滃笇鏈涗繚鐣欎腑鏂囧彲璇绘€э紝鐢?`urlencode`锛涘鏋滃笇鏈涗繚鐣欎腑鏂囦笖鍙鐞嗗嵄闄╁瓧绗︼紝鐢?`sanitize`銆?

## 褰掍竴鍖栬鍒欙紙Normalization锛?

瑕嗙洊瀛楁浼氳褰掍竴鍖栵細

- url锛?
  - 鑷姩琛ラ綈鍓嶅 `/`
  - 鑷姩琛ラ綈灏鹃殢 `/`
  - 渚嬪锛歚custom` 鈫?`/custom/`
- outputPath锛?
  - 鍘绘帀鍓嶅 `/` 鎴?`\\`
  - 缁熶竴涓?`/` 鍒嗛殧
  - 渚嬪锛歚/a\\b\\index.html` 鈫?`a/b/index.html`

## 缁存姢寤鸿

- 璺敱瑕嗙洊鏄ǔ瀹氬绾︼細鍐呭鐢熶骇渚э紙Markdown/Notion/AI intent锛夊彲鑳戒緷璧栧畠锛屼慨鏀硅鍒欓渶鑰冭檻鍏煎鎬?
- 寤鸿鍦ㄦ枃妗?涓婚涓害瀹氬皯閲忊€滃叕鍏辫矾鐢辫鐩栨ā寮忊€濓紝閬垮厤姣忛〉闅忔剰瀹氬埗瀵艰嚧绔欑偣缁撴瀯涓嶅彲棰勬湡

寮曟搸杩樹細鐢熸垚涓€浜涗笉渚濊禆鍐呭鐨勫浐瀹氳仛鍚堥〉锛坄/`銆乣/blog/`銆乣/pages/`锛夛紝瑙?[寮曟搸鍥哄畾浜х墿](./engine-outputs.zh-CN.md)銆?

