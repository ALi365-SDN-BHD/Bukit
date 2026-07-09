# Dasar Keselamatan

## Versi Disokong

| Versi  | Disokong           |
|--------|--------------------|
| 1.0.x  | :white_check_mark: |

## Melaporkan Kerentanan

Jika anda menemui kerentanan keselamatan dalam Bukit, sila laporkan secara peribadi.

**Jangan buka isu awam.** Sebaliknya, hantar butiran kepada penyelenggara.

Kami akan mengakui laporan anda dalam masa 7 hari dan bertujuan untuk menyediakan pembaikan dalam masa 30 hari.

## Pertimbangan Keselamatan

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
