# 鍛戒护琛岋紙CLI锛夊弬鏁板弬鑰?
鏈枃妗ｉ潰鍚戠淮鎶よ€咃紝鐩爣鏄妸 CLI 鐨勫懡浠ゃ€佸弬鏁般€佽鐩栧叧绯讳笌甯歌鐢ㄦ硶璇存竻妤氥€?
瀹炵幇鍙傝€冿細
- `src/Bukit.Cli/Cli/BukitCliSpecs.cs`
- `src/Bukit.Cli/Cli/Parsing/CliParser.cs`
- `src/Bukit.Cli/Commands/*Command.cs`

璇存槑锛?- 椤跺眰鍛戒护涓庨鎵瑰懡浠?help 宸茬敱鍏冩暟鎹眰缁熶竴鐢熸垚
- `--jobs` 宸茶繘鍏ョ粺涓€ help 鍙ｅ緞
- 鍙傛暟瀹氫箟鐜板湪浠?`BukitCliSpecs.CreateRegistry()` 涓殑 `CliCommandSpec` / `CliOptionSpec` 澹版槑涓哄噯

## 鍛戒护鎬昏

| 鍛戒护 | 浣滅敤 |
|---|---|
| `create <dir>` | 浠庨浂鍒涘缓绔欑偣宸ョ▼锛堢瓑浠?`init`锛?|
| `init <dir>` | 鍒濆鍖栫珯鐐瑰伐绋嬮鏋?|
| `build` | 鐢熸垚闈欐€佺珯鐐?|
| `preview` | 鏈湴棰勮杈撳嚭鐩綍 |
| `dev` | HMR 开发服务器（文件监控 + 增量构建 + 浏览器实时刷新） |
| `clean` | 娓呯悊杈撳嚭涓庣紦瀛?|
| `doctor` | 鐜涓庨厤缃瘖鏂?|
| `plugin` | 鎻掍欢鐩稿叧鍛戒护 |
| `theme` | 涓婚鐩稿叧鍛戒护 |
| `intent` | AI Intent 鐩稿叧鍛戒护 |
| `webhook` | Webhook 瑙﹀彂鍣?|
| `version` | 鐗堟湰淇℃伅 |

璇存槑锛?- 鎵ц澶у鏁板懡浠ゆ椂锛孋LI 浼氬厛杈撳嚭涓€琛?`bukit <version>`锛堢敤浜庣‘璁ゅ綋鍓嶈繍琛岀増鏈紱`help/version` 渚嬪锛?- 鐗堟湰鍙锋潵鑷?`src/Bukit.Cli/Bukit.Cli.csproj` 鐨?`BuildInfoVersionBase`锛屼篃鍙€氳繃 `VersionPrefix` 鎴?`Version` 瑕嗙洊

## 鍏抽敭瑕嗙洊鍏崇郴

鏋勫缓鐩稿叧鐨勮鐩栭『搴忥紙浠庨珮鍒颁綆锛夛細

1. CLI 鍙傛暟锛堜緥濡?`--output` / `--base-url` / `--clean` / `--draft` / `--site-url`锛?2. `site.yaml`
3. 浠ｇ爜榛樿鍊硷紙瑙?`Bukit.Config` 鐨勯粯璁ゅ€间笌 `ConfigLoader`锛?
## 閫氱敤鏋勫缓鍙傛暟锛坆uild/doctor 绛夊叡鐢級

鏉ユ簮锛歚BukitCliSpecs` 涓?`BuildCommand`

| 鍙傛暟 | 浣滅敤 | 瑕嗙洊瀛楁/琛屼负 |
|---|---|---|
| `--config <path>` | 鎸囧畾閰嶇疆鏂囦欢璺緞 | 浣滀负 config rootDir 涓庨粯璁ょ浉瀵硅矾寰勫熀鍑?|
| `--site <name>` | 澶氱珯鐐硅鍙?`sites/<name>.yaml` | rootDir 浠嶄负褰撳墠鐩綍 |
| `--output <dir>` | 瑕嗙洊杈撳嚭鐩綍 | 瑕嗙洊 `build.output` |
| `--base-url <path>` | 瑕嗙洊 baseUrl | 瑕嗙洊 `site.baseUrl` |
| `--site-url <url>` | 瑕嗙洊绔欑偣缁濆 URL | 瑕嗙洊 `site.url`锛堢敤浜?sitemap/rss锛?|
| `--clean` | 鏋勫缓鍓嶆竻鐞?| 瑕嗙洊 `build.clean=true` |
| `--no-clean` | 绂佺敤鏋勫缓鍓嶆竻鐞?| 瑕嗙洊 `build.clean=false` |
| `--draft` | 娓叉煋鑽夌 | 瑕嗙洊 `build.draft=true` |
| `--ci` | CI 妯″紡 | 浼氬奖鍝嶆棩蹇楃瓑绾х瓑绛栫暐锛堢ず渚嬶細build 榛樿 WARN锛?|
| `--incremental` | 鍚敤澧為噺鏋勫缓 | 瑕嗙洊澧為噺寮€鍏筹紙榛樿鍚敤锛?|
| `--no-incremental` | 鍏抽棴澧為噺鏋勫缓 | 瑕嗙洊澧為噺寮€鍏?|
| `--cache-dir <dir>` | 瑕嗙洊缂撳瓨鐩綍 | 榛樿 `<rootDir>/.cache` |
| `--jobs <n>` | 骞惰娓叉煋骞跺彂搴?| 姝ｆ暣鏁帮紱榛樿 CPU 鏍稿績鏁?|
| `--metrics <path>` | 杈撳嚭鏋勫缓鎸囨爣 JSON | 鐩稿璺緞鎸?rootDir 瑙ｆ瀽 |
| `--log-format <text|json>` | 鎺у埗鏃ュ織杈撳嚭鏍煎紡 | 榛樿 `text` |

## build

瀹炵幇鍙傝€冿細`src/Bukit.Cli/Commands/BuildCommand.cs`

甯哥敤绀轰緥锛?
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --clean
```

澶氱珯鐐癸細

```bash
dotnet run --project src/Bukit.Cli -c Release -- build --site blog --clean
```

瑕嗙洊杈撳嚭涓?baseUrl锛圙itHub Pages 瀛愯矾寰勶級锛?
```bash
dotnet run --project src/Bukit.Cli -c Release -- build --output dist --base-url /my-repo --site-url https://user.github.io/my-repo --clean
```

## preview

瀹炵幇鍙傝€冿細`src/Bukit.Cli/Commands/PreviewCommand.cs`

| 鍙傛暟 | 榛樿鍊?| 璇存槑 |
|---|---|---|
| `--dir <path>` | `dist` | 棰勮鐩綍 |
| `--host <host>` | `localhost` | 鐩戝惉鍦板潃 |
| `--port <port\|auto>` | `4173` | `auto` 鑷姩閫夋嫨鍙敤绔彛 |
| `--strict-port` | false | 绔彛鍗犵敤鍒欏け璐ワ紙榛樿浼氶€掑閲嶈瘯锛?|

### `dev` — HMR 开发服务器

```
bukit dev [--config <path>] [--site <name>] [--host <host>] [--port <port>] [--output <dir>] [--no-watch]
```

开发用途：监控 content/themes/layouts/assets/static 目录的文件变更，自动增量重构建，通过 WebSocket 实时刷新浏览器。端口默认 35729，`--no-watch` 禁用文件监控（纯静态服务）。

## doctor / clean / theme / plugin / intent / webhook

杩欎簺鍛戒护鐨勫弬鏁扮粏鑺傞殢鐗堟湰婕旇繘锛屼紭鍏堜互瀵瑰簲 `*Command.cs` 涓哄噯锛?
- `src/Bukit.Cli/Commands/DoctorCommand.cs`
- `src/Bukit.Cli/Commands/CleanCommand.cs`
- `src/Bukit.Cli/Commands/ThemeCommand.cs`
- `src/Bukit.Cli/Commands/PluginCommand.cs`
- `src/Bukit.Cli/Commands/IntentCommand.cs`
- `src/Bukit.Cli/Commands/WebhookCommand.cs`

琛ュ厖璇存槑锛?
- init/create 鐨勮剼鎵嬫灦杈撳嚭涓庣洰褰曠粨鏋勮 [init/create](./init-create.zh-CN.md)銆?- doctor 鐨勬鏌ラ」涓庡父瑙佸け璐ヤ慨澶嶈 [doctor](./doctor.zh-CN.md)銆?- clean 涓庣紦瀛樼洰褰曡涔夎 [缂撳瓨涓庢竻鐞哴(./cache-clean.md)銆?- theme 鐨勫紑鍙戜笌鍙傛暟浣跨敤瑙?[涓婚寮€鍙慮(./theme.md)銆?- intent 鐨?CLI 钀藉湴涓?rootDir 鎺ㄦ柇瑙勫垯瑙?[Intent](./intent-cli.zh-CN.md)銆?- webhook 鐨勫畨鍏ㄧ害鏉熶笌鐜鍙橀噺璇存槑瑙?[Webhook](./webhook.zh-CN.md)銆?

