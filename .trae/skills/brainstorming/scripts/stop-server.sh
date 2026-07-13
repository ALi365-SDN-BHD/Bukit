#!/usr/bin/env bash
# Stop a validated brainstorm server and clean up its session.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
source "$SCRIPT_DIR/session-state.sh"

if [[ $# -ne 1 || -z "${1-}" ]]; then
  echo '{"error":"Usage: stop-server.sh <session_dir>"}' >&2
  exit 2
fi

SESSION_DIR="$(resolve_session_dir "$1" 2>/dev/null || true)"
[[ -n "$SESSION_DIR" ]] || { echo '{"status":"refused","error":"session directory is unavailable"}' >&2; exit 1; }
classification="$(classify_session_dir "$SESSION_DIR" 2>/dev/null || true)"
[[ -n "$classification" ]] || { echo '{"status":"refused","error":"untrusted session directory"}' >&2; exit 1; }

STATE_DIR="$SESSION_DIR/state"
pid="$(validate_session_process "$STATE_DIR" 2>/dev/null || true)"
[[ -n "$pid" ]] || { echo '{"status":"refused","error":"untrusted or stale session state"}' >&2; exit 1; }

process_has_ended() {
  local process_state
  if ! kill -0 "$pid" 2>/dev/null; then
    return 0
  fi
  process_state="$(ps -o stat= -p "$pid" 2>/dev/null | tr -d ' ' || true)"
  [[ "$process_state" == Z* ]]
}

kill "$pid" 2>/dev/null || true

still_valid=""
for _ in {1..20}; do
  still_valid="$(validate_session_process "$STATE_DIR" 2>/dev/null || true)"
  [[ "$still_valid" == "$pid" ]] || break
  sleep 0.1
done

if [[ "$still_valid" != "$pid" ]] && ! process_has_ended; then
  echo '{"status":"refused","error":"server identity became unverifiable while PID remained live"}' >&2
  exit 1
fi

if [[ "$still_valid" == "$pid" ]]; then
  kill_pid="$(validate_session_process "$STATE_DIR" 2>/dev/null || true)"
  [[ "$kill_pid" == "$pid" ]] || { echo '{"status":"refused","error":"server identity changed before SIGKILL"}' >&2; exit 1; }
  kill -9 "$pid" 2>/dev/null || true
  for _ in {1..10}; do
    [[ "$(validate_session_process "$STATE_DIR" 2>/dev/null || true)" == "$pid" ]] || break
    sleep 0.05
  done
fi

post_signal_pid="$(validate_session_process "$STATE_DIR" 2>/dev/null || true)"
[[ "$post_signal_pid" != "$pid" ]] || {
  echo '{"status":"failed","error":"validated process still running"}' >&2
  exit 1
}
process_has_ended || {
  echo '{"status":"refused","error":"PID remained live after identity validation failed"}' >&2
  exit 1
}

[[ "$(resolve_session_dir "$SESSION_DIR" 2>/dev/null || true)" == "$SESSION_DIR" ]] || {
  echo '{"status":"refused","error":"session directory identity changed"}' >&2
  exit 1
}
[[ "$(classify_session_dir "$SESSION_DIR" 2>/dev/null || true)" == "$classification" ]] || {
  echo '{"status":"refused","error":"session directory classification changed"}' >&2
  exit 1
}

if [[ "$classification" == ephemeral ]]; then
  rm -rf -- "$SESSION_DIR"
else
  rm -f "$STATE_DIR/server.pid" "$STATE_DIR/owner.uid" "$STATE_DIR/server.path" \
    "$STATE_DIR/server.token" "$STATE_DIR/server.log"
fi

echo '{"status":"stopped"}'
