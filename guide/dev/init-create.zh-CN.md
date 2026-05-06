# init/create锛堣剼鎵嬫灦鍒濆鍖栵級

`bukit init <dir>`锛堜篃鍙敤鍚屼箟鍛戒护 `create <dir>`锛夌敤浜庡垱寤轰竴涓渶灏忓彲杩愯绔欑偣宸ョ▼锛氬寘鍚?`site.yaml`銆佸唴瀹圭洰褰曘€佷互鍙婁竴涓彲鐢ㄤ富棰橈紙layouts/assets/static 涓庡繀闇€妯℃澘锛夈€?

瀹炵幇鍙傝€冿細`src/Bukit.Cli/Commands/InitCommand.cs`

鐩稿叧鏂囨。锛?
- [鍛戒护琛岋紙CLI锛夊弬鏁板弬鑰僝(./cli.md)
- [閰嶇疆锛坰ite.yaml锛夊瓧娈靛弬鑰僝(./config-site-yaml.zh-CN.md)
- [涓婚寮€鍙慮(./theme.zh-CN.md)
- [doctor](./doctor.zh-CN.md)

## 鍩烘湰鐢ㄦ硶

```bash
bukit init my-site
```

鍚屼箟鍛戒护锛?

```bash
bukit create my-site
```

## 鍙傛暟

褰撳墠鑴氭墜鏋舵敮鎸佷袱绫诲弬鏁帮細

- `--provider <markdown|notion>`锛堥粯璁?markdown锛?
- `--template <name>`锛堥粯璁?minimal锛涘綋鍓嶇増鏈粎鍐欏叆閰嶇疆锛屼笉褰卞搷鏂囦欢鐢熸垚锛?

璇存槑锛?
- `--provider notion` 浼氱敓鎴?Notion 妯″紡鐨?`site.yaml`锛屼絾 `databaseId` 闇€瑕佷綘鑷濉啓
- 鏈懡浠や笉浼氳Е纰板綋鍓嶇洰褰曚互澶栫殑鏂囦欢锛屽彧浼氬湪鐩爣鐩綍涓嬪垱寤?瑕嗙洊鑴氭墜鏋舵枃浠?

## 鐢熸垚鐨勭洰褰曠粨鏋?

鎵ц鍚庯紝鐩爣鐩綍浼氱敓鎴愬涓嬬粨鏋勶紙鐪佺暐閮ㄥ垎鏂囦欢锛夛細

```text
<dir>/
  site.yaml
  README.md
  .gitignore
  content/
    hello-world.md
  themes/
    starter/
      assets/
        style.css
      static/
      layouts/
        layouts/
          base.html
        pages/
          index.html
          list.html
          page.html
          post.html
        partials/
          header.html
          footer.html
```

瀵瑰簲鍏崇郴锛?
- `site.yaml` 涓粯璁ゅ啓鍏?`theme.name: starter`锛屽苟淇濈暀 `layouts/assets/static` 涓洪粯璁ゅ€硷紙瑙?[涓婚寮€鍙慮(./theme.zh-CN.md)锛?
- `hello-world.md` 榛樿浣滀负 `type: page` 鐨勫唴瀹归〉娓叉煋銆傛柊椤圭洰寤鸿鍦?site.yaml 涓厤缃?`site.collections`锛堢敓鎴愬櫒榛樿宸插寘鍚級锛屼娇璺敱鐢?collection 瑙勫垯椹卞姩锛堣矾鐢辫鍒欒 [routing](./routing.zh-CN.md)锛?
- 涓婚妯℃澘婊¤冻 `doctor` 鐨勫繀闇€妯℃澘娓呭崟锛堣 [doctor](./doctor.zh-CN.md)锛?

## 鐢熸垚鐨勫叧閿枃浠惰鏄?

### 1) .gitignore

鑴氭墜鏋堕粯璁ゅ拷鐣ワ細
- `dist/`锛氭瀯寤鸿緭鍑虹洰褰?
- `.bukit/`锛氬巻鍙茬紦瀛樼洰褰曪紙褰撳墠娓呯悊/缂撳瓨涓荤洰褰曟槸 `.cache/`锛岃 [缂撳瓨涓庢竻鐞哴(./cache-clean.md)锛?

娉ㄦ剰锛氬鏋滀綘甯屾湜榛樿蹇界暐 `.cache/`锛屽簲鍦ㄦ柊绔欑偣鐨?`.gitignore` 閲屾墜鍔ㄥ姞涓婏紙鎴栧悗缁皟鏁磋剼鎵嬫灦瀹炵幇锛夈€?

### 2) site.yaml

Markdown 妯″紡涓嬬殑鍏抽敭瀛楁锛?
- `content.provider: markdown`
- `content.markdown.dir: content`
- `theme.name: starter`
- `build.output: dist`

Notion 妯″紡涓嬬殑鍏抽敭瀛楁锛?
- `content.provider: notion`
- `content.notion.databaseId: xxxxx`锛堝崰浣嶏級

瀛楁鍚箟涓庨粯璁ゅ€艰瑙侊細[config-site-yaml.md](./config-site-yaml.zh-CN.md)銆?

### 3) starter 涓婚

starter 涓婚鏄€滄渶灏忓彲杩愯涓婚鈥濓紝鍖呭惈锛?
- `layouts/layouts/base.html`锛氬熀纭€ layout锛堝紩鐢?`site.base_url` 鎷兼帴璧勬簮锛?
- `partials/header.html` / `partials/footer.html`
- `pages/page.html` / `pages/post.html` / `pages/index.html` / `pages/list.html`
- `assets/style.css`

涓婚濡備綍浣跨敤 `theme.params`銆佸浣曟墿灞?modules 绛夐珮绾х敤娉曡锛歔theme.md](./theme.zh-CN.md)銆?

## 寤鸿鐨勯獙璇佹祦绋?

鍦ㄧ洰鏍囩洰褰曞唴锛?

1. `bukit doctor` 纭閰嶇疆涓庢ā鏉垮仴鍏?
2. `bukit build --clean` 鐢熸垚绔欑偣
3. `bukit preview --dir dist` 鏈湴棰勮

## 宸茬煡闄愬埗涓庢敼杩涚偣

- `--template` 鐩墠鍙奖鍝嶅啓鍏ラ厤缃殑 templateName锛堟殏鏈┍鍔ㄤ笉鍚屾枃浠舵ā鏉跨敓鎴愶級
- `.gitignore` 鐩墠榛樿蹇界暐 `.bukit/`锛屼絾寮曟搸榛樿缂撳瓨鐩綍涓?`.cache/`锛堣 [缂撳瓨涓庢竻鐞哴(./cache-clean.md)锛?



