#!/bin/bash
#
# Post-Commit Update Hook
# Updates progress documents after successful commits
#
# Usage: Called automatically by Claude Code after Bash tool execution
#

TOOL_INPUT="$1"
TOOL_OUTPUT="$2"

# Only process successful git commit commands
if [[ ! "$TOOL_INPUT" =~ "git commit" ]]; then
    exit 0
fi

# Check if commit was successful (look for commit hash in output)
if [[ ! "$TOOL_OUTPUT" =~ \[.*[a-f0-9]{7,}\] ]]; then
    exit 0
fi

# Get current branch
CURRENT_BRANCH=$(git branch --show-current 2>/dev/null)
if [[ -z "$CURRENT_BRANCH" ]]; then
    exit 0
fi

# Extract feature name from branch
# feature/user-authentication -> user-authentication
# fix/login-bug -> login-bug
FEATURE_NAME=""
if [[ "$CURRENT_BRANCH" =~ ^(feature|fix|hotfix|refactor|docs)/(.+)$ ]]; then
    FEATURE_NAME="${BASH_REMATCH[2]}"
fi

if [[ -z "$FEATURE_NAME" ]]; then
    exit 0
fi

# Find project root (look for docs directory)
PROJECT_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
if [[ -z "$PROJECT_ROOT" ]]; then
    exit 0
fi

PROGRESS_FILE="$PROJECT_ROOT/docs/progress/${FEATURE_NAME}-progress.md"

# Check if progress file exists
if [[ ! -f "$PROGRESS_FILE" ]]; then
    echo "ℹ️ HOOK_INFO: Progress document not found"
    echo "Consider creating: $PROGRESS_FILE"
    exit 0
fi

# Extract commit message
COMMIT_MSG=""
if [[ "$TOOL_INPUT" =~ -m[[:space:]]*[\"\']([^\"\']+)[\"\'] ]]; then
    COMMIT_MSG="${BASH_REMATCH[1]}"
fi

# Get current date
TODAY=$(date +%Y-%m-%d)

# Suggest update
echo ""
echo "📋 HOOK_INFO: Progress Document Update Suggestion"
echo ""
echo "Progress file: $PROGRESS_FILE"
echo "Branch: $CURRENT_BRANCH"
echo "Commit: $COMMIT_MSG"
echo ""
echo "Consider updating the progress document with:"
echo "- [x] $COMMIT_MSG | $(whoami) | $TODAY"
echo ""

# Extract commit type for phase suggestion
if [[ "$COMMIT_MSG" =~ ^(feat|fix|refactor|test|docs) ]]; then
    COMMIT_TYPE="${BASH_REMATCH[1]}"
    case "$COMMIT_TYPE" in
        feat|fix|refactor)
            echo "Phase suggestion: Phase 3 (개발)"
            ;;
        test)
            echo "Phase suggestion: Phase 4 (테스트)"
            ;;
        docs)
            echo "Phase suggestion: Phase 1-2 (기획/설계)"
            ;;
    esac
fi

exit 0
