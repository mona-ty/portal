#!/usr/bin/env bash
set -euo pipefail

# Defaults (use config.toml for model/approval/effort)
CODEX_CWD="${CODEX_CWD:-/mnt/c/Codex}"
CODEX_BIN="${CODEX_BIN:-codex}"

cd "$CODEX_CWD" 2>/dev/null || {
  echo "[codex-dev] CWD not found: $CODEX_CWD" >&2
  exit 1
}

# Ensure Git safe.directory to avoid 'dubious ownership' on /mnt/c
if command -v git >/dev/null 2>&1; then
  if ! git config --global --get-all safe.directory | grep -Fx "$CODEX_CWD" >/dev/null 2>&1; then
    git config --global --add safe.directory "$CODEX_CWD" || true
  fi
fi

ts() { date +"%Y-%m-%d %H:%M:%S"; }
stamp() { date +"%Y%m%d-%H%M%S"; }

out_dir="TASKS/autoscan"
mkdir -p "$out_dir"
out_file="$out_dir/scan_$(stamp).md"

# Git scan helpers
in_repo="false"
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then in_repo="true"; fi

branch="(none)"; head="(none)"; changes=0; remotes="(none)"
if [[ "$in_repo" == "true" ]]; then
  branch=$(git rev-parse --abbrev-ref HEAD 2>/dev/null || echo "(detached)")
  head=$(git log -1 --pretty=format:"%h %s (%cr)" 2>/dev/null || echo "(no commits)")
  changes=$(git status --porcelain 2>/dev/null | wc -l | awk '{print $1}')
  remotes=$(git remote -v 2>/dev/null | sed -n '1,6p' | sed 's/^/  /')
fi

# Nested .git detection (should be none)
mapfile -t nested_git < <(find . -type d -name .git -not -path "./.git" -print | sed 's#^\./##' | sed -n '1,20p')

# Project detection
mapfile -t csprojs < <(find . -type f -name "*.csproj" -print | sed 's#^\./##' | sed -n '1,40p')
mapfile -t nodepkgs < <(find . -type f -name "package.json" -print | sed 's#^\./##' | sed -n '1,40p')
mapfile -t pyproj  < <(find . -type f -name "pyproject.toml" -print | sed 's#^\./##' | sed -n '1,40p')

# .gitignore sanity (common heavy dirs accidentally tracked)
tracked_heavy=""
if [[ "$in_repo" == "true" ]]; then
  tracked_heavy=$(git ls-files 2>/dev/null | grep -E '/(bin|obj)/' || true)
fi

{
  echo "# Autoscan Report ($(ts))"
  echo
  echo "## Git"
  echo "- Repo: $in_repo"
  echo "- Branch: $branch"
  echo "- Head: $head"
  echo "- Changes (porcelain lines): $changes"
  echo "- Remotes:"
  if [[ -n "$remotes" ]]; then echo "$remotes"; else echo "  (none)"; fi
  echo
  echo "## Nested .git (should be none)"
  if ((${#nested_git[@]})); then printf -- "- %s\n" "${nested_git[@]}"; else echo "- (none)"; fi
  echo
  echo "## Projects"
  echo "- .NET (*.csproj):"; if ((${#csprojs[@]})); then printf -- "  - %s\n" "${csprojs[@]}"; else echo "  - (none)"; fi
  echo "- Node (package.json):"; if ((${#nodepkgs[@]})); then printf -- "  - %s\n" "${nodepkgs[@]}"; else echo "  - (none)"; fi
  echo "- Python (pyproject.toml):"; if ((${#pyproj[@]})); then printf -- "  - %s\n" "${pyproj[@]}"; else echo "  - (none)"; fi
  echo
  echo "## .gitignore sanity"
  if [[ -n "$tracked_heavy" ]]; then
    echo "- WARNING: tracked build artifacts detected under bin/obj"
    echo '```'
    echo "$tracked_heavy" | sed -n '1,50p'
    echo '```'
  else
    echo "- OK: no tracked bin/obj detected"
  fi
} > "$out_file"

echo "[codex-dev] Autoscan written: $out_file" >&2

# Launch Codex CLI (config-driven: no explicit flags)
if command -v "$CODEX_BIN" >/dev/null 2>&1; then
  "$CODEX_BIN" || true
elif command -v codex-cli >/dev/null 2>&1; then
  codex-cli || true
elif command -v codex >/dev/null 2>&1; then
  codex || true
else
  echo "[codex-dev] Codex CLI not found (set CODEX_BIN or install codex)" >&2
fi
