#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
start_script="$repo_root/.trae/skills/brainstorming/scripts/start-server.sh"
stop_script="$repo_root/.trae/skills/brainstorming/scripts/stop-server.sh"
state_lib="$repo_root/.trae/skills/brainstorming/scripts/session-state.sh"
server_script="$repo_root/.trae/skills/brainstorming/scripts/server.cjs"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-brainstorm-self-test.XXXXXX")"
output="$scratch/output"
source "$repo_root/scripts/checks/brainstorm-server-self-test-lib.sh"
source "$state_lib"
trap cleanup EXIT
setup_test_runtime

assert_exit_2_before_deadline "missing --host value" bash "$start_script" --host
assert_exit_2_before_deadline "foreground/background conflict" bash "$start_script" --foreground --background

argument_bin="$scratch/argument-bin"; mkdir -p "$argument_bin"
printf '%s\n' '#!/usr/bin/env bash' ': > "${ARG_SPAWN_MARKER:?}"' 'if [[ ${1-} == --version ]]; then echo v1.2.3; exit 0; fi' 'exit 23' > "$argument_bin/node"; chmod +x "$argument_bin/node"
argument_failures=0
assert_newline_value_rejected() {
  local option=$1 kind=$2 newline=$3 root value marker status spawned=false session=false args
  root="$scratch/argument-${option#--}-$kind"; value="bad${newline}value"; marker="$root/spawned"
  mkdir -p "$root"; cleanup_dirs+=("$root")
  args=(--project-dir "$root" "$option" "$value" --background)
  if [[ "$option" == --project-dir ]]; then value="$root/bad${newline}value"; mkdir -p "$value"; args=(--project-dir "$value" --background); fi
  if run_with_deadline env PATH="$argument_bin:$PATH" ARG_SPAWN_MARKER="$marker" bash "$start_script" "${args[@]}"; then status=0; else status=$?; fi
  [[ ! -e "$marker" ]] || spawned=true
  if find "$root" -type d -name .superpowers -print | grep -q .; then session=true; fi
  if [[ "$status" -ne 2 || "$spawned" == true || "$session" == true ]]; then
    echo "brainstorm server self-test: $option $kind RED status=$status spawned=$spawned session=$session" >&2; argument_failures=$((argument_failures + 1))
  else echo "brainstorm server self-test: $option $kind rejected without side effects"; fi
}
for option in --project-dir --host --url-host; do
  assert_newline_value_rejected "$option" CR $'\r'
  assert_newline_value_rejected "$option" LF $'\n'
done
[[ "$argument_failures" -eq 0 ]] || fail "$argument_failures CR/LF argument regressions failed"

assert_publish_failure_safe() {
  local label=$1 mode=$2 value=$3 project pid_file token_file count_file status pid token record
  project="$scratch/publish-$label"; pid_file="$scratch/publish-$label.pid"; token_file="$scratch/publish-$label.token"; count_file="$scratch/publish-$label.count"
  mkdir -p "$project"; cleanup_dirs+=("$project")
  if [[ "$mode" == mv ]]; then
    if env PATH="$inject_bin:$fake_bin:$PATH" NODE_OPTIONS="$node_options" BRAINSTORM_FAKE_PID_FILE="$pid_file" BRAINSTORM_FAKE_TOKEN_FILE="$token_file" BRAINSTORM_FAKE_PROBE_DELAY=2 INJECT_COUNT_FILE="$count_file" INJECT_MV_FAIL_AT="$value" bash "$start_script" --project-dir "$project" --background >"$output" 2>&1; then status=0; else status=$?; fi
  else
    if env PATH="$inject_bin:$fake_bin:$PATH" NODE_OPTIONS="$node_options" BRAINSTORM_FAKE_PID_FILE="$pid_file" BRAINSTORM_FAKE_TOKEN_FILE="$token_file" BRAINSTORM_FAKE_PROBE_DELAY=2 INJECT_CHMOD_FAIL=1 bash "$start_script" --project-dir "$project" --background >"$output" 2>&1; then status=0; else status=$?; fi
  fi
  [[ "$status" -ne 0 ]] || fail "$label injected state publication failure was accepted"
  wait_for_file "$pid_file" && wait_for_file "$token_file" || fail "$label did not expose server identity"
  pid="$(cat "$pid_file")"; token="$(cat "$token_file")"; record="$pid:$token"; active_servers+=("$record")
  if kill -0 "$pid" 2>/dev/null; then stop_test_server "$pid" "$token" || true; fail "$label left a live spawned server"; fi
  remove_server_record "$record"; assert_no_identity_or_temp "$project"
  echo "brainstorm server self-test: $label publication failure reaped server and rolled back state"
}

for stage in 1 2 3 4; do assert_publish_failure_safe "mv-$stage" mv "$stage"; done
assert_publish_failure_safe chmod chmod 1

space_root="$scratch/node path"; space_bin="$space_root/bin"; space_project="$scratch/space-project"; mkdir -p "$space_bin" "$space_project"
/bin/cp "$real_node" "$space_bin/node"; ln -s "$(cd "$(dirname "$real_node")/../lib" && pwd -P)" "$space_root/lib"
"$space_bin/node" --version >/dev/null || fail "space-path Node copy was unusable"
if env PATH="$space_bin:$PATH" bash "$start_script" --project-dir "$space_project" --foreground >"$output" 2>&1; then status=0; else status=$?; fi
[[ "$status" -ne 0 && ! -e "$space_project/.superpowers" ]] || fail "whitespace Node path was not rejected before session creation"
echo "brainstorm server self-test: whitespace Node path rejected before session creation"

execfail_bin="$scratch/execfail-bin"; execfail_project="$scratch/execfail-project"; execfail_count="$scratch/execfail.count"
mkdir -p "$execfail_bin" "$execfail_project"; cleanup_dirs+=("$execfail_project")
printf '%s\n' '#!/usr/bin/env bash' 'if [[ ${1-} == --version ]]; then echo v1.2.3; rm -- "$0"; exit 0; fi' 'exit 88' > "$execfail_bin/node"; chmod +x "$execfail_bin/node"
if env PATH="$execfail_bin:$inject_bin:$PATH" INJECT_COUNT_FILE="$execfail_count" bash "$start_script" --project-dir "$execfail_project" --foreground >"$output" 2>&1; then status=0; else status=$?; fi
[[ "$status" -ne 0 ]] || fail "foreground exec failure was accepted"
[[ "$(cat "$execfail_count")" == 4 ]] || fail "foreground exec failure did not occur after state publication"
assert_no_identity_or_temp "$execfail_project"
echo "brainstorm server self-test: foreground exec failure cleaned state"

signal_project="$scratch/signal-window"; signal_count="$scratch/signal-window.count"; mkdir -p "$signal_project"; cleanup_dirs+=("$signal_project")
if env PATH="$inject_bin:$fake_bin:$PATH" NODE_OPTIONS="$node_options" INJECT_COUNT_FILE="$signal_count" INJECT_MV_SIGNAL_AFTER=4 bash "$start_script" --project-dir "$signal_project" --foreground >"$output" 2>&1; then status=0; else status=$?; fi
[[ "$status" -ne 0 ]] || fail "post-publication signal window was accepted"
assert_no_identity_or_temp "$signal_project"
echo "brainstorm server self-test: post-publication foreground signal cleaned state"

fail_bin="$scratch/fail-bin"; fail_project="$scratch/fail-project"
mkdir -p "$fail_bin" "$fail_project"; cleanup_dirs+=("$fail_project")
printf '%s\n' '#!/usr/bin/env bash' 'exit 23' > "$fail_bin/node"; chmod +x "$fail_bin/node"
if run_with_deadline env PATH="$fail_bin:$PATH" bash "$start_script" --project-dir "$fail_project" --background; then status=0; else status=$?; fi
[[ "$status" -ne 0 && "$status" -ne 124 ]] || fail "node startup failure did not fail promptly"
assert_no_identity_or_temp "$fail_project"
echo "brainstorm server self-test: node startup failure cleaned identity"

json_project="$scratch/json-project"; json_pid_file="$scratch/json.pid"; json_token_file="$scratch/json.token"
mkdir -p "$json_project"; cleanup_dirs+=("$json_project")
if env PATH="$fake_bin:$PATH" NODE_OPTIONS="$node_options" BRAINSTORM_TERM_MODE=ignore BRAINSTORM_JSON_STATE_DIR=/wrong/state BRAINSTORM_FAKE_PID_FILE="$json_pid_file" BRAINSTORM_FAKE_TOKEN_FILE="$json_token_file" bash "$start_script" --project-dir "$json_project" --background >"$output" 2>&1; then status=0; else status=$?; fi
[[ "$status" -ne 0 ]] || fail "JSON state_dir mismatch was accepted"
pid="$(cat "$json_pid_file")"; token="$(cat "$json_token_file")"; kill -0 "$pid" 2>/dev/null && { active_servers+=("$pid:$token"); stop_test_server "$pid" "$token" || true; fail "JSON mismatch left an orphan"; }
assert_no_identity_or_temp "$json_project"
echo "brainstorm server self-test: JSON mismatch stopped server and cleaned identity"

if ! env PATH="$fake_bin:$PATH" NODE_OPTIONS="$node_options" BRAINSTORM_TERM_MODE=ps-fail bash "$start_script" --background >"$output" 2>&1; then fail "ephemeral start failed"; fi
ephemeral_state="$(sed -n 's/.*"state_dir":"\([^"]*\)".*/\1/p' "$output" | head -1)"
ephemeral_dir="${ephemeral_state%/state}"; cleanup_dirs+=("$ephemeral_dir"); ephemeral_pid="$(cat "$ephemeral_state/server.pid")"
assert_live_tamper_refused() {
  local file=$1 bad=$2 label=$3 saved
  saved="$(cat "$ephemeral_state/$file")"
  printf '%s\n' "$bad" > "$ephemeral_state/$file"
  bash "$stop_script" "$ephemeral_dir" >"$output" 2>&1 && fail "$label state was accepted"
  kill -0 "$ephemeral_pid" 2>/dev/null || fail "$label killed the server"
  printf '%s\n' "$saved" > "$ephemeral_state/$file"
  echo "brainstorm server self-test: $label independently refused"
}
assert_live_tamper_refused server.token wrong-token "wrong token"
assert_live_tamper_refused owner.uid 999999 "wrong UID"
assert_live_tamper_refused server.path /tmp/not-server.cjs "wrong server path"
ps_marker="$ephemeral_state/ps-fail"
if env PATH="$fake_bin:$PATH" BRAINSTORM_TEST_PS_MARKER="$ps_marker" bash "$stop_script" "$ephemeral_dir" >"$output" 2>&1; then fail "post-TERM ps failure was accepted"; fi
kill -0 "$ephemeral_pid" 2>/dev/null || fail "post-TERM ps failure killed server"; rm -f "$ps_marker"
bash "$stop_script" "$ephemeral_dir" >"$output" 2>&1 || fail "ephemeral stop failed"
[[ ! -e "$ephemeral_dir" ]] || fail "ephemeral directory survived stop"
echo "brainstorm server self-test: post-TERM failure refused; ephemeral session deleted after valid stop"

persistent_project="$scratch/persistent-project"; mkdir -p "$persistent_project"
env PATH="$fake_bin:$PATH" NODE_OPTIONS="$node_options" BRAINSTORM_TERM_MODE=ignore bash "$start_script" --project-dir "$persistent_project" --foreground >"$output" 2>&1 &
foreground_pid=$!; active_children+=("$foreground_pid"); persistent_parent="$persistent_project/.superpowers/brainstorm"
for _ in {1..60}; do persistent_pid_file="$(find "$persistent_parent" -path '*/state/server.pid' -type f -print 2>/dev/null | head -1 || true)"; [[ -n "$persistent_pid_file" ]] && break; sleep 0.05; done
[[ -n "${persistent_pid_file:-}" ]] || fail "foreground state missing"
for _ in {1..60}; do grep -q '"type":"server-started"' "$output" 2>/dev/null && break; sleep 0.05; done; grep -q '"type":"server-started"' "$output" || fail "foreground server did not become ready"
persistent_state="${persistent_pid_file%/server.pid}"; persistent_dir="${persistent_state%/state}"; cleanup_dirs+=("$persistent_dir")
ps_count="$scratch/pre-kill.count"
if env PATH="$fake_bin:$PATH" BRAINSTORM_PS_COUNT_FILE="$ps_count" BRAINSTORM_PS_FAIL_AT=64 bash "$stop_script" "$persistent_dir" >"$output" 2>&1; then fail "pre-SIGKILL identity failure was accepted"; fi
kill -0 "$foreground_pid" 2>/dev/null || fail "pre-SIGKILL failure killed foreground server"
bash "$stop_script" "$persistent_dir" >"$output" 2>&1 || fail "persistent SIGKILL stop failed"
wait "$foreground_pid" 2>/dev/null || true; remove_child_pid "$foreground_pid"
[[ -d "$persistent_dir" ]] || fail "persistent directory was deleted"
echo "brainstorm server self-test: pre-SIGKILL revalidation refused; valid SIGKILL retained persistent session"

assert_command_shape_rejected "extra prefix" extra-prefix
assert_command_shape_rejected "extra suffix" extra-suffix
assert_command_shape_rejected "single argument carrier" carrier
assert_command_shape_rejected "token boundary" token-boundary
assert_command_shape_rejected "server boundary" server-boundary
assert_command_shape_rejected "newline" newline

state_probe="$scratch/state-probe"; mkdir -p "$state_probe"
for field in server.pid owner.uid server.path server.token; do
  printf 'first\nsecond\n' > "$state_probe/$field"
  if read_session_line "$state_probe/$field" >/dev/null 2>&1; then fail "$field multiline state was accepted"; fi
  rm -f "$state_probe/$field"; printf 'safe\n' > "$state_probe/target"; ln -s target "$state_probe/$field"
  if read_session_line "$state_probe/$field" >/dev/null 2>&1; then fail "$field symlink state was accepted"; fi
  rm -f "$state_probe/$field" "$state_probe/target"
done
printf 'bad\rvalue\n' > "$state_probe/crlf"
if read_session_line "$state_probe/crlf" >/dev/null 2>&1; then fail "CR state was accepted"; fi
echo "brainstorm server self-test: multiline, symlink, and CR state refused for all identity fields"

alias_root="$scratch/alias-project/.superpowers/brainstorm"; alias_id="$$-$(date +%s)-$RANDOM"
mkdir -p "$alias_root/$alias_id"; ln -s "$alias_root/$alias_id" "$scratch/session-alias"
[[ "$(classify_session_dir "$scratch/session-alias")" == persistent ]] || fail "physical alias was not classified"
[[ "$(classify_session_dir "$alias_root/./$alias_id")" == persistent ]] || fail "dot physical path was not classified"
[[ "$(classify_session_dir "$alias_root/../brainstorm/$alias_id")" == persistent ]] || fail "dot-dot physical path was not classified"
echo "brainstorm server self-test: alias, dot, and dot-dot paths classified by physical target"

current_uid="$(id -u)"
assert_untrusted_sleep() {
  local label=$1 dir=$2 kind=$3 pid; sleep 60 & pid=$!; active_children+=("$pid"); cleanup_dirs+=("$dir")
  case "$kind" in
    safe) write_raw_state "$dir" "$pid" "$current_uid" "$server_script" safe-token ;;
    token) write_raw_state "$dir" "$pid" "$current_uid" "$server_script" wrong-token ;;
    uid) write_raw_state "$dir" "$pid" 999999 "$server_script" safe-token ;;
    missing) mkdir -p "$dir/state"; printf '%s\n' "$pid" > "$dir/state/server.pid" ;;
    malformed) write_raw_state "$dir" "\$(: > $scratch/state-executed)" "$current_uid" "$server_script" 'bad token!' ;;
  esac
  bash "$stop_script" "$dir" >"$output" 2>&1 || true
  kill -0 "$pid" 2>/dev/null || fail "$label killed protected sleep"
  stop_direct_child "$pid" || fail "$label sleep cleanup failed"
  [[ -d "$dir" ]] || fail "$label deleted directory"; echo "brainstorm server self-test: $label preserved protected sleep"
}
assert_untrusted_sleep "arbitrary tmp path" "/tmp/not-brainstorm-$$-$(date +%s)-$RANDOM" safe
assert_untrusted_sleep "wrong token" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" token
assert_untrusted_sleep "wrong UID" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" uid
assert_untrusted_sleep "wrong command" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" safe
assert_untrusted_sleep "missing state" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" missing
assert_untrusted_sleep "malformed state" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" malformed
[[ ! -e "$scratch/state-executed" ]] || fail "malformed state was executed"

cleanup_probe="/tmp/brainstorm-$$-$(date +%s)-$RANDOM"; cleanup_token=cleanup-token
mkdir -p "$cleanup_probe/state"; cleanup_dirs+=("$cleanup_probe")
env BRAINSTORM_DIR="$cleanup_probe" "$real_node" "$carrier_script" "$server_script --session-token=$cleanup_token" >"$output" 2>&1 & cleanup_pid=$!
sleep 0.15; write_raw_state "$cleanup_probe" "$cleanup_pid" "$(id -u)" "$server_script" "$cleanup_token"
active_servers+=("$cleanup_pid:$cleanup_token"); cleanup
kill -0 "$cleanup_pid" 2>/dev/null || fail "strict cleanup refusal killed carrier Node"
[[ -d "$cleanup_probe" ]] || fail "strict cleanup refusal deleted live directory"
remove_server_record "$cleanup_pid:$cleanup_token"; stop_direct_child "$cleanup_pid" || fail "cleanup carrier could not be safely reaped"
echo "brainstorm server self-test: strict cleanup refusal preserved live carrier and directory"
cleanup
[[ ! -e "$scratch" ]] || fail "successful run retained scratch directory"
trap - EXIT
echo "brainstorm server self-test: PASS"
