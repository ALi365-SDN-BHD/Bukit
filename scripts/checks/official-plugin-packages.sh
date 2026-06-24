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
  if [ ! -s "$config_path" ]; then
    echo "Missing official plugin example config: $config_path" >&2
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
done

if [ "$checked" -eq 0 ]; then
  echo "No official plugin package directories found." >&2
  exit 1
fi

echo "Official plugin package configs OK"
