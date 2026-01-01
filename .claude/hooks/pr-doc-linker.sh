#!/bin/bash
#
# PR Document Linker Hook
# Automatically finds and suggests related documents for PR creation
#
# Usage: Called when creating a PR to link related documents
#

TOOL_INPUT="$1"

# Only process gh pr create commands
if [[ ! "$TOOL_INPUT" =~ "gh pr create" ]]; then
    exit 0
fi

# Get current branch
CURRENT_BRANCH=$(git branch --show-current 2>/dev/null)
if [[ -z "$CURRENT_BRANCH" ]]; then
    exit 0
fi

# Extract feature name from branch
FEATURE_NAME=""
if [[ "$CURRENT_BRANCH" =~ ^(feature|fix|hotfix|refactor|docs)/(.+)$ ]]; then
    FEATURE_NAME="${BASH_REMATCH[2]}"
fi

if [[ -z "$FEATURE_NAME" ]]; then
    exit 0
fi

# Find project root
PROJECT_ROOT=$(git rev-parse --show-toplevel 2>/dev/null)
if [[ -z "$PROJECT_ROOT" ]]; then
    exit 0
fi

echo ""
echo "📎 HOOK_INFO: Related Documents Found"
echo ""

# Check for related documents
PRD_FILE="$PROJECT_ROOT/docs/prd/${FEATURE_NAME}-prd.md"
SPEC_FILE="$PROJECT_ROOT/docs/tech-specs/${FEATURE_NAME}-spec.md"
PROGRESS_FILE="$PROJECT_ROOT/docs/progress/${FEATURE_NAME}-progress.md"

FOUND_DOCS=0

if [[ -f "$PRD_FILE" ]]; then
    echo "✅ PRD: docs/prd/${FEATURE_NAME}-prd.md"
    ((FOUND_DOCS++))
else
    echo "❌ PRD: Not found"
fi

if [[ -f "$SPEC_FILE" ]]; then
    echo "✅ Tech Spec: docs/tech-specs/${FEATURE_NAME}-spec.md"
    ((FOUND_DOCS++))
else
    echo "❌ Tech Spec: Not found"
fi

if [[ -f "$PROGRESS_FILE" ]]; then
    echo "✅ Progress: docs/progress/${FEATURE_NAME}-progress.md"
    ((FOUND_DOCS++))
else
    echo "❌ Progress: Not found"
fi

echo ""

if [[ $FOUND_DOCS -gt 0 ]]; then
    echo "Include these in PR description under 'Related Documents' section."
else
    echo "⚠️ No related documents found for feature: $FEATURE_NAME"
    echo "Consider creating documents in docs/ directory."
fi

echo ""

# Analyze commits for PR summary
echo "📝 Commits to be included:"
git log main..HEAD --oneline 2>/dev/null | head -10

# Check for breaking changes
BREAKING_COMMITS=$(git log main..HEAD --oneline 2>/dev/null | grep -E '!:|BREAKING')
if [[ -n "$BREAKING_COMMITS" ]]; then
    echo ""
    echo "⚠️ BREAKING CHANGES detected:"
    echo "$BREAKING_COMMITS"
    echo ""
    echo "Make sure to:"
    echo "1. Add 'breaking-change' label"
    echo "2. Document migration steps"
fi

exit 0
