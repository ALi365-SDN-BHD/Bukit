#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

script="scripts/smoke/release-artifacts.sh"

require_literal() {
  local needle="$1"
  if ! grep -Fq -- "$needle" "$script"; then
    echo "ERROR: release artifact smoke is missing required check: $needle" >&2
    exit 1
  fi
}

require_regex() {
  local pattern="$1"
  if ! grep -Eq "$pattern" "$script"; then
    echo "ERROR: release artifact smoke is missing required regex: $pattern" >&2
    exit 1
  fi
}

require_literal 'Version command returns version text'
require_literal 'CLI help includes core commands'
require_literal 'CLI help excludes non-Core command family'
require_literal 'CLI dev help includes LiveReload wording'
require_literal 'CLI dev help excludes HMR wording'
require_literal 'CLI deploy help includes github-pages provider'
require_literal 'Generate site schema'
require_literal 'Config check fixture site'
require_literal 'Doctor check fixture site'
require_literal 'Build fixture site'
require_literal 'Deploy dry-run fixture site'
require_literal 'SEO audit'
require_literal 'Geo audit'
require_literal 'Publish audit'
require_literal 'Validate .bukit artifacts JSON'
require_literal 'DevFileWatcher_RebuildFailure_DoesNotDisposeWatcher'
require_literal 'DevFileWatcher_RapidChanges_DebouncedToSingleRebuild'
require_literal 'DevRequestHandler_LiveReloadScript_UsesSameOriginWebSocket'
require_literal 'non_core_command_family='
require_regex "non_core_command_family=.*clone"
require_regex "non_core_command_family=.*import"
require_regex "non_core_command_family=.*webhook"
require_regex "non_core_command_family=.*plugin"
require_regex "non_core_command_family=.*theme"
legacy_external_plugin_flag="--allow-external""-plugins"
require_literal "$legacy_external_plugin_flag"

if grep -Fq 'DevFileWatcher_RebuildException_DoesNotDisposeWatcher' "$script"; then
  echo "ERROR: release artifact smoke references removed dev watcher test name" >&2
  exit 1
fi

echo "Release artifact smoke contract OK"
