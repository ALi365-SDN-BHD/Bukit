# Log Perubahan

Semua perubahan penting kepada Bukit akan didokumenkan dalam fail ini.

## [Belum Dikeluarkan]

- **Perubahan tingkah laku: UI carian memuatkan indeks bahasanya sendiri**: `bukit-search.html` dahulu sentiasa mengambil `/search.json`, jadi ia tidak menemui apa-apa pada tapak yang dihidangkan di bawah `baseUrl` seperti `/repo/`, dan dalam tapak berbilang bahasa setiap bahasa mencari fail akar (tiada dalam mod `split` dan `index`, indeks gabungan dalam mod `merged`). Fragmen kini membaca URL indeks daripada atribut `data-search-url` pada input, ditetapkan kepada `<URL asas bahasa>/search.json` (contohnya `/repo/zh-CN/search.json`); `/search.json` kekal sebagai sandaran jika atribut itu dibuang. Dalam `search.mode: merged` fragmen kini hanya mencari bahasanya sendiri; untuk mencari indeks lain, salin markup fragmen ke dalam templat dan tetapkan `data-search-url` di situ (fail yang dijana ditulis semula pada setiap binaan). Jika indeks gagal dimuatkan atau bukan tatasusunan, fragmen memaparkan "Search is unavailable" dan bukan hasil kosong, dan pertanyaan yang ditaip semasa indeks dimuatkan dijalankan sebaik sahaja ia tiba.
- **Ubah hala pilihan untuk setiap URL lama bahasa lalai**: `site.defaultLanguageLegacyRedirects` (lalai `false`) membantu tapak yang memindahkan bahasa lalai yang sudah disiarkan ke susun atur akar pada hos tanpa 301, seperti GitHub Pages. Dengan `defaultLanguageInSubdir: false`, ia menulis halaman meta refresh noindex pada laluan lama `/<bahasaLalai>/...` bagi setiap halaman HTML bahasa lalai (kandungan, senarai koleksi dan penomboran, halaman taksonomi, laluan HTML statik, dan alias yang terus menuju halaman sasarannya). Halaman utama kekal dikawal oleh `defaultLanguageRedirect`, output sedia ada pada laluan itu diutamakan, dan fail bukan HTML serta alias taksonomi tidak diubah hala. Binaan merekod satu ringkasan `i18n.legacy_redirects.derived`; kiraan laluan dan laporan bertambah mengikut bilangan ubah hala. Dalam susun atur berawalan pilihan ini diabaikan dan merekod `i18n.config.legacy_redirects_ignored`. Dokumen ubah hala alias kini turut merekod halaman sasaran; HTML-nya tidak berubah. Tapak yang tidak menghidupkan pilihan ini tidak melihat perubahan output.
- **Bahasa lalai boleh berada di akar output**: `site.defaultLanguageInSubdir` (lalai `true`) mengekalkan setiap bahasa di bawah `dist/<lang>/`; tetapkan `false` untuk membina bahasa lalai ke `dist/` dengan base URL akar tapak sementara bahasa lain kekal di `dist/<lang>/`. `site.defaultLanguageRedirect` (lalai `true`) menulis halaman ubah hala noindex supaya `/` berfungsi pada hosting statik: `dist/index.html` → `/<bahasaLalai>/` dalam susun atur berawalan, atau `dist/<bahasaLalai>/index.html` → `/` dalam susun atur akar. Halaman itu dikecualikan daripada peta laman, carian, feed, llms dan manifes ejen. **Perubahan tingkah laku**: tapak berbilang bahasa sedia ada mendapat `dist/index.html` selepas naik taraf; tapak yang hos atau CDN-nya sudah mengubah hala `/` ke `/<bahasa>/` patut menetapkan `defaultLanguageRedirect: false`. Dalam susun atur akar, laporan setiap bahasa berpindah ke `.bukit/variants/<lang>/`, `search.mode: merged` ditolak, bahasa lalai tidak boleh menggunakan kod bahasa lain sebagai segmen laluan pertama (`i18n-route-reserved-language-prefix`), `static/robots.txt` bahasa lalai menggantikan robots yang dijana, dan fail statik lain yang bertembung dengan output milik akar gagal dengan `i18n-root-output-conflict`.
- **Perubahan tingkah laku: mod merged untuk sitemap dan feed tidak lagi menulis salinan setiap bahasa**: apabila `site.languages` ditetapkan, `sitemapMode: merged` sebelum ini masih meninggalkan `dist/<lang>/sitemap.xml` dalam setiap bahasa, ditulis oleh unjuran penerbitan setiap bahasa walaupun kontrak yang didokumenkan ialah satu sitemap akar sahaja; `feed.mode: merged` juga meninggalkan `rss.xml`, `<feed.path>/atom.xml`, `<feed.path>/feed.json` dan feed koleksi setiap bahasa. Fail setiap bahasa ini tidak lagi dijana dalam kedua-dua susun atur, dan laporan `publish-audit` bahasa menandakan representasi itu sebagai tidak dijana. Hantar `sitemap.xml` akar kepada enjin carian, dan tukar pautan feed, termasuk pautan tema yang dibina daripada `{{ site.base_url }}/rss.xml`, kepada feed gabungan akar.
- **Tapak satu bahasa memiliki sitemap dan feed mod merged**: tanpa `site.languages`, `sitemapMode: merged` dan `feed.mode: merged` sudah menulis `sitemap.xml` dan feed, tetapi manifes binaan tidak merekodkannya. Kini ia ialah output yang dimiliki: mematikan ciri itu kemudian membuang fail lapuk, dan fail `static/` dengan nama yang sama gagal dengan `BuildAssetOutputCollision` sama seperti mod split.
- **Perubahan tingkah laku: halaman ubah hala tidak diindeks di mana-mana**: halaman alias dan ubah hala laman utama lama dalam susun atur akar (`/<defaultLanguage>/`) kini dijana dengan `robots: noindex` dan dikecualikan daripada sitemap, carian dan feed. Pada setiap tapak yang menggunakan `aliases`, halaman ini tidak lagi muncul dalam `llms.txt` dan audit penerbitan tidak lagi menganggapnya halaman boleh diindeks (tiada lagi amaran `publish.sitemap_missing_route` atau `publish.search_missing_route` untuknya).
- **Semakan static mengikut `build.followSymlinks`**: semakan konflik output akar dan ubah hala laman utama lama kini menilai pautan simbolik dalam `static/` sama seperti langkah salinan: diabaikan apabila `followSymlinks` dimatikan, dan hanya dikira apabila ia menghala ke dalam direktori static apabila dihidupkan. Pautan simbolik yang dilangkau oleh langkah salinan tidak lagi menggagalkan binaan atau menyekat ubah hala. Helaian gaya token tema (`assets/css/theme-tokens.css`) juga disemak terhadap awalan bahasa terpelihara, jadi kod bahasa `assets` dengan bahasa lalai di akar gagal dengan `i18n-route-reserved-language-prefix`.
- **Hanya `static/robots.txt` dengan nama tepat menggantikan robots yang dijana**: dengan bahasa lalai di akar, fail static bernama `Robots.txt` (atau huruf besar/kecil lain) kini gagal dengan `i18n-root-output-conflict` dan tidak lagi menyekat `robots.txt` yang dijana; perangkak hanya meminta laluan huruf kecil, jadi pada hos yang peka huruf tapak itu tiada peraturan robots. Semakan konflik output akar juga melangkau laluan berawalan titik yang dilangkau oleh langkah salinan (melainkan `build.publishDotFiles` dihidupkan), dan audit bahasa lalai mengikut peraturan fail titik dan pautan simbolik yang sama apabila menentukan sama ada `robots.txt` static wujud.
- **Halaman ubah hala ialah HTML mentah lengkap**: halaman alias tidak lagi melalui saluran transformasi HTML, jadi suntikan tajuk SEO/analitik tidak mengubahnya; ia ditulis sebagai dokumen ubah hala lengkap. Halaman ubah hala alias dan taksonomi juga mendapat `<title>` dan `<meta name="robots" content="noindex">`. Output alias dalam susun atur berawalan lalai berubah; sasaran dan lokasi ubah hala tidak berubah.
- **Terjemahan yang berkongsi slug tidak lagi menggagalkan pengesahan skema ketat**: Apabila `site.languages` ditetapkan, `canonical_duplicate_id` kini menyemak ID dalam satu bahasa sahaja; halaman tanpa bahasa dikira sebagai bahasa lalai, dan tapak satu bahasa masih memerlukan ID unik di seluruh tapak. Terjemahan yang menggunakan nama fail atau slug yang sama dalam setiap bahasa (contohnya `content/en/index.md` dan `content/zh/index.md`) berkongsi ID mengikut reka bentuk, tetapi setiap pasangan merekodkan amaran ID pendua dan menggagalkan binaan di bawah `build.schemaFailMode: strict`. `bukit doctor` juga tidak lagi melaporkan pasangan ini sebagai ralat skema yang menyekat. Dua halaman dengan ID yang sama dalam satu bahasa masih melaporkan amaran itu, kini dengan nama bahasa.
- **Binaan berbilang bahasa merekodkan setiap isu audit sekali sahaja**: isu `seo.audit`, `publish.audit` dan `geo.audit` dahulu direkodkan dua kali, sekali oleh setiap varian bahasa dengan laluan tanpa awalan bahasa (jadi `/index/` muncul sekali bagi setiap bahasa) dan sekali lagi oleh audit gabungan dengan laluan penuh. Varian kini hanya menulis fail laporan. Proses induk merekodkan isu audit gabungan bersama isu setiap bahasa (dengan awalan bahasa ditambah pada laluan, untuk semakan yang hanya berjalan bagi setiap bahasa seperti peta laman setiap bahasa dalam mod `split`), dan merekodkan setiap isu yang berbeza sekali dengan laluan seperti `/en/index/`. Kiraan amaran dan ralat laporan binaan tidak lagi mengira isu ini dua kali, jadi kiraannya turun bagi tapak berbilang bahasa. Fail `.bukit/seo-report.json` setiap bahasa tidak berubah. Jika binaan gagal semasa menulis output akar, isu setiap bahasa dan amaran pautan bertandatangan masih direkodkan sebelum ralat.
- **Binaan berbilang bahasa merekodkan amaran pautan bertandatangan sekali sahaja**: `event=incremental.signed_links` dahulu direkodkan sekali bagi setiap bahasa, biasanya dengan kunci yang sama kerana bahasa-bahasa membaca data kongsi yang sama. Kini setiap bahasa menyerahkan dapatannya kepada proses induk, yang merekodkan satu baris bagi bahasa yang dapatannya serupa, dengan senarai dipisahkan koma seperti `language=en,zh-CN`. Bahasa dengan dapatan berbeza masih mendapat baris sendiri. Penghurai log perlu menjangkakan senarai dipisahkan koma dalam `language=`, dan kiraan amaran laporan binaan turun satu bagi setiap baris berulang. Binaan satu bahasa tidak berubah.
- **Diagnostik i18n untuk kandungan yang digugurkan dan pendua**: binaan kini merekodkan `i18n.document.language_unknown` apabila `language` halaman tiada dalam `site.languages` (atau bukan `site.language` dalam binaan satu bahasa), `i18n.document.i18n_key_duplicate` apabila dua halaman dalam satu bahasa berkongsi `i18nKey`, dan `i18n.config.default_language_ignored` apabila `site.defaultLanguage` ditetapkan tanpa `site.languages`. Halaman atau konfigurasi ini sebelum ini diabaikan, ditimpa atau digugurkan tanpa amaran. Tapak satu bahasa yang sengaja memilih satu bahasa daripada sumber berbilang bahasa kini memberi amaran `language_unknown` untuk bahasa lain pada setiap binaan; amaran dihadkan kepada lima bagi setiap jenis serta satu baris ringkasan, dan bilangan amaran laporan binaan boleh meningkat. Selain perubahan ID awam `i18n_key` di bawah, output tapak yang sah tidak berubah.
- **Doctor menyemak setiap bahasa yang dikonfigurasikan**: `bukit doctor` kini membina dan mengesahkan laluan untuk setiap entri `site.languages`, bukan hanya bahasa lalai, jadi konflik laluan atau templat hilang yang hanya wujud dalam bahasa bukan lalai gagal di peringkat doctor dan bukan semasa binaan.
- **`i18n_key` kini menentukan ID awam**: halaman yang menggunakan alias `i18n_key` kini menggunakan nilai itu untuk `id` dan `canonicalId` dalam `agent-manifest.json` dan unjuran kandungan awam, sama seperti `i18nKey`. Sebelum ini alias itu kembali kepada slug, jadi kumpulan terjemahan yang sama boleh mendapat ID awam berbeza dalam bahasa berbeza. Tapak yang menggunakan `i18nKey` atau fallback slug tidak berubah.
- **Diagnostik kurasi llms kini berjalan**: kurasi `geo.llms` peringkat halaman yang tidak sah (kunci tidak dikenali, `visibility` atau `tier` yang salah, `priority` yang bukan integer seperti `"10"` atau di luar julat -100 hingga 100, `llms: false`, atau nilai `geo:` tidak kosong yang bukan pemetaan) kini merekodkan `geo.llms_field_unknown`, `geo.llms_visibility_invalid`, `geo.llms_tier_invalid`, `geo.llms_priority_invalid` atau `geo.llms_shape_invalid` (nilai `geo` atau `llms` yang bukan pemetaan), setiap satu dengan `route=<laluan output>`, semasa binaan untuk setiap halaman berlaluan (termasuk halaman noindex), dan menggagalkan binaan di bawah `site.seo.diagnostics: strict`. Sejak penyusunan semula unjuran penerbitan, semakan ini tidak pernah berjalan, jadi halaman sedemikian digugurkan daripada `llms.txt` dan `llms-full.txt` tanpa amaran. Tapak yang menggunakan `strict` dan mempunyai kurasi tidak sah kini akan gagal dibina; betulkan front matter atau gunakan `visibility: exclude`. Kurasi yang sah dan output tidak berubah. Kunci `geo:` atau `llms:` yang kosong (atau hanya ruang kosong) kini dianggap tiada: halaman sedemikian dimasukkan ke dalam `llms.txt` dan `llms-full.txt` dan tidak lagi menggagalkan binaan `strict` (sebelum ini ia dikecualikan secara senyap).

## [2.0.15] - 2026-11-11

- **Pautan imej tamat tempoh gagal serta-merta**: apabila muat turun imej ditolak (HTTP 400, 401 atau 403) dan tandatangan URL itu sendiri telah tamat tempoh (AWS SigV4, Google Cloud Storage V4, AWS SigV2/CloudFront atau proksi fail Notion), ia tidak lagi dicuba semula sebanyak `content.media.maxRetries` kali, kerana tandatangan yang tamat tempoh tidak akan berjaya. Ini terutamanya mempercepat binaan daripada cache Notion `readonly` yang masih menyimpan pautan tamat tempoh. Kegagalan, hasil `onFailure` dan penggunaan semula imej yang telah disimpan tidak berubah, dan amaran menambah `signature=expired`. Kegagalan lain masih dicuba semula.
- **Amaran untuk pautan bertandatangan dalam data kongsi**: dalam binaan tambahan, Bukit kini merekodkan satu amaran `event=incremental.signed_links` apabila `site.modules`, `site.data`, `site.data_index`, `site.related_pages`, `site.data_files`, sumber metadata laluan atau medan halaman (kunci `pages`) masih mengandungi pautan skema tandatangan yang dikenali (AWS SigV4, Google Cloud Storage V4, AWS SigV2/CloudFront, proksi fail Notion). Amaran itu menamakan setiap kunci bacaan dan bilangan pautannya, serta `language=` dalam binaan berbilang bahasa. Pautan ini mungkin berubah pada setiap pengambilan, jadi halaman yang membacanya mungkin dipaparkan semula pada setiap binaan; setempatkan fail dengan `content.media` untuk mengelakkannya. Pencincangan dan output tidak berubah.
- **`bodyFingerprint` tidak lagi ditunjukkan untuk halaman lain**: dalam senarai `pages` dan item `source` seksyen, `fields` tidak lagi memegang nilai dalaman `bodyFingerprint`. Ia mengenal pasti kandungan badan, yang tidak dijejaki oleh senarai itu, jadi halaman yang mencetaknya boleh mengekalkan teks lama selepas badan halaman lain disunting. `page.fields` halaman itu sendiri tidak berubah.
- **Huruf besar atau kecil nama pemalam dan pilihan tidak lagi memaparkan semula laman**: nama pemalam dan nama pilihan peringkat atas dipadankan tanpa mengira huruf besar atau kecil, dan cincangan tambahan kini mengendalikannya dengan cara yang sama, jadi menukar huruf sahaja tidak lagi memaparkan semula setiap halaman.

## [2.0.14] - 2026-11-10

- **Badan Notion mengikut perubahan pemapar dan suntingan dalam minit yang sama**: cache badan Notion (format 2) merekodkan versi pemapar dan masa pengambilan daripada pengepala `Date` Notion. Dalam mod `readwrite`, badan dalam cache hanya digunakan semula bagi versi pemapar dan `last_edited_time` yang sama, dengan pautan yang belum tamat tempoh, dan hanya jika ia diambil sekurang-kurangnya 5 saat selepas minit suntingan berakhir, jadi suntingan kemudian dalam minit yang sama dengan pengambilan tidak lagi hilang. Cap jari badan tokokan menjadi `{last_edited_time}:renderer={versi}[:unsettled]:content={on|off}`; halaman tanpa `last_edited_time` mendapat cincang kandungan daripada badannya. Binaan `readwrite` pertama selepas naik taraf mengambil semula semua badan Notion, yang boleh mengambil masa lama bagi pangkalan data besar. `readonly` masih menggunakan semula entri yang sepadan dan melaporkan entri lama dalam satu amaran `notion.cache.renderer_mismatch`; bina dengan `--clean` apabila bertukar semula kepada `readwrite`.

## [2.0.13] - 2026-11-10

- **Pemaparan lebih pantas untuk tapak besar**: sebelum ini setiap halaman menukar semula keseluruhan indeks halaman tapak (`pages`) dan model tapak (`site`, termasuk `site.data.pages_by_id`), jadi masa pemaparan meningkat mengikut kuasa dua bilangan halaman. Kini kedua-duanya ditukar sekali bagi setiap pusingan pemaparan dan dikongsi. Output tidak berubah untuk templat yang hanya membacanya. Perubahan tingkah laku: `site`, `pages`, `items` halaman senarai dan semua kandungannya kini baca sahaja pada setiap peringkat, jadi templat yang menambah kunci baharu atau menetapkan elemen tatasusunan (seperti `pages[0] = x`) gagal dengan ralat baca sahaja; dalam komponen dan seksyen, ralat itu mengikut `theme.componentValidation`.
- **Pemasaan pemaparan dan laporan**: `--metrics` kini merekodkan masa sebenar `pageRender`, `listBuild` dan `staticRender` (sebelum ini sentiasa 0), serta `renderCall`, `htmlTransform`, `renderWrite`, `renderModelBind` dan `renderDispatch`, serta langkah varian sebelum pemaparan `variantSetup`, `mediaResolve`, `renderPlan`, `assetPrepare` dan `seoPrepare`. Setiap langkah laporan selepas pemaparan merekodkan `event=build.report.timing` bersama tempohnya, dan setiap skop berakhir dengan `step=total`. Fail laporan dan struktur `metrics.json` tidak berubah.
- **Peringkat laporan lebih pantas untuk tapak besar**: semakan sama ada setiap laluan muncul dalam `sitemap.xml`, `search.json`, `agent-manifest.json`, `llms.txt` dan fail agregat lain kini mengimbas setiap fail sekali, bukan sekali bagi setiap laluan; audit penerbitan membaca dan menghurai `agent-manifest.json` sekali, bukan sekali bagi setiap dokumen; dan pengesahan output media mengira tetapan media berkesan sekali, bukan sekali bagi setiap pautan. Pada tapak 4,651 halaman, tiga langkah ini mengambil sebahagian besar masa binaan. Output, fail laporan dan isu audit tidak berubah.
- **Imej yang ditandatangani semula mengekalkan failnya**: nama fail imej setempat tidak lagi berubah apabila hanya tandatangannya berubah. Bagi URL yang ditandatangani dengan AWS SigV4, Google Cloud Storage V4, AWS SigV2/CloudFront (`Signature` bersama `AWSAccessKeyId`, `GoogleAccessId` atau `Key-Pair-Id`), atau yang disajikan oleh proksi fail Notion (`notion.so`, `*.notion.so`, `*.notion.site`), parameter tandatangan dan tamat tempoh tidak lagi dikira dalam identiti fail; parameter lain masih dikira, dan muat turun masih menggunakan URL bertandatangan penuh. Oleh itu imej Notion dimuat turun sekali, bukan lagi pada setiap binaan selepas pautannya tamat tempoh. Apabila satu pautan kepada imej sudah tamat tempoh tetapi rujukan lain mempunyai tandatangan baharu, tandatangan baharu digunakan. Selepas naik taraf, imej bertandatangan dimuat turun sekali lagi di bawah nama stabil baharunya, kecuali fail yang disimpan oleh versi terdahulu masih digunakan untuk URL bertandatangan tepat asalnya, jadi cache Notion `readonly` dengan pautan tamat tempoh mengekalkan imej setempatnya; imej tanpa tandatangan mengekalkan nama failnya.
- **Binaan tokokan mengesan perubahan enjin dan pemalam**: cincang templat kini merangkumi versi dan semakan sumber enjin Bukit, jadi binaan `--no-clean` pertama selepas naik taraf memaparkan semula semua halaman (dilaporkan sebagai `template_changed`, senarai sebagai `list_render`), bukan mengekalkan halaman yang dipaparkan oleh enjin lama. Pilihan pemalam terbina dalam yang didayakan kini dikira sebagai input pemaparan, jadi mengubahnya memaparkan semula tapak; susunan kunci tidak penting. Binaan tanpa metadata git atau dengan perubahan yang belum dikomit berkongsi identiti keluarannya dan memerlukan `--clean`.
- **Sebab pemaparan `metadata_changed`**: halaman yang dipaparkan semula kerana rekod atau medan tersuainya berubah kini dikira sebagai `metadata_changed` dalam `renderReasons` bagi `.bukit/incremental-manifest.json`; `content_changed` kini bermaksud hanya kandungan badan berubah, termasuk perubahan `bodyFingerprint` (contohnya suntingan badan Markdown). Menyunting halaman Notion turut mengemas kini medan `last_edited_time`nya, jadi suntingan Notion dipaparkan sebagai `metadata_changed`. Skrip yang membaca `content_changed` juga perlu membaca `metadata_changed`.
- **Menyunting satu halaman tidak lagi memaparkan semula seluruh tapak**: binaan `--no-clean` kini menjejak data kongsi bagi setiap halaman. Bukit merekodkan koleksi yang dibaca oleh templat setiap halaman (susun atur, `include`, komponen dan seksyen): kunci `site.data`, `site.modules` atau `site.data_index`, `site.related_pages`, `site.data_files`, atau `pages` (turut dibaca oleh seksyen yang mempunyai `source`). Menyunting artikel memaparkan semula artikel itu dan halaman yang membaca data yang diubahnya, seperti `site.data.pages_by_id`, senarai dan halaman yang menggelung `pages`, dilaporkan sebagai sebab pemaparan baharu `data_dependency_changed`; perubahan tetapan seluruh tapak masih `render_dependency_changed`. Templat yang menggunakan `site` atau `site.data` secara keseluruhan, pemboleh ubah global yang tidak dikenali, `this` atau pemalam seksyen dikira membaca semua koleksi, begitu juga pemapar tersuai. Halaman yang menggelung `pages` kini turut dipaparkan semula apabila halaman lain berubah, sedangkan dahulu ia mengekalkan HTML lama. `build-manifest.json` mendapat medan entri pilihan `renderReads` dan `renderBaseHash`; binaan `--no-clean` pertama selepas naik taraf memaparkan semua halaman.

- **Content Signals dalam robots.txt**: `site.seo.robotsTxt.contentSignal` (`search`, `aiInput`, `aiTrain`, setiap satu `true` atau `false`) menulis baris `Content-Signal:` dalam kumpulan `User-agent: *`, contohnya `Content-Signal: search=yes, ai-input=yes, ai-train=no`, supaya tapak boleh menyatakan cara syarikat AI boleh menggunakan kandungannya. Hanya nilai yang ditetapkan disenaraikan. Jika tidak ditetapkan, `robots.txt` tidak berubah.

## [2.0.12] - 2026-11-10

- **URL berkod peratus**: medan `url` atau `route.url`, atau entri `aliases`, yang ditampal dalam bentuk sudah berkod peratus (seperti `/%e8%81%94%e7%b3%bb%e6%96%b9%e5%bc%8f/`) kini dinyahkod sekali (di sini kepada `/联系方式/`), jadi folder output sepadan dengan alamat yang diminta pelayar dan tidak lagi memulangkan 404. Kod yang akan mengubah laluan (`/`, `\`, `.`, `%`), aksara kawalan, aksara tidak kelihatan atau mirip (lebar sifar, pengatasan bidi, NBSP) dan UTF-8 tidak sah dikekalkan berkod. Binaan merekodkan `event=route.url.decoded` atau `event=route.alias.decoded` bersama nilai asal supaya sumber boleh dibetulkan. URL tanpa `%` tidak berubah.

## [2.0.11] - 2026-11-09

- **Susunan masa sama seluruh tapak**: `content.ordering.tieBreakField` (`url`, `title`, atau mana-mana medan kandungan seperti `ID`) dan `content.ordering.tieBreakOrder` (`asc` | `desc`) menyusun entri yang mempunyai masa terbit sama dalam setiap senarai: `pages` halaman utama, senarai koleksi dan penomboran, arkib, suapan tapak dan koleksi (RSS, Atom, JSON Feed, dan tetingkap suapan audit), serta `llms.txt`. Halaman istilah menggunakannya apabila `taxonomy.tieBreakField` tidak ditetapkan, jadi satu tapak tidak lagi memaparkan siaran yang sama dalam susunan berbeza di halaman utama dan halaman kategorinya. `title` membandingkan tajuk dokumen tanpa mengira huruf besar/kecil dalam setiap senarai, sama seperti susunan terbina dalam halaman istilah: `title` `asc` ditulis atau tidak memberi halaman istilah dan cache tokokan yang sama, dan `title` `desc` ialah terbalik tepatnya (sebelum ini kembali kepada menaik). Nombor bulat dibandingkan dengan tepat, jadi ID melebihi 15 digit kekal tersusun. Halaman arkib bulanan menyenaraikan siaran masa sama mengikut susunan senarai koleksi. `taxonomy.tieBreakOrder` yang ditulis bersendirian kini digunakan pada medan yang diwarisi (atau tajuk), bukan diabaikan. Suapan RSS taksonomi kini menyusun entri masa sama seperti halaman istilahnya (tie-break, kemudian tajuk, kemudian URL), bukan mengikut URL sahaja. Jika tidak ditetapkan, output lain dan cache tokokan tidak berubah.
- **Suapan RSS kosong**: suapan tanpa item tidak lagi menulis `lastBuildDate` sebagai `Thu, 01 Jan 1970`; elemen pilihan itu ditinggalkan. Atom mengekalkan `<updated>` yang diwajibkan.
- **Susunan masa sama dalam taksonomi**: `taxonomy.tieBreakField` (`title`, `url`, atau mana-mana medan kandungan seperti `ID`) dan `taxonomy.tieBreakOrder` (`asc` | `desc`) menyusun entri halaman istilah yang mempunyai masa terbit sama sebelum penomboran, jadi tapak WordPress yang dipindahkan boleh mengekalkan susunan ID lama merentas halaman. Nombor dibandingkan mengikut nilai; entri tanpa medan itu diletakkan terakhir; suapan RSS taksonomi menggunakan susunan yang sama. Jika tidak ditetapkan, atau `title` `asc`, susunan tajuk terbina dalam, output dan cache tokokan semasa dikekalkan.
- **Output besar tidak lagi dipotong**: templat tidak lagi menggunakan had rentetan 1 MiB Scriban, yang di dalam `include` memotong output sekitar 524k aksara dan mengosongkan baki halaman. JSON-LD ItemList menyenaraikan paling banyak 100 entri (dengan `numberOfItems` apabila dihadkan), jadi halaman senarai tanpa penomboran kekal kecil. Halaman yang membuka `<html>` tetapi tidak menutupnya dilaporkan sebagai `render.output_truncated`, bukan `seo.document_title_missing`.
- **Laman utama dan induk taksonomi dalam breadcrumb**: `site.seo.schema.breadcrumbHome` memulakan JSON-LD BreadcrumbList dengan laman utama, dan `site.seo.schema.breadcrumbParent` (jenis taksonomi seperti `categories`) meletakkan halaman istilah pertama sebelum halaman kandungan, jadi URL rata seperti `/2596/` mendapat Laman utama › kategori › artikel. Jenis taksonomi yang tidak wujud gagal pengesahan, dan binaan tokokan memaparkan semula halaman apabila mana-mana nod breadcrumbnya berubah. Kedua-duanya dimatikan secara lalai; jika tidak ditetapkan, output dan cache tokokan tidak berubah.
- **Pengarang SEO lalai**: `site.seo.defaultAuthor` dan `site.seo.defaultAuthorType` (lalai `Person`, atau `Organization`) menetapkan pengarang JSON-LD artikel apabila halaman tiada hubungan pengarang, medan `author` atau pengarang GEO. Jika tidak ditetapkan, output dan cache tokokan tidak berubah.

## [2.0.10] - 2026-11-09

- **Kawalan penyetempatan media**: `content.media.onFailure: placeholder | keep` (lalai `placeholder`) mengekalkan URL asal apabila muat turun gagal; `content.media.downloadHosts` dan `skipHosts` mengehadkan muat turun mengikut hos (kad liar `*.`). Respons berjenis `application/octet-stream` atau tanpa jenis dikenal pasti melalui tandatangan fail dan mesti lulus semakan imej sedia ada.
- **Pembinaan tapak besar**: cincang kebergantungan render membenarkan sehingga 10,000,000 nod; praambil ringkasan Notion melog `event=notion.progress`; pembinaan yang gagal atau dibatalkan mengekalkan cache Notion yang dipentaskan (`event=build.cache_preserved`); Ctrl+C membatalkan pembinaan dengan bersih, dan Ctrl+C kedua keluar serta-merta; ralat masa jalan templat menyertakan punca asal.
- **Ringkasan amaran**: amaran berulang daripada jenis yang sama mencetak lima baris, kemudian satu baris `event=diagnostics.summary` dengan jumlahnya.
- **Imbasan privasi**: semakan privasi penerbitan kini berjalan dalam masa linear.
- **Taksonomi**: `taxonomy.emptyTerms` mengawal halaman istilah kosong; istilah berkod peratus dinyahkod; indeks tag tidak lagi mengandungi istilah kategori.
- **Diagnostik SEO**: tanda petik sesat dalam tag head tidak lagi menyembunyikan `<title>`; halaman tanpa JSON-LD mendapat satu `seo.json_ld_not_rendered` dan bukan isu skema bagi setiap medan; di luar mod strict, isu tajuk dilaporkan sekali oleh audit penerbitan; panjang tajuk dan deskripsi menggunakan lebar paparan (CJK dikira 2); deskripsi terbitan dipotong kepada lebar 160 dengan "…" (`seo_desc` eksplisit dikekalkan); `bukit doctor` memberi amaran apabila templat mod tema tidak mengeluarkan `seo.document_title` atau `seo.json_ld`.
- **Persetujuan Analytics kini pilihan**: penyedia GA dan GTM tidak lagi memerlukan `site.analytics.consent.google`. Tanpanya, Bukit tidak menjana arahan lalai persetujuan dan Google menggunakan nilai lalainya sendiri (secara efektif semua storan `granted`); konfigurasi persetujuan tanpa penyedia Google masih ditolak. Konfigurasi sedia ada menghasilkan output yang sama.

## [2.0.8] - 2026-11-07

- **Pentas media berbilang bahasa**: `content.media.downloadDir` tersuai kini dipentaskan sekali dan dikongsi oleh semua pekerja bahasa. Muat turun baharu dan `.media-index.json` hanya diserahkan bersama output dan cache selepas semua bahasa berjaya. Perubahan media dikesan melalui laluan, saiz, dan masa ubah suai; pautan simbolik di dalam direktori media ditolak. Ini perubahan tingkah laku: direktori media yang mengandungi sebarang pautan simbolik kini menggagalkan pembinaan berbilang bahasa.
- **Pemulihan gangguan pembinaan**: Pembinaan seterusnya memulihkan rekod transaksi lengkap terakhir untuk output tersebut, termasuk output, cache, media yang dipentaskan, dan metrik luaran. Fail sementara yang tidak diketahui dikekalkan; sasaran yang berubah atau sandaran yang hilang menghentikan pemulihan dan mengekalkan keadaan untuk pemeriksaan. Pembinaan berbilang bahasa eksplisit kini berkongsi pajakan sumber binaan dengan pembinaan dalam proses. Pautan simbolik input yang diperlukan mesti relatif dan kekal di dalam repositori; pautan simbolik input mutlak ditolak.
- **Pembaikan audit Analytics**: serpihan permulaan head mengikut pengisytiharan pengekodan halaman, laporan keperluan CSP menambah `connectSrc` dan `imgSrc`, dan iframe `<noscript>` GTM tidak dijana apabila mana-mana nilai lalai persetujuan Google ialah `denied`.
- **Pembaikan audit SEO**: canonical kandungan relatif-akar diselesaikan terhadap `site.url` + `baseUrl` tanpa mengulang laluan base yang sedia ada, bentuk relatif lain dilaporkan sebagai `seo.canonical_relative`, dan `og:locale` menggunakan pemisah garis bawah seperti `zh_CN` dengan diagnostik `seo.og_locale_region_missing` bukan maut untuk bahasa tanpa wilayah, termasuk di bawah diagnostik ketat.
- **Polisi perangkak AI**: `robots.txt` mengumpulkan perangkak AI mengikut tujuan (latihan, carian AI, ambilan pengguna) dan menambah `aiBotMode: block-training` (ia juga menyekat `Google-Extended`, yang mengawal latihan dan grounding Gemini; binaan merekodkan `event=robots.ai_grounding_blocked`). Perubahan tingkah laku: mod `selective` dengan `aiBotAllowList` kini menyekat semua perangkak lain yang dikenali, termasuk perangkak carian AI; binaan memberi amaran `event=robots.ai_search_blocked`. Binaan juga memberi amaran `event=robots.ai_policy_inactive` apabila polisi AI dikonfigurasi tetapi tidak dapat ditulis.
- **Skop sitemap**: sitemap hanya menyenaraikan canonical di bawah `site.url` + `baseUrl` (port lalai dan huruf hos dinormalkan), sekali setiap satu; audit penerbitan tidak lagi menjangka halaman yang canonical-nya menunjuk ke tempat lain.
- **Penemuan ejen AI**: apabila GEO didayakan dalam mod `inject`, halaman kandungan yang boleh diindeks memautkan versi Markdown dan JSON-nya dengan `<link rel="alternate">`, dan `llms.txt` memautkan `agent-manifest.json` dalam bahagian `## Optional`. Menukar `site.seo.geo.enabled` kini memaparkan semula halaman dalam binaan tokokan. Dalam mod `theme` dan `off`, susun atur boleh membaca URL yang sama daripada `page.representation_urls`, yang hanya ditetapkan untuk halaman diterbitkan tanpa `noindex`. `agent-manifest.json` kini ditulis tanpa BOM UTF-8.

## [2.0.7] - 2026-11-04

- **Versi**: versi produk dalam `Directory.Build.props` ditetapkan kepada 2.0.7 supaya sepadan dengan barisan keluaran (v2.0.6 dikeluarkan ketika fail masih 2.0.4; binaan keluaran menimpanya dengan `-p:VersionPrefix`).
- **Ujian kendiri regresi keselamatan**: setiap pemilih dalam `scripts/security/security-regression.sh` kini mesti sepadan dengan pengisytiharan ujian sebenar dalam fail yang mempunyai `[Fact]`/`[Theory]`; entri yang tidak dapat dihuraikan gagal dan bukan dilangkau; ujian kendiri dijalankan dalam pintu gerbang CI fast dan menyokong laluan checkout dengan ruang.
- **Dokumen keluaran**: menambah seksyen log perubahan 2.0.0 hingga 2.0.6 dan senarai semak keluaran dalaman 2.0.

## [2.0.6] - 2026-11-03

- Mengintegrasikan peringkat Core 6-10 (penutupan output enjin dan audit, ujian hujung-ke-hujung saluran, liputan CLI dan pemalam, pintu gerbang seni bina/liputan/tiga platform), kontrak ralat pemalam dan penyamaran rahsia, serta pembetulan audit pintu gerbang ujian (PR #66-#86).

### Integrasi Dalaman SEO/GEO

- Menambah kontrak publisher organisasi yang disahkan (`type`, output URL
  HTTP(S) mutlak, dan `sameAs`), unjuran relation silang sumber, author berasaskan
  relation, indexability senarai kosong, entiti syarikat, serta schema snapshot
  dan change-set URL penerbitan yang deterministik.
- Menambah process plugin IndexNow yang diasingkan, contoh projek minimum, dan
  manifest pemasangan dalaman Core/plugin `osx-arm64` dengan SHA-256 sebenar.
  Artifak ini untuk kegunaan dalaman, bukan keluaran awam atau keputusan gate
  release.

### Pembaikan Audit

- Menutup empat belas penemuan audit: front matter Markdown tidak sah kini gagal tertutup dan tidak menggugurkan metadata draf; deployment menolak penulisan melalui symlink `CNAME`/`.nojekyll`; Notion upsert membaca nilai medan unik sebenar dan menolak nilai hilang, tidak disokong, atau padanan berbilang halaman; WeChat berhenti mencuba semula penghantaran draf yang tidak pasti dan mengekalkan keadaan pemulihan.
- Penyetempatan media hanya menulis semula rujukan imej; glob rekursif `**/` merangkumi fail pada akar kandungan; tatasusunan Seed ditulis sebagai jujukan YAML; pemantau Dev merangkumi direktori Markdown tersuai dan input tema warisan; cache Notion menyegarkan pautan bertandatangan yang tamat tempoh dan sasaran relation dalam talian; penyediaan data berbilang bahasa mematuhi pembatalan; Markdown memajukan tetapan auto-summary.
- Menggunakan semula penyelesai laluan canonical bersama dan membuang logik laluan serta pembaca medan yang bertindih/tidak digunakan. Menambah liputan regresi dan panduan pemulihan `DraftSubmitting`.

### Keselamatan Dan Kebolehpercayaan

- **Pembersihan output selamat (F-01)**: Clean eksplisit/konfigurasi, pembersihan build, dan recovery berkongsi cleaner terkawal. Root berbahaya, descendant `.git`, sasaran di luar projek atau symlink/reparse, dan direktori bukan kosong tanpa marker ditolak.
- **Keselamatan DOM UI carian lalai (F-02)**: Title/snippet kandungan dirender dengan text node dan elemen `<mark>`, bukan sink yang mentafsir HTML; placeholder dikodkan.
- **Pemilikan output deterministik (F-03)**: Destinasi static, assets, media, generated tokens, dan render dipra-semak sebelum penulisan penerbitan. Konflik silang kategori, struktur, dan variasi huruf pada volume case-insensitive gagal dengan `BuildAssetOutputCollision`.
- **Penemuan rekursif selamat (F-04)**: Laluan lalai content, static, media, hash, dan inventori report melangkau symlink direktori/reparse point. Follow eksplisit kekal terhad kepada copy path yang disokong.
- **Keputusan templat semasa (F-05)**: Manifest capability menggunakan fingerprint kandungan, analisis dependensi diasingkan per panggilan, dan rebuild proses sama melihat perubahan manifest/root/include/layout tanpa kebocoran hasil cache boleh ubah.
- **Penguatkuasaan had content carian (F-06)**: `site.search.maxContentLength` sedia ada kini digunakan pada `content` carian document, list, plugin, publish projection, dan berbilang bahasa; default dan bentuk schema tidak berubah.
- **Konkurens media pada tahap muat turun (F-07)**: `content.media.maxConcurrency` sedia ada kini mengehadkan muat turun localization aktif dalam skop rewrite operation dan localized body store.
- **Build health tepat (F-08)**: Medan `build-report.v1` sedia ada kini mengandungi kiraan diagnostik build semasa dan inventori public `generatedFiles` yang stabil; bentuk schema beku tidak berubah.

### Changed
- **SiteEngine direfaktor**: 856 → 592 baris orkestrator dengan 8 kelas pipeline bebas (`BuildPipeline`, `ContentPipeline`, `RoutePipeline`, `RenderPipeline`, `AssetPipeline`, `SeoPipeline`, `PluginPipeline`, `BuildReportPipeline`), serta `ThemeBootstrapper`, `BuildOptionsMapper`, `FixedContentProviderFactory`. Dua laluan `BuildAsync` disatukan menjadi rantaian pipeline tunggal. Semua helper ujian refleksi dihapuskan (sifar `BindingFlags`). Ujian regresi prestasi ditambah.

### Added
- **Taxonomy v3.0.0**: Pembinaan semula utama sistem taxonomy dengan 7 ciri baharu
  - Taxonomy hierarki: `taxonomy.kinds[].hierarchical: true` mendayakan hubungan induk-anak term melalui `ParentSlug`, dengan pengiraan automatik `children` dan `ancestors`
  - Metadata term: Konvensyen `_index.md` (gaya Hugo) di `content/_taxonomy/<kind>/<slug>/_index.md` untuk description, image, weight, parent setiap term
  - Suapan RSS 2.0: Setiap term menjana `<output>/<kind>/<slug>/feed.xml` secara automatik
  - Transliterasi slug: Penguraian Unicode NFD (`é→e`, `ß→ss`, `æ→ae`, `œ→oe`, `ø→o`), aksara CJK dikekalkan
  - Alias redirect: Medan `Aliases` menjana halaman redirect HTML `<meta http-equiv="refresh">`
  - Kawalan keterlihatan term: Medan `IsVisible` dan `Weight` untuk penapisan dan pengisihan
  - Skema `taxonomy.json` dinaik taraf ke v2 (termasuk tatasusunan `children` dan `ancestors`)
- **SlugHelper**: Utiliti penjanaan slug berkongsi dalam `Bukit.Shared`, menggabungkan 3 pelaksanaan pendua dengan sokongan transliterasi Latin

### Changed
- **TaxonomyPlugin** dibina semula dari 1194 baris ke 245 baris — 7 pembantu dalaman diekstrak: `TaxonomyIndexBuilder`, `TaxonomyPageCreator`, `TaxonomyDataWriter`, `TaxonomyTemplateResolver`, `TaxonomySortHelper`, `TaxonomyHierarchyBuilder`, `TaxonomyMetadataLoader`
- **Model TaxonomyTerm** diperkaya dengan medan `Description`, `Image`, `Weight`, `IsVisible`, `ParentSlug`, `Aliases`, `Pages`
- `TaxonomyKindConfig` mendapat medan boolean `Hierarchical` baharu (lalai `false`)

### Ujian
- 5 fail ujian baharu (38 kes ujian): `SlugHelperTests` (22), `TaxonomyHierarchyBuilderTests` (3), `TaxonomyMetadataLoaderTests` (6), `TaxonomyFeedWriterTests` (3), `TaxonomyRedirectWriterTests` (4)
- Semua 1311 ujian lulus (Shared 67, Engine 793, Content 451)

## [2.0.5]

- Versi dilangkau: tiada tag `v2.0.5` atau GitHub Release.

## [2.0.4] - 2026-09-13

- **Transaksi binaan**: mendokumenkan transaksi binaan dan melindungi laluan output berperingkat; membetulkan regresi analisis transaksi binaan.
- **Audit penerbitan**: audit unjuran kini menjejak URL yang diterbitkan.
- **Pembungkusan natif**: pakej natif dikecilkan dan artifak tidak lengkap ditolak.

## [2.0.3] - 2026-09-12

- **Imej responsif**: mengasingkan penemuan sumber imej responsif, membaiki kitar hayat imej responsif, mengekalkan senarai format imej kosong, dan menyegarkan output media tokokan apabila format imej berubah.
- **Laluan dalaman**: laluan dalaman dilindungi daripada output awam.
- **CI**: memperkukuh pintu gerbang CI dan pengesahan bukti keluaran; menjelaskan pengesahan SHA tag keluaran.

## [2.0.2] - 2026-09-12

- **Imej**: menambah pemprosesan imej responsif dengan sandaran ImageSharp; mengoptimumkan regex pemprosesan imej dan normalisasi saiz.

## [2.0.1] - 2026-09-12

- **Keselamatan output**: kitar hayat output awam kini gagal-tutup; laluan output statik `404.html` dikekalkan; laluan relatif pemalam dikuatkuasakan.
- **Preview**: Preview menutup penyajian fail dalaman dan pendedahan LAN; dasar laluan dalaman mengklasifikasi mengikut sasaran fizikal yang diselesaikan, menutup pintasan alias symlink dalam root.
- **Pelaksanaan proses**: alat luaran di macOS dijalankan melalui pembungkus kumpulan proses yang menghapuskan interpolasi shell.
- **Kandungan dan SEO**: unjuran badan Markdown mengekalkan pautan; pembetulan cache Notion dan penghuraian teks; peraturan audit robots.txt dipusatkan; penghalaan HTML dan penemuan media dibetulkan; tajuk halaman disertakan dalam konfigurasi Google Analytics.
- **CI**: keputusan liputan unik dikekalkan dan fixture platform dihadkan; `bukit-test.slnx` kini termasuk `Bukit.Content.Notion.Tests`.

## [2.0.0] - 2026-08-07

- **API awam**: permukaan API awam 2.0 yang disemak diterima dan dasar kestabilan 2.x ditakrifkan; kontrak binari rekod proses dan manifest pemalam dikekalkan.
- **SSRF dan laluan**: pengawal SSRF kongsi gagal-tutup apabila resolusi hos tidak selamat dan mencuba semula alamat sambungan awam; dasar laluan lalai diterbitkan secara atom; laluan mutlak berakar dinormalkan.
- **Kumpulan proses**: Linux menggunakan `setsid` untuk pengurusan pokok proses.
- **Enjin**: atribut URL halaman ringkas dilepaskan; laluan salinan output yang diselesaikan dihormati.

## [2.0.0-RC.1] - 2026-07-24

- Calon keluaran 2.0 pertama. Core diposisikan semula sebagai pengkompil penerbitan deterministik mengutamakan dalaman (lihat Kedudukan Produk di bawah Belum Dikeluarkan).

## [1.0.6] - 2026-05-21

### Added
- **Shortcodes system**: `theme.shortcodes` in site.yaml — define reusable snippets (`youtube`, `callout`, etc.) that work in both Markdown (`{% name args %}`) and Scriban templates (`{{ shortcode }}`)
- **Content schema validation**: `schema` in collection config — validate Front Matter fields by type (string/number/bool/date/list) with warn/strict failure modes
- **SCSS compilation pipeline**: `theme.scss` — automatic `.scss` → `.css` compilation during build using system `sass`/`dart-sass` CLI
- **Theme inheritance**: `theme.extends` in site.yaml — child themes cascade from parent; template lookup (child-first, parent-fallback), static/assets merging
- **Component-based templates**: `theme.components` — declare reusable components with props in site.yaml, use `{{ comp.render "Name" args }}` in Scriban templates
- **Image optimization pipeline**: `theme.images` — automatic WebP/AVIF conversion using system `cwebp`/`magick` CLI, with `ImageOptimizer.BuildSrcset()` for responsive images
- **HMR development server**: `bukit dev` command — file watching, incremental rebuild on change, WebSocket live reload to all connected browsers, debounced 300ms
- **Shared layout directive parser**: `ScribanLayoutDirectiveParser` extracted to `Bukit.Shared`, eliminating DRY violations between renderer and static analyzer
- **Async I/O in core pipeline**: `TemplateCapabilitiesResolver` now uses `File.ReadAllTextAsync` with `Task<T>` caching for non-blocking YAML manifest loading

### Changed
- **Refactored God Classes**: `SiteEngine` reduced from 1122 to 558 lines (extracted `SeoAlternatesService`, `RobotsTxtWriter`, `StaticFileService`); `PageRenderDispatcher` reduced from 581 to 491 lines (extracted `SpecialListRouteBuilder`)
- **ScribanTemplateRenderer** now accepts `shortcodes` and `components` dictionaries for runtime function injection
- **FileTemplateLoader** supports cascading lookup: primary directory first, optional fallback directory for theme inheritance
- **BuildVariantContext** extended with `ParentLayoutsDir`, `ParentAssetsDir`, `ParentStaticDir` for inherited themes
- **ConfigLoader** extended with new YAML deserialization helpers: `ReadComponents`, `ReadImageOptimizationConfig`, `ReadScssConfig`, `ReadSchema`

## [1.0.0] - 2026-05-05

### Preview

Perintah import HTML Demo (`bukit import html-demo`), import seed (`bukit import seed`), dan migrasi Notion (`bukit notion push`) adalah ciri Preview dan bukan sebahagian daripada kontrak stabil teras Bukit 1.0. Perintah ini mungkin berubah tanpa notis pada keluaran akan datang.

### Ditambah
- Keluaran pertama Bukit, penjana tapak statik Native AOT .NET 10
- Sumber kandungan Markdown dengan sokongan Front Matter
- Sumber kandungan Notion dengan pemetaan pangkalan data dan penormalan medan
- Agregasi kandungan pelbagai sumber (`markdown` + `notion`)
- Enjin templat Scriban dengan binaan vendored serasi AOT
- Sistem penghalaan dipacu Collections dengan lapisan keserasian permalink
- Plugin terbina dalam: sitemap, RSS, search JSON, taxonomy, pagination, archive, pages-index
- Sokongan tapak pelbagai bahasa dengan mod split/merged/index
- Binaan tokokan dengan pengesanan perubahan berasaskan manifes
- Sistem tema dengan perintah `theme list` / `theme use`
- Sumber data Modules (`mode=data`) untuk kandungan berstruktur
- Protokol plugin luaran v1/v2 (runtime process dan WASM)
- Penjana sumber plugin untuk pendaftaran sifar pantulan
- Perintah diagnostik `doctor` untuk semakan persekitaran dan konfigurasi
- Perintah `webhook` untuk penghantaran repositori Notion-ke-GitHub Actions
- Sistem Intent untuk konfigurasi tapak berbantu AI
- Penerbitan AOT dengan output fail tunggal
- Output metrik prestasi (`--metrics`)
- Rendering selari dengan konkurens boleh konfigurasi (`--jobs`)
- Mod CI (`--ci`) dengan pengelogan berstruktur JSON
- Dokumentasi pengguna komprehensif (16 bab, 3 bahasa)
- Dokumentasi pembangun (35+ fail merangkumi seni bina, CLI, plugin, dll.)
- Pek prompt ChatGPT untuk pembinaan tapak secara perbualan
