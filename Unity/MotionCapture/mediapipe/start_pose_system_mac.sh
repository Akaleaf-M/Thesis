#!/usr/bin/env bash

# macOS launcher for the MediaPipe / UPose / UDP pose system.
# Run from any directory; this script will switch to its own folder.
#
# Before first use:
#   chmod +x start_pose_system_mac.sh
#
# IMPORTANT:
# Camera indexes are machine-dependent, especially on Mac Studio.
# Confirm camera indexes first with:
#   python list_cameras_mac.py
# Then edit CAM_P1..CAM_P4 below if needed.

set -u

CONDA_ENV=mediapipe

CAM_P1=0
CAM_P2=1
CAM_P3=2
CAM_P4=3

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

LOG_DIR="$SCRIPT_DIR/logs"
PID_FILE="$LOG_DIR/pose_system_pids.txt"
COMMAND_DIR="$LOG_DIR/terminal_tab_commands"
mkdir -p "$LOG_DIR"
: > "$PID_FILE"

# Port mapping:
# P1: camera ${CAM_P1} -> Unity solo 52733, aggregator input 52833
# P2: camera ${CAM_P2} -> Unity solo 52734, aggregator input 52834
# P3: camera ${CAM_P3} -> Unity solo 52735, aggregator input 52835
# P4: camera ${CAM_P4} -> Unity solo 52736, aggregator input 52836
# Aggregator output -> Unity collective 53000

cleanup() {
  echo
  echo "Stopping pose system..."
  if [[ -f "$PID_FILE" ]]; then
    while read -r pid; do
      if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
        kill "$pid" 2>/dev/null || true
      fi
    done < "$PID_FILE"
  fi
  echo "Stopped. Logs are in: $LOG_DIR"
  exit 0
}

trap cleanup INT TERM

activate_conda() {
  if ! command -v conda >/dev/null 2>&1; then
    echo "ERROR: conda command is not available in this shell."
    echo "Open a shell where conda is initialized, or initialize conda for your shell first."
    echo "Expected environment: $CONDA_ENV"
    exit 1
  fi

  CONDA_BASE="$(conda info --base 2>/dev/null || true)"
  if [[ -z "$CONDA_BASE" ]] || [[ ! -f "$CONDA_BASE/etc/profile.d/conda.sh" ]]; then
    echo "ERROR: could not find conda activation script."
    echo "conda info --base returned: ${CONDA_BASE:-<empty>}"
    echo "Expected file: <conda-base>/etc/profile.d/conda.sh"
    exit 1
  fi

  # shellcheck disable=SC1090
  source "$CONDA_BASE/etc/profile.d/conda.sh"

  if ! conda activate "$CONDA_ENV"; then
    echo "ERROR: failed to activate conda environment: $CONDA_ENV"
    echo "Check available environments with: conda env list"
    exit 1
  fi
}

launch_process() {
  local name="$1"
  shift
  local log_file="$LOG_DIR/${name}.log"

  echo "Starting $name"
  echo "  log: $log_file"
  "$@" > "$log_file" 2>&1 &
  local pid=$!
  echo "$pid" >> "$PID_FILE"
  echo "  pid: $pid"
}

shell_quote() {
  printf "'%s'" "$(printf "%s" "$1" | sed "s/'/'\\\\''/g")"
}

write_terminal_command() {
  local file="$1"
  shift
  local quoted_cmd=""

  while (($#)); do
    quoted_cmd+=" $(shell_quote "$1")"
    shift
  done

  cat > "$file" <<EOF
#!/usr/bin/env bash
cd $(shell_quote "$SCRIPT_DIR") || exit 1
export PYTHONPATH=$(shell_quote "$SCRIPT_DIR/../upose")\${PYTHONPATH:+:\$PYTHONPATH}

if ! command -v conda >/dev/null 2>&1; then
  echo "ERROR: conda command is not available in this shell."
  echo "Open a shell where conda is initialized, or initialize conda first."
  echo "Expected environment: $CONDA_ENV"
  exec "\${SHELL:-/bin/bash}" -l
fi

CONDA_BASE="\$(conda info --base 2>/dev/null || true)"
if [[ -z "\$CONDA_BASE" ]] || [[ ! -f "\$CONDA_BASE/etc/profile.d/conda.sh" ]]; then
  echo "ERROR: could not find conda activation script."
  echo "conda info --base returned: \${CONDA_BASE:-<empty>}"
  exec "\${SHELL:-/bin/bash}" -l
fi

source "\$CONDA_BASE/etc/profile.d/conda.sh"
if ! conda activate $(shell_quote "$CONDA_ENV"); then
  echo "ERROR: failed to activate conda environment: $CONDA_ENV"
  echo "Check available environments with: conda env list"
  exec "\${SHELL:-/bin/bash}" -l
fi

echo "Running:${quoted_cmd}"
${quoted_cmd}
status=\$?
echo
echo "Process exited with status \$status. Press Ctrl+D or close this tab."
exec "\${SHELL:-/bin/bash}" -l
EOF

  chmod +x "$file"
}

launch_terminal_tabs() {
  if ! command -v osascript >/dev/null 2>&1; then
    return 1
  fi

  rm -rf "$COMMAND_DIR"
  mkdir -p "$COMMAND_DIR"

  local agg_cmd="$COMMAND_DIR/aggregator.command"
  local p1_cmd="$COMMAND_DIR/p1.command"
  local p2_cmd="$COMMAND_DIR/p2.command"
  local p3_cmd="$COMMAND_DIR/p3.command"
  local p4_cmd="$COMMAND_DIR/p4.command"

  write_terminal_command "$agg_cmd" python -u aggregator.py
  write_terminal_command "$p1_cmd" python -u run_mediapipe.py "$CAM_P1" 52733 52833
  write_terminal_command "$p2_cmd" python -u run_mediapipe.py "$CAM_P2" 52734 52834
  write_terminal_command "$p3_cmd" python -u run_mediapipe.py "$CAM_P3" 52735 52835
  write_terminal_command "$p4_cmd" python -u run_mediapipe.py "$CAM_P4" 52736 52836

  osascript <<OSA
tell application "Terminal"
  activate
  do script "bash $(shell_quote "$agg_cmd")"
end tell
delay 0.6
tell application "System Events" to keystroke "t" using command down
delay 0.3
tell application "Terminal" to do script "bash $(shell_quote "$p1_cmd")" in selected tab of front window
delay 0.3
tell application "System Events" to keystroke "t" using command down
delay 0.3
tell application "Terminal" to do script "bash $(shell_quote "$p2_cmd")" in selected tab of front window
delay 0.3
tell application "System Events" to keystroke "t" using command down
delay 0.3
tell application "Terminal" to do script "bash $(shell_quote "$p3_cmd")" in selected tab of front window
delay 0.3
tell application "System Events" to keystroke "t" using command down
delay 0.3
tell application "Terminal" to do script "bash $(shell_quote "$p4_cmd")" in selected tab of front window
OSA
}

activate_conda

# Let run_mediapipe.py import the local Python UPose package.
export PYTHONPATH="$SCRIPT_DIR/../upose${PYTHONPATH:+:$PYTHONPATH}"

echo "Starting pose system from: $SCRIPT_DIR"
echo "Conda environment: $CONDA_ENV"
echo "PYTHONPATH includes: $SCRIPT_DIR/../upose"
echo "Confirm camera indexes with: python list_cameras_mac.py"
echo

echo "Trying to open Terminal.app tabs..."
if launch_terminal_tabs; then
  echo "Launched pose system in Terminal.app tabs."
  echo "Stop each tab with Ctrl+C, or close the Terminal window."
  echo "If Terminal tab automation is blocked, grant Terminal/osascript accessibility permission and rerun."
  exit 0
fi

echo "Terminal.app tab launch failed. Falling back to background processes with logs."
echo

launch_process "aggregator_52833_52836_to_53000" python -u aggregator.py

sleep 2

launch_process "mediapipe_P1_cam${CAM_P1}_unity52733_agg52833" python -u run_mediapipe.py "$CAM_P1" 52733 52833
launch_process "mediapipe_P2_cam${CAM_P2}_unity52734_agg52834" python -u run_mediapipe.py "$CAM_P2" 52734 52834
launch_process "mediapipe_P3_cam${CAM_P3}_unity52735_agg52835" python -u run_mediapipe.py "$CAM_P3" 52735 52835
launch_process "mediapipe_P4_cam${CAM_P4}_unity52736_agg52836" python -u run_mediapipe.py "$CAM_P4" 52736 52836

echo
echo "Launched aggregator and four MediaPipe capture processes."
echo "Logs: $LOG_DIR"
echo "PID file: $PID_FILE"
echo
echo "Press Ctrl+C in this terminal to stop all launched processes."
echo "From another terminal, you can also stop them with:"
echo "  while read -r pid; do kill \"\$pid\" 2>/dev/null; done < \"$PID_FILE\""
echo "Or, if needed:"
echo "  pkill -f run_mediapipe.py"
echo "  pkill -f aggregator.py"
echo

while true; do
  sleep 1
done
