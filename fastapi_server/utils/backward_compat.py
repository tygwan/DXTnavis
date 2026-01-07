"""
Backward compatibility utilities for legacy payload migration.

Phase B requirement (techspec.md §2.2):
- Support legacy unique_id field from older DXrevit clients
- Auto-promote unique_id to unique_key if not present
- Extract object_guid from UUID-like unique_id values
"""

import re
from typing import Dict, Any
from fastapi_server.models.schemas import UnifiedObjectDto


# UUID pattern (RFC 4122 format)
UUID_PATTERN = re.compile(
    r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
)


def is_valid_uuid(value: str) -> bool:
    """
    Check if a string is a valid UUID format.

    Args:
        value: String to check

    Returns:
        True if value matches UUID pattern, False otherwise
    """
    if not value:
        return False
    return bool(UUID_PATTERN.match(value))


def migrate_legacy_object(obj: UnifiedObjectDto) -> UnifiedObjectDto:
    """
    Migrate legacy object payload to new dual-identity schema.

    Migration rules (techspec.md §2.2):
    1. If unique_key exists, use it as-is
    2. If unique_key is None but unique_id exists:
       - Set unique_key = unique_id
       - If unique_id is UUID format, also set object_guid = unique_id
    3. If object_guid is None and unique_key is UUID format:
       - Set object_guid = unique_key

    Args:
        obj: UnifiedObjectDto instance (may have legacy unique_id field)

    Returns:
        Migrated UnifiedObjectDto with unique_key and object_guid properly set
    """
    # Rule 1: unique_key already exists, check if we can extract object_guid
    if obj.unique_key:
        if not obj.object_guid and is_valid_uuid(obj.unique_key):
            obj.object_guid = obj.unique_key
        return obj

    # Rule 2: unique_id exists but unique_key doesn't - promote it
    if obj.unique_id:
        obj.unique_key = obj.unique_id

        # If unique_id is UUID format, also set object_guid
        if is_valid_uuid(obj.unique_id):
            obj.object_guid = obj.unique_id

        return obj

    # Fallback: Neither unique_key nor unique_id exists - this should not happen
    # but we'll let downstream validation handle it
    return obj


def migrate_legacy_batch(objects: list[UnifiedObjectDto]) -> list[UnifiedObjectDto]:
    """
    Migrate a batch of legacy objects.

    Args:
        objects: List of UnifiedObjectDto instances

    Returns:
        List of migrated UnifiedObjectDto instances
    """
    return [migrate_legacy_object(obj) for obj in objects]
