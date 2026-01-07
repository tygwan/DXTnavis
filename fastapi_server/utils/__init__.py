"""
Utility modules for FastAPI backend.

Phase B utilities:
- backward_compat: Legacy payload migration
- db_helpers: Database helper functions
"""

from .backward_compat import migrate_legacy_object, migrate_legacy_batch, is_valid_uuid
from .db_helpers import (
    get_or_create_project,
    get_or_create_revision,
    ensure_project_and_revision
)

__all__ = [
    # Backward compatibility
    "migrate_legacy_object",
    "migrate_legacy_batch",
    "is_valid_uuid",
    # Database helpers
    "get_or_create_project",
    "get_or_create_revision",
    "ensure_project_and_revision",
]
