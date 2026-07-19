#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

if [[ $# -ne 1 ]]; then
  echo "usage: bash scripts/build/yaml-static-context.sh <check|update>" >&2
  exit 2
fi

mode="$1"
case "$mode" in
  check|update) ;;
  *)
    echo "usage: bash scripts/build/yaml-static-context.sh <check|update>" >&2
    exit 2
    ;;
esac

project="src/Bukit-Core/Bukit.Theme/Bukit.Theme.csproj"
tracked_source="src/Bukit-Core/Bukit.Theme/ThemeManifestYamlStaticContext.Generated.cs"
normalizer="scripts/build/normalize-yaml-static-context.py"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-yaml-static-context.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

generated_root="$scratch/generated"
mkdir -p "$generated_root"

dotnet build "$project" \
  -c Release \
  --artifacts-path "$scratch/artifacts" \
  -p:BukitGenerateYamlStaticContext=true \
  -p:EmitCompilerGeneratedFiles=true \
  -p:CompilerGeneratedFilesOutputPath="$generated_root" \
  -p:ContinuousIntegrationBuild=true \
  -p:Deterministic=true \
  --nologo >&2

generated_list="$scratch/generated-files.txt"
find "$generated_root" -type f -name 'YamlDotNetAutoGraph.g.cs' -print >"$generated_list"
generated_count="$(wc -l <"$generated_list" | tr -d '[:space:]')"
if [[ "$generated_count" != "1" ]]; then
  echo "expected exactly one YamlDotNetAutoGraph.g.cs, found $generated_count" >&2
  sed 's/^/  /' "$generated_list" >&2
  exit 1
fi

generated_source="$(sed -n '1p' "$generated_list")"
normalized_source="$scratch/ThemeManifestYamlStaticContext.Generated.cs"
python3 "$normalizer" "$generated_source" "$normalized_source"

if [[ "$mode" == "update" ]]; then
  python3 "$normalizer" "$generated_source" "$tracked_source"
  echo "updated $tracked_source"
else
  if [[ ! -f "$tracked_source" ]]; then
    echo "checked-in YAML static context is missing: $tracked_source" >&2
    echo "run: bash scripts/build/yaml-static-context.sh update" >&2
    exit 1
  fi

  if ! cmp -s "$normalized_source" "$tracked_source"; then
    echo "checked-in YAML static context is stale: $tracked_source" >&2
    diff -u "$tracked_source" "$normalized_source" >&2 || true
    echo "run: bash scripts/build/yaml-static-context.sh update" >&2
    exit 1
  fi
fi

default_generated_root="$scratch/default-generated"
mkdir -p "$default_generated_root"
dotnet build "$project" \
  -c Release \
  --artifacts-path "$scratch/default-artifacts" \
  -p:BukitGenerateYamlStaticContext=false \
  -p:EmitCompilerGeneratedFiles=true \
  -p:CompilerGeneratedFilesOutputPath="$default_generated_root" \
  -p:ContinuousIntegrationBuild=true \
  -p:Deterministic=true \
  --nologo >&2

if find "$default_generated_root" -type f -name 'YamlDotNetAutoGraph.g.cs' -print -quit | grep -q .; then
  echo "default build unexpectedly ran Vecc.YamlDotNet.Analyzers.StaticGenerator" >&2
  exit 1
fi

echo "YAML static context is deterministic and current"
