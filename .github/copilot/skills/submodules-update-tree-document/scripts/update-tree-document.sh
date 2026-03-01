#!/usr/bin/env bash
set -euo pipefail

# update-tree-document.sh — Update agent-files/submodule-tree.txt with flat tree of external/
#
# Usage:
#   update-tree-document.sh
#   update-tree-document.sh --help

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DISPLAY_TREE_SCRIPT="${SCRIPT_DIR}/../../submodules-display-tree/scripts/display-tree.sh"
OUTPUT_DIR="agent-files"
OUTPUT_FILE="${OUTPUT_DIR}/submodule-tree.txt"

usage() {
  cat <<'EOF'
Usage:
  update-tree-document.sh
  update-tree-document.sh --help

Updates agent-files/submodule-tree.txt with the flat tree view of external/.
The file contains only the raw display-tree output — no headers or wrapping.

Exit codes:
  0  Success
  1  General error
EOF
}

die() { echo "Error: $*" >&2; exit 1; }

# ── Argument parsing ─────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
  case "$1" in
    --help|-h) usage; exit 0 ;;
    *) die "Unexpected argument: $1" ;;
  esac
done

# ── Main ─────────────────────────────────────────────────────────────────────

if [[ ! -f "$DISPLAY_TREE_SCRIPT" ]]; then
  die "Display tree script not found at: ${DISPLAY_TREE_SCRIPT}"
fi

mkdir -p "$OUTPUT_DIR"

echo "Generating flat tree of external/..." >&2
tree_output="$(bash "$DISPLAY_TREE_SCRIPT" external --format flat)"

echo "$tree_output" > "$OUTPUT_FILE"
echo "Updated ${OUTPUT_FILE}" >&2
