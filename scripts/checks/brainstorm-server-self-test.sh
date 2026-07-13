#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
start_script="$repo_root/.trae/skills/brainstorming/scripts/start-server.sh"
stop_script="$repo_root/.trae/skills/brainstorming/scripts/stop-server.sh"
server_script="$repo_root/.trae/skills/brainstorming/scripts/server.cjs"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-brainstorm-self-test.XXXXXX")"
output="$scratch/output"
cleanup_pids=()
cleanup_dirs=()
cleanup() {
  local pid path
  for pid in "${cleanup_pids[@]-}"; do
    [[ -n "$pid" ]] || continue
    [[ "$(ps -o ppid= -p "$pid" 2>/dev/null | tr -d ' ' || true)" == "$$" ]] || continue
    kill "$pid" 2>/dev/null || true; wait "$pid" 2>/dev/null || true
  done
  for path in "${cleanup_dirs[@]-}"; do
    [[ -n "$path" ]] || continue
    bash "$stop_script" "$path" >/dev/null 2>&1 || true; rm -rf "$path"
  done
  rm -rf "$scratch"
}
trap cleanup EXIT
fail() { echo "brainstorm server self-test failed: $*" >&2; exit 1; }
run_with_deadline() {
  "$@" >"$output" 2>&1 &
  local pid=$! i status
  for i in {1..20}; do
    if ! kill -0 "$pid" 2>/dev/null; then
      if wait "$pid"; then status=0; else status=$?; fi
      return "$status"
    fi
    sleep 0.05
  done
  kill "$pid" 2>/dev/null || true
  sleep 0.05
  kill -9 "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  return 124
}
assert_exit_2_before_deadline() {
  local label=$1 status
  shift
  if run_with_deadline "$@"; then status=0; else status=$?; fi
  [[ "$status" -eq 2 ]] || fail "$label: expected exit 2 before deadline, got $status; output=$(tr '\n' ' ' < "$output")"
  echo "brainstorm server self-test: $label rejected with exit 2 before deadline"
}
assert_alive() { kill -0 "$1" 2>/dev/null || fail "$2: protected PID $1 was killed"; }
cat > "$scratch/fake-node" <<'FAKE_NODE'
#!/usr/bin/env bash
set -eu
: "${BRAINSTORM_DIR:?}"
case "${BRAINSTORM_TERM_MODE:-exit}" in
  ignore) trap '' TERM ;;
  ps-fail) trap 'if [[ -e "$BRAINSTORM_DIR/state/term-seen" ]]; then exit 0; fi; : > "$BRAINSTORM_DIR/state/term-seen"; : > "$BRAINSTORM_DIR/state/ps-fail"' TERM ;;
  *) trap 'exit 0' TERM ;;
esac
trap 'exit 0' INT
mkdir -p "$BRAINSTORM_DIR/content" "$BRAINSTORM_DIR/state"
[[ -z "${BRAINSTORM_FAKE_PID_FILE:-}" ]] || printf '%s\n' "$$" > "$BRAINSTORM_FAKE_PID_FILE"
json_state_dir="${BRAINSTORM_JSON_STATE_DIR:-$BRAINSTORM_DIR/state}"
printf '{"type":"server-started","port":54321,"host":"%s","url_host":"%s","url":"http://%s:54321","screen_dir":"%s/content","state_dir":"%s/state"}\n' \
  "${BRAINSTORM_HOST:-127.0.0.1}" "${BRAINSTORM_URL_HOST:-localhost}" \
  "${BRAINSTORM_URL_HOST:-localhost}" "$BRAINSTORM_DIR" "${json_state_dir%/state}"
while :; do sleep 0.1; done
FAKE_NODE
fake_bin="$scratch/fake-bin"
mkdir -p "$fake_bin"
cp "$scratch/fake-node" "$fake_bin/node"
cat > "$fake_bin/ps" <<'FAKE_PS'
#!/usr/bin/env bash
[[ -z "${BRAINSTORM_TEST_PS_MARKER:-}" || ! -e "$BRAINSTORM_TEST_PS_MARKER" ]] || exit 1
exec /bin/ps "$@"
FAKE_PS
chmod +x "$fake_bin/node" "$fake_bin/ps"
assert_exit_2_before_deadline "missing --host value" env PATH="$fake_bin:$PATH" bash "$start_script" --host
assert_exit_2_before_deadline "foreground/background conflict" env PATH="$fake_bin:$PATH" bash "$start_script" --foreground --background
fail_bin="$scratch/fail-bin"
mkdir -p "$fail_bin"
cat > "$fail_bin/node" <<'FAIL_NODE'
#!/usr/bin/env bash
exit 23
FAIL_NODE
chmod +x "$fail_bin/node"
failure_project="$scratch/node-failure-project"
mkdir -p "$failure_project"
if run_with_deadline env PATH="$fail_bin:$PATH" bash "$start_script" --project-dir "$failure_project" --background; then status=0; else status=$?; fi
[[ "$status" -ne 0 && "$status" -ne 124 ]] || fail "node startup failure did not fail promptly (status $status)"
if find "$failure_project" -name server.pid -o -name owner.uid -o -name server.path -o -name server.token | grep -q .; then fail "node startup failure left partial identity state"; fi
echo "brainstorm server self-test: node startup failure cleaned identity state"
json_project="$scratch/json-mismatch-project"
json_pid_file="$scratch/json-mismatch.pid"
mkdir -p "$json_project"
if env PATH="$fake_bin:$PATH" BRAINSTORM_TERM_MODE=ignore BRAINSTORM_JSON_STATE_DIR=/wrong/state BRAINSTORM_FAKE_PID_FILE="$json_pid_file" bash "$start_script" --project-dir "$json_project" --background >"$output" 2>&1; then status=0; else status=$?; fi
[[ "$status" -ne 0 ]] || fail "JSON state_dir mismatch was accepted"
json_pid="$(cat "$json_pid_file")"
if kill -0 "$json_pid" 2>/dev/null; then kill -9 "$json_pid" 2>/dev/null || true; fail "JSON state_dir mismatch left an orphan server"; fi
if find "$json_project" -name server.pid -o -name owner.uid -o -name server.path -o -name server.token | grep -q .; then fail "JSON mismatch left identity state"; fi
echo "brainstorm server self-test: JSON state_dir mismatch stopped server and cleaned identity"
if ! env PATH="$fake_bin:$PATH" BRAINSTORM_TERM_MODE=ps-fail bash "$start_script" --background >"$output" 2>&1; then fail "ephemeral background start failed: $(tr '\n' ' ' < "$output")"; fi
ephemeral_state="$(sed -n 's/.*"state_dir":"\([^"]*\)".*/\1/p' "$output" | head -1)"
[[ -n "$ephemeral_state" ]] || fail "ephemeral start did not emit server-started JSON with BRAINSTORM_DIR"
ephemeral_dir="${ephemeral_state%/state}"
cleanup_dirs+=("$ephemeral_dir")
ephemeral_pid="$(cat "$ephemeral_state/server.pid")"
assert_alive "$ephemeral_pid" "ephemeral background start"
assert_live_tamper_refused() {
  local file=$1 bad=$2 label=$3 saved
  saved="$(cat "$ephemeral_state/$file")"
  printf '%s\n' "$bad" > "$ephemeral_state/$file"
  bash "$stop_script" "$ephemeral_dir" >"$output" 2>&1 && fail "$label state was accepted"
  assert_alive "$ephemeral_pid" "$label"
  [[ -d "$ephemeral_dir" ]] || fail "$label state deleted the session"
  printf '%s\n' "$saved" > "$ephemeral_state/$file"
  echo "brainstorm server self-test: $label independently refused"
}
assert_live_tamper_refused server.token wrong-token "wrong token"
assert_live_tamper_refused owner.uid 999999 "wrong UID"
assert_live_tamper_refused server.path /tmp/not-server.cjs "wrong server path"
ps_marker="$ephemeral_state/ps-fail"
if env PATH="$fake_bin:$PATH" BRAINSTORM_TEST_PS_MARKER="$ps_marker" bash "$stop_script" "$ephemeral_dir" >"$output" 2>&1; then
  fail "post-TERM ps failure was accepted"
fi
assert_alive "$ephemeral_pid" "post-TERM ps failure"
[[ -d "$ephemeral_dir" ]] || fail "post-TERM ps failure deleted the session"
rm -f "$ps_marker"
echo "brainstorm server self-test: post-TERM ps failure refused without cleanup"
bash "$stop_script" "$ephemeral_dir" >"$output" 2>&1 || fail "ephemeral stop failed: $(tr '\n' ' ' < "$output")"
[[ ! -e "$ephemeral_dir" ]] || fail "ephemeral session directory was not deleted"
echo "brainstorm server self-test: ephemeral background session stopped and deleted"
persistent_project="$scratch/persistent-project"
mkdir -p "$persistent_project"
env PATH="$fake_bin:$PATH" BRAINSTORM_TERM_MODE=ignore bash "$start_script" --project-dir "$persistent_project" --foreground >"$output" 2>&1 &
foreground_pid=$!
cleanup_pids+=("$foreground_pid")
disown "$foreground_pid" 2>/dev/null || true
persistent_parent="$persistent_project/.superpowers/brainstorm"
persistent_state=""
for _ in {1..40}; do
  persistent_state="$(find "$persistent_parent" -type f -path '*/state/server.pid' -print 2>/dev/null | head -1 || true)"
  [[ -n "$persistent_state" ]] && break
  sleep 0.05
done
[[ -n "$persistent_state" ]] || fail "foreground start did not create state: $(tr '\n' ' ' < "$output")"
persistent_state="${persistent_state%/server.pid}"
[[ -s "$persistent_state/server.pid" ]] || fail "foreground PID state was empty"
persistent_dir="${persistent_state%/state}"
cleanup_dirs+=("$persistent_dir")
[[ "$(cat "$persistent_state/server.pid")" == "$foreground_pid" ]] || fail "foreground state did not record exec PID"
assert_alive "$foreground_pid" "persistent foreground start"
bash "$stop_script" "$persistent_dir" >"$output" 2>&1 || fail "persistent stop failed: $(tr '\n' ' ' < "$output")"
wait "$foreground_pid" 2>/dev/null || true
cleanup_pids=()
[[ -d "$persistent_dir" ]] || fail "persistent session directory was deleted"
echo "brainstorm server self-test: persistent foreground SIGKILL session stopped and retained"
write_raw_state() {
  local session_dir=$1 pid=$2 uid=$3 path=$4 token=$5
  mkdir -p "$session_dir/state"
  printf '%s\n' "$pid" > "$session_dir/state/server.pid"
  printf '%s\n' "$uid" > "$session_dir/state/owner.uid"
  printf '%s\n' "$path" > "$session_dir/state/server.path"
  printf '%s\n' "$token" > "$session_dir/state/server.token"
}
assert_untrusted_state_safe() {
  local label=$1 session_dir=$2 kind=$3 pid
  sleep 60 &
  pid=$!
  cleanup_pids+=("$pid")
  cleanup_dirs+=("$session_dir")
  case "$kind" in
    arbitrary|wrong-token)
      write_raw_state "$session_dir" "$pid" "$current_uid" "$server_script" wrong-token ;;
    wrong-command)
      write_raw_state "$session_dir" "$pid" "$current_uid" "$server_script" safe-token ;;
    wrong-uid)
      write_raw_state "$session_dir" "$pid" 999999 "$server_script" safe-token ;;
    missing)
      mkdir -p "$session_dir/state"
      printf '%s\n' "$pid" > "$session_dir/state/server.pid" ;;
    malformed)
      write_raw_state "$session_dir" "\$(: > $scratch/state-executed)" "$current_uid" "$server_script" 'bad token!' ;;
  esac
  bash "$stop_script" "$session_dir" >"$output" 2>&1 || true
  assert_alive "$pid" "$label"
  [[ -d "$session_dir" ]] || fail "$label: untrusted session directory was deleted"
  kill "$pid" 2>/dev/null || true
  wait "$pid" 2>/dev/null || true
  unset "cleanup_pids[${#cleanup_pids[@]}-1]"
  echo "brainstorm server self-test: $label preserved protected PID and directory"
}
current_uid="$(id -u)"
assert_untrusted_state_safe "arbitrary tmp path" "/tmp/not-brainstorm-$$-$(date +%s)-$RANDOM" arbitrary
assert_untrusted_state_safe "wrong token" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" wrong-token
assert_untrusted_state_safe "wrong UID" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" wrong-uid
assert_untrusted_state_safe "wrong command" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" wrong-command
assert_untrusted_state_safe "missing state" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" missing
assert_untrusted_state_safe "malformed state" "/tmp/brainstorm-$$-$(date +%s)-$RANDOM" malformed
[[ ! -e "$scratch/state-executed" ]] || fail "malformed state was executed"
echo "brainstorm server self-test: PASS"
