# intent-cli (Fail Intent) Pelaksanaan dan Penggunaan

Intent menukar `intent.yaml` berstruktur kepada `site.yaml` boleh laku.

Pelaksanaan: `src/Bukit.Cli/Commands/IntentCommand.cs`

## Tiga Subperintah
- `init`: Jana fail intent secara interaktif — `bukit intent init --out intent.yaml`
- `validate`: Sahkan intent boleh digunakan — `bukit intent validate intent.yaml`
- `apply`: Tukar intent kepada site.yaml — `bukit intent apply intent.yaml --out site.yaml`

## Hubungan dengan build
- Akar `site.yaml`: `bukit build`
- `sites/<name>.yaml`: `bukit build --site <name>`
