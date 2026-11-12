#!/usr/bin/env bash
# Stop or reap the server that start-server.sh launched as its own background job.
# Sourced by start-server.sh; uses its SERVER_PID.

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

# A direct child that already exited but was not reaped yet is a zombie: reap it.
reap_ended_child() {
  local parent process_state
  [[ "$SERVER_PID" =~ ^[0-9]+$ ]] || return 1
  parent="$(ps -o ppid= -p "$SERVER_PID" 2>/dev/null | tr -d ' ' || true)"
  [[ "$parent" == "$$" ]] || return 1
  process_state="$(ps -o stat= -p "$SERVER_PID" 2>/dev/null | tr -d ' ' || true)"
  [[ "$process_state" == Z* ]] || return 1
  wait "$SERVER_PID" 2>/dev/null || true
}
