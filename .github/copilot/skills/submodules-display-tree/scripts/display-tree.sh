#!/usr/bin/env bash
set -euo pipefail

# display-tree.sh — Display a directory tree in multiple formats
#
# Usage:
#   display-tree.sh [directory] [--format tree|indented|flat] [--depth N]
#   display-tree.sh --help

DEFAULT_DIR="external"
DEFAULT_FORMAT="flat"

usage() {
  cat <<'EOF'
Usage:
  display-tree.sh [directory] [options]
  display-tree.sh --help

Arguments:
  directory             Directory to display (default: external/)

Options:
  --format FORMAT       Output format: tree, indented, flat (default: flat)
  --depth N             Maximum traversal depth (default: unlimited)

Formats:
  flat       Brace-expansion notation:  root/[dir1/{a,b},dir2/c]
  indented   Space-indented list with trailing / on dirs
  tree       Classic tree with box-drawing characters

Exit codes:
  0  Success
  1  General error
  2  Invalid arguments
EOF
}

die() { echo "Error: $*" >&2; exit 1; }
die_usage() { echo "Error: $*" >&2; echo >&2; usage >&2; exit 2; }

# ── Argument parsing ─────────────────────────────────────────────────────────

target_dir=""
format="$DEFAULT_FORMAT"
max_depth=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --format)
      [[ $# -ge 2 ]] || die_usage "--format requires a value"
      format="$2"; shift 2 ;;
    --depth)
      [[ $# -ge 2 ]] || die_usage "--depth requires a value"
      max_depth="$2"; shift 2 ;;
    --help|-h)
      usage; exit 0 ;;
    -*)
      die_usage "Unknown option: $1" ;;
    *)
      [[ -z "$target_dir" ]] || die_usage "Unexpected argument: $1"
      target_dir="$1"; shift ;;
  esac
done

target_dir="${target_dir:-$DEFAULT_DIR}"
# Strip trailing slash for consistency
target_dir="${target_dir%/}"

[[ -d "$target_dir" ]] || die "Directory not found: ${target_dir}"

case "$format" in
  tree|indented|flat) ;;
  *) die_usage "Unknown format: ${format}. Must be one of: tree, indented, flat" ;;
esac

# ── Gather entries ───────────────────────────────────────────────────────────

# Build find arguments
find_args=("$target_dir" -mindepth 1)
if [[ -n "$max_depth" ]]; then
  find_args+=(-maxdepth "$max_depth")
fi
# Exclude .git directories
find_args+=(-name .git -prune -o -print)

# Collect all entries, sorted
mapfile -t entries < <(find "${find_args[@]}" 2>/dev/null | sort)

if [[ ${#entries[@]} -eq 0 ]]; then
  echo "${target_dir}/"
  exit 0
fi

# ── Format: indented ────────────────────────────────────────────────────────

format_indented() {
  local base_depth
  base_depth="$(echo "$target_dir" | tr '/' '\n' | wc -l)"

  echo "${target_dir}/"
  for entry in "${entries[@]}"; do
    local rel="${entry#${target_dir}/}"
    local depth
    depth="$(echo "$rel" | tr '/' '\n' | wc -l)"
    local indent=""
    for ((i = 1; i < depth + 1; i++)); do
      indent="${indent} "
    done
    local name
    name="$(basename "$entry")"
    if [[ -d "$entry" ]]; then
      echo "${indent}${name}/"
    else
      echo "${indent}${name}"
    fi
  done
}

# ── Format: tree ─────────────────────────────────────────────────────────────

format_tree() {
  echo "${target_dir}/"

  # Group entries by parent for proper tree rendering
  # We process entries level by level
  local prev_parts=()

  for idx in "${!entries[@]}"; do
    local entry="${entries[$idx]}"
    local rel="${entry#${target_dir}/}"
    local name
    name="$(basename "$entry")"

    # Calculate depth (0-based from target_dir)
    IFS='/' read -ra parts <<< "$rel"
    local depth=$(( ${#parts[@]} - 1 ))

    # Determine if this is the last entry at its level among siblings
    local parent_prefix
    if [[ $depth -gt 0 ]]; then
      parent_prefix="$(dirname "$rel")"
    else
      parent_prefix=""
    fi

    local is_last=true
    for ((j = idx + 1; j < ${#entries[@]}; j++)); do
      local other_rel="${entries[$j]#${target_dir}/}"
      local other_parent
      if [[ "$other_rel" == */* ]]; then
        other_parent="$(dirname "$other_rel")"
      else
        other_parent=""
      fi
      if [[ "$other_parent" == "$parent_prefix" ]]; then
        is_last=false
        break
      fi
      # If we've moved past siblings, stop
      IFS='/' read -ra other_parts <<< "$other_rel"
      if [[ ${#other_parts[@]} -le $depth ]]; then
        break
      fi
    done

    # Build prefix
    local prefix=""
    for ((d = 0; d < depth; d++)); do
      # Check if ancestor at depth d was the last among its siblings
      # Build the ancestor path
      local ancestor_path=""
      for ((k = 0; k <= d; k++)); do
        if [[ -z "$ancestor_path" ]]; then
          ancestor_path="${parts[$k]}"
        else
          ancestor_path="${ancestor_path}/${parts[$k]}"
        fi
      done

      local ancestor_parent
      if [[ $d -gt 0 ]]; then
        ancestor_parent="$(dirname "$ancestor_path")"
      else
        ancestor_parent=""
      fi

      # Check if this ancestor is the last in its parent
      local ancestor_is_last=true
      local ancestor_full="${target_dir}/${ancestor_path}"
      for other_entry in "${entries[@]}"; do
        local other_rel2="${other_entry#${target_dir}/}"
        local other_parent2
        if [[ "$other_rel2" == */* ]]; then
          other_parent2="$(dirname "$other_rel2")"
        else
          other_parent2=""
        fi
        if [[ "$other_parent2" == "$ancestor_parent" && "$other_entry" > "$ancestor_full" ]]; then
          ancestor_is_last=false
          break
        fi
      done

      if [[ "$ancestor_is_last" == true ]]; then
        prefix="${prefix}    "
      else
        prefix="${prefix}│   "
      fi
    done

    local connector
    if [[ "$is_last" == true ]]; then
      connector="└── "
    else
      connector="├── "
    fi

    local suffix=""
    [[ -d "$entry" ]] && suffix="/"

    echo "${prefix}${connector}${name}${suffix}"
  done
}

# ── Format: flat ─────────────────────────────────────────────────────────────

# Recursively build brace-expansion notation for a directory
build_flat() {
  local dir="$1"
  local effective_depth="${2:-0}"
  local max_d="${3:-}"

  # If depth limited and we've reached it, just output the dir name
  if [[ -n "$max_d" && "$effective_depth" -ge "$max_d" ]]; then
    return
  fi

  # Get direct children
  local children=()
  if [[ -d "$dir" ]]; then
    while IFS= read -r child; do
      [[ -n "$child" ]] || continue
      local cname
      cname="$(basename "$child")"
      [[ "$cname" == ".git" ]] && continue
      children+=("$child")
    done < <(find "$dir" -mindepth 1 -maxdepth 1 -not -name .git 2>/dev/null | sort)
  fi

  if [[ ${#children[@]} -eq 0 ]]; then
    return
  fi

  local parts=()
  for child in "${children[@]}"; do
    local cname
    cname="$(basename "$child")"
    if [[ -d "$child" ]]; then
      # Recurse to get sub-content
      local sub
      sub="$(build_flat "$child" $((effective_depth + 1)) "$max_d")"
      if [[ -n "$sub" ]]; then
        parts+=("${cname}/${sub}")
      else
        parts+=("${cname}")
      fi
    else
      parts+=("${cname}")
    fi
  done

  if [[ ${#parts[@]} -eq 1 ]]; then
    echo "${parts[0]}"
  else
    local joined
    joined="$(IFS=','; echo "${parts[*]}")"
    echo "{${joined}}"
  fi
}

format_flat() {
  local content
  content="$(build_flat "$target_dir" 0 "$max_depth")"
  if [[ -n "$content" ]]; then
    echo "${target_dir}/${content}"
  else
    echo "${target_dir}/"
  fi
}

# ── Dispatch ─────────────────────────────────────────────────────────────────

case "$format" in
  indented) format_indented ;;
  tree)     format_tree ;;
  flat)     format_flat ;;
esac
