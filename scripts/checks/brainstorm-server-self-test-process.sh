#!/usr/bin/env bash
# Process helpers for the brainstorm server self-test: stop the children and servers it
# started, and decide when a process has ended. Sourced by brainstorm-server-self-test-lib.sh.

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

# Same rule as stop-server.sh: a PID that is gone or a zombie has ended. Poll briefly, because
# a killed process can take a moment to exit and to be reaped.
process_has_ended() {
  local pid=$1 process_state
  kill -0 "$pid" 2>/dev/null || return 0
  process_state="$(ps -o stat= -p "$pid" 2>/dev/null | tr -d ' ' || true)"
  [[ "$process_state" == Z* ]]
}

wait_for_process_end() {
  local pid=$1 i
  for i in {1..40}; do process_has_ended "$pid" && return 0; sleep 0.05; done
  return 1
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
