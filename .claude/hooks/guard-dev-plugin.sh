#!/usr/bin/env bash
# PreToolUse hook on Bash: agents may not force-release another checkout's claim on the
# in-game dev folder, or touch the claim directly. Only the user does that, in their own terminal.
command=$(jq -r '.tool_input.command // ""')

if [[ $command == *dev-plugin* && $command == *--force* ]] ||
    [[ $command == *devPlugins* && $command == *.claim* ]]; then
    cat >&2 <<'EOF'
Blocked: only the user may force-release the in-game dev folder or change its claim.
Run 'scripts/dev-plugin status' to see who holds it, and ask the user to run
'scripts/dev-plugin release --force' themselves if the claim is stale.
EOF
    exit 2
fi
