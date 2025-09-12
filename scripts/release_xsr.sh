#!/usr/bin/env bash
set -euo pipefail

# Automate publishing apps/XIVSubmarinesReturn to remote 'xsr' and tag a release.
# Usage: scripts/release_xsr.sh vX.Y.Z [base_branch]
# Example: scripts/release_xsr.sh v1.2.3 main

VERSION_TAG=${1:-}
BASE_BRANCH=${2:-main}
APP_PREFIX="apps/XIVSubmarinesReturn"
REMOTE_NAME="xsr"

if [[ -z "$VERSION_TAG" ]]; then
  echo "Usage: $0 vX.Y.Z [base_branch]" >&2
  exit 1
fi
if [[ ! "$VERSION_TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "Error: version tag must be like v1.2.3" >&2
  exit 1
fi

if ! git rev-parse --show-toplevel >/dev/null 2>&1; then
  echo "Error: not inside a git repository" >&2
  exit 1
fi
REPO_ROOT=$(git rev-parse --show-toplevel)
cd "$REPO_ROOT"

if [[ ! -d "$APP_PREFIX" ]]; then
  echo "Error: $APP_PREFIX not found (run from monorepo root)" >&2
  exit 1
fi

if ! git rev-parse --verify --quiet "$BASE_BRANCH" >/dev/null; then
  echo "Error: base branch '$BASE_BRANCH' not found" >&2
  exit 1
fi

if ! git remote get-url "$REMOTE_NAME" >/dev/null 2>&1; then
  echo "Error: remote '$REMOTE_NAME' not configured" >&2
  exit 1
fi

# Ensure clean working tree
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Error: working tree has changes. Please commit or stash first." >&2
  exit 1
fi

DATE_STAMP=$(date +%Y%m%d-%H%M%S)
EXPORT_BRANCH="xsr/export-${DATE_STAMP}"
BACKUP_BRANCH="backup/initial-${DATE_STAMP}"

echo "==> Checkout base branch: $BASE_BRANCH"
git checkout -q "$BASE_BRANCH"

echo "==> Bump csproj version to ${VERSION_TAG#v} in $APP_PREFIX"
CSPROJ="$APP_PREFIX/XIVSubmarinesReturn.csproj"
if [[ -f "$CSPROJ" ]]; then
  NEWV_NO_V=${VERSION_TAG#v}
  ASM_V="$NEWV_NO_V.0"
  sed -i -E "s#<AssemblyVersion>[^<]+</AssemblyVersion>#<AssemblyVersion>${ASM_V}</AssemblyVersion>#" "$CSPROJ"
  sed -i -E "s#<FileVersion>[^<]+</FileVersion>#<FileVersion>${ASM_V}</FileVersion>#" "$CSPROJ"
  sed -i -E "s#<Version>[^<]+</Version>#<Version>${NEWV_NO_V}</Version>#" "$CSPROJ"
  if ! git diff --quiet -- "$CSPROJ"; then
    git add "$CSPROJ"
    git commit -m "chore(xsr): bump csproj version to ${VERSION_TAG}" >/dev/null
  else
    echo "csproj already at ${NEWV_NO_V}; no commit"
  fi
else
  echo "Warning: csproj not found at $CSPROJ; skipping bump" >&2
fi

echo "==> Split subtree: $APP_PREFIX from $BASE_BRANCH -> $EXPORT_BRANCH"
git subtree split --prefix="$APP_PREFIX" "$BASE_BRANCH" -b "$EXPORT_BRANCH" >/dev/null

echo "==> Backup current $REMOTE_NAME/main to $REMOTE_NAME:$BACKUP_BRANCH"
git fetch "$REMOTE_NAME" --quiet
git branch -f xsr-main-backup "$REMOTE_NAME/main" >/dev/null 2>&1 || true
git push "$REMOTE_NAME" xsr-main-backup:"$BACKUP_BRANCH"

echo "==> Force-push $EXPORT_BRANCH to $REMOTE_NAME:main"
git push -f "$REMOTE_NAME" "$EXPORT_BRANCH":main

echo "==> Create and push tag $VERSION_TAG"
if git rev-parse -q --verify "refs/tags/$VERSION_TAG" >/dev/null; then
  echo "Tag $VERSION_TAG already exists locally; moving it to current HEAD"
  git tag -f "$VERSION_TAG"
else
  git tag -a "$VERSION_TAG" -m "Release $VERSION_TAG"
fi
git push -f "$REMOTE_NAME" "$VERSION_TAG"

echo "==> Done. Check Actions & Release:"
echo "    https://github.com/$(git remote get-url $REMOTE_NAME | sed -E 's#.*github.com[:/ ]##;s/.git$//')/actions"
echo "    https://github.com/$(git remote get-url $REMOTE_NAME | sed -E 's#.*github.com[:/ ]##;s/.git$//')/releases/tag/${VERSION_TAG}"
