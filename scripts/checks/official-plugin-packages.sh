#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

official_packages=(
  "plugins/Bukit.Plugin.Import:import"
  "plugins/Bukit.Plugin.Clone:clone"
)

checked=0
for package in "${official_packages[@]}"; do
  package_dir="${package%%:*}"
  plugin_id="${package##*:}"
  if [ ! -d "$package_dir" ]; then
    continue
  fi

  checked=1
  config_path="$package_dir/examples/minimal/.bukit/plugins.yaml"
  manifest_path="$package_dir/examples/minimal/plugins/$plugin_id/plugin.yaml"
  if [ ! -s "$config_path" ]; then
    echo "Missing official plugin example config: $config_path" >&2
    exit 1
  fi

  if [ ! -s "$manifest_path" ]; then
    echo "Missing official plugin example manifest: $manifest_path" >&2
    exit 1
  fi

  if ! grep -Fq "version: 1" "$config_path"; then
    echo "Official plugin example config must declare version: 1: $config_path" >&2
    exit 1
  fi

  if ! grep -Fq "  $plugin_id:" "$config_path"; then
    echo "Official plugin example config must declare plugin id '$plugin_id': $config_path" >&2
    exit 1
  fi

  if ! grep -Fq "source: plugins/$plugin_id" "$config_path"; then
    echo "Official plugin example config must use source: plugins/$plugin_id: $config_path" >&2
    exit 1
  fi

  if ! grep -Fq "permissions:" "$config_path"; then
    echo "Official plugin example config must declare permissions: $config_path" >&2
    exit 1
  fi

  if grep -Fq "manifestPolicy: runtime-only" "$config_path"; then
    echo "Official plugin example config must not use manifestPolicy: runtime-only: $config_path" >&2
    exit 1
  fi

  for forbidden in "entry:" ".bukit/plugins" "site.externalPlugins"; do
    if grep -Fq "$forbidden" "$config_path"; then
      echo "Official plugin example config contains forbidden field '$forbidden': $config_path" >&2
      exit 1
    fi
  done

  if ! grep -Fq "protocol: bukit-plugin-v1" "$manifest_path"; then
    echo "Official plugin manifest must declare protocol: bukit-plugin-v1: $manifest_path" >&2
    exit 1
  fi

  if ! grep -Fq "kind: process" "$manifest_path"; then
    echo "Official plugin manifest must declare kind: process: $manifest_path" >&2
    exit 1
  fi

  if ! grep -Fq "distribution: self-contained" "$manifest_path"; then
    echo "Official plugin manifest must declare distribution: self-contained: $manifest_path" >&2
    exit 1
  fi

  if ! grep -Fq "requiredPermissions:" "$manifest_path"; then
    echo "Official plugin manifest must declare requiredPermissions: $manifest_path" >&2
    exit 1
  fi

  if [ "$plugin_id" = "import" ]; then
    test -x scripts/build/import-plugin-package.sh
    test -x scripts/smoke/import-plugin-package.sh

    for rid in win-x64 linux-x64 osx-arm64; do
      if ! grep -Fq "  $rid:" "$manifest_path"; then
        echo "Import plugin manifest must declare platform RID '$rid': $manifest_path" >&2
        exit 1
      fi
    done

    if ! grep -Fq "entry: bin/win-x64/bukit-plugin-import.exe" "$manifest_path"; then
      echo "Import plugin manifest must declare the win-x64 executable entry." >&2
      exit 1
    fi

    for entry in "bin/linux-x64/bukit-plugin-import" "bin/osx-arm64/bukit-plugin-import"; do
      if ! grep -Fq "entry: $entry" "$manifest_path"; then
        echo "Import plugin manifest must declare executable entry '$entry'." >&2
        exit 1
      fi
    done

    if ! grep -Eq "sha256: [a-f0-9]{64}" "$manifest_path"; then
      echo "Import plugin manifest platform entries must contain sha256 placeholders or real hashes." >&2
      exit 1
    fi
  fi
done

if [ "$checked" -eq 0 ]; then
  echo "No official plugin package directories found." >&2
  exit 1
fi

echo "Official plugin package configs OK"
