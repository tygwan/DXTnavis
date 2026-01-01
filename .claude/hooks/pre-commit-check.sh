#!/bin/bash
#
# Pre-Commit Check Hook
# Validates commit messages before execution
#
# Usage: Called automatically by Claude Code before Bash tool execution
#

TOOL_INPUT="$1"

# Only check git commit commands
if [[ ! "$TOOL_INPUT" =~ "git commit" ]]; then
    exit 0
fi

# Extract commit message from command
# Handles: git commit -m "message" or git commit -m 'message'
COMMIT_MSG=""
if [[ "$TOOL_INPUT" =~ -m[[:space:]]*[\"\']([^\"\']+)[\"\'] ]]; then
    COMMIT_MSG="${BASH_REMATCH[1]}"
elif [[ "$TOOL_INPUT" =~ -m[[:space:]]*([^[:space:]]+) ]]; then
    COMMIT_MSG="${BASH_REMATCH[1]}"
fi

# If no message found (might be using HEREDOC), skip validation
if [[ -z "$COMMIT_MSG" ]]; then
    exit 0
fi

# Conventional Commits regex
# Format: <type>[optional scope][!]: <description>
CONVENTIONAL_REGEX="^(feat|fix|docs|style|refactor|test|chore|perf|ci|build|revert)(\([a-zA-Z0-9_-]+\))?(!)?: .+"

# Validate commit message format
if [[ ! "$COMMIT_MSG" =~ $CONVENTIONAL_REGEX ]]; then
    echo "❌ HOOK_ERROR: Invalid commit message format"
    echo ""
    echo "Expected: <type>[scope][!]: <description>"
    echo "Received: $COMMIT_MSG"
    echo ""
    echo "Valid types: feat, fix, docs, style, refactor, test, chore, perf, ci, build, revert"
    echo ""
    echo "Examples:"
    echo "  feat: add user authentication"
    echo "  fix(auth): resolve login issue"
    echo "  feat!: breaking change description"
    exit 1
fi

# Check for breaking change indicators
if [[ "$COMMIT_MSG" =~ ^[a-z]+\(.+\)!: ]] || [[ "$COMMIT_MSG" =~ ^[a-z]+!: ]]; then
    echo "⚠️ HOOK_WARNING: Breaking Change Detected"
    echo ""
    echo "Commit message: $COMMIT_MSG"
    echo ""
    echo "This commit will trigger a MAJOR version bump."
    echo "Make sure to document the breaking change properly."
    echo ""
    # Don't block, just warn
    exit 0
fi

# Check version impact
if [[ "$COMMIT_MSG" =~ ^feat ]]; then
    echo "ℹ️ HOOK_INFO: This commit will trigger a MINOR version bump"
elif [[ "$COMMIT_MSG" =~ ^fix ]]; then
    echo "ℹ️ HOOK_INFO: This commit will trigger a PATCH version bump"
fi

exit 0
