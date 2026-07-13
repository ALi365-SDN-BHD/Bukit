#!/usr/bin/env bash
# Start the brainstorm server and output connection info.
set -euo pipefail
umask 077

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
SERVER_PATH="$SCRIPT_DIR/server.cjs"
source "$SCRIPT_DIR/session-state.sh"

usage_error() {
  printf '{"error":"%s"}\n' "$1" >&2
  exit 2
}

require_value() {
  local option=$1 count=$2 value=${3-}
  [[ "$count" -ge 2 && -n "$value" && "$value" != --* ]] || usage_error "$option requires a value"
}

PROJECT_DIR=""
FOREGROUND="false"
FORCE_BACKGROUND="false"
BIND_HOST="127.0.0.1"
URL_HOST=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --project-dir)
      require_value "$1" "$#" "${2-}"
      PROJECT_DIR=$2
      shift 2
      ;;
    --host)
      require_value "$1" "$#" "${2-}"
      BIND_HOST=$2
      shift 2
      ;;
    --url-host)
      require_value "$1" "$#" "${2-}"
      URL_HOST=$2
      shift 2
      ;;
    --foreground|--no-daemon)
      FOREGROUND="true"
      shift
      ;;
    --background|--daemon)
      FORCE_BACKGROUND="true"
      shift
      ;;
    *) usage_error "Unknown argument: $1" ;;
  esac
done

[[ "$FOREGROUND" != "true" || "$FORCE_BACKGROUND" != "true" ]] || usage_error "foreground and background modes conflict"

if [[ -z "$URL_HOST" ]]; then
  if [[ "$BIND_HOST" == "127.0.0.1" || "$BIND_HOST" == "localhost" ]]; then
    URL_HOST="localhost"
  else
    URL_HOST="$BIND_HOST"
  fi
fi

if [[ -n "${CODEX_CI:-}" && "$FOREGROUND" != "true" && "$FORCE_BACKGROUND" != "true" ]]; then
  FOREGROUND="true"
fi
if [[ "$FOREGROUND" != "true" && "$FORCE_BACKGROUND" != "true" ]]; then
  case "${OSTYPE:-}" in
    msys*|cygwin*|mingw*) FOREGROUND="true" ;;
  esac
  [[ -z "${MSYSTEM:-}" ]] || FOREGROUND="true"
fi

NODE_COMMAND="$(command -v node 2>/dev/null || true)"
NODE_BIN="$(physical_executable_path "$NODE_COMMAND" 2>/dev/null || true)"
[[ -n "$NODE_BIN" ]] || { echo '{"error":"node executable not found"}' >&2; exit 1; }
[[ "$NODE_BIN" != *' '* && "$NODE_BIN" != *$'\t'* && "$NODE_BIN" != *$'\r'* && "$NODE_BIN" != *$'\n'* ]] || { echo '{"error":"node executable path contains unsupported whitespace"}' >&2; exit 1; }
NODE_VERSION="$("$NODE_BIN" --version 2>/dev/null || true)"
[[ "$NODE_VERSION" =~ ^v[0-9]+([.][0-9]+){1,2}$ ]] || { echo '{"error":"node executable validation failed"}' >&2; exit 1; }
[[ -f "$SERVER_PATH" && ! -L "$SERVER_PATH" ]] || { echo '{"error":"server.cjs is unavailable"}' >&2; exit 1; }
SESSION_ID="$$-$(date +%s)-$RANDOM"
TOKEN="$(LC_ALL=C od -An -N32 -tx1 /dev/urandom | tr -d ' \n')"
[[ "$TOKEN" =~ ^[A-Za-z0-9._-]+$ && ${#TOKEN} -ge 32 ]] || { echo '{"error":"failed to generate session token"}' >&2; exit 1; }

if [[ -n "$PROJECT_DIR" ]]; then
  [[ -d "$PROJECT_DIR" ]] || { echo '{"error":"project directory does not exist"}' >&2; exit 1; }
  PROJECT_DIR="$(cd -- "$PROJECT_DIR" && pwd -P)"
  SESSION_DIR="$PROJECT_DIR/.superpowers/brainstorm/$SESSION_ID"
else
  SESSION_DIR="/tmp/brainstorm-$SESSION_ID"
fi
STATE_DIR="$SESSION_DIR/state"
LOG_FILE="$STATE_DIR/server.log"
mkdir -p "$SESSION_DIR/content" "$STATE_DIR"
classification="$(classify_session_dir "$SESSION_DIR")" || { echo '{"error":"unsafe session directory"}' >&2; exit 1; }

OWNER_PID="$(ps -o ppid= -p "$PPID" 2>/dev/null | tr -d ' ' || true)"
if [[ -z "$OWNER_PID" || "$OWNER_PID" == "1" || ! "$OWNER_PID" =~ ^[0-9]+$ ]]; then
  OWNER_PID="$PPID"
fi

remove_identity_state() {
  rm -f "$STATE_DIR/server.pid" "$STATE_DIR/owner.uid" "$STATE_DIR/server.path" "$STATE_DIR/server.token"
}
SERVER_PID=""

direct_child_job_is_current() {
  local parent jobs_output
  [[ "$SERVER_PID" =~ ^[0-9]+$ ]] || return 1
  parent="$(ps -o ppid= -p "$SERVER_PID" 2>/dev/null | tr -d ' ' || true)"
  jobs_output="$(jobs -pr 2>/dev/null || true)"
  [[ "$parent" == "$$" ]] || return 1
  case $'\n'"$jobs_output"$'\n' in *$'\n'"$SERVER_PID"$'\n'*) return 0 ;; esac
  return 1
}

stop_owned_child_job() {
  local i
  direct_child_job_is_current || return 1
  kill "$SERVER_PID" 2>/dev/null || true
  for i in {1..20}; do kill -0 "$SERVER_PID" 2>/dev/null || break; sleep 0.05; done
  if kill -0 "$SERVER_PID" 2>/dev/null; then
    direct_child_job_is_current || return 1
    kill -9 "$SERVER_PID" 2>/dev/null || true
  fi
  wait "$SERVER_PID" 2>/dev/null || true
}

cleanup_failed_start() {
  local status=$?
  trap - EXIT INT TERM
  if session_state_matches "$STATE_DIR" "$SERVER_PID" "$TOKEN" "$SERVER_PATH"; then
    if [[ "$FOREGROUND" == true && "$SERVER_PID" == "$$" ]]; then
      remove_identity_state
    elif ! bash "$SCRIPT_DIR/stop-server.sh" "$SESSION_DIR" >/dev/null 2>&1; then
      if ! kill -0 "$SERVER_PID" 2>/dev/null; then remove_identity_state; fi
    fi
  elif [[ "$FOREGROUND" == true && "$SERVER_PID" == "$$" ]]; then
    remove_identity_state
  elif [[ -n "$SERVER_PID" ]]; then
    if stop_owned_child_job; then remove_identity_state; fi
  else
    remove_identity_state
  fi
  exit "$status"
}

trap cleanup_failed_start EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [[ "$FOREGROUND" == "true" ]]; then
  SERVER_PID="$$"
  write_session_state "$STATE_DIR" "$SERVER_PID" "$TOKEN" "$SERVER_PATH"
  export BRAINSTORM_DIR="$SESSION_DIR" BRAINSTORM_HOST="$BIND_HOST"
  export BRAINSTORM_URL_HOST="$URL_HOST" BRAINSTORM_OWNER_PID="$OWNER_PID"
  shopt -s execfail
  set +e; exec "$NODE_BIN" "$SERVER_PATH" "--session-token=$TOKEN"
  status=$?; remove_identity_state || true; trap - EXIT INT TERM; exit "$status"
fi

nohup env BRAINSTORM_DIR="$SESSION_DIR" BRAINSTORM_HOST="$BIND_HOST" \
  BRAINSTORM_URL_HOST="$URL_HOST" BRAINSTORM_OWNER_PID="$OWNER_PID" \
  "$NODE_BIN" "$SERVER_PATH" "--session-token=$TOKEN" > "$LOG_FILE" 2>&1 &
SERVER_PID=$!
write_session_state "$STATE_DIR" "$SERVER_PID" "$TOKEN" "$SERVER_PATH"

started_line=""
for _ in {1..50}; do
  if ! kill -0 "$SERVER_PID" 2>/dev/null; then
    wait "$SERVER_PID" 2>/dev/null || true
    echo '{"error":"server process exited before startup"}' >&2
    exit 1
  fi
  started_line="$(grep -E -m 1 '"type"[[:space:]]*:[[:space:]]*"server-started"' "$LOG_FILE" 2>/dev/null || true)"
  if [[ -n "$started_line" ]]; then
    case "$started_line" in
      *"\"state_dir\":\"$STATE_DIR\""*) ;;
      *) echo '{"error":"server-started state_dir mismatch"}' >&2; exit 1 ;;
    esac
    [[ "$(validate_session_process "$STATE_DIR" 2>/dev/null || true)" == "$SERVER_PID" ]] || {
      echo '{"error":"server identity validation failed"}' >&2
      exit 1
    }
    sleep 0.05
    [[ "$(validate_session_process "$STATE_DIR" 2>/dev/null || true)" == "$SERVER_PID" ]] || {
      echo '{"error":"server exited during startup"}' >&2
      exit 1
    }
    disown "$SERVER_PID" 2>/dev/null || true
    trap - EXIT INT TERM
    printf '%s\n' "$started_line"
    exit 0
  fi
  sleep 0.1
done

echo '{"error":"server failed to start within 5 seconds"}' >&2
exit 1
