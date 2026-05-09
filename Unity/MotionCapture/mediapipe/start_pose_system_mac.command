#!/usr/bin/env bash

# Double-click wrapper for macOS Finder.
# Keep startup logic in start_pose_system_mac.sh.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$SCRIPT_DIR/start_pose_system_mac.sh"
