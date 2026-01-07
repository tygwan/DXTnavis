"""
Tests for deploy_all utilities - Deployment automation
Phase F.1 TDD: Red → Green
"""
import sys
from pathlib import Path
import re
from datetime import datetime

# Add project root to Python path
project_root = Path(__file__).parent.parent.parent
if str(project_root) not in sys.path:
    sys.path.insert(0, str(project_root))

import pytest
from scripts.deploy_all import generate_backup_filename


def test_generates_backup_name():
    """
    Test that generate_backup_filename creates a timestamped backup filename.

    Expected format: backup_YYYYMMDD_HHMMSS.sql
    Example: backup_20251101_143052.sql

    Phase F.1 TDD: This test will fail because deploy_all.py doesn't exist yet.
    """
    # Act: Generate backup filename
    filename = generate_backup_filename()

    # Assert: Check filename format
    assert isinstance(filename, str), "Filename should be a string"
    assert filename.endswith(".sql"), "Filename should end with .sql"
    assert filename.startswith("backup_"), "Filename should start with 'backup_'"

    # Verify timestamp format: backup_YYYYMMDD_HHMMSS.sql
    pattern = r'^backup_\d{8}_\d{6}\.sql$'
    assert re.match(pattern, filename), \
        f"Filename '{filename}' should match pattern 'backup_YYYYMMDD_HHMMSS.sql'"

    # Verify the timestamp is recent (within last minute)
    timestamp_str = filename[7:22]  # Extract "YYYYMMDD_HHMMSS"
    timestamp = datetime.strptime(timestamp_str, "%Y%m%d_%H%M%S")
    now = datetime.now()
    time_diff = abs((now - timestamp).total_seconds())

    assert time_diff < 60, \
        f"Generated timestamp should be recent (within 60 seconds), got {time_diff}s difference"


def test_generates_unique_backup_names():
    """
    Test that consecutive calls generate different filenames.
    This ensures timestamps are being used correctly.
    """
    # Act: Generate two filenames with a small delay
    filename1 = generate_backup_filename()

    import time
    time.sleep(1.1)  # Sleep for more than 1 second to ensure different timestamps

    filename2 = generate_backup_filename()

    # Assert: Filenames should be different
    assert filename1 != filename2, \
        "Consecutive calls should generate different filenames"
