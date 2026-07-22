#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() {
  echo "code analysis ratchet policy self-test failed: $*" >&2
  exit 1
}

[[ "$(grep -Fxc '    <AnalysisLevel>9.0</AnalysisLevel>' Directory.Build.props)" == "1" ]] ||
  fail "Directory.Build.props must pin AnalysisLevel to 9.0"

assert_editorconfig_severity() {
  local diagnostic="$1" severity="$2"
  local expected="dotnet_diagnostic.$diagnostic.severity = $severity"
  [[ "$(grep -Fxc "$expected" .editorconfig)" == "1" ]] ||
    fail "$diagnostic must be configured exactly once at $severity"
}

for diagnostic in IDE0055 IDE1006 CA1001 CA1063 CA1816 CA2012 CA2016 CA2213 CA2215 CA2216 CA2250; do
  assert_editorconfig_severity "$diagnostic" warning
done
for diagnostic in CA1068 CA1849 CA2000 CA1000 CA1050 CA1710 CA1711 CA1720 CA1502 CA1505 CA1506; do
  assert_editorconfig_severity "$diagnostic" suggestion
done

complexity_contract=(
  'dotnet_code_quality.CA1502.threshold = 25'
  'dotnet_code_quality.CA1505.threshold = 10'
  'dotnet_code_quality.CA1506.threshold = 40'
)
for contract_line in "${complexity_contract[@]}"; do
  [[ "$(grep -Fxc "$contract_line" .editorconfig)" == "1" ]] ||
    fail "missing or duplicated complexity contract: $contract_line"
done

naming_contract=(
  'dotnet_naming_rule.interfaces_must_have_i_prefix.severity = warning'
  'dotnet_naming_rule.interfaces_must_have_i_prefix.symbols = interfaces'
  'dotnet_naming_rule.interfaces_must_have_i_prefix.style = i_prefix_pascal_case'
  'dotnet_naming_symbols.interfaces.applicable_kinds = interface'
  'dotnet_naming_style.i_prefix_pascal_case.required_prefix = I'
  'dotnet_naming_style.i_prefix_pascal_case.capitalization = pascal_case'
  'dotnet_naming_rule.types_must_be_pascal_case.severity = warning'
  'dotnet_naming_rule.types_must_be_pascal_case.symbols = types'
  'dotnet_naming_rule.types_must_be_pascal_case.style = pascal_case'
  'dotnet_naming_symbols.types.applicable_kinds = class, struct, interface, enum, delegate'
  'dotnet_naming_rule.members_must_be_pascal_case.severity = warning'
  'dotnet_naming_rule.members_must_be_pascal_case.symbols = ordinary_members'
  'dotnet_naming_rule.members_must_be_pascal_case.style = pascal_case'
  'dotnet_naming_symbols.ordinary_members.applicable_kinds = property, method, event, field'
  'dotnet_naming_symbols.ordinary_members.applicable_accessibilities = public, internal, protected, protected_internal, private_protected'
  'dotnet_naming_style.pascal_case.capitalization = pascal_case'
)
for contract_line in "${naming_contract[@]}"; do
  [[ "$(grep -Fxc "$contract_line" .editorconfig)" == "1" ]] ||
    fail "missing or duplicated naming contract: $contract_line"
done

[[ "$(grep -Fxc '[src/Bukit-Core/Bukit.Rendering/Scriban/SectionRenderHelper.cs]' .editorconfig)" == "1" ]] ||
  fail "Scriban helper naming exception must use the exact file boundary"
[[ "$(grep -Fxc 'dotnet_diagnostic.IDE1006.severity = none' .editorconfig)" == "1" ]] ||
  fail "the intentional Scriban CLR name must have exactly one narrow IDE1006 exception"
grep -Fq 'Task/ValueTask-aware `Async` suffix' guide/dev/code-quality-governance.md ||
  fail "code quality guide must document the Async suffix enforcement boundary"
grep -Fq 'Complexity diagnostics are report-only' guide/dev/code-quality-governance.md ||
  fail "code quality guide must state that complexity diagnostics are report-only"

echo "code analysis ratchet policy self-test OK"
