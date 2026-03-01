#!/usr/bin/env bash
set -euo pipefail

# manage-submodules.sh — Add, remove, list, or manage sparse checkout for git submodules under external/
#
# Usage:
#   manage-submodules.sh add <owner/repo|url> [--depth N] [--full] [--sparse-paths <paths>] [--info-only]
#   manage-submodules.sh remove <owner/repo> --confirm
#   manage-submodules.sh list
#   manage-submodules.sh sparse add <owner/repo> --paths <paths>
#   manage-submodules.sh sparse remove <owner/repo> --paths <paths>
#   manage-submodules.sh sparse list <owner/repo>
#   manage-submodules.sh --help

SUBMODULE_ROOT="external"

usage() {
  cat <<'EOF'
Usage:
  manage-submodules.sh add <owner/repo|url> [options]
  manage-submodules.sh remove <owner/repo> --confirm
  manage-submodules.sh list
  manage-submodules.sh sparse <add|remove|list> <owner/repo> [--paths <paths>]
  manage-submodules.sh --help

Commands:
  add       Add a git submodule under external/<owner>/<repo>
  remove    Remove a git submodule (requires --confirm)
  list      List all submodules as JSON
  sparse    Manage sparse checkout paths for an existing submodule

Add options:
  --depth N           Clone depth (default: 1)
  --full              Clone with full history (overrides --depth)
  --sparse-paths P    Comma-separated top-level paths for sparse checkout
  --info-only         Show repo size and top-level contents without adding

Remove options:
  --confirm           Required flag to confirm removal

Sparse sub-commands:
  sparse add <owner/repo> --paths <csv>     Add paths to sparse checkout
  sparse remove <owner/repo> --paths <csv>  Remove paths from sparse checkout
  sparse list <owner/repo>                  List current sparse checkout paths

Exit codes:
  0  Success
  1  General error
  2  Invalid arguments
EOF
}

# ── Helpers ──────────────────────────────────────────────────────────────────

die() { echo "Error: $*" >&2; exit 1; }
die_usage() { echo "Error: $*" >&2; echo >&2; usage >&2; exit 2; }

# Parse owner/repo from a GitHub URL or shorthand
parse_owner_repo() {
  local input="$1"
  # Strip trailing .git
  input="${input%.git}"
  # Strip https://github.com/ prefix if present
  input="${input#https://github.com/}"
  # Strip git@github.com: prefix if present
  input="${input#git@github.com:}"
  # Validate format
  if [[ ! "$input" =~ ^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$ ]]; then
    die_usage "Invalid owner/repo format: $1. Expected 'owner/repo' or a GitHub URL."
  fi
  echo "$input"
}

to_github_url() {
  echo "https://github.com/$1.git"
}

# ── Commands ─────────────────────────────────────────────────────────────────

cmd_add() {
  local owner_repo=""
  local depth=1
  local full=false
  local sparse_paths=""
  local info_only=false

  # Parse arguments
  while [[ $# -gt 0 ]]; do
    case "$1" in
      --depth)
        [[ $# -ge 2 ]] || die_usage "--depth requires a value"
        depth="$2"; shift 2 ;;
      --full)
        full=true; shift ;;
      --sparse-paths)
        [[ $# -ge 2 ]] || die_usage "--sparse-paths requires a value"
        sparse_paths="$2"; shift 2 ;;
      --info-only)
        info_only=true; shift ;;
      -*)
        die_usage "Unknown option: $1" ;;
      *)
        [[ -z "$owner_repo" ]] || die_usage "Unexpected argument: $1"
        owner_repo="$1"; shift ;;
    esac
  done

  [[ -n "$owner_repo" ]] || die_usage "Missing required argument: owner/repo"

  owner_repo="$(parse_owner_repo "$owner_repo")"
  local url
  url="$(to_github_url "$owner_repo")"
  local submodule_path="${SUBMODULE_ROOT}/${owner_repo}"

  # ── Info-only mode ───────────────────────────────────────────────────────
  if [[ "$info_only" == true ]]; then
    echo "Fetching info for ${owner_repo}..." >&2
    local tmpdir
    tmpdir="$(mktemp -d)"
    trap 'rm -rf "$tmpdir"' EXIT

    git clone --depth 1 --no-checkout "$url" "$tmpdir/repo" 2>&2

    # Checkout just the top level
    (cd "$tmpdir/repo" && git sparse-checkout init --no-cone && git sparse-checkout set '/*' && git checkout HEAD 2>&2) || true

    # Gather info
    local file_count disk_size
    file_count="$(find "$tmpdir/repo" -not -path '*/.git/*' -not -path '*/.git' -type f | wc -l | tr -d ' ')"
    disk_size="$(du -sh "$tmpdir/repo" --exclude='.git' 2>/dev/null | cut -f1)" || \
      disk_size="$(du -sh "$tmpdir/repo" 2>/dev/null | cut -f1)"

    # Top-level listing
    local entries=()
    for entry in "$tmpdir/repo"/*; do
      [[ -e "$entry" ]] || continue
      local name
      name="$(basename "$entry")"
      [[ "$name" == ".git" ]] && continue
      local entry_type="file"
      [[ -d "$entry" ]] && entry_type="dir"
      local entry_size
      entry_size="$(du -sh "$entry" 2>/dev/null | cut -f1)"
      entries+=("{\"name\":\"${name}\",\"type\":\"${entry_type}\",\"size\":\"${entry_size}\"}")
    done

    local entries_json
    entries_json="$(printf '%s\n' "${entries[@]}" | paste -sd ',' -)"

    cat <<EOJSON
{
  "repo": "${owner_repo}",
  "url": "${url}",
  "file_count": ${file_count},
  "disk_size": "${disk_size}",
  "top_level_contents": [${entries_json}]
}
EOJSON
    return 0
  fi

  # ── Idempotency check ───────────────────────────────────────────────────
  if [[ -d "$submodule_path" ]]; then
    echo "Submodule already exists at ${submodule_path}, skipping."
    return 0
  fi

  # ── Add submodule ────────────────────────────────────────────────────────
  local depth_args=()
  if [[ "$full" == false ]]; then
    depth_args=(--depth "$depth")
  fi

  echo "Adding submodule ${owner_repo} at ${submodule_path}..." >&2

  if [[ -n "$sparse_paths" ]]; then
    # When sparse-paths is specified, we must configure sparse checkout BEFORE
    # the initial checkout to avoid checking out files we don't need (and to
    # avoid long-path errors on repos with deeply nested files).
    #
    # Approach:
    #   1. Ensure parent directory exists
    #   2. Clone with --no-checkout into the submodule path
    #   3. Configure sparse checkout in the cloned repo
    #   4. Checkout
    #   5. Register as a submodule via .gitmodules and git add

    mkdir -p "$(dirname "$submodule_path")"

    git clone "${depth_args[@]}" --no-checkout "$url" "$submodule_path"

    echo "Configuring sparse checkout for: ${sparse_paths}" >&2
    (
      cd "$submodule_path"
      git sparse-checkout init --cone
      IFS=',' read -ra paths <<< "$sparse_paths"
      git sparse-checkout set "${paths[@]}"
      git checkout
    )

    # Register as submodule: add .gitmodules entry, stage, and absorb git dir
    git config -f .gitmodules "submodule.${submodule_path}.path" "$submodule_path"
    git config -f .gitmodules "submodule.${submodule_path}.url" "$url"
    git add .gitmodules
    # Suppress embedded-repo hint — we absorb the gitdir immediately after
    git -c advice.addEmbeddedRepo=false add "$submodule_path"
    # Move .git dir from submodule into parent's .git/modules (proper submodule layout)
    git submodule absorbgitdirs "$submodule_path"
    # Now init so .git/config knows the submodule
    git submodule init "$submodule_path" 2>/dev/null || true
    git config "submodule.${submodule_path}.url" "$url"
  else
    git submodule add "${depth_args[@]}" "$url" "$submodule_path"
  fi

  echo "Successfully added submodule: ${submodule_path}"
}

cmd_remove() {
  local owner_repo=""
  local confirm=false

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --confirm)
        confirm=true; shift ;;
      -*)
        die_usage "Unknown option: $1" ;;
      *)
        [[ -z "$owner_repo" ]] || die_usage "Unexpected argument: $1"
        owner_repo="$1"; shift ;;
    esac
  done

  [[ -n "$owner_repo" ]] || die_usage "Missing required argument: owner/repo"
  [[ "$confirm" == true ]] || die "Removal requires --confirm flag to prevent accidental deletion."

  owner_repo="$(parse_owner_repo "$owner_repo")"
  local submodule_path="${SUBMODULE_ROOT}/${owner_repo}"

  if [[ ! -d "$submodule_path" ]]; then
    die "Submodule not found at ${submodule_path}"
  fi

  echo "Removing submodule at ${submodule_path}..." >&2
  git submodule deinit -f "$submodule_path"
  git rm -f "$submodule_path"
  rm -rf ".git/modules/${submodule_path}"

  echo "Successfully removed submodule: ${submodule_path}"
}

cmd_list() {
  if [[ ! -f .gitmodules ]]; then
    echo "[]"
    return 0
  fi

  local entries=()
  local current_path="" current_url=""

  while IFS= read -r line; do
    # Match path = ...
    if [[ "$line" =~ path[[:space:]]*=[[:space:]]*(.*) ]]; then
      current_path="${BASH_REMATCH[1]}"
      current_path="$(echo "$current_path" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
    fi
    # Match url = ...
    if [[ "$line" =~ url[[:space:]]*=[[:space:]]*(.*) ]]; then
      current_url="${BASH_REMATCH[1]}"
      current_url="$(echo "$current_url" | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')"
    fi
    # When we have both, emit entry
    if [[ -n "$current_path" && -n "$current_url" ]]; then
      entries+=("{\"path\":\"${current_path}\",\"url\":\"${current_url}\"}")
      current_path=""
      current_url=""
    fi
  done < .gitmodules

  if [[ ${#entries[@]} -eq 0 ]]; then
    echo "[]"
  else
    local json
    json="$(printf '%s\n' "${entries[@]}" | paste -sd ',' -)"
    echo "[${json}]"
  fi
}

cmd_sparse() {
  local action="$1"; shift

  case "$action" in
    add)    cmd_sparse_add "$@" ;;
    remove) cmd_sparse_remove "$@" ;;
    list)   cmd_sparse_list "$@" ;;
    *)      die_usage "Unknown sparse sub-command: ${action}. Must be one of: add, remove, list" ;;
  esac
}

cmd_sparse_add() {
  local owner_repo=""
  local paths_csv=""

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --paths)
        [[ $# -ge 2 ]] || die_usage "--paths requires a value"
        paths_csv="$2"; shift 2 ;;
      -*)
        die_usage "Unknown option: $1" ;;
      *)
        [[ -z "$owner_repo" ]] || die_usage "Unexpected argument: $1"
        owner_repo="$1"; shift ;;
    esac
  done

  [[ -n "$owner_repo" ]] || die_usage "Missing required argument: owner/repo"
  [[ -n "$paths_csv" ]] || die_usage "Missing required option: --paths"

  owner_repo="$(parse_owner_repo "$owner_repo")"
  local submodule_path="${SUBMODULE_ROOT}/${owner_repo}"

  [[ -d "$submodule_path" ]] || die "Submodule not found at ${submodule_path}"

  (
    cd "$submodule_path"

    # Auto-init sparse checkout if not active
    if ! git sparse-checkout list &>/dev/null || [[ ! -f .git/info/sparse-checkout ]] && [[ ! -f "$(git rev-parse --git-dir)/info/sparse-checkout" ]]; then
      echo "Initializing sparse checkout..." >&2
      git sparse-checkout init --cone
    fi

    IFS=',' read -ra new_paths <<< "$paths_csv"
    echo "Adding sparse checkout paths: ${new_paths[*]}" >&2
    git sparse-checkout add "${new_paths[@]}"
  )

  echo "Successfully added sparse checkout paths to ${submodule_path}"
}

cmd_sparse_remove() {
  local owner_repo=""
  local paths_csv=""

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --paths)
        [[ $# -ge 2 ]] || die_usage "--paths requires a value"
        paths_csv="$2"; shift 2 ;;
      -*)
        die_usage "Unknown option: $1" ;;
      *)
        [[ -z "$owner_repo" ]] || die_usage "Unexpected argument: $1"
        owner_repo="$1"; shift ;;
    esac
  done

  [[ -n "$owner_repo" ]] || die_usage "Missing required argument: owner/repo"
  [[ -n "$paths_csv" ]] || die_usage "Missing required option: --paths"

  owner_repo="$(parse_owner_repo "$owner_repo")"
  local submodule_path="${SUBMODULE_ROOT}/${owner_repo}"

  [[ -d "$submodule_path" ]] || die "Submodule not found at ${submodule_path}"

  (
    cd "$submodule_path"

    # Read current sparse checkout paths
    mapfile -t current_paths < <(git sparse-checkout list 2>/dev/null)

    IFS=',' read -ra remove_paths <<< "$paths_csv"

    # Filter out the paths to remove
    local remaining=()
    for p in "${current_paths[@]}"; do
      local keep=true
      for rp in "${remove_paths[@]}"; do
        if [[ "$p" == "$rp" ]]; then
          keep=false
          break
        fi
      done
      if [[ "$keep" == true ]]; then
        remaining+=("$p")
      fi
    done

    if [[ ${#remaining[@]} -eq 0 ]]; then
      die "Cannot remove all sparse checkout paths. Use 'git sparse-checkout disable' to restore full checkout."
    fi

    echo "Removing sparse checkout paths: ${remove_paths[*]}" >&2
    git sparse-checkout set "${remaining[@]}"
  )

  echo "Successfully removed sparse checkout paths from ${submodule_path}"
}

cmd_sparse_list() {
  local owner_repo=""

  while [[ $# -gt 0 ]]; do
    case "$1" in
      -*)
        die_usage "Unknown option: $1" ;;
      *)
        [[ -z "$owner_repo" ]] || die_usage "Unexpected argument: $1"
        owner_repo="$1"; shift ;;
    esac
  done

  [[ -n "$owner_repo" ]] || die_usage "Missing required argument: owner/repo"

  owner_repo="$(parse_owner_repo "$owner_repo")"
  local submodule_path="${SUBMODULE_ROOT}/${owner_repo}"

  [[ -d "$submodule_path" ]] || die "Submodule not found at ${submodule_path}"

  (cd "$submodule_path" && git sparse-checkout list)
}

# ── Main ─────────────────────────────────────────────────────────────────────

if [[ $# -eq 0 ]]; then
  die_usage "No command specified"
fi

case "$1" in
  add)    shift; cmd_add "$@" ;;
  remove) shift; cmd_remove "$@" ;;
  list)   shift; cmd_list "$@" ;;
  sparse) shift; cmd_sparse "$@" ;;
  --help|-h) usage; exit 0 ;;
  *)      die_usage "Unknown command: $1" ;;
esac
