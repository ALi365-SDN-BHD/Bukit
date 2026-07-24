# Dasar Keselamatan

## Versi Disokong

| Versi  | Status sokongan |
|--------|-----------------|
| 2.0.x  | Ditadbir untuk kegunaan dalaman; tiada SLA sokongan awam |
| 1.x    | Sejarah; tiada komitmen sokongan awam |

## Melaporkan Kerentanan

Jika anda menemui kerentanan keselamatan dalam Bukit, sila laporkan secara peribadi.

**Jangan buka isu awam.** Sebaliknya, hantar butiran kepada penyelenggara.

Laporan peribadi yang dibuat dengan niat baik dialu-alukan dan mungkin disemak atas dasar usaha terbaik. Projek ini tidak menjanjikan tempoh pengakuan awam, tempoh pemulihan, SLA sokongan, atau garis masa keluaran. Lihat [Kedudukan Produk Bukit Core](docs/governance/bukit-core-product-positioning.md).

## Pertimbangan Keselamatan

### Sempadan Kandungan Dan Output Core

Tingkah laku keselamatan Core semasa termasuk:

- pembersihan output melalui konfigurasi atau arahan eksplisit berkongsi satu
  cleaner terkawal; root projek, home, root filesystem, `.git`, path di luar
  projek, sasaran symlink/reparse, dan direktori bukan kosong tanpa marker akan
  ditolak;
- UI carian lalai menganggap title dan snippet kandungan sebagai teks dan tidak
  menghantarnya ke sink yang mentafsir HTML;
- laluan rekursif lalai untuk content, static, media, dan inventori report tidak
  menuruni symlink direktori atau reparse point.

Jaminan ini tidak membersihkan tema sewenang-wenangnya, skrip tersuai, atau
output plugin pihak ketiga. `build.followSymlinks: true` kekal terhad kepada
copy path yang disokong. Lihat
[Keselamatan Dan Kebolehpercayaan Core](guide/user/20-core-safety-reliability.md)
untuk tingkah laku dan pengecualian penuh.

### Sempadan Core dan Labs

Bukit Core tidak mendedahkan API hook dalam proses sebagai sempadan sambungan stabil. Semakan keselamatan untuk tingkah laku sambungan harus bermula daripada laluan plugin proses luaran: `Bukit.PluginHost`, `Bukit.Plugin.Abstractions`, konfigurasi plugin projek, dan pakej plugin `plugin.yaml`.

Ciri Labs, termasuk aliran kerja webhook, berada di luar daftar arahan Core yang stabil. Anggap perkhidmatan Labs sebagai permukaan penerapan berasingan dan jangan gambarkannya sebagai jaminan runtime Core. Lihat [guide/labs/webhook.md](guide/labs/webhook.md) untuk sempadan webhook Labs semasa.

### Plugin Luaran

Plugin luaran berjalan sebagai proses berasingan di bawah protokol `bukit-plugin-v1`. Hanya gunakan plugin daripada sumber yang dipercayai, dan sahkan manifest pakej sebelum mengaktifkannya.

Semakan keselamatan plugin harus mengesahkan:

- `plugin.yaml` mengisytiharkan id, protocol, platforms, entries, dan permissions yang dijangka.
- Entry runtime dipilih melalui `Bukit.PluginHost` dan disemak hash sebelum invocation.
- Permissions untuk filesystem, environment, timeout, dan output adalah eksplisit serta minimum.
- Pelaksanaan CI adalah disengajakan dan tidak memintas manifest plugin atau semakan permissions.
- Laporan menutup secrets dan mengelakkan penulisan nilai token mentah.

Lihat [guide/dev/plugins.md](guide/dev/plugins.md) untuk sempadan plugin host semasa.

### Secrets dan Tokens

Jangan commit token, API key, webhook shared secret, atau credentials penerapan ke kawalan versi. Fail konfigurasi boleh menamakan sumber secret yang diperlukan tanpa menyimpan nilai secret.

Gunakan penyedia secrets luaran untuk automasi dan penerapan, seperti GitHub Actions secrets, pengurus secret platform penerapan, atau pengurus environment tempatan untuk pembangunan. Bukit membaca provider secrets daripada runtime environment; plugin hanya menerima permissions environment yang diberikan secara eksplisit.

Lihat [guide/dev/config-site-yaml.md](guide/dev/config-site-yaml.md) untuk peraturan kontrak konfigurasi dan [guide/dev/publish-deploy.md](guide/dev/publish-deploy.md) untuk sempadan publish/deploy.
