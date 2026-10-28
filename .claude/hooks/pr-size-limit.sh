#!/usr/bin/env bash
# PreToolUse hook: block `gh pr create` when the branch diff exceeds the line limit.
# Exit 2 blocks the call and returns stderr to Claude so it can split the work.
limit=${PR_SIZE_LIMIT:-500}

input=$(cat)
if command -v jq >/dev/null; then
  cmd=$(jq -r '.tool_input.command // ""' <<<"$input")
else
  cmd=$input
fi
[[ $cmd == *"gh pr create"* ]] || exit 0

base=$(gh repo view --json defaultBranchRef -q .defaultBranchRef.name 2>/dev/null || echo main)
git fetch -q origin "$base" 2>/dev/null
git rev-parse -q --verify "origin/$base" >/dev/null || exit 0

lines=$(git diff --numstat "origin/$base...HEAD" -- . \
  ':!*.lock' ':!package-lock.json' ':!pnpm-lock.yaml' ':!Cargo.lock' \
  | awk '{s+=$1+$2} END{print s+0}')

if (( lines > limit )); then
  echo "PR blocked: diff against origin/$base is $lines changed lines (limit $limit, lockfiles excluded). Split it into smaller, independently mergeable PRs." >&2
  exit 2
fi
