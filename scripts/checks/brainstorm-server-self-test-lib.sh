#!/usr/bin/env bash

fail() { echo "brainstorm server self-test failed: $*" >&2; exit 1; }

active_children=()
active_servers=()
cleanup_dirs=()

remove_child_pid() {
  local needle=$1 index
  for index in "${!active_children[@]}"; do
    [[ "${active_children[$index]}" != "$needle" ]] || unset "active_children[$index]"
  done
}

remove_server_record() {
  local needle=$1 index
  for index in "${!active_servers[@]}"; do
    [[ "${active_servers[$index]}" != "$needle" ]] || unset "active_servers[$index]"
  done
}

direct_child_is_current() {
  local pid=$1 parent jobs_output
  parent="$(ps -o ppid= -p "$pid" 2>/dev/null | tr -d ' ' || true)"
  jobs_output="$(jobs -pr 2>/dev/null || true)"
  [[ "$parent" == "$$" ]] || return 1
  case $'\n'"$jobs_output"$'\n' in *$'\n'"$pid"$'\n'*) return 0 ;; esac
  return 1
}

test_node_is_current() {
  [[ "$(validate_process_identity "$1" "$(id -u)" "$server_script" "$2" 2>/dev/null || true)" == "$1" ]]
}

stop_direct_child() {
  local pid=$1 i
  direct_child_is_current "$pid" || return 1
  kill "$pid" 2>/dev/null || true
  for i in {1..20}; do kill -0 "$pid" 2>/dev/null || break; sleep 0.05; done
  if kill -0 "$pid" 2>/dev/null; then
    direct_child_is_current "$pid" || return 1
    kill -9 "$pid" 2>/dev/null || true
  fi
  wait "$pid" 2>/dev/null || true
  remove_child_pid "$pid"
}

stop_test_server() {
  local pid=$1 token=$2 record="$1:$2" i
  test_node_is_current "$pid" "$token" || return 1
  kill "$pid" 2>/dev/null || true
  for i in {1..20}; do kill -0 "$pid" 2>/dev/null || break; sleep 0.05; done
  if kill -0 "$pid" 2>/dev/null; then
    test_node_is_current "$pid" "$token" || return 1
    kill -9 "$pid" 2>/dev/null || true
  fi
  remove_server_record "$record"
}

cleanup() {
  local pid path record token state_pid safe=true
  for pid in "${active_children[@]-}"; do [[ -n "$pid" ]] && stop_direct_child "$pid" || safe=false; done
  for record in "${active_servers[@]-}"; do
    [[ -n "$record" ]] || continue; pid=${record%%:*}; token=${record#*:}
    stop_test_server "$pid" "$token" || safe=false
  done
  [[ "$safe" == true ]] || return 0
  for path in "${cleanup_dirs[@]-}"; do
    [[ -n "$path" ]] || continue
    if ! bash "$stop_script" "$path" >/dev/null 2>&1; then
      state_pid="$(read_session_line "$path/state/server.pid" 2>/dev/null || true)"
      if [[ "$state_pid" =~ ^[0-9]+$ ]] && kill -0 "$state_pid" 2>/dev/null; then safe=false; break; fi
    fi
    rm -rf "$path"
  done
  [[ "$safe" == true ]] && rm -rf "$scratch"
}

run_with_deadline() {
  "$@" >"$output" 2>&1 & local pid=$! i status
  active_children+=("$pid")
  for i in {1..20}; do
    if ! kill -0 "$pid" 2>/dev/null; then
      if wait "$pid"; then status=0; else status=$?; fi
      remove_child_pid "$pid"; return "$status"
    fi
    sleep 0.05
  done
  stop_direct_child "$pid" || true
  return 124
}

assert_exit_2_before_deadline() {
  local label=$1 status; shift
  if run_with_deadline "$@"; then status=0; else status=$?; fi
  [[ "$status" -eq 2 ]] || fail "$label: expected exit 2, got $status; output=$(tr '\n' ' ' < "$output")"
  echo "brainstorm server self-test: $label rejected with exit 2 before deadline"
}

wait_for_file() {
  local path=$1 i
  for i in {1..60}; do [[ -s "$path" ]] && return 0; sleep 0.05; done
  return 1
}

write_raw_state() {
  local session_dir=$1 pid=$2 uid=$3 path=$4 token=$5
  mkdir -p "$session_dir/state"
  printf '%s\n' "$pid" > "$session_dir/state/server.pid"
  printf '%s\n' "$uid" > "$session_dir/state/owner.uid"
  printf '%s\n' "$path" > "$session_dir/state/server.path"
  printf '%s\n' "$token" > "$session_dir/state/server.token"
}

assert_no_identity_or_temp() {
  local project=$1 found
  found="$(find "$project" -type f \( -name 'server.pid' -o -name 'owner.uid' -o -name 'server.path' -o -name 'server.token' -o -name '.session-state.*' \) -print)"
  [[ -z "$found" ]] || fail "$project retained final or temporary identity state: $found"
}

assert_command_shape_rejected() {
  local label=$1 mode=$2 token="shape-token" session pid
  session="/tmp/brainstorm-$$-$(date +%s)-$RANDOM"
  cleanup_dirs+=("$session"); mkdir -p "$session/state" "$session/content"
  case "$mode" in
    extra-prefix) env BRAINSTORM_DIR="$session" NODE_OPTIONS="$node_options" "$real_node" --no-warnings "$server_script" "--session-token=$token" >"$output" 2>&1 & ;;
    extra-suffix) env BRAINSTORM_DIR="$session" NODE_OPTIONS="$node_options" "$real_node" "$server_script" "--session-token=$token" extra >"$output" 2>&1 & ;;
    carrier) env BRAINSTORM_DIR="$session" "$real_node" "$carrier_script" "$server_script --session-token=$token" >"$output" 2>&1 & ;;
    token-boundary) env BRAINSTORM_DIR="$session" NODE_OPTIONS="$node_options" "$real_node" "$server_script" "--session-token=${token}suffix" >"$output" 2>&1 & ;;
    server-boundary) env BRAINSTORM_DIR="$session" "$real_node" "$carrier_script" "${server_script}suffix --session-token=$token" >"$output" 2>&1 & ;;
    newline) env BRAINSTORM_DIR="$session" "$real_node" "$carrier_script" "$server_script --session-token=$token"$'\n''suffix' >"$output" 2>&1 & ;;
  esac
  pid=$!; active_children+=("$pid"); sleep 0.15
  write_raw_state "$session" "$pid" "$(id -u)" "$server_script" "$token"
  if validate_session_process "$session/state" >/dev/null 2>&1; then fail "$label command shape was accepted"; fi
  kill -0 "$pid" 2>/dev/null || fail "$label probe exited before validation"
  stop_direct_child "$pid" || fail "$label probe could not be safely reaped"
  echo "brainstorm server self-test: $label command shape refused"
}

setup_test_runtime() {
  real_node="$(realpath "$(command -v node)")"
  fake_bin="$scratch/fake-bin"
  inject_bin="$scratch/inject-bin"
  preload="$scratch/brainstorm-preload.cjs"
  carrier_script="$scratch/carrier.cjs"
  mkdir -p "$fake_bin" "$inject_bin"
  ln -s "$real_node" "$fake_bin/node"
  cat > "$preload" <<'NODE_PRELOAD'
const fs = require('fs');
const mode = process.env.BRAINSTORM_TERM_MODE || 'exit'; const probeDelayMs = Number(process.env.BRAINSTORM_FAKE_PROBE_DELAY || '0') * 1000; if (probeDelayMs > 0) Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, probeDelayMs);
if (process.env.BRAINSTORM_FAKE_PID_FILE) fs.writeFileSync(process.env.BRAINSTORM_FAKE_PID_FILE, `${process.pid}\n`);
if (process.env.BRAINSTORM_FAKE_TOKEN_FILE) fs.writeFileSync(process.env.BRAINSTORM_FAKE_TOKEN_FILE, `${process.argv.find(v => v.startsWith('--session-token='))?.slice(16) || ''}\n`);
if (mode === 'ignore') process.on('SIGTERM', () => {});
if (mode === 'ps-fail') process.on('SIGTERM', () => {
  const state = `${process.env.BRAINSTORM_DIR}/state`;
  if (fs.existsSync(`${state}/term-seen`)) process.exit(0);
  fs.writeFileSync(`${state}/term-seen`, '1\n'); fs.writeFileSync(`${state}/ps-fail`, '1\n');
});
if (process.env.BRAINSTORM_JSON_STATE_DIR) {
  const original = console.log;
  console.log = (value, ...rest) => {
    try { const data = JSON.parse(value); if (data.type === 'server-started') data.state_dir = process.env.BRAINSTORM_JSON_STATE_DIR; value = JSON.stringify(data); } catch {}
    original(value, ...rest);
  };
}
NODE_PRELOAD
  printf '%s\n' 'setInterval(() => {}, 1000);' > "$carrier_script"
  cat > "$fake_bin/ps" <<'FAKE_PS'
#!/usr/bin/env bash
if [[ -n "${BRAINSTORM_PS_COUNT_FILE:-}" ]]; then
  count=0; [[ ! -f "$BRAINSTORM_PS_COUNT_FILE" ]] || read -r count < "$BRAINSTORM_PS_COUNT_FILE"
  count=$((count + 1)); printf '%s\n' "$count" > "$BRAINSTORM_PS_COUNT_FILE"
  [[ -z "${BRAINSTORM_PS_FAIL_AT:-}" || "$count" -lt "$BRAINSTORM_PS_FAIL_AT" ]] || exit 1
fi
[[ -z "${BRAINSTORM_TEST_PS_MARKER:-}" || ! -e "$BRAINSTORM_TEST_PS_MARKER" ]] || exit 1
exec /bin/ps "$@"
FAKE_PS
  cat > "$inject_bin/mv" <<'FAKE_MV'
#!/usr/bin/env bash
wait_for_identity_probes() { for attempt in {1..100}; do [[ -s "${BRAINSTORM_FAKE_PID_FILE:-}" && -s "${BRAINSTORM_FAKE_TOKEN_FILE:-}" ]] && return 0; sleep 0.05; done; echo "brainstorm server self-test: injected failure identity probes did not become ready" >&2; return 1; }
count=0; [[ ! -f "$INJECT_COUNT_FILE" ]] || read -r count < "$INJECT_COUNT_FILE"
count=$((count + 1)); printf '%s\n' "$count" > "$INJECT_COUNT_FILE"
if [[ -n "${INJECT_MV_FAIL_AT:-}" && "$count" -eq "$INJECT_MV_FAIL_AT" ]]; then wait_for_identity_probes || exit 72; exit 73; fi
/bin/mv "$@"; status=$?
[[ -z "${INJECT_MV_SIGNAL_AFTER:-}" || "$count" -ne "$INJECT_MV_SIGNAL_AFTER" ]] || kill -TERM "$PPID"
exit "$status"
FAKE_MV
  cat > "$inject_bin/chmod" <<'FAKE_CHMOD'
#!/usr/bin/env bash
wait_for_identity_probes() { for attempt in {1..100}; do [[ -s "${BRAINSTORM_FAKE_PID_FILE:-}" && -s "${BRAINSTORM_FAKE_TOKEN_FILE:-}" ]] && return 0; sleep 0.05; done; echo "brainstorm server self-test: injected failure identity probes did not become ready" >&2; return 1; }
if [[ -n "${INJECT_CHMOD_FAIL:-}" ]]; then wait_for_identity_probes || exit 72; exit 74; fi
exec /bin/chmod "$@"
FAKE_CHMOD
  chmod +x "$fake_bin/ps" "$inject_bin/mv" "$inject_bin/chmod"
  node_options="--require=$preload"
}
