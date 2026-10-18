#!/usr/bin/env bash
# Runs Prettier on client source files after Claude edits them, so format:check stays green.
set -euo pipefail

file=$(jq -r '.tool_input.file_path // empty')
client="$CLAUDE_PROJECT_DIR/client"

case "$file" in
  "$client"/src/*.ts | "$client"/src/*.html | "$client"/src/*.css) ;;
  *) exit 0 ;;
esac

prettier="$client/node_modules/.bin/prettier"
[ -x "$prettier" ] || exit 0

cd "$client" && "$prettier" --write --log-level warn "$file" >&2
