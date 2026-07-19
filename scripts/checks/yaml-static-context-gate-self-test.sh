#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() {
  echo "YAML static context gate self-test failed: $*" >&2
  exit 1
}

expected_gate='run_step "YAML static context drift" bash scripts/build/yaml-static-context.sh check'
grep -Fqx "$expected_gate" scripts/gates/ci-fast.sh ||
  fail "ci-fast does not run the drift check"

theme_project="src/Bukit-Core/Bukit.Theme/Bukit.Theme.csproj"
cli_project="src/Bukit-Core/Bukit.Cli/Bukit.Cli.csproj"
generated_source="src/Bukit-Core/Bukit.Theme/ThemeManifestYamlStaticContext.Generated.cs"

[[ "$(grep -Fc '<PackageReference Include="Vecc.YamlDotNet.Analyzers.StaticGenerator" PrivateAssets="all" />' "$theme_project")" == "1" ]] ||
  fail "Theme generator reference is missing, duplicated, or not private"
grep -Fq '<ItemGroup Condition="'"'"'$(BukitGenerateYamlStaticContext)'"'"' == '"'"'true'"'"'">' "$theme_project" ||
  fail "Theme generator reference is not conditional"
if grep -Fq 'Vecc.YamlDotNet.Analyzers.StaticGenerator' "$cli_project"; then
  fail "CLI retains the unused YAML static generator reference"
fi

grep -Fq '// Generator package: Vecc.YamlDotNet.Analyzers.StaticGenerator ' "$generated_source" ||
  fail "checked-in generated source lacks package provenance"
grep -Fq 'public partial class ThemeManifestYamlStaticContext' "$generated_source" ||
  fail "checked-in generated source lacks the public static context"
grep -Fq 'public class StaticTypeInspector' "$generated_source" ||
  fail "checked-in generated source lacks the existing public inspector"

echo "YAML static context gate self-test OK"
